using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Iwxxm.Net.Reports
{
    /// <summary>
    /// Domain Aggregate Root representing a WMO/SWIM Collective Bulletin (<c>collect:MeteorologicalBulletin</c>,
    /// <c>http://def.wmo.int/collect/2014</c>) that bundles one or more IWXXM 2025-2 reports.
    /// Supports both aggregate access to valid reports (<see cref="Reports"/>) and individual per-report
    /// validation results (<see cref="ReportResults"/>).
    /// </summary>
    /// <remarks>
    /// <para>A COLLECT bulletin carries no issue time, status or permissible usage of its own
    /// (<c>collect:MeteorologicalBulletin</c> only holds <c>bulletinIdentifier</c> and <c>meteorologicalInformation</c>).
    /// The <see cref="IIwxxmReport"/> metadata of the envelope is therefore <b>derived from the contained reports</b>,
    /// never invented, and a bulletin without any successfully mapped report cannot be created:</para>
    /// <list type="bullet">
    /// <item><see cref="IssueTimeUtc"/>: the earliest issue time of the contained reports.</item>
    /// <item><see cref="PermissibleUsage"/>: <c>NonOperational</c> as soon as one report is
    /// non-operational (never upgrades non-operational content to operational); otherwise operational.</item>
    /// <item><see cref="PermissibleUsageReason"/>: the reason shared by every non-operational report, otherwise <c>null</c>.</item>
    /// <item><see cref="ReportStatus"/>: the most significant status present (<c>Correction</c> &gt; <c>Amendment</c> &gt; <c>Normal</c>).</item>
    /// <item><see cref="PermissibleUsageSupplementary"/>: always <c>null</c> (free text is not aggregated).</item>
    /// </list>
    /// </remarks>
    public sealed class MeteorologicalBulletin : IIwxxmReport
    {
        public string Id { get; }
        public IwxxmReportType ReportType => IwxxmReportType.MeteorologicalBulletin;
        public string BulletinIdentifier { get; }
        public DateTimeOffset IssueTimeUtc { get; }
        public ReportStatus ReportStatus { get; }
        public PermissibleUsage PermissibleUsage { get; }
        public PermissibleUsageReason? PermissibleUsageReason { get; }
        public string? PermissibleUsageSupplementary => null;
        public bool IsTranslationFailed => false;
        public string? TranslationFailedTac => null;

        /// <inheritdoc />
        public TResult Accept<TResult>(IIwxxmReportVisitor<TResult> visitor) =>
            (visitor ?? throw new ArgumentNullException(nameof(visitor))).Visit(this);

        /// <summary>
        /// Gets all successfully parsed IWXXM 2025-2 reports contained inside this collective bulletin.
        /// </summary>
        public IReadOnlyList<IIwxxmReport> Reports { get; }

        /// <summary>
        /// Gets the individual <see cref="Result{IIwxxmReport}"/> outcomes for every <c>collect:meteorologicalInformation</c>
        /// entry in the bulletin, enabling partial-success processing on AMHS/SWIM feeds.
        /// </summary>
        public IReadOnlyList<Result<IIwxxmReport>> ReportResults { get; }

        private MeteorologicalBulletin(
            string id,
            string bulletinIdentifier,
            DateTimeOffset issueTimeUtc,
            ReportStatus reportStatus,
            PermissibleUsage permissibleUsage,
            PermissibleUsageReason? permissibleUsageReason,
            IReadOnlyList<IIwxxmReport> reports,
            IReadOnlyList<Result<IIwxxmReport>> reportResults)
        {
            Id = id;
            BulletinIdentifier = bulletinIdentifier;
            IssueTimeUtc = issueTimeUtc;
            ReportStatus = reportStatus;
            PermissibleUsage = permissibleUsage;
            PermissibleUsageReason = permissibleUsageReason;
            Reports = reports;
            ReportResults = reportResults;
        }

        public static Result<MeteorologicalBulletin> Create(
            string id,
            string bulletinIdentifier,
            IEnumerable<Result<IIwxxmReport>> reportResults)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return Result<MeteorologicalBulletin>.Failure(
                    "DOMAIN-BULLETIN-ID",
                    "The MeteorologicalBulletin gml:id identifier is required.",
                    ErrorCategory.DomainMapping);
            }

            if (string.IsNullOrWhiteSpace(bulletinIdentifier))
            {
                return Result<MeteorologicalBulletin>.Failure(
                    "DOMAIN-BULLETIN-IDENTIFIER",
                    "The collect:bulletinIdentifier is required.",
                    ErrorCategory.DomainMapping,
                    id);
            }

            var resultsList = reportResults?.ToList() ?? new List<Result<IIwxxmReport>>(0);
            var validReports = resultsList
                .Where(r => r.IsSuccess)
                .Select(r => r.Value)
                .ToList();

            if (validReports.Count == 0)
            {
                return Result<MeteorologicalBulletin>.Failure(
                    "DOMAIN-BULLETIN-EMPTY",
                    "The MeteorologicalBulletin contains no successfully mapped report; its issue time and metadata cannot be derived.",
                    ErrorCategory.DomainMapping,
                    id);
            }

            DateTimeOffset issueTime = validReports.Min(r => r.IssueTimeUtc);
            ReportStatus status = validReports.Any(r => r.ReportStatus == ReportStatus.Correction)
                ? ReportStatus.Correction
                : validReports.Any(r => r.ReportStatus == ReportStatus.Amendment) ? ReportStatus.Amendment : ReportStatus.Normal;

            var nonOperational = validReports.Where(r => r.PermissibleUsage != PermissibleUsage.Operational).ToList();
            PermissibleUsage usage = nonOperational.Count > 0 ? PermissibleUsage.NonOperational : PermissibleUsage.Operational;
            var reasons = nonOperational.Select(r => r.PermissibleUsageReason).Distinct().ToList();
            PermissibleUsageReason? reason = reasons.Count == 1 ? reasons[0] : null;

            return Result<MeteorologicalBulletin>.Success(new MeteorologicalBulletin(
                id,
                bulletinIdentifier.Trim(),
                issueTime,
                status,
                usage,
                reason,
                new ReadOnlyCollection<IIwxxmReport>(validReports),
                new ReadOnlyCollection<Result<IIwxxmReport>>(resultsList)));
        }
    }
}
