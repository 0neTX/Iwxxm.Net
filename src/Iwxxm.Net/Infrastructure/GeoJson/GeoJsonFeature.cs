using System.Collections.Generic;
using Iwxxm.Net.Infrastructure.GeoJson.Writing;
using Iwxxm.Net.Reports;

namespace Iwxxm.Net.Infrastructure.GeoJson
{
    /// <summary>
    /// An RFC 7946 <c>Feature</c> ready to be written: identifier, one geometry (<c>Point</c>, <c>LineString</c>,
    /// <c>MultiLineString</c>, <c>Polygon</c>, <c>MultiPolygon</c> or <c>null</c>) and the ordered <c>properties</c>
    /// (entries with a <c>null</c> value are omitted).
    /// </summary>
    internal sealed class GeoJsonFeature
    {
        private GeoJsonFeature(string id, GeoJsonGeometry? geometry, List<(string Key, object? Value)> properties)
        {
            Id = id;
            Geometry = geometry;
            Properties = properties;
            if (geometry != null && geometry.DroppedInteriorRings > 0)
                properties.Add(("droppedInteriorRings", geometry.DroppedInteriorRings));
        }

        public string Id { get; }
        public GeoJsonGeometry? Geometry { get; }
        public List<(string Key, object? Value)> Properties { get; }

        public static GeoJsonFeature ForPoint(string id, GeoCoordinate point, List<(string Key, object? Value)> properties) =>
            new GeoJsonFeature(id, GeoJsonGeometryFactory.Point(point), properties);

        public static GeoJsonFeature ForLineString(string id, IReadOnlyList<GeoCoordinate> coordinates, List<(string Key, object? Value)> properties) =>
            new GeoJsonFeature(id, GeoJsonGeometryFactory.LineString(coordinates), properties);

        public static GeoJsonFeature ForPolygon(string id, GeoPolygon polygon, List<(string Key, object? Value)> properties) =>
            new GeoJsonFeature(id, GeoJsonGeometryFactory.Polygon(polygon), properties);

        /// <summary>
        /// Creates a feature for a polygon or a circle. A circle is written as its approximating polygon
        /// (<see cref="GeodesicCircle"/>) and adds the properties <c>center</c> (<c>[longitude, latitude]</c>),
        /// <c>radius</c>, <c>radiusUom</c> (as reported) and <c>circleSegments</c>, from which the exact circle can be rebuilt.
        /// </summary>
        public static GeoJsonFeature ForSurface(string id, GeoSurface surface, List<(string Key, object? Value)> properties)
        {
            if (surface is GeoCircle circle)
            {
                properties.Add(("center", new[] { circle.Centre.Lon, circle.Centre.Lat }));
                properties.Add(("radius", circle.Radius.Value));
                properties.Add(("radiusUom", circle.Radius.UnitOfMeasure));
                properties.Add(("circleSegments", GeodesicCircle.SegmentCount));
            }

            return new GeoJsonFeature(id, GeoJsonGeometryFactory.Surface(surface), properties);
        }

        /// <summary>
        /// Creates a feature for several surfaces (one per <c>gml:PolygonPatch</c>): a single surface is written as by
        /// <see cref="ForSurface"/>; several are written as one <c>MultiPolygon</c> (circles approximated) with the
        /// property <c>patchCount</c>.
        /// </summary>
        public static GeoJsonFeature ForSurfaces(string id, IReadOnlyList<GeoSurface> surfaces, List<(string Key, object? Value)> properties)
        {
            if (surfaces.Count == 1)
                return ForSurface(id, surfaces[0], properties);

            var polygons = new List<IReadOnlyList<IReadOnlyList<GeoJsonPosition>>>();
            bool crosses = false;
            int dropped = 0;
            foreach (GeoSurface surface in surfaces)
            {
                GeoJsonGeometry geometry = GeoJsonGeometryFactory.Surface(surface);
                polygons.AddRange(geometry.Polygons!);
                crosses |= geometry.CrossesAntimeridian;
                dropped += geometry.DroppedInteriorRings;
            }

            properties.Add(("patchCount", surfaces.Count));
            return new GeoJsonFeature(id, GeoJsonGeometry.ForPolygons(polygons, crosses).WithDroppedInteriorRings(dropped), properties);
        }

        public static GeoJsonFeature WithoutGeometry(string id, List<(string Key, object? Value)> properties) =>
            new GeoJsonFeature(id, null, properties);
    }
}
