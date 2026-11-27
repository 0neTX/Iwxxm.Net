using System;
using System.Collections.Generic;
using Iwxxm.Net.Export.GeoPackage.Model;
using Iwxxm.Net.Infrastructure.GeoJson;
using Iwxxm.Net.Infrastructure.GeoJson.Writing;
using Iwxxm.Net.Infrastructure.Mapping;
using Iwxxm.Net.Reports;

namespace Iwxxm.Net.Export.GeoPackage
{
    /// <summary>
    /// Reads the data a GeoPackage is written from out of an IWXXM document. The features are not rebuilt here: they come
    /// from the <c>GeoJsonFeatureCollector</c> of the core, so a GeoPackage row carries the same properties and the same
    /// (antimeridian-split) geometry as the GeoJSON feature of the same document.
    /// </summary>
    internal static class GeoPackageFeatureSource
    {
        /// <summary>Reads the document-level information, the report records and the features of <paramref name="document"/>.</summary>
        /// <exception cref="ArgumentNullException"><paramref name="document"/> is <c>null</c>.</exception>
        public static GeoPackageDocumentModel Read(IwxxmDocument document)
        {
            if (document == null)
                throw new ArgumentNullException(nameof(document));

            var bulletin = document as MeteorologicalBulletin;
            // The GeoJSON exporter omits a blank bulletin identifier; the tables do the same.
            string? bulletinIdentifier = string.IsNullOrWhiteSpace(bulletin?.BulletinIdentifier) ? null : bulletin!.BulletinIdentifier;

            var reports = new List<GeoPackageReportRecord>();
            if (bulletin != null)
            {
                foreach (IwxxmReport report in bulletin.Reports)
                    reports.Add(RecordOf(report, bulletinIdentifier));
            }
            else
            {
                reports.Add(RecordOf(document, null));
            }

            IReadOnlyList<GeoJsonFeature> collected = document.Accept(new GeoJsonFeatureCollector());
            var features = new List<GeoPackageFeature>(collected.Count);
            foreach (GeoJsonFeature feature in collected)
                features.Add(new GeoPackageFeature(feature, ConvertGeometry(feature.Geometry)));

            return new GeoPackageDocumentModel(document.Id, document.ReportType, bulletinIdentifier, reports, features);
        }

        /// <summary>
        /// Fills the neutral geometry from the geometry the core GeoJSON writer produced. The kinds are promoted to the
        /// ones a feature table stores: a <c>Point</c> stays a point, a <c>LineString</c> becomes a
        /// <c>MultiLineString</c> of one member, a <c>Polygon</c> becomes a <c>MultiPolygon</c> of one member and the
        /// <c>Multi*</c> kinds stay as they are. The core emits no other kind (no <c>GeometryCollection</c>). The
        /// coordinates are copied as they are (x = longitude, y = latitude, 2D): the core already applied the RFC 7946
        /// ring orientation, the antimeridian split and the polar caps, so nothing is re-oriented, re-split or re-closed.
        /// </summary>
        /// <returns>The neutral geometry, or <c>null</c> when <paramref name="geometry"/> is <c>null</c> (a feature without geometry).</returns>
        internal static GeoPackageGeometry? ConvertGeometry(GeoJsonGeometry? geometry)
        {
            if (geometry == null)
                return null;

            switch (geometry.Type)
            {
                case GeoJsonGeometryType.Point:
                    return GeoPackageGeometry.ForPoint(ConvertPosition(geometry.Point));

                case GeoJsonGeometryType.LineString:
                case GeoJsonGeometryType.MultiLineString:
                    return ConvertLine(geometry.Lines!);

                case GeoJsonGeometryType.Polygon:
                case GeoJsonGeometryType.MultiPolygon:
                    return ConvertPolygon(geometry.Polygons!);

                default:
                    throw new InvalidOperationException($"Unsupported geometry type: {geometry.Type}.");
            }
        }

        private static GeoPackageGeometry ConvertLine(IReadOnlyList<IReadOnlyList<GeoJsonPosition>> source)
        {
            var lines = new List<IReadOnlyList<GeoPackagePosition>>(source.Count);
            foreach (IReadOnlyList<GeoJsonPosition> line in source)
                lines.Add(ConvertPositions(line));
            return GeoPackageGeometry.ForMultiLineString(lines);
        }

        private static GeoPackageGeometry ConvertPolygon(IReadOnlyList<IReadOnlyList<IReadOnlyList<GeoJsonPosition>>> source)
        {
            var polygons = new List<IReadOnlyList<IReadOnlyList<GeoPackagePosition>>>(source.Count);
            foreach (IReadOnlyList<IReadOnlyList<GeoJsonPosition>> polygon in source)
            {
                var rings = new List<IReadOnlyList<GeoPackagePosition>>(polygon.Count);
                foreach (IReadOnlyList<GeoJsonPosition> ring in polygon)
                    rings.Add(ConvertPositions(ring));
                polygons.Add(rings);
            }

            return GeoPackageGeometry.ForMultiPolygon(polygons);
        }

        private static GeoPackagePosition ConvertPosition(GeoJsonPosition position) => new GeoPackagePosition(position.Lon, position.Lat);

        private static IReadOnlyList<GeoPackagePosition> ConvertPositions(IReadOnlyList<GeoJsonPosition> positions)
        {
            var result = new GeoPackagePosition[positions.Count];
            for (int i = 0; i < result.Length; i++)
                result[i] = ConvertPosition(positions[i]);
            return result;
        }

        private static GeoPackageReportRecord RecordOf(IwxxmDocument report, string? bulletinIdentifier)
        {
            (DateTimeOffset? validFrom, DateTimeOffset? validUntil) = ValidityOf(report);
            return new GeoPackageReportRecord(
                report.Id,
                report.ReportType,
                ReportMetadataCodes.ToCode(report.ReportStatus),
                ReportMetadataCodes.ToCode(report.PermissibleUsage),
                report.PermissibleUsageReason.HasValue ? ReportMetadataCodes.ToCode(report.PermissibleUsageReason.Value) : null,
                (report as IwxxmReport)?.PermissibleUsageSupplementary,
                report.IssueTimeUtc,
                validFrom,
                validUntil,
                bulletinIdentifier);
        }

        /// <summary>
        /// The validity period the domain states for a report, the same values the core GeoJSON writes as <c>validFromUtc</c> and
        /// <c>validUntilUtc</c>: SIGWX, SIGMET, AIRMET and TAF. No other report type has one in the model (their phenomenon, next
        /// advisory and model initialisation times are not validity periods), so no period is made up for them.
        /// </summary>
        private static (DateTimeOffset? From, DateTimeOffset? Until) ValidityOf(IwxxmDocument report)
        {
            switch (report)
            {
                case Sigwx sigwx:
                    return (sigwx.ValidFromUtc, sigwx.ValidUntilUtc);
                case Sigmet sigmet:
                    return (sigmet.ValidFromUtc, sigmet.ValidUntilUtc);
                case Airmet airmet:
                    return (airmet.ValidFromUtc, airmet.ValidUntilUtc);
                case Taf taf:
                    return (taf.ValidFromUtc, taf.ValidUntilUtc);
                default:
                    return (null, null);
            }
        }
    }
}
