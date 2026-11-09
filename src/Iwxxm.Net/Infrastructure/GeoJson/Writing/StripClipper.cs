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

            public int Index { get; set; }

            public IReadOnlyList<GeoJsonPosition> Source { get; set; } = Array.Empty<GeoJsonPosition>();
        }

        /// <summary>
        /// Clips a chain whose first and last vertices lie outside the strip. <paramref name="shift"/> is added to every
        /// longitude of the result (it maps a strip beyond [-180, 180] back into it).
        /// </summary>
        public static List<List<GeoJsonPosition>> Clip(IReadOnlyList<Node> chain, IReadOnlyList<GeoJsonPosition> source, double west, double east, double shift) =>
            Clip(new[] { (chain, source) }, west, east, shift);

        /// <summary>
        /// Clips several chains together (each one starts and ends outside the strip and keeps the region on its left, each one
        /// with its own source positions): the arcs of all chains are joined along the rectangle as one set. The chains of
        /// the interior rings of a polygon, traversed so that the polygon is on their left (clockwise), cut notches into the
        /// pieces of the exterior where they leave the strip. The arcs of the first chain come first (the lowest arc indexes).
        /// </summary>
        public static List<List<GeoJsonPosition>> Clip(IReadOnlyList<(IReadOnlyList<Node> Chain, IReadOnlyList<GeoJsonPosition> Source)> chains, double west, double east, double shift)
        {
            var arcs = new List<Arc>();
            foreach ((IReadOnlyList<Node> chain, IReadOnlyList<GeoJsonPosition> source) in chains)
            {
                int first = arcs.Count;
                CollectArcs(chain, west, east, arcs);
                for (int a = first; a < arcs.Count; a++)
                    arcs[a].Source = source;
            }

            var rings = new List<List<GeoJsonPosition>>();
            if (arcs.Count == 0)
                return rings;

            double width = east - west;
            double height = MaxLat - MinLat;
            double perimeter = (2.0 * width) + (2.0 * height);
            for (int i = 0; i < arcs.Count; i++)
            {
                Arc arc = arcs[i];
                arc.Index = i;
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

            var successors = new SuccessorIndex(arcs);
            foreach (Arc start in arcs)
            {
                if (start.Used)
                    continue;

                var ring = new List<GeoJsonPosition>();
                Arc current = start;
                for (int guard = 0; guard <= arcs.Count; guard++)
                {
                    current.Used = true;
                    successors.Remove(current);
                    foreach (Node node in current.Nodes)
                        Append(ring, ToPosition(node, current.Source, shift));

                    // The successor is the unused arc nearest counterclockwise to the exit point, unless the start arc is at least
                    // as near (a tie closes the ring); see SuccessorIndex for how it is found without scanning every arc.
                    Arc next = start;
                    double best = CounterclockwiseDistance(current.ExitT, start.EntryT, perimeter);
                    if (!double.IsNaN(best) && successors.TryFindNearest(current.ExitT, perimeter, out Arc? nearest, out double nearestDistance) && nearestDistance < best)
                    {
                        best = nearestDistance;
                        next = nearest!;
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

        /// <summary>
        /// Finds the unused arc whose entry point is nearest counterclockwise to a given position on the rectangle perimeter, in
        /// O(log n) amortised instead of scanning every arc.
        /// <para>
        /// The reference behaviour (kept as a test-only copy and compared on generated rings) is: among the unused arcs, the one with the
        /// smallest <see cref="CounterclockwiseDistance"/> to the exit position wins; on equal distances the arc that comes first in the
        /// list (lowest <see cref="Arc.Index"/>) wins. The arcs are sorted once by entry position and grouped in buckets of exactly equal
        /// positions. Because floating-point subtraction and addition are monotone, the distance is non-decreasing along the sorted buckets
        /// within each of the two runs the exit position cuts the order into (entries at or after it, then entries before it, which pass
        /// through the wrap-around); only the tail of a run can break the order, when a distance rounds up to the perimeter and is reset
        /// to 0 (an entry a hair before the exit position). So the minimum is always at the first live bucket of a run or at the last live
        /// bucket of a run, and equal minima (including different positions that round to the same distance) are neighbours of those
        /// buckets; they are walked while the distance stays equal. Within a bucket the arcs are always consumed in ascending index order
        /// (the successor and the next ring start are both the lowest index among the best candidates), so a bucket keeps one pointer to
        /// its first unused arc. Emptied buckets are skipped with two union-find structures (next and previous live bucket).
        /// </para>
        /// </summary>
        private sealed class SuccessorIndex
        {
            private readonly List<Arc> _arcs;
            private readonly double[] _keys;
            private readonly int[] _members;
            private readonly int[] _bucketStart;
            private readonly int[] _head;
            private readonly int[] _bucketOf;
            private readonly int[] _next;
            private readonly int[] _previous;

            public SuccessorIndex(List<Arc> arcs)
            {
                _arcs = arcs;
                _bucketOf = new int[arcs.Count];

                // An arc whose entry position is not a number never has a defined distance, so it is never a successor.
                var order = new List<int>(arcs.Count);
                for (int i = 0; i < arcs.Count; i++)
                {
                    _bucketOf[i] = -1;
                    if (!double.IsNaN(arcs[i].EntryT))
                        order.Add(i);
                }

                _members = order.ToArray();
                Array.Sort(_members, (a, b) =>
                {
                    int byPosition = arcs[a].EntryT.CompareTo(arcs[b].EntryT);
                    return byPosition != 0 ? byPosition : a.CompareTo(b);
                });

                var keys = new List<double>();
                var starts = new List<int>();
                for (int i = 0; i < _members.Length; i++)
                {
                    double t = arcs[_members[i]].EntryT;
                    if (keys.Count == 0 || keys[keys.Count - 1] != t)
                    {
                        keys.Add(t);
                        starts.Add(i);
                    }

                    _bucketOf[_members[i]] = keys.Count - 1;
                }

                starts.Add(_members.Length);
                _keys = keys.ToArray();
                _bucketStart = starts.ToArray();
                _head = new int[_keys.Length];
                Array.Copy(_bucketStart, _head, _keys.Length);

                // _next[b]: first live bucket >= b (Count when none). _previous[b + 1]: last live bucket <= b, plus one (0 when none).
                _next = new int[_keys.Length + 1];
                _previous = new int[_keys.Length + 1];
                for (int i = 0; i < _next.Length; i++)
                {
                    _next[i] = i;
                    _previous[i] = i;
                }
            }

            /// <summary>Takes an arc that has just been marked used out of the index.</summary>
            public void Remove(Arc arc)
            {
                int bucket = _bucketOf[arc.Index];
                if (bucket < 0)
                    return;

                int end = _bucketStart[bucket + 1];
                while (_head[bucket] < end && _arcs[_members[_head[bucket]]].Used)
                    _head[bucket]++;

                if (_head[bucket] == end)
                {
                    _next[bucket] = bucket + 1;
                    _previous[bucket + 1] = bucket;
                }
            }

            /// <summary>The unused arc with the smallest counterclockwise distance (lowest index on ties) from <paramref name="from"/>.</summary>
            public bool TryFindNearest(double from, double perimeter, out Arc? arc, out double distance)
            {
                int count = _keys.Length;
                int split = LowerBound(from);

                // First live bucket of each run (entries at or after the exit position, entries before it) and last live bucket of each run.
                int firstAfter = NextLive(split);
                int firstBefore = NextLive(0);
                if (firstBefore >= split)
                    firstBefore = count;

                int lastAfter = PreviousLive(count - 1);
                if (lastAfter < split)
                    lastAfter = -1;

                int lastBefore = split > 0 ? PreviousLive(split - 1) : -1;

                double minimum = double.PositiveInfinity;
                minimum = Math.Min(minimum, DistanceOf(firstAfter, from, perimeter));
                minimum = Math.Min(minimum, DistanceOf(firstBefore, from, perimeter));
                minimum = Math.Min(minimum, DistanceOf(lastAfter, from, perimeter));
                minimum = Math.Min(minimum, DistanceOf(lastBefore, from, perimeter));
                if (double.IsPositiveInfinity(minimum))
                {
                    arc = null;
                    distance = double.NaN;
                    return false;
                }

                int lowest = int.MaxValue;
                lowest = Math.Min(lowest, Walk(firstAfter, 1, minimum, from, perimeter));
                lowest = Math.Min(lowest, Walk(firstBefore, 1, minimum, from, perimeter));
                lowest = Math.Min(lowest, Walk(lastAfter, -1, minimum, from, perimeter));
                lowest = Math.Min(lowest, Walk(lastBefore, -1, minimum, from, perimeter));
                arc = _arcs[lowest];
                distance = minimum;
                return true;
            }

            // Lowest arc index among the live buckets reached from `bucket` in one direction while their distance equals the minimum.
            private int Walk(int bucket, int step, double minimum, double from, double perimeter)
            {
                int lowest = int.MaxValue;
                while (bucket >= 0 && bucket < _keys.Length && CounterclockwiseDistance(from, _keys[bucket], perimeter) == minimum)
                {
                    lowest = Math.Min(lowest, _members[_head[bucket]]);
                    bucket = step > 0 ? NextLive(bucket + 1) : PreviousLive(bucket - 1);
                }

                return lowest;
            }

            private double DistanceOf(int bucket, double from, double perimeter) =>
                bucket >= 0 && bucket < _keys.Length ? CounterclockwiseDistance(from, _keys[bucket], perimeter) : double.PositiveInfinity;

            // First bucket whose position is >= value (Count when none).
            private int LowerBound(double value)
            {
                int low = 0;
                int high = _keys.Length;
                while (low < high)
                {
                    int middle = low + ((high - low) / 2);
                    if (_keys[middle] < value)
                        low = middle + 1;
                    else
                        high = middle;
                }

                return low;
            }

            private int NextLive(int bucket) => Find(_next, bucket);

            private int PreviousLive(int bucket) => Find(_previous, bucket + 1) - 1;

            private static int Find(int[] parent, int item)
            {
                while (parent[item] != item)
                {
                    parent[item] = parent[parent[item]];
                    item = parent[item];
                }

                return item;
            }
        }

        /// <summary>Gets a value indicating whether the chain has at least one arc inside the strip that encloses something.</summary>
        public static bool HasArcs(IReadOnlyList<Node> chain, double west, double east)
        {
            var arcs = new List<Arc>();
            CollectArcs(chain, west, east, arcs);
            return arcs.Count > 0;
        }

        private static void CollectArcs(IReadOnlyList<Node> chain, double west, double east, List<Arc> arcs)
        {
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
