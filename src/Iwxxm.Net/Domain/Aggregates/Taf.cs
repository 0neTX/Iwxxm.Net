using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Iwxxm.Net.Reports
{
    /// <summary>
    /// Domain Aggregate Root representing a Terminal Aerodrome Forecast (<c>TAF</c>)
    /// under ICAO/WMO IWXXM 2025-2.
    /// </summary>
    public sealed class Taf : IwxxmReport
    {
        /// <inheritdoc />
        public override string Id { get; }
        /// <inheritdoc />
        public override IwxxmReportType ReportType => IwxxmReportType.Taf;
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
        /// <summary>Gets the ICAO location indicator of the aerodrome the forecast applies to (<c>aixm:designator</c> of the <c>iwxxm:aerodrome</c> feature, falling back to <c>aixm:locationIndicatorICAO</c>), trimmed and never blank.</summary>
        public string AerodromeDesignator { get; }
        /// <summary>Gets the aerodrome name (<c>aixm:name</c>), or <c>null</c> when the document does not carry it.</summary>
        public string? AerodromeName { get; }
        /// <summary>Gets the aerodrome reference point (<c>aixm:ARP</c> of the <c>iwxxm:aerodrome</c> feature), or <c>null</c> when the document does not carry it.</summary>
        public GeoCoordinate? AerodromePosition { get; }
        /// <summary>Gets the start of the forecast validity period (<c>iwxxm:validPeriod</c>), or <c>null</c> when absent (the element is optional, for example in a cancellation or a missing report).</summary>
        public DateTimeOffset? ValidFromUtc { get; }
        /// <summary>Gets the end of the forecast validity period (<c>iwxxm:validPeriod</c>), or <c>null</c> when absent. When both bounds are present the end is strictly after <see cref="ValidFromUtc"/>.</summary>
        public DateTimeOffset? ValidUntilUtc { get; }
        /// <summary>Gets a value indicating whether this is a cancellation (<c>isCancelReport="true"</c>) that cancels a previously issued TAF; <c>false</c> when the attribute is false or absent.</summary>
        public bool IsCancelReport { get; }
        /// <summary>Gets the start of the validity period of the TAF being cancelled (<c>iwxxm:cancelledReportValidPeriod</c>), or <c>null</c> when not given (only meaningful for a cancellation).</summary>
        public DateTimeOffset? CancelledReportValidFromUtc { get; }
        /// <summary>Gets the end of the validity period of the TAF being cancelled (<c>iwxxm:cancelledReportValidPeriod</c>), or <c>null</c> when not given. When both bounds are present the end is strictly after the start.</summary>
        public DateTimeOffset? CancelledReportValidUntilUtc { get; }
        /// <summary>Gets a value indicating whether the <c>iwxxm:baseForecast</c> is nil (a missing report): the element carries a <c>nilReason</c> instead of a forecast, see <see cref="BaseForecastNilReason"/>.</summary>
        public bool IsNilForecast { get; }
        /// <summary>Gets the <c>nilReason</c> of a nil <c>iwxxm:baseForecast</c> as a code of the WMO nil-reason register <c>http://codes.wmo.int/common/nil</c> (typically <see cref="NilReason.Missing"/>: the whole report is missing), or <c>null</c> when a forecast is present. A reason outside the official register is kept as <c>Unknown</c> with its raw value in <see cref="CodeListValue{TCode}.Notation"/>.</summary>
        public CodeListValue<NilReason>? BaseForecastNilReason { get; }
        /// <inheritdoc />
        public override bool IsTranslationFailed { get; }
        /// <inheritdoc />
        public override string? TranslationFailedTac { get; }

        /// <inheritdoc />
        public override TResult Accept<TResult>(IIwxxmDocumentVisitor<TResult> visitor) =>
            (visitor ?? throw new ArgumentNullException(nameof(visitor))).Visit(this);

        /// <summary>Gets the prevailing conditions of the forecast (<c>iwxxm:baseForecast</c>), or <c>null</c> for a missing or cancelled report. The schema makes it mandatory in every other case.</summary>
        public AerodromeForecastGroup? BaseForecast { get; }
        /// <summary>Gets the change and probability groups that modify the base forecast (<c>iwxxm:changeForecast</c>: BECMG, TEMPO, FM and PROB groups), in document order; empty when there are none. ICAO PANS-MET recommends no more than five.</summary>
        public IReadOnlyList<AerodromeForecastGroup> ChangeForecasts { get; }

        private Taf(
            string id,
            DateTimeOffset issueTimeUtc,
            ReportStatus reportStatus,
            PermissibleUsage permissibleUsage,
            PermissibleUsageReason? permissibleUsageReason,
            string? permissibleUsageSupplementary,
            string aerodromeDesignator,
            string? aerodromeName,
            GeoCoordinate? aerodromePosition,
            DateTimeOffset? validFromUtc,
            DateTimeOffset? validUntilUtc,
            bool isCancelReport,
            DateTimeOffset? cancelledReportValidFromUtc,
            DateTimeOffset? cancelledReportValidUntilUtc,
            bool isNilForecast,
            CodeListValue<NilReason>? baseForecastNilReason,
            bool isTranslationFailed,
            string? translationFailedTac,
            AerodromeForecastGroup? baseForecast,
            IReadOnlyList<AerodromeForecastGroup> changeForecasts)
        {
            Id = id;
            IssueTimeUtc = issueTimeUtc;
            ReportStatus = reportStatus;
            PermissibleUsage = permissibleUsage;
            PermissibleUsageReason = permissibleUsageReason;
            PermissibleUsageSupplementary = permissibleUsageSupplementary;
            AerodromeDesignator = aerodromeDesignator;
            AerodromeName = aerodromeName;
            AerodromePosition = aerodromePosition;
            ValidFromUtc = validFromUtc;
            ValidUntilUtc = validUntilUtc;
            IsCancelReport = isCancelReport;
            CancelledReportValidFromUtc = cancelledReportValidFromUtc;
            CancelledReportValidUntilUtc = cancelledReportValidUntilUtc;
            IsNilForecast = isNilForecast;
            BaseForecastNilReason = baseForecastNilReason;
            IsTranslationFailed = isTranslationFailed;
            TranslationFailedTac = translationFailedTac;
            BaseForecast = baseForecast;
            ChangeForecasts = changeForecasts;
        }

        /// <summary>Creates a TAF, enforcing the minimal domain invariants and collecting every violation in the returned <see cref="Result{T}"/>.</summary>
        /// <param name="id">The <c>gml:id</c> of the report root element; required (not blank).</param>
        /// <param name="issueTimeUtc">The <c>iwxxm:issueTime</c> instant; required (not <c>default</c>) and expressed in UTC (offset zero).</param>
        /// <param name="reportStatus">The <c>reportStatus</c> attribute (<c>NORMAL</c>, <c>AMENDMENT</c> or <c>CORRECTION</c>); must be a defined value.</param>
        /// <param name="permissibleUsage">The <c>permissibleUsage</c> attribute (<c>OPERATIONAL</c> or <c>NON-OPERATIONAL</c>); must be a defined value.</param>
        /// <param name="permissibleUsageReason">The optional <c>permissibleUsageReason</c> (<c>TEST</c> or <c>EXERCISE</c>), meaningful for non-operational data; <c>null</c> when not reported. When given it must be a defined value.</param>
        /// <param name="permissibleUsageSupplementary">The optional free-text <c>permissibleUsageSupplementary</c>; <c>null</c> when not reported.</param>
        /// <param name="aerodromeDesignator">The ICAO location indicator or designator of the aerodrome (<c>aixm:designator</c>); required (not blank), stored trimmed.</param>
        /// <param name="aerodromeName">The aerodrome name (<c>aixm:name</c>); <c>null</c> when not reported.</param>
        /// <param name="aerodromePosition">The aerodrome reference point (<c>aixm:ARP</c>); <c>null</c> when not reported.</param>
        /// <param name="validFromUtc">The start of <c>iwxxm:validPeriod</c> in UTC; <c>null</c> when the period is not reported.</param>
        /// <param name="validUntilUtc">The end of <c>iwxxm:validPeriod</c> in UTC; must be strictly after <paramref name="validFromUtc"/> when both are given.</param>
        /// <param name="isCancelReport">Whether the report is a cancellation (<c>isCancelReport</c>).</param>
        /// <param name="cancelledReportValidFromUtc">The start of <c>iwxxm:cancelledReportValidPeriod</c> in UTC; <c>null</c> when not reported.</param>
        /// <param name="cancelledReportValidUntilUtc">The end of <c>iwxxm:cancelledReportValidPeriod</c> in UTC; must be strictly after the start when both are given.</param>
        /// <param name="isNilForecast">Whether <c>iwxxm:baseForecast</c> is nil (a missing report).</param>
        /// <param name="baseForecastNilReason">The <c>nilReason</c> of the nil base forecast; <c>null</c> when a forecast is present.</param>
        /// <param name="isTranslationFailed">Whether the TAC-to-XML translation failed (<c>translationFailedTAC</c> present).</param>
        /// <param name="translationFailedTac">The original TAC text that could not be translated; <c>null</c> when the translation succeeded.</param>
        /// <param name="baseForecast">The prevailing conditions; its own invariants (period order, non-negative visibility, wind ranges) are checked.</param>
        /// <param name="changeForecasts">The change and probability groups in document order; <c>null</c> is treated as empty. Each group's invariants are checked.</param>
        public static Result<Taf> Create(
            string id,
            DateTimeOffset issueTimeUtc,
            ReportStatus reportStatus,
            PermissibleUsage permissibleUsage,
            string aerodromeDesignator,
            string? aerodromeName,
            GeoCoordinate? aerodromePosition,
            DateTimeOffset? validFromUtc = null,
            DateTimeOffset? validUntilUtc = null,
            bool isCancelReport = false,
            DateTimeOffset? cancelledReportValidFromUtc = null,
            DateTimeOffset? cancelledReportValidUntilUtc = null,
            bool isNilForecast = false,
            CodeListValue<NilReason>? baseForecastNilReason = null,
            bool isTranslationFailed = false,
            string? translationFailedTac = null,
            AerodromeForecastGroup? baseForecast = null,
            IEnumerable<AerodromeForecastGroup>? changeForecasts = null,
            PermissibleUsageReason? permissibleUsageReason = null,
            string? permissibleUsageSupplementary = null)
        {
            var changeList = changeForecasts?.ToList() ?? new List<AerodromeForecastGroup>(0);

            var invariants = new DomainInvariants(id);
            invariants.RequireText(id, "DOMAIN-TAF-ID", "The TAF report gml:id identifier is required.");
            invariants.RequireText(aerodromeDesignator, "DOMAIN-TAF-AERODROME", "The aerodrome ICAO designator is required.");
            invariants.RequireMetadata(reportStatus, permissibleUsage, permissibleUsageReason);
            invariants.RequireIssueTime(issueTimeUtc);
            invariants.RequireOrderedPeriod(validFromUtc, validUntilUtc, "DOMAIN-TAF-VALID-PERIOD", "TAF validity period");
            invariants.RequireOrderedPeriod(cancelledReportValidFromUtc, cancelledReportValidUntilUtc, "DOMAIN-TAF-CANCELLED-VALID-PERIOD", "cancelled TAF validity period");
            invariants.Check(baseForecast);
            foreach (AerodromeForecastGroup change in changeList)
            {
                invariants.Check(change);
            }

            if (invariants.HasErrors)
            {
                return Result<Taf>.Failure(invariants.Errors);
            }

            return Result<Taf>.Success(new Taf(
                id,
                issueTimeUtc,
                reportStatus,
                permissibleUsage,
                permissibleUsageReason,
                permissibleUsageSupplementary,
                aerodromeDesignator.Trim(),
                aerodromeName,
                aerodromePosition,
                validFromUtc,
                validUntilUtc,
                isCancelReport,
                cancelledReportValidFromUtc,
                cancelledReportValidUntilUtc,
                isNilForecast,
                baseForecastNilReason,
                isTranslationFailed,
                translationFailedTac,
                baseForecast,
                new ReadOnlyCollection<AerodromeForecastGroup>(changeList)));
        }
    }
}
