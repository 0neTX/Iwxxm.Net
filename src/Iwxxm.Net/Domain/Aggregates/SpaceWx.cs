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
        /// <inheritdoc />
        public override string Id { get; }
        /// <inheritdoc />
        public override IwxxmReportType ReportType => IwxxmReportType.SpaceWeatherAdvisory;
        /// <inheritdoc />
        public override DateTimeOffset IssueTimeUtc { get; }
        /// <inheritdoc />
        public override ReportStatus ReportStatus { get; }
        /// <inheritdoc />
        public override PermissibleUsage PermissibleUsage { get; }
        /// <inheritdoc />
        public override PermissibleUsageReason? PermissibleUsageReason { get; }
        /// <inheritdoc />
        public override string? PermissibleUsageSupplementary { get; }
        /// <summary>Gets the name of the issuing space weather centre (<c>aixm:name</c> of <c>iwxxm:issuingSpaceWeatherCentre</c>, falling back to its designator); empty only when the document carries neither.</summary>
        public string IssuingSwxcName { get; }
        /// <summary>Gets the advisory number (<c>iwxxm:advisoryNumber</c>: four-digit year and a unique message number, for example <c>2018/1</c>), or <c>null</c> when not reported.</summary>
        public string? AdvisoryNumber { get; }
        /// <summary>Gets the numbers of the advisories replaced by this one (<c>iwxxm:replacedAdvisoryNumber</c>, for example <c>2018/1</c>; the schema allows up to four), in document order; empty when it replaces none.</summary>
        public IReadOnlyList<string> ReplacedAdvisoryNumbers { get; }
        /// <summary>Gets the affected systems (<c>iwxxm:effect</c>) as written in the document, in document order: <c>HF_COM</c> (high frequency communications), <c>SATCOM</c> (satellite communications), <c>GNSS</c> (GNSS-based navigation and surveillance) or <c>RADIATION</c> (radiation dose rates on polar flights); empty when none is reported.</summary>
        public IReadOnlyList<string> Effects { get; }
        /// <summary>One entry per <c>iwxxm:analysis</c> (observation and each forecast offset), in document order.</summary>
        public IReadOnlyList<SpaceWeatherAnalysis> Analyses { get; }
        /// <summary>Gets the free-text remarks (<c>iwxxm:remarks</c>), or <c>null</c> when absent or nil (nil reason <c>inapplicable</c>).</summary>
        public string? Remarks { get; }
        /// <summary>Gets the time the next advisory will be issued (<c>iwxxm:nextAdvisoryTime</c>), or <c>null</c> when absent or nil (nil reason <c>inapplicable</c>, no subsequent advisory expected). The <c>indeterminatePosition</c> qualifier (before or after the stated time) that the schema allows is not read, so it is dropped.</summary>
        public DateTimeOffset? NextAdvisoryTimeUtc { get; }
        /// <inheritdoc />
        public override bool IsTranslationFailed { get; }
        /// <inheritdoc />
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

        /// <summary>Creates a space weather advisory, enforcing the minimal domain invariants and collecting every violation in the returned <see cref="Result{T}"/>.</summary>
        /// <param name="id">The <c>gml:id</c> of the report root element; required (not blank).</param>
        /// <param name="issueTimeUtc">The <c>iwxxm:issueTime</c> instant; required (not <c>default</c>) and expressed in UTC (offset zero).</param>
        /// <param name="reportStatus">The <c>reportStatus</c> attribute (<c>NORMAL</c>, <c>AMENDMENT</c> or <c>CORRECTION</c>); must be a defined value.</param>
        /// <param name="permissibleUsage">The <c>permissibleUsage</c> attribute (<c>OPERATIONAL</c> or <c>NON-OPERATIONAL</c>); must be a defined value.</param>
        /// <param name="permissibleUsageReason">The optional <c>permissibleUsageReason</c> (<c>TEST</c> or <c>EXERCISE</c>), meaningful for non-operational data; <c>null</c> when not reported. When given it must be a defined value.</param>
        /// <param name="permissibleUsageSupplementary">The optional free-text <c>permissibleUsageSupplementary</c>; <c>null</c> when not reported.</param>
        /// <param name="issuingSwxcName">The name of the issuing space weather centre (the designator when no name is given).</param>
        /// <param name="advisoryNumber">The advisory number; <c>null</c> when not reported.</param>
        /// <param name="replacedAdvisoryNumbers">The numbers of the replaced advisories; <c>null</c> is treated as empty.</param>
        /// <param name="effects">The affected systems as written in the document; <c>null</c> is treated as empty.</param>
        /// <param name="analyses">The observed and forecast analyses in document order, each with its time in UTC; <c>null</c> is treated as empty.</param>
        /// <param name="remarks">The free-text remarks; <c>null</c> when absent.</param>
        /// <param name="nextAdvisoryTimeUtc">The time of the next advisory in UTC; <c>null</c> when none is expected.</param>
        /// <param name="isTranslationFailed">Whether the TAC-to-XML translation failed (<c>translationFailedTAC</c> present).</param>
        /// <param name="translationFailedTac">The original TAC text that could not be translated; <c>null</c> when the translation succeeded.</param>
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
