using System;
using System.Collections.Generic;
using Iwxxm.Net.Reports;

namespace Iwxxm.Net.Infrastructure.GeoJson.Writing
{
    /// <summary>An RFC 7946 position <c>[longitude, latitude]</c> (degrees).</summary>
    internal readonly struct GeoJsonPosition : IEquatable<GeoJsonPosition>
    {
        public GeoJsonPosition(double lon, double lat)
        {
            Lon = lon;
            Lat = lat;
        }

        public double Lon { get; }

        public double Lat { get; }

        public static GeoJsonPosition From(GeoCoordinate coordinate) => new GeoJsonPosition(coordinate.Lon, coordinate.Lat);

        /// <summary>Rounds a computed position to the <see cref="GeoCoordinate.Precision"/> grid used by the Domain.</summary>
        public static GeoJsonPosition Computed(double lon, double lat) =>
            new GeoJsonPosition(
                Math.Round(lon, GeoCoordinate.DecimalPlaces, MidpointRounding.AwayFromZero) + 0.0,
                Math.Round(lat, GeoCoordinate.DecimalPlaces, MidpointRounding.AwayFromZero) + 0.0);

        public bool Equals(GeoJsonPosition other) => Lon.Equals(other.Lon) && Lat.Equals(other.Lat);

        public override bool Equals(object? obj) => obj is GeoJsonPosition other && Equals(other);

        public override int GetHashCode() => unchecked((Lon.GetHashCode() * 397) ^ Lat.GetHashCode());
    }

    /// <summary>RFC 7946 geometry types written by the exporter.</summary>
    internal enum GeoJsonGeometryType
    {
        Point,
        LineString,
        MultiLineString,
        Polygon,
        MultiPolygon
    }

    /// <summary>
    /// A GeoJSON geometry ready to be written: RFC 7946 coordinates (<c>[longitude, latitude]</c>), exterior rings
    /// counterclockwise and holes clockwise, geometries crossing the antimeridian split into Multi* geometries.
    /// </summary>
    internal sealed class GeoJsonGeometry
    {
        private GeoJsonGeometry(
            GeoJsonGeometryType type,
            GeoJsonPosition point,
            IReadOnlyList<IReadOnlyList<GeoJsonPosition>>? lines,
            IReadOnlyList<IReadOnlyList<IReadOnlyList<GeoJsonPosition>>>? polygons,
            bool crossesAntimeridian)
        {
            Type = type;
            Point = point;
            Lines = lines;
            Polygons = polygons;
            CrossesAntimeridian = crossesAntimeridian;
        }

        public GeoJsonGeometryType Type { get; }

        /// <summary>Position of a <c>Point</c>.</summary>
        public GeoJsonPosition Point { get; }

        /// <summary>Lines of a <c>LineString</c> (exactly one) or <c>MultiLineString</c>.</summary>
        public IReadOnlyList<IReadOnlyList<GeoJsonPosition>>? Lines { get; }

        /// <summary>Polygons (rings: exterior first, then holes) of a <c>Polygon</c> (exactly one) or <c>MultiPolygon</c>.</summary>
        public IReadOnlyList<IReadOnlyList<IReadOnlyList<GeoJsonPosition>>>? Polygons { get; }

        /// <summary>Gets a value indicating whether the source geometry crossed the antimeridian (RFC 7946 §3.1.9).</summary>
        public bool CrossesAntimeridian { get; }

        public string TypeName => Type.ToString();

        public static GeoJsonGeometry ForPoint(GeoJsonPosition position) =>
            new GeoJsonGeometry(GeoJsonGeometryType.Point, position, null, null, false);

        public static GeoJsonGeometry ForLines(IReadOnlyList<IReadOnlyList<GeoJsonPosition>> lines, bool crossesAntimeridian) =>
            new GeoJsonGeometry(
                lines.Count == 1 ? GeoJsonGeometryType.LineString : GeoJsonGeometryType.MultiLineString,
                default,
                lines,
                null,
                crossesAntimeridian);

        public static GeoJsonGeometry ForPolygons(IReadOnlyList<IReadOnlyList<IReadOnlyList<GeoJsonPosition>>> polygons, bool crossesAntimeridian) =>
            new GeoJsonGeometry(
                polygons.Count == 1 ? GeoJsonGeometryType.Polygon : GeoJsonGeometryType.MultiPolygon,
                default,
                null,
                polygons,
                crossesAntimeridian);

        /// <summary>Enumerates every position of the geometry.</summary>
        public IEnumerable<GeoJsonPosition> Positions()
        {
            if (Type == GeoJsonGeometryType.Point)
            {
                yield return Point;
                yield break;
            }

            if (Lines != null)
            {
                foreach (IReadOnlyList<GeoJsonPosition> line in Lines)
                    foreach (GeoJsonPosition position in line)
                        yield return position;
            }

            if (Polygons != null)
            {
                foreach (IReadOnlyList<IReadOnlyList<GeoJsonPosition>> polygon in Polygons)
                    foreach (IReadOnlyList<GeoJsonPosition> ring in polygon)
                        foreach (GeoJsonPosition position in ring)
                            yield return position;
            }
        }
    }
}
