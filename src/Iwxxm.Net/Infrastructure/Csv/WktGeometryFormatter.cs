using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Iwxxm.Net.Infrastructure.GeoJson.Writing;
using Iwxxm.Net.Reports;

namespace Iwxxm.Net.Infrastructure.Csv
{
    /// <summary>
    /// Formats <see cref="GeoJsonGeometry"/> and <see cref="GeoSurface"/> collections into OGC Well-Known Text (WKT)
    /// in <c>longitude latitude</c> axis order.
    /// </summary>
    internal static class WktGeometryFormatter
    {
        public static string Format(GeoJsonGeometry geometry)
        {
            var sb = new StringBuilder();
            switch (geometry.Type)
            {
                case GeoJsonGeometryType.Point:
                    sb.Append("POINT (");
                    AppendPosition(sb, geometry.Point);
                    sb.Append(')');
                    break;

                case GeoJsonGeometryType.LineString:
                    sb.Append("LINESTRING ");
                    AppendPositionList(sb, geometry.Lines![0]);
                    break;

                case GeoJsonGeometryType.MultiLineString:
                    sb.Append("MULTILINESTRING (");
                    for (int i = 0; i < geometry.Lines!.Count; i++)
                    {
                        if (i > 0) sb.Append(", ");
                        AppendPositionList(sb, geometry.Lines[i]);
                    }
                    sb.Append(')');
                    break;

                case GeoJsonGeometryType.Polygon:
                    sb.Append("POLYGON ");
                    AppendPolygon(sb, geometry.Polygons![0]);
                    break;

                case GeoJsonGeometryType.MultiPolygon:
                    sb.Append("MULTIPOLYGON (");
                    for (int i = 0; i < geometry.Polygons!.Count; i++)
                    {
                        if (i > 0) sb.Append(", ");
                        AppendPolygon(sb, geometry.Polygons[i]);
                    }
                    sb.Append(')');
                    break;

                default:
                    throw new InvalidOperationException($"Unsupported geometry type: {geometry.Type}.");
            }

            return sb.ToString();
        }

        public static string FormatSurfaces(IReadOnlyList<GeoSurface> surfaces)
        {
            var polygons = new List<IReadOnlyList<IReadOnlyList<GeoJsonPosition>>>();
            foreach (GeoSurface surface in surfaces)
            {
                GeoJsonGeometry geometry = GeoJsonGeometryFactory.Surface(surface);
                if (geometry.Polygons != null)
                    polygons.AddRange(geometry.Polygons);
            }

            return Format(GeoJsonGeometry.ForPolygons(polygons, crossesAntimeridian: false));
        }

        private static void AppendPolygon(StringBuilder sb, IReadOnlyList<IReadOnlyList<GeoJsonPosition>> rings)
        {
            sb.Append('(');
            for (int i = 0; i < rings.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                AppendPositionList(sb, rings[i]);
            }
            sb.Append(')');
        }

        private static void AppendPositionList(StringBuilder sb, IReadOnlyList<GeoJsonPosition> positions)
        {
            sb.Append('(');
            for (int i = 0; i < positions.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                AppendPosition(sb, positions[i]);
            }
            sb.Append(')');
        }

        private static void AppendPosition(StringBuilder sb, GeoJsonPosition position)
        {
            sb.Append(position.Lon.ToString("G17", CultureInfo.InvariantCulture));
            sb.Append(' ');
            sb.Append(position.Lat.ToString("G17", CultureInfo.InvariantCulture));
        }
    }
}
