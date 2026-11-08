using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Iwxxm.Net.Reports
{
    /// <summary>
    /// Domain Aggregate Root representing a WMO/SWIM Collective Bulletin (<c>collect:MeteorologicalBulletin</c>,
    /// <c>http://def.wmo.int/collect/2014</c>) that bundles one or more IWXXM 2025-2 reports. It is the sealed envelope
    /// branch of <see cref="IwxxmDocument"/>, not an <see cref="IwxxmReport"/>: it exposes its bulletin identifier, its
    /// reports and metadata derived from them, and no per-report fields (translation metadata, supplementary usage text).
    /// Supports both aggregate access to valid reports (<see cref="Reports"/>) and individual per-report
    /// validation results (<see cref="ReportResults"/>).
    /// </summary>
    /// <remarks>
    /// <para>A COLLECT bulletin carries no issue time, status or permissible usage of its own
    /// (<c>collect:MeteorologicalBulletin</c> only holds <c>bulletinIdentifier</c> and <c>meteorologicalInformation</c>).
    /// The <see cref="IwxxmDocument"/> metadata of the envelope is therefore <b>derived from the contained reports</b>,
    /// never invented, and a bulletin without any successfully mapped report cannot be created:</para>
    /// <list type="bullet">
    /// <item><see cref="IssueTimeUtc"/>: the earliest issue time of the contained reports.</item>
    /// <item><see cref="PermissibleUsage"/>: <c>NonOperational</c> as soon as one report is
    /// non-operational (never upgrades non-operational content to operational); otherwise operational.</item>
    /// <item><see cref="PermissibleUsageReason"/>: the reason shared by every non-operational report, otherwise <c>null</c>.</item>
    /// <item><see cref="ReportStatus"/>: the most significant status present (<c>Correction</c> &gt; <c>Amendment</c> &gt; <c>Normal</c>).</item>
    /// </list>
    /// </remarks>
    public sealed class MeteorologicalBulletin : IwxxmDocument
    {
        public override string Id { get; }
        public override IwxxmReportType ReportType => IwxxmReportType.MeteorologicalBulletin;
        public string BulletinIdentifier { get; }
        public override DateTimeOffset IssueTimeUtc { get; }
        public override ReportStatus ReportStatus { get; }
        public override PermissibleUsage PermissibleUsage { get; }
        public override PermissibleUsageReason? PermissibleUsageReason { get; }

        /// <inheritdoc />
        public override TResult Accept<TResult>(IIwxxmDocumentVisitor<TResult> visitor) =>
            (visitor ?? throw new ArgumentNullException(nameof(visitor))).Visit(this);

        /// <summary>
        /// Gets all successfully parsed IWXXM 2025-2 reports contained inside this collective bulletin.
        /// </summary>
        public IReadOnlyList<IwxxmReport> Reports { get; }

        /// <summary>
        /// Gets the individual <see cref="Result{IwxxmReport}"/> outcomes for every <c>collect:meteorologicalInformation</c>
        /// entry in the bulletin, enabling partial-success processing on AMHS/SWIM feeds.
        /// </summary>
        public IReadOnlyList<Result<IwxxmReport>> ReportResults { get; }

        /// <summary>
        /// The <c>collect:meteorologicalInformation</c> entries that could not be mapped, with their position and report
        /// <c>gml:id</c>; the bulletin keeps every other report in <see cref="Reports"/>. Empty when every report mapped.
        /// </summary>
        public IReadOnlyList<ItemMappingError> ItemErrors { get; }

        private MeteorologicalBulletin(
            string id,
            string bulletinIdentifier,
            DateTimeOffset issueTimeUtc,
            ReportStatus reportStatus,
            PermissibleUsage permissibleUsage,
            PermissibleUsageReason? permissibleUsageReason,
            IReadOnlyList<IwxxmReport> reports,
            IReadOnlyList<Result<IwxxmReport>> reportResults,
            IReadOnlyList<ItemMappingError> itemErrors)
        {
            Id = id;
            BulletinIdentifier = bulletinIdentifier;
            IssueTimeUtc = issueTimeUtc;
            ReportStatus = reportStatus;
            PermissibleUsage = permissibleUsage;
            PermissibleUsageReason = permissibleUsageReason;
            Reports = reports;
            ReportResults = reportResults;
            ItemErrors = itemErrors;
        }

        /// <summary>
        /// Creates a bulletin from the per-entry mapping results. Failed entries become <see cref="ItemErrors"/> and the
        /// result succeeds with one <see cref="ValidationSeverity.Warning"/> per item error plus the warnings of the mapped reports, as long as at least one
        /// report mapped; with no mapped report the result fails with <c>DOMAIN-BULLETIN-EMPTY</c> and the entry errors.
        /// </summary>
        /// <param name="id">Bulletin <c>gml:id</c>.</param>
        /// <param name="bulletinIdentifier"><c>collect:bulletinIdentifier</c>.</param>
        /// <param name="reportResults">One result per <c>collect:meteorologicalInformation</c>, in document order.</param>
        /// <param name="itemIds">Optional <c>gml:id</c> of each entry (same order), used to identify failed entries.</param>
        public static Result<MeteorologicalBulletin> Create(
            string id,
            string bulletinIdentifier,
            IEnumerable<Result<IwxxmReport>> reportResults,
            IReadOnlyList<string?>? itemIds = null)
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

            var resultsList = reportResults?.ToList() ?? new List<Result<IwxxmReport>>(0);
            var validReports = resultsList
                .Where(r => r.IsSuccess)
                .Select(r => r.Value)
                .ToList();

            var itemErrors = new List<ItemMappingError>();
            var warnings = new List<ValidationError>();
            for (int i = 0; i < resultsList.Count; i++)
            {
                if (resultsList[i].IsFailure)
                {
                    string? itemId = itemIds != null && i < itemIds.Count ? itemIds[i] : null;
                    var itemError = new ItemMappingError(i, itemId, resultsList[i].Errors);
                    itemErrors.Add(itemError);
                    warnings.AddRange(itemError.ToWarnings("Report"));
                }
                else
                {
                    // Warnings of a successfully mapped report are not lost at the envelope.
                    warnings.AddRange(resultsList[i].Warnings);
                }
            }

            if (validReports.Count == 0)
            {
                var emptyErrors = itemErrors.SelectMany(e => e.Errors).ToList();
                emptyErrors.Add(new ValidationError(
                    IwxxmErrorCodes.BulletinEmpty,
                    "The MeteorologicalBulletin contains no successfully mapped report; its issue time and metadata cannot be derived.",
                    ErrorCategory.DomainMapping,
                    ValidationSeverity.Error,
                    id));
                return Result<MeteorologicalBulletin>.Failure(emptyErrors);
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
                new ReadOnlyCollection<IwxxmReport>(validReports),
                new ReadOnlyCollection<Result<IwxxmReport>>(resultsList),
                new ReadOnlyCollection<ItemMappingError>(itemErrors)),
                warnings);
        }
    }
}
