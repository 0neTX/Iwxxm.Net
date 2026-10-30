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
        public override string Id { get; }
        public override IwxxmReportType ReportType => IwxxmReportType.Taf;
        public override DateTimeOffset IssueTimeUtc { get; }
        public override ReportStatus ReportStatus { get; }
        public override PermissibleUsage PermissibleUsage { get; }
        public override PermissibleUsageReason? PermissibleUsageReason { get; }
        public override string? PermissibleUsageSupplementary { get; }
        public string AerodromeDesignator { get; }
        public string? AerodromeName { get; }
        public GeoCoordinate? AerodromePosition { get; }
        public DateTimeOffset? ValidFromUtc { get; }
        public DateTimeOffset? ValidUntilUtc { get; }
        public bool IsCancelReport { get; }
        public DateTimeOffset? CancelledReportValidFromUtc { get; }
        public DateTimeOffset? CancelledReportValidUntilUtc { get; }
        public bool IsNilForecast { get; }
        public string? BaseForecastNilReason { get; }
        public override bool IsTranslationFailed { get; }
        public override string? TranslationFailedTac { get; }

        /// <inheritdoc />
        public override TResult Accept<TResult>(IIwxxmDocumentVisitor<TResult> visitor) =>
            (visitor ?? throw new ArgumentNullException(nameof(visitor))).Visit(this);

        public AerodromeForecastGroup? BaseForecast { get; }
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
            string? baseForecastNilReason,
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
            string? baseForecastNilReason = null,
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
