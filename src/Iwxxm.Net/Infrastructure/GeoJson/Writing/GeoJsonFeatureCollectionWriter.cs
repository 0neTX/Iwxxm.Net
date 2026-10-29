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
        public static string Write(IIwxxmReport report, IReadOnlyList<GeoJsonFeature> features, GeoJsonExportOptions options)
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

            WriteMetadata(writer, report);

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

        private static void WriteMetadata(Rfc8259JsonWriter writer, IIwxxmReport report)
        {
            writer.WritePropertyName("metadata");
            writer.BeginObject();
            writer.WriteProperty("id", report.Id);
            writer.WriteProperty("reportType", report.ReportType.ToString());
            writer.WriteProperty("issueTimeUtc", GeoJsonProperties.FormatInstant(report.IssueTimeUtc));
            writer.WriteProperty("reportStatus", ReportMetadataCodes.ToCode(report.ReportStatus));
            writer.WriteProperty("permissibleUsage", ReportMetadataCodes.ToCode(report.PermissibleUsage));
            if (report.PermissibleUsageReason.HasValue)
            {
                writer.WriteProperty("permissibleUsageReason", ReportMetadataCodes.ToCode(report.PermissibleUsageReason.Value));
            }

            if (report.PermissibleUsageSupplementary != null)
            {
                writer.WriteProperty("permissibleUsageSupplementary", report.PermissibleUsageSupplementary);
            }

            // Envelope-only metadata: a single-type refinement, not a report-type dispatch.
            if (report is MeteorologicalBulletin bulletin)
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
            if (feature.GeometryType == null)
            {
                writer.WriteNullValue();
            }
            else
            {
                writer.BeginObject();
                writer.WriteProperty("type", feature.GeometryType);
                writer.WritePropertyName("coordinates");
                if (feature.GeometryType == "Point" && feature.PointCoordinate.HasValue)
                {
                    writer.WriteCoordinatePair(feature.PointCoordinate.Value);
                }
                else if (feature.GeometryType == "LineString" && feature.LineCoordinates != null)
                {
                    writer.WriteCoordinateSequence(feature.LineCoordinates);
                }
                else if (feature.GeometryType == "Polygon" && feature.Polygon != null)
                {
                    writer.BeginArray();
                    writer.WriteCoordinateRingElement(feature.Polygon.ExteriorRing);
                    for (int h = 0; h < feature.Polygon.InteriorRings.Count; h++)
                    {
                        writer.WriteCoordinateRingElement(feature.Polygon.InteriorRings[h]);
                    }
                    writer.EndArray();
                }
                writer.EndObject();
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
    }
}
