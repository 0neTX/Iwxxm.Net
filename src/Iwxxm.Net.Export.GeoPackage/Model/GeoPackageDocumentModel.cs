using System.Collections.Generic;
using Iwxxm.Net.Reports;

namespace Iwxxm.Net.Export.GeoPackage.Model
{
    /// <summary>
    /// What the tables of a GeoPackage are written from: the document-level information, one record per report and the
    /// ordered features (the same features, in the same order, as the GeoJSON exporter writes for the document).
    /// </summary>
    internal sealed class GeoPackageDocumentModel
    {
        public GeoPackageDocumentModel(
            string documentId,
            IwxxmReportType documentType,
            string? bulletinIdentifier,
            IReadOnlyList<GeoPackageReportRecord> reports,
            IReadOnlyList<GeoPackageFeature> features)
        {
            DocumentId = documentId;
            DocumentType = documentType;
            BulletinIdentifier = bulletinIdentifier;
            Reports = reports;
            Features = features;
        }

        /// <summary>The <c>gml:id</c> of the document (the report, or the bulletin envelope).</summary>
        public string DocumentId { get; }

        /// <summary>The IWXXM classification of the document (<see cref="IwxxmReportType.MeteorologicalBulletin"/> for a bulletin).</summary>
        public IwxxmReportType DocumentType { get; }

        /// <summary>The bulletin identifier, or <c>null</c> when the document is a standalone report.</summary>
        public string? BulletinIdentifier { get; }

        /// <summary>One record per report: the document itself for a report, the contained reports for a bulletin.</summary>
        public IReadOnlyList<GeoPackageReportRecord> Reports { get; }

        /// <summary>The features in GeoJSON order (empty when the document has no mappable content).</summary>
        public IReadOnlyList<GeoPackageFeature> Features { get; }
    }
}
