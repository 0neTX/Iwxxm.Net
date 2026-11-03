using System;
using System.Collections.Generic;
using System.Linq;

namespace Iwxxm.Net.Infrastructure.GeoJson.Writing
{
    /// <summary>
    /// RFC 7946 §3.1.9 antimeridian handling for output geometries. Two consecutive positions whose longitudes differ by
    /// more than 180 degrees are joined by the short way, across the antimeridian. Such lines are cut at ±180 into a
    /// <c>MultiLineString</c>; such polygons are unwrapped and clipped into the [-180, 180] longitude strips, giving a
    /// <c>MultiPolygon</c>. A ring that wraps the whole globe (its unwrapped longitudes advance by a full turn: it
    /// encloses a pole) is written as the region between the ring and the pole on the side of its mean latitude: the
    /// ring is repeated every 360 degrees, clipped to the [-180, 180] strip and closed along the antimeridian and the
    /// pole, so a ring that leaves the strip and comes back (a lobe across the antimeridian) loses no vertex and may give
    /// several polygons. A ring that crosses a strip border more than twice is clipped the same way, so its pieces are
    /// never joined by a zero-width bridge. Interior rings that lie entirely inside a cap are kept as its holes; an
    /// interior ring that cannot be represented that way (it crosses the antimeridian, or is not inside the cap) is
    /// dropped and counted, so the caller can report it explicitly.
    /// </summary>
    internal static class AntimeridianSplitter
    {
        private const double Epsilon = 1e-9;

        /// <summary>Gets a value indicating whether a sequence crosses the antimeridian (an edge spans more than 180 degrees).</summary>
        public static bool Crosses(IReadOnlyList<GeoJsonPosition> positions)
        {
            for (int i = 1; i < positions.Count; i++)
            {
                if (IsCrossing(positions[i - 1], positions[i]))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// An edge crosses the antimeridian when its longitudes differ by more than 180 degrees, unless both ends lie on
        /// the antimeridian itself (an edge from -180 to +180 along a parallel is a full-globe band edge, e.g. the space
        /// weather latitude bands "W180-E180", and is kept as reported).
        /// </summary>
        private static bool IsCrossing(GeoJsonPosition a, GeoJsonPosition b) =>
            Math.Abs(b.Lon - a.Lon) > 180.0 && !(Math.Abs(a.Lon) == 180.0 && Math.Abs(b.Lon) == 180.0);

        /// <summary>Cuts a line at every antimeridian crossing.</summary>
        public static List<IReadOnlyList<GeoJsonPosition>> SplitLine(IReadOnlyList<GeoJsonPosition> line)
        {
            var parts = new List<IReadOnlyList<GeoJsonPosition>>();
            var current = new List<GeoJsonPosition> { line[0] };
            for (int i = 1; i < line.Count; i++)
            {
                GeoJsonPosition a = line[i - 1];
                GeoJsonPosition b = line[i];
                double delta = b.Lon - a.Lon;
                if (IsCrossing(a, b))
                {
                    double boundary = delta < 0 ? 180.0 : -180.0;            // a is east of the antimeridian when b wraps to the west
                    double unwrappedB = b.Lon + (delta < 0 ? 360.0 : -360.0);
                    double t = (boundary - a.Lon) / (unwrappedB - a.Lon);
                    double lat = a.Lat + (t * (b.Lat - a.Lat));
                    current.Add(GeoJsonPosition.Computed(boundary, lat));
                    parts.Add(current);
                    current = new List<GeoJsonPosition> { GeoJsonPosition.Computed(-boundary, lat) };
                }

                current.Add(b);
            }

            parts.Add(current);
            return parts;
        }

        /// <summary>Splits a polygon (exterior plus holes) that crosses the antimeridian into polygons inside [-180, 180].</summary>
        public static List<List<List<GeoJsonPosition>>> SplitPolygon(List<GeoJsonPosition> exterior, List<List<GeoJsonPosition>> holes, out bool isPolarCap, out int droppedInteriorRings)
        {
            isPolarCap = false;
            droppedInteriorRings = 0;
            List<double> unwrapped = Unwrap(exterior);
            double wrap = unwrapped[unwrapped.Count - 1] - unwrapped[0];
            if (Math.Abs(wrap) > 180.0)
            {
                // The ring goes once around the globe: it encloses a pole.
                isPolarCap = true;
                double meanLat = exterior.Average(p => p.Lat);
                List<List<GeoJsonPosition>> caps = PolarCaps(exterior, unwrapped, meanLat >= 0 ? 90.0 : -90.0);
                var polygons = new List<List<List<GeoJsonPosition>>>(caps.Count);
                foreach (List<GeoJsonPosition> cap in caps)
                    polygons.Add(new List<List<GeoJsonPosition>> { cap });

                foreach (List<GeoJsonPosition> hole in holes)
                {
                    List<List<GeoJsonPosition>>? owner = null;
                    if (hole.Count >= 4 && !Crosses(hole))
                        owner = polygons.FirstOrDefault(p => hole.All(v => IsInside(p[0], v)));

                    if (owner != null)
                        owner.Add(hole);
                    else
                        droppedInteriorRings++;
                }

                return polygons;
            }

            double min = unwrapped.Min();
            double max = unwrapped.Max();
            var pieces = new List<List<List<GeoJsonPosition>>>();
            List<StripClipper.Node> ringNodes = RingNodes(exterior, unwrapped);
            for (int k = (int)Math.Floor((min + 180.0) / 360.0); k <= (int)Math.Floor((max + 180.0) / 360.0); k++)
            {
                double west = -180.0 + (360.0 * k);
                double east = west + 360.0;

                // More than one piece inside the strip: assemble them properly (Sutherland-Hodgman would join them
                // with zero-width bridges along the strip border).
                List<List<GeoJsonPosition>> separate = ClipSeparatePieces(ringNodes, exterior, west, east, -360.0 * k);
                if (separate.Count > 1)
                {
                    var multi = separate.Select(r => new List<List<GeoJsonPosition>> { r }).ToList();
                    foreach (List<GeoJsonPosition> hole in holes)
                    {
                        List<double> holeLons = Unwrap(hole);
                        double holeShift = 360.0 * Math.Round(((min + max) / 2.0 - holeLons.Average()) / 360.0);
                        List<GeoJsonPosition>? clippedHole = Clip(hole, holeLons.Select(l => l + holeShift).ToList(), west, east, -360.0 * k);
                        if (clippedHole == null)
                            continue;

                        List<List<GeoJsonPosition>>? owner = multi.FirstOrDefault(p => IsInside(p[0], clippedHole[0]));
                        if (owner != null)
                            owner.Add(clippedHole);
                        else
                            droppedInteriorRings++;
                    }

                    pieces.AddRange(multi);
                    continue;
                }

                List<GeoJsonPosition>? clipped = Clip(exterior, unwrapped, west, east, -360.0 * k);
                if (clipped == null)
                    continue;

                var rings = new List<List<GeoJsonPosition>> { clipped };
                foreach (List<GeoJsonPosition> hole in holes)
                {
                    List<double> holeLons = Unwrap(hole);
                    double shift = 360.0 * Math.Round(((min + max) / 2.0 - holeLons.Average()) / 360.0);
                    List<double> shifted = holeLons.Select(l => l + shift).ToList();
                    List<GeoJsonPosition>? clippedHole = Clip(hole, shifted, west, east, -360.0 * k);
                    if (clippedHole != null)
                        rings.Add(clippedHole);
                }

                pieces.Add(rings);
            }

            return pieces;
        }

        /// <summary>Nodes of a closed ring at their unwrapped longitudes (the closing vertex is not repeated).</summary>
        private static List<StripClipper.Node> RingNodes(IReadOnlyList<GeoJsonPosition> ring, IReadOnlyList<double> unwrapped)
        {
            int count = ring.Count > 1 && ring[0].Equals(ring[ring.Count - 1]) ? ring.Count - 1 : ring.Count;
            var nodes = new List<StripClipper.Node>(count);
            for (int i = 0; i < count; i++)
                nodes.Add(new StripClipper.Node(unwrapped[i], ring[i].Lat, i));

            return nodes;
        }

        /// <summary>
        /// The pieces of an unwrapped closed ring inside the strip [west, east], assembled so that the region stays on the left
        /// of the boundary (a clockwise ring is traversed backwards).
        /// </summary>
        private static List<List<GeoJsonPosition>> ClipSeparatePieces(List<StripClipper.Node> nodes, IReadOnlyList<GeoJsonPosition> source, double west, double east, double shift)
        {
            double area = 0.0;
            for (int i = 0, j = nodes.Count - 1; i < nodes.Count; j = i++)
                area += (nodes[j].X * nodes[i].Y) - (nodes[i].X * nodes[j].Y);

            var ordered = new List<StripClipper.Node>(nodes);
            if (area < 0.0)
                ordered.Reverse();

            // Start at a vertex outside the strip so that every piece is a complete arc.
            int start = ordered.FindIndex(n => n.X < west || n.X > east);
            if (start < 0)
                return new List<List<GeoJsonPosition>>();

            var chain = new List<StripClipper.Node>(ordered.Count + 1);
            for (int i = 0; i <= ordered.Count; i++)
                chain.Add(ordered[(start + i) % ordered.Count]);

            return StripClipper.Clip(chain, source, west, east, shift);
        }

        /// <summary>
        /// The polygons of the region between a pole-enclosing ring and the pole: the unwrapped ring is repeated every 360
        /// degrees, clipped to [-180, 180] and closed along the antimeridian and the pole. Every vertex of the ring is kept.
        /// </summary>
        private static List<List<GeoJsonPosition>> PolarCaps(IReadOnlyList<GeoJsonPosition> ring, IReadOnlyList<double> unwrapped, double poleLat)
        {
            List<StripClipper.Node> nodes = RingNodes(ring, unwrapped);
            double turn = unwrapped[unwrapped.Count - 1] - unwrapped[0];
            double period = turn > 0 ? 360.0 : -360.0;
            double minLon = nodes.Min(n => n.X);
            double maxLon = nodes.Max(n => n.X);

            // Repetitions (shift = j * period) that reach the strip, plus one on each side so that the chain starts and ends outside it.
            int low = (int)Math.Ceiling((-180.0 - maxLon) / 360.0) - 1;
            int high = (int)Math.Floor((180.0 - minLon) / 360.0) + 1;
            var chain = new List<StripClipper.Node>(nodes.Count * (high - low + 1));
            for (int j = turn > 0 ? low : -high; j <= (turn > 0 ? high : -low); j++)
            {
                double shift = j * period;
                foreach (StripClipper.Node node in nodes)
                    chain.Add(new StripClipper.Node(node.X + shift, node.Y, node.Index));
            }

            // The region (towards the pole) must be on the left of the direction of travel: east with the north pole, west with the south pole.
            if ((poleLat > 0) != (turn > 0))
                chain.Reverse();

            List<List<GeoJsonPosition>> caps = StripClipper.Clip(chain, ring, -180.0, 180.0, 0.0);
            if (caps.Count == 0)
            {
                // Degenerate input (for example every vertex on the antimeridian): fall back to the single-turn cap.
                caps.Add(PolarCap(unwrapped, ring.Select(p => p.Lat).ToList(), poleLat));
            }

            return caps;
        }

        /// <summary>Builds the ring of a pole-enclosing boundary from its unwrapped longitudes.</summary>
        internal static List<GeoJsonPosition> PolarCap(IReadOnlyList<double> unwrappedLons, IReadOnlyList<double> lats, double poleLat)
        {
            int n = unwrappedLons.Count;
            var lons = unwrappedLons.ToArray();
            var latitudes = lats.ToArray();
            if (lons[n - 1] < lons[0])
            {
                Array.Reverse(lons);
                Array.Reverse(latitudes);
            }

            double turn = lons[n - 1] - lons[0];
            double shift = -360.0 * Math.Ceiling((lons[0] + 180.0) / 360.0);
            var extendedLon = new List<double>(2 * n);
            var extendedLat = new List<double>(2 * n);
            for (int copy = 0; copy < 2; copy++)
            {
                for (int k = copy == 0 ? 0 : 1; k < n; k++)
                {
                    extendedLon.Add(lons[k] + shift + (copy * turn));
                    extendedLat.Add(latitudes[k]);
                }
            }

            var ring = new List<GeoJsonPosition>(n + 6);
            for (int k = 1; k < extendedLon.Count; k++)
            {
                double a = extendedLon[k - 1];
                double b = extendedLon[k];
                if (a <= -180.0 && b > -180.0)
                    ring.Add(GeoJsonPosition.Computed(-180.0, Interpolate(a, extendedLat[k - 1], b, extendedLat[k], -180.0)));

                if (b > -180.0 && b < 180.0)
                    ring.Add(GeoJsonPosition.Computed(b, extendedLat[k]));

                if (a < 180.0 && b >= 180.0)
                {
                    ring.Add(GeoJsonPosition.Computed(180.0, Interpolate(a, extendedLat[k - 1], b, extendedLat[k], 180.0)));
                    break;
                }
            }

            ring.Add(new GeoJsonPosition(180.0, poleLat));
            ring.Add(new GeoJsonPosition(-180.0, poleLat));
            ring.Add(ring[0]);
            return ring;
        }

        /// <summary>Even-odd point-in-ring test in the longitude/latitude plane.</summary>
        private static bool IsInside(IReadOnlyList<GeoJsonPosition> ring, GeoJsonPosition point)
        {
            bool inside = false;
            for (int i = 0, j = ring.Count - 1; i < ring.Count; j = i++)
            {
                GeoJsonPosition a = ring[i];
                GeoJsonPosition b = ring[j];
                if ((a.Lat > point.Lat) != (b.Lat > point.Lat) &&
                    point.Lon < ((b.Lon - a.Lon) * (point.Lat - a.Lat) / (b.Lat - a.Lat)) + a.Lon)
                {
                    inside = !inside;
                }
            }

            return inside;
        }

        private static List<double> Unwrap(IReadOnlyList<GeoJsonPosition> ring)
        {
            // Longitude plus a whole number of turns, so a position on the antimeridian stays exactly at ±180 (+ k * 360).
            var lons = new List<double>(ring.Count) { ring[0].Lon };
            int turns = 0;
            for (int i = 1; i < ring.Count; i++)
            {
                if (IsCrossing(ring[i - 1], ring[i]))
                {
                    double delta = ring[i].Lon - ring[i - 1].Lon;
                    while (delta > 180.0) { delta -= 360.0; turns--; }
                    while (delta < -180.0) { delta += 360.0; turns++; }
                }

                lons.Add(ring[i].Lon + (360.0 * turns));
            }

            return lons;
        }

        /// <summary>Sutherland–Hodgman clipping of an unwrapped ring against the strip [west, east], shifted back by <paramref name="shift"/>.</summary>
        private static List<GeoJsonPosition>? Clip(IReadOnlyList<GeoJsonPosition> ring, IReadOnlyList<double> lons, double west, double east, double shift)
        {
            var points = new List<(double Lon, double Lat)>(ring.Count);
            for (int i = 0; i < ring.Count; i++)
                points.Add((lons[i], ring[i].Lat));

            if (points.Count > 1 && points[0].Lon == points[points.Count - 1].Lon && points[0].Lat == points[points.Count - 1].Lat)
                points.RemoveAt(points.Count - 1);

            points = ClipHalfPlane(points, west, keepGreater: true);
            points = ClipHalfPlane(points, east, keepGreater: false);
            if (points.Count < 3 || Math.Abs(Area(points)) < Epsilon)
                return null;

            var result = points.Select(p => GeoJsonPosition.Computed(p.Lon + shift, p.Lat)).ToList();
            result.Add(result[0]);
            return result;
        }

        private static List<(double Lon, double Lat)> ClipHalfPlane(List<(double Lon, double Lat)> input, double x, bool keepGreater)
        {
            var output = new List<(double Lon, double Lat)>(input.Count + 4);
            if (input.Count == 0)
                return output;

            bool Inside((double Lon, double Lat) p) => keepGreater ? p.Lon >= x : p.Lon <= x;
            (double Lon, double Lat) previous = input[input.Count - 1];
            foreach ((double Lon, double Lat) current in input)
            {
                bool currentInside = Inside(current);
                bool previousInside = Inside(previous);
                if (currentInside != previousInside)
                    output.Add((x, Interpolate(previous.Lon, previous.Lat, current.Lon, current.Lat, x)));
                if (currentInside)
                    output.Add(current);
                previous = current;
            }

            return output;
        }

        private static double Area(List<(double Lon, double Lat)> points)
        {
            double area = 0.0;
            for (int i = 0, j = points.Count - 1; i < points.Count; j = i++)
                area += (points[j].Lon * points[i].Lat) - (points[i].Lon * points[j].Lat);
            return area / 2.0;
        }

        private static double Interpolate(double lonA, double latA, double lonB, double latB, double lon) =>
            lonB == lonA ? latA : latA + ((latB - latA) * (lon - lonA) / (lonB - lonA));
    }
}
