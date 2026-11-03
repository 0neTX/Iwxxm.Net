using System;
using System.Collections.Generic;
using System.Linq;

namespace Iwxxm.Net.Infrastructure.GeoJson.Writing
{
    /// <summary>
    /// RFC 7946 §3.1.9 antimeridian handling for output geometries. Two consecutive positions whose longitudes differ by
    /// more than 180 degrees are joined by the short way, across the antimeridian. Such lines are cut at ±180 into a
    /// <c>MultiLineString</c>; such polygons are unwrapped and clipped into the [-180, 180] longitude strips, giving a
    /// <c>MultiPolygon</c>. A ring that wraps the whole globe (it encloses a pole) is written as the polygon bounded by the
    /// ring from longitude -180 to +180 and by the pole on the side of its mean latitude. Interior rings that lie entirely
    /// inside that cap are kept as its holes; an interior ring that cannot be represented that way (it crosses the
    /// antimeridian, or is not inside the cap) is dropped and counted, so the caller can report it explicitly.
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
                var cap = PolarCap(unwrapped, exterior.Select(p => p.Lat).ToList(), meanLat >= 0 ? 90.0 : -90.0);
                var capRings = new List<List<GeoJsonPosition>> { cap };
                foreach (List<GeoJsonPosition> hole in holes)
                {
                    if (hole.Count >= 4 && !Crosses(hole) && hole.All(p => IsInside(cap, p)))
                        capRings.Add(hole);
                    else
                        droppedInteriorRings++;
                }

                return new List<List<List<GeoJsonPosition>>> { capRings };
            }

            double min = unwrapped.Min();
            double max = unwrapped.Max();
            var pieces = new List<List<List<GeoJsonPosition>>>();
            for (int k = (int)Math.Floor((min + 180.0) / 360.0); k <= (int)Math.Floor((max + 180.0) / 360.0); k++)
            {
                double west = -180.0 + (360.0 * k);
                double east = west + 360.0;
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
            var lons = new List<double>(ring.Count) { ring[0].Lon };
            for (int i = 1; i < ring.Count; i++)
            {
                double delta = ring[i].Lon - ring[i - 1].Lon;
                if (IsCrossing(ring[i - 1], ring[i]))
                {
                    while (delta > 180.0) delta -= 360.0;
                    while (delta < -180.0) delta += 360.0;
                }
                lons.Add(lons[i - 1] + delta);
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
