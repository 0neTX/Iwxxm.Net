using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Iwxxm.Net.Reports
{
    /// <summary>
    /// Domain Aggregate Root representing a Space Weather Advisory (<c>SpaceWeatherAdvisory</c>)
    /// under ICAO/WMO IWXXM 2025-2.
    /// </summary>
    public sealed class SpaceWx : IwxxmReport
    {
        public override string Id { get; }
        public override IwxxmReportType ReportType => IwxxmReportType.SpaceWeatherAdvisory;
        public override DateTimeOffset IssueTimeUtc { get; }
        public override ReportStatus ReportStatus { get; }
        public override PermissibleUsage PermissibleUsage { get; }
        public override PermissibleUsageReason? PermissibleUsageReason { get; }
        public override string? PermissibleUsageSupplementary { get; }
        public string IssuingSwxcName { get; }
        public string? AdvisoryNumber { get; }
        public IReadOnlyList<string> ReplacedAdvisoryNumbers { get; }
        public IReadOnlyList<string> Effects { get; }
        /// <summary>One entry per <c>iwxxm:analysis</c> (observation and each forecast offset), in document order.</summary>
        public IReadOnlyList<SpaceWeatherAnalysis> Analyses { get; }
        public string? Remarks { get; }
        public DateTimeOffset? NextAdvisoryTimeUtc { get; }
        public override bool IsTranslationFailed { get; }
        public override string? TranslationFailedTac { get; }

        /// <inheritdoc />
        public override TResult Accept<TResult>(IIwxxmDocumentVisitor<TResult> visitor) =>
            (visitor ?? throw new ArgumentNullException(nameof(visitor))).Visit(this);

        private SpaceWx(
            string id,
            DateTimeOffset issueTimeUtc,
            ReportStatus reportStatus,
            PermissibleUsage permissibleUsage,
            PermissibleUsageReason? permissibleUsageReason,
            string? permissibleUsageSupplementary,
            string issuingSwxcName,
            string? advisoryNumber,
            IReadOnlyList<string> replacedAdvisoryNumbers,
            IReadOnlyList<string> effects,
            IReadOnlyList<SpaceWeatherAnalysis> analyses,
            string? remarks,
            DateTimeOffset? nextAdvisoryTimeUtc,
            bool isTranslationFailed,
            string? translationFailedTac)
        {
            Id = id;
            IssueTimeUtc = issueTimeUtc;
            ReportStatus = reportStatus;
            PermissibleUsage = permissibleUsage;
            PermissibleUsageReason = permissibleUsageReason;
            PermissibleUsageSupplementary = permissibleUsageSupplementary;
            IssuingSwxcName = issuingSwxcName;
            AdvisoryNumber = advisoryNumber;
            ReplacedAdvisoryNumbers = replacedAdvisoryNumbers;
            Effects = effects;
            Analyses = analyses;
            Remarks = remarks;
            NextAdvisoryTimeUtc = nextAdvisoryTimeUtc;
            IsTranslationFailed = isTranslationFailed;
            TranslationFailedTac = translationFailedTac;
        }

        public static Result<SpaceWx> Create(
            string id,
            DateTimeOffset issueTimeUtc,
            ReportStatus reportStatus,
            PermissibleUsage permissibleUsage,
            string issuingSwxcName,
            string? advisoryNumber = null,
            IEnumerable<string>? replacedAdvisoryNumbers = null,
            IEnumerable<string>? effects = null,
            IEnumerable<SpaceWeatherAnalysis>? analyses = null,
            string? remarks = null,
            DateTimeOffset? nextAdvisoryTimeUtc = null,
            bool isTranslationFailed = false,
            string? translationFailedTac = null,
            PermissibleUsageReason? permissibleUsageReason = null,
            string? permissibleUsageSupplementary = null)
        {
            var invariants = new DomainInvariants(id);
            invariants.RequireText(id, "DOMAIN-SWX-ID", "The SpaceWeatherAdvisory report gml:id identifier is required.");
            invariants.RequireMetadata(reportStatus, permissibleUsage, permissibleUsageReason);
            invariants.RequireIssueTime(issueTimeUtc);
            invariants.RequireUtc(nextAdvisoryTimeUtc, "next advisory time");
            foreach (SpaceWeatherAnalysis analysis in analyses ?? Enumerable.Empty<SpaceWeatherAnalysis>())
            {
                invariants.RequireUtc(analysis?.PhenomenonTimeUtc, "space weather analysis time");
            }

            if (invariants.HasErrors)
            {
                return Result<SpaceWx>.Failure(invariants.Errors);
            }

            return Result<SpaceWx>.Success(new SpaceWx(
                id,
                issueTimeUtc,
                reportStatus,
                permissibleUsage,
                permissibleUsageReason,
                permissibleUsageSupplementary,
                issuingSwxcName ?? string.Empty,
                advisoryNumber,
                new ReadOnlyCollection<string>(replacedAdvisoryNumbers?.ToList() ?? new List<string>(0)),
                new ReadOnlyCollection<string>(effects?.ToList() ?? new List<string>(0)),
                new ReadOnlyCollection<SpaceWeatherAnalysis>(analyses?.ToList() ?? new List<SpaceWeatherAnalysis>(0)),
                remarks,
                nextAdvisoryTimeUtc,
                isTranslationFailed,
                translationFailedTac));
        }
    }
}
