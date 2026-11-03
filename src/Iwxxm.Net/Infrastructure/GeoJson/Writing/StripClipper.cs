using System;
using System.Collections.Generic;

namespace Iwxxm.Net.Infrastructure.GeoJson.Writing
{
    /// <summary>
    /// Clips an unwrapped boundary against a vertical longitude strip and assembles the pieces (Weiler-Atherton style):
    /// the boundary arcs inside the strip are joined along the rectangle <c>[west, east] x [-90, 90]</c> in counterclockwise
    /// order, so every vertex of the source stays in exactly one output ring and disjoint pieces are never joined by a
    /// zero-width bridge. The region must lie on the left of the direction of travel of the boundary.
    /// </summary>
    internal static class StripClipper
    {
        private const double MinLat = -90.0;
        private const double MaxLat = 90.0;

        /// <summary>A vertex in unwrapped longitude, remembering the source position it comes from (-1 when computed).</summary>
        internal readonly struct Node
        {
            public Node(double x, double y, int index)
            {
                X = x;
                Y = y;
                Index = index;
            }

            public double X { get; }

            public double Y { get; }

            public int Index { get; }
        }

        private sealed class Arc
        {
            public List<Node> Nodes { get; } = new List<Node>();

            public double EntryT { get; set; }

            public double ExitT { get; set; }

            public bool Used { get; set; }
        }

        /// <summary>
        /// Clips a chain whose first and last vertices lie outside the strip. <paramref name="shift"/> is added to every
        /// longitude of the result (it maps a strip beyond [-180, 180] back into it).
        /// </summary>
        public static List<List<GeoJsonPosition>> Clip(IReadOnlyList<Node> chain, IReadOnlyList<GeoJsonPosition> source, double west, double east, double shift)
        {
            List<Arc> arcs = CollectArcs(chain, west, east);
            var rings = new List<List<GeoJsonPosition>>();
            if (arcs.Count == 0)
                return rings;

            double width = east - west;
            double height = MaxLat - MinLat;
            double perimeter = (2.0 * width) + (2.0 * height);
            foreach (Arc arc in arcs)
            {
                arc.EntryT = Perimeter(arc.Nodes[0], west, width, height);
                arc.ExitT = Perimeter(arc.Nodes[arc.Nodes.Count - 1], west, width, height);
            }

            // Rectangle corners in counterclockwise order from the south-west corner.
            var corners = new[]
            {
                (T: 0.0, X: west, Y: MinLat),
                (T: width, X: east, Y: MinLat),
                (T: width + height, X: east, Y: MaxLat),
                (T: (2.0 * width) + height, X: west, Y: MaxLat),
            };

            foreach (Arc start in arcs)
            {
                if (start.Used)
                    continue;

                var ring = new List<GeoJsonPosition>();
                Arc current = start;
                for (int guard = 0; guard <= arcs.Count; guard++)
                {
                    current.Used = true;
                    foreach (Node node in current.Nodes)
                        Append(ring, ToPosition(node, source, shift));

                    Arc next = start;
                    double best = CounterclockwiseDistance(current.ExitT, start.EntryT, perimeter);
                    foreach (Arc candidate in arcs)
                    {
                        if (candidate.Used)
                            continue;

                        double distance = CounterclockwiseDistance(current.ExitT, candidate.EntryT, perimeter);
                        if (distance < best)
                        {
                            best = distance;
                            next = candidate;
                        }
                    }

                    // Corners passed between the exit point and the next entry point.
                    var passed = new List<(double Distance, double X, double Y)>();
                    foreach (var corner in corners)
                    {
                        double distance = CounterclockwiseDistance(current.ExitT, corner.T, perimeter);
                        if (distance > 0.0 && distance < best)
                            passed.Add((distance, corner.X, corner.Y));
                    }

                    passed.Sort((a, b) => a.Distance.CompareTo(b.Distance));
                    foreach (var corner in passed)
                        Append(ring, new GeoJsonPosition(corner.X + shift, corner.Y));

                    if (ReferenceEquals(next, start))
                        break;

                    current = next;
                }

                if (ring.Count > 1 && ring[ring.Count - 1].Equals(ring[0]))
                    ring.RemoveAt(ring.Count - 1);

                if (ring.Count >= 3)
                {
                    ring.Add(ring[0]);
                    if (ring.Count >= 4)
                        rings.Add(ring);
                }
            }

            return rings;
        }

        private static List<Arc> CollectArcs(IReadOnlyList<Node> chain, double west, double east)
        {
            var arcs = new List<Arc>();
            Arc? current = null;
            for (int i = 0; i < chain.Count; i++)
            {
                Node p = chain[i];
                bool inside = p.X >= west && p.X <= east;
                if (i > 0)
                {
                    Node q = chain[i - 1];
                    bool previousInside = q.X >= west && q.X <= east;
                    if (!previousInside && inside)
                    {
                        double side = q.X < west ? west : east;
                        current = new Arc();
                        if (p.X != side)
                            current.Nodes.Add(new Node(side, Interpolate(q, p, side), -1));
                    }
                    else if (previousInside && !inside && current != null)
                    {
                        double side = p.X < west ? west : east;
                        if (q.X != side)
                            current.Nodes.Add(new Node(side, Interpolate(q, p, side), -1));

                        Complete(arcs, current, west, east);
                        current = null;
                    }
                }

                if (inside && current != null)
                    current.Nodes.Add(p);
            }

            return arcs;
        }

        // A touch of the strip border (a single point) or a run along the border encloses nothing.
        private static void Complete(List<Arc> arcs, Arc arc, double west, double east)
        {
            if (arc.Nodes.Count < 2)
                return;

            bool onWest = true;
            bool onEast = true;
            foreach (Node node in arc.Nodes)
            {
                onWest &= node.X == west;
                onEast &= node.X == east;
            }

            if (!onWest && !onEast)
                arcs.Add(arc);
        }

        private static double Interpolate(Node a, Node b, double x) =>
            b.X == a.X ? a.Y : a.Y + ((b.Y - a.Y) * (x - a.X) / (b.X - a.X));

        // Position along the rectangle perimeter (counterclockwise from the south-west corner) of a point on its west or east side.
        private static double Perimeter(Node node, double west, double width, double height)
        {
            double y = Math.Max(MinLat, Math.Min(MaxLat, node.Y));
            return node.X == west
                ? (2.0 * width) + height + (MaxLat - y)
                : width + (y - MinLat);
        }

        private static double CounterclockwiseDistance(double from, double to, double perimeter)
        {
            double distance = to - from;
            while (distance < 0.0)
                distance += perimeter;
            return distance >= perimeter ? 0.0 : distance;
        }

        private static GeoJsonPosition ToPosition(Node node, IReadOnlyList<GeoJsonPosition> source, double shift)
        {
            double x = node.X + shift;
            if (node.Index >= 0 && Math.Abs(source[node.Index].Lon - x) <= 1e-9)
                return source[node.Index];

            return GeoJsonPosition.Computed(x, node.Y);
        }

        private static void Append(List<GeoJsonPosition> ring, GeoJsonPosition position)
        {
            if (ring.Count == 0 || !ring[ring.Count - 1].Equals(position))
                ring.Add(position);
        }
    }
}
