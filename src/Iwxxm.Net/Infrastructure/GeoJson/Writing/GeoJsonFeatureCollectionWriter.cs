using System;
using System.Collections.Generic;
using System.Text;
using Iwxxm.Net.Infrastructure.Mapping;
using Iwxxm.Net.Reports;

namespace Iwxxm.Net.Infrastructure.GeoJson.Writing
{
    /// <summary>
    /// Writes an RFC 7946 <c>FeatureCollection</c>: optional <c>bbox</c>, report <c>metadata</c> and the features
    /// (geometry in <c>[longitude, latitude]</c> order and non-null properties in insertion order).
    /// </summary>
    internal static class GeoJsonFeatureCollectionWriter
    {
        public static string Write(IwxxmDocument document, IReadOnlyList<GeoJsonFeature> features, GeoJsonExportOptions options)
        {
            var sb = new StringBuilder(Math.Max(512, features.Count * 256));
            var writer = new Rfc8259JsonWriter(sb, options.Indented);

            writer.BeginObject();
            writer.WriteProperty("type", "FeatureCollection");

            GeoJsonBoundingBox bbox = GeoJsonBoundingBox.Of(features);
            if (options.IncludeBoundingBox && bbox.HasCoordinates)
            {
                writer.WritePropertyName("bbox");
                writer.BeginInlineArray();
                writer.WriteInlineNumber(bbox.MinLon);
                writer.WriteInlineNumber(bbox.MinLat);
                writer.WriteInlineNumber(bbox.MaxLon);
                writer.WriteInlineNumber(bbox.MaxLat);
                writer.EndInlineArray();
            }

            WriteMetadata(writer, document);

            writer.WritePropertyName("features");
            writer.BeginArray();
            for (int i = 0; i < features.Count; i++)
            {
                WriteFeature(writer, features[i]);
            }
            writer.EndArray();
            writer.EndObject();

            return sb.ToString();
        }

        private static void WriteMetadata(Rfc8259JsonWriter writer, IwxxmDocument document)
        {
            writer.WritePropertyName("metadata");
            writer.BeginObject();
            writer.WriteProperty("id", document.Id);
            writer.WriteProperty("reportType", document.ReportType.ToString());
            writer.WriteProperty("issueTimeUtc", GeoJsonProperties.FormatInstant(document.IssueTimeUtc));
            writer.WriteProperty("reportStatus", ReportMetadataCodes.ToCode(document.ReportStatus));
            writer.WriteProperty("permissibleUsage", ReportMetadataCodes.ToCode(document.PermissibleUsage));
            if (document.PermissibleUsageReason.HasValue)
            {
                writer.WriteProperty("permissibleUsageReason", ReportMetadataCodes.ToCode(document.PermissibleUsageReason.Value));
            }

            // Branch-specific metadata of the closed document hierarchy (two-way refinement, not a report-type dispatch):
            // only reports carry supplementary usage text, only bulletins carry an identifier and a report count.
            if (document is IwxxmReport report && report.PermissibleUsageSupplementary != null)
            {
                writer.WriteProperty("permissibleUsageSupplementary", report.PermissibleUsageSupplementary);
            }

            if (document is MeteorologicalBulletin bulletin)
            {
                writer.WriteProperty("bulletinIdentifier", bulletin.BulletinIdentifier);
                writer.WriteProperty("reportCount", bulletin.Reports.Count);
            }
            writer.EndObject();
        }

        private static void WriteFeature(Rfc8259JsonWriter writer, GeoJsonFeature feature)
        {
            writer.BeginObject();
            writer.WriteProperty("type", "Feature");
            if (!string.IsNullOrWhiteSpace(feature.Id))
            {
                writer.WriteProperty("id", feature.Id);
            }

            writer.WritePropertyName("geometry");
            if (feature.Geometry == null)
            {
                writer.WriteNullValue();
            }
            else
            {
                WriteGeometry(writer, feature.Geometry);
            }

            writer.WritePropertyName("properties");
            writer.BeginObject();
            for (int i = 0; i < feature.Properties.Count; i++)
            {
                var prop = feature.Properties[i];
                if (prop.Value != null)
                {
                    writer.WriteDynamicProperty(prop.Key, prop.Value);
                }
            }
            writer.EndObject();
            writer.EndObject();
        }

        private static void WriteGeometry(Rfc8259JsonWriter writer, GeoJsonGeometry geometry)
        {
            writer.BeginObject();
            writer.WriteProperty("type", geometry.TypeName);
            writer.WritePropertyName("coordinates");
            switch (geometry.Type)
            {
                case GeoJsonGeometryType.Point:
                    writer.WritePosition(geometry.Point);
                    break;
                case GeoJsonGeometryType.LineString:
                    writer.WritePositionSequence(geometry.Lines![0]);
                    break;
                case GeoJsonGeometryType.MultiLineString:
                    writer.BeginArray();
                    foreach (IReadOnlyList<GeoJsonPosition> line in geometry.Lines!)
                    {
                        writer.WritePositionSequenceElement(line);
                    }
                    writer.EndArray();
                    break;
                case GeoJsonGeometryType.Polygon:
                    WriteRings(writer, geometry.Polygons![0]);
                    break;
                case GeoJsonGeometryType.MultiPolygon:
                    writer.BeginArray();
                    foreach (IReadOnlyList<IReadOnlyList<GeoJsonPosition>> polygon in geometry.Polygons!)
                    {
                        writer.BeginArrayElement();
                        WriteRingElements(writer, polygon);
                        writer.EndArray();
                    }
                    writer.EndArray();
                    break;
            }
            writer.EndObject();
        }

        private static void WriteRings(Rfc8259JsonWriter writer, IReadOnlyList<IReadOnlyList<GeoJsonPosition>> rings)
        {
            writer.BeginArray();
            WriteRingElements(writer, rings);
            writer.EndArray();
        }

        private static void WriteRingElements(Rfc8259JsonWriter writer, IReadOnlyList<IReadOnlyList<GeoJsonPosition>> rings)
        {
            for (int r = 0; r < rings.Count; r++)
            {
                writer.WritePositionSequenceElement(rings[r]);
            }
        }
    }
}
