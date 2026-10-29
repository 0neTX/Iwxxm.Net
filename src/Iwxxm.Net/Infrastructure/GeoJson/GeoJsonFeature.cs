using System.Collections.Generic;
using Iwxxm.Net.Reports;

namespace Iwxxm.Net.Infrastructure.GeoJson
{
    /// <summary>
    /// An RFC 7946 <c>Feature</c> ready to be written: identifier, one geometry (<c>Point</c>, <c>LineString</c>,
    /// <c>Polygon</c> or <c>null</c>) and the ordered <c>properties</c> (entries with a <c>null</c> value are omitted).
    /// </summary>
    internal sealed class GeoJsonFeature
    {
        private GeoJsonFeature(
            string id,
            string? geometryType,
            GeoCoordinate? pointCoordinate,
            IReadOnlyList<GeoCoordinate>? lineCoordinates,
            GeoPolygon? polygon,
            List<(string Key, object? Value)> properties)
        {
            Id = id;
            GeometryType = geometryType;
            PointCoordinate = pointCoordinate;
            LineCoordinates = lineCoordinates;
            Polygon = polygon;
            Properties = properties;
        }

        public string Id { get; }
        public string? GeometryType { get; }
        public GeoCoordinate? PointCoordinate { get; }
        public IReadOnlyList<GeoCoordinate>? LineCoordinates { get; }
        public GeoPolygon? Polygon { get; }
        public List<(string Key, object? Value)> Properties { get; }

        public static GeoJsonFeature ForPoint(string id, GeoCoordinate point, List<(string Key, object? Value)> properties) =>
            new GeoJsonFeature(id, "Point", point, null, null, properties);

        public static GeoJsonFeature ForLineString(string id, IReadOnlyList<GeoCoordinate> coordinates, List<(string Key, object? Value)> properties) =>
            new GeoJsonFeature(id, "LineString", null, coordinates, null, properties);

        public static GeoJsonFeature ForPolygon(string id, GeoPolygon polygon, List<(string Key, object? Value)> properties) =>
            new GeoJsonFeature(id, "Polygon", null, null, polygon, properties);

        public static GeoJsonFeature WithoutGeometry(string id, List<(string Key, object? Value)> properties) =>
            new GeoJsonFeature(id, null, null, null, null, properties);
    }
}
