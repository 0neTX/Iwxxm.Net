using System;
using Iwxxm.Net.Reports;

namespace Iwxxm.Net.Export.GeoPackage.Model
{
    /// <summary>
    /// One row of the <c>iwxxm_reports</c> table: the report-level metadata of one report of the exported document.
    /// </summary>
    internal sealed class GeoPackageReportRecord
    {
        public GeoPackageReportRecord(
            string reportId,
            IwxxmReportType reportType,
            string reportStatus,
            string permissibleUsage,
            DateTimeOffset issueTime,
            string? bulletinIdentifier)
        {
            ReportId = reportId;
            ReportType = reportType;
            ReportStatus = reportStatus;
            PermissibleUsage = permissibleUsage;
            IssueTime = issueTime;
            BulletinIdentifier = bulletinIdentifier;
        }

        /// <summary>The <c>gml:id</c> of the report (the value of the <c>reportId</c> feature property).</summary>
        public string ReportId { get; }

        /// <summary>The IWXXM classification of the report.</summary>
        public IwxxmReportType ReportType { get; }

        /// <summary>The <c>reportStatus</c> code as the GeoJSON exporter writes it (<c>NORMAL</c>, <c>AMENDMENT</c>, <c>CORRECTION</c>).</summary>
        public string ReportStatus { get; }

        /// <summary>The <c>permissibleUsage</c> code as the GeoJSON exporter writes it (<c>OPERATIONAL</c>, <c>NON-OPERATIONAL</c>).</summary>
        public string PermissibleUsage { get; }

        /// <summary>The issue time of the report; its offset is whatever the mapper produced (UTC for every mapped report).</summary>
        public DateTimeOffset IssueTime { get; }

        /// <summary>The identifier of the bulletin that contains the report, or <c>null</c> for a standalone report.</summary>
        public string? BulletinIdentifier { get; }
    }
}
