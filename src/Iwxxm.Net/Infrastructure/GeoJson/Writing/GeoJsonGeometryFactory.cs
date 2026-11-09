using System.Collections.Generic;
using Iwxxm.Net.Reports;

namespace Iwxxm.Net.Infrastructure.GeoJson.Writing
{
    /// <summary>
    /// Converts Domain spatial value objects into RFC 7946 geometries. The Domain keeps the geometry exactly as reported;
    /// output-only adaptations (circle approximation, ring orientation, antimeridian splitting) happen here.
    /// </summary>
    internal static class GeoJsonGeometryFactory
    {
        public static GeoJsonGeometry Point(GeoCoordinate coordinate) =>
            GeoJsonGeometry.ForPoint(GeoJsonPosition.From(coordinate));

        /// <summary>A line string; a line crossing the antimeridian becomes a <c>MultiLineString</c> cut at ±180.</summary>
        public static GeoJsonGeometry LineString(IReadOnlyList<GeoCoordinate> coordinates)
        {
            List<GeoJsonPosition> line = ToPositions(coordinates);
            if (line.Count > 1 && AntimeridianSplitter.Crosses(line))
            {
                List<IReadOnlyList<GeoJsonPosition>> parts = AntimeridianSplitter.SplitLine(line);
                return GeoJsonGeometry.ForLines(parts, parts.Count > 1);
            }

            return GeoJsonGeometry.ForLines(new[] { (IReadOnlyList<GeoJsonPosition>)line }, false);
        }

        public static GeoJsonGeometry Surface(GeoSurface surface) => surface.Match(Polygon, Circle);

        /// <summary>
        /// A polygon written with the RFC 7946 §3.1.6 right-hand rule (exterior counterclockwise, holes clockwise) and split
        /// at the antimeridian (§3.1.9) into a <c>MultiPolygon</c> when it crosses it. Only the output is adapted; the
        /// Domain keeps the source vertex order.
        /// </summary>
        public static GeoJsonGeometry Polygon(GeoPolygon polygon)
        {
            List<GeoJsonPosition> exterior = ToPositions(polygon.ExteriorRing);
            var holes = new List<List<GeoJsonPosition>>(polygon.InteriorRings.Count);
            foreach (IReadOnlyList<GeoCoordinate> hole in polygon.InteriorRings)
                holes.Add(ToPositions(hole));

            return FromRings(exterior, holes);
        }

        /// <summary>Approximates a circle by a ring of <see cref="GeodesicCircle.SegmentCount"/> segments (see <see cref="GeodesicCircle"/>).</summary>
        public static GeoJsonGeometry Circle(GeoCircle circle)
        {
            List<GeoJsonPosition> ring = GeodesicCircle.Densify(circle, out bool enclosesPole);
            if (enclosesPole)
            {
                return GeoJsonGeometry.ForPolygons(
                    new[] { (IReadOnlyList<IReadOnlyList<GeoJsonPosition>>)new[] { (IReadOnlyList<GeoJsonPosition>)Oriented(ring, counterclockwise: true) } },
                    false);
            }

            return FromRings(ring, new List<List<GeoJsonPosition>>());
        }

        private static GeoJsonGeometry FromRings(List<GeoJsonPosition> exterior, List<List<GeoJsonPosition>> holes)
        {
            if (!AntimeridianSplitter.Crosses(exterior))
            {
                var rings = new List<IReadOnlyList<GeoJsonPosition>>(1 + holes.Count) { Oriented(exterior, counterclockwise: true) };
                foreach (List<GeoJsonPosition> hole in holes)
                    rings.Add(Oriented(hole, counterclockwise: false));

                return GeoJsonGeometry.ForPolygons(new[] { (IReadOnlyList<IReadOnlyList<GeoJsonPosition>>)rings }, false);
            }

            var polygons = new List<IReadOnlyList<IReadOnlyList<GeoJsonPosition>>>();
            List<List<List<GeoJsonPosition>>> pieces = AntimeridianSplitter.SplitPolygon(exterior, holes, out bool isPolarCap, out int dropped);
            foreach (List<List<GeoJsonPosition>> piece in pieces)
            {
                var rings = new List<IReadOnlyList<GeoJsonPosition>>(piece.Count) { Oriented(piece[0], counterclockwise: true) };
                for (int h = 1; h < piece.Count; h++)
                    rings.Add(Oriented(piece[h], counterclockwise: false));
                polygons.Add(rings);
            }

            // A polar cap is already laid out from -180 to +180 (bbox spans every longitude, RFC 7946 §5.3).
            return GeoJsonGeometry.ForPolygons(polygons, !isPolarCap).WithDroppedInteriorRings(dropped);
        }

        /// <summary>Reverses <paramref name="ring"/> when its orientation (signed area on longitude/latitude) is not the requested one.</summary>
        internal static List<GeoJsonPosition> Oriented(List<GeoJsonPosition> ring, bool counterclockwise)
        {
            double area = SignedArea(ring);
            if ((counterclockwise && area < 0.0) || (!counterclockwise && area > 0.0))
                ring.Reverse();

            return ring;
        }

        /// <summary>Shoelace signed area in the longitude/latitude plane: positive for a counterclockwise ring.</summary>
        internal static double SignedArea(IReadOnlyList<GeoJsonPosition> ring)
        {
            double area = 0.0;
            for (int i = 0, j = ring.Count - 1; i < ring.Count; j = i++)
            {
                area += (ring[j].Lon * ring[i].Lat) - (ring[i].Lon * ring[j].Lat);
            }

            return area / 2.0;
        }

        private static List<GeoJsonPosition> ToPositions(IReadOnlyList<GeoCoordinate> coordinates)
        {
            var positions = new List<GeoJsonPosition>(coordinates.Count);
            foreach (GeoCoordinate coordinate in coordinates)
                positions.Add(GeoJsonPosition.From(coordinate));

            return positions;
        }
    }
}
