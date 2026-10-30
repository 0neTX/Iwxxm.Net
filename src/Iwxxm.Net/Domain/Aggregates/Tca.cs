using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Iwxxm.Net.Reports
{
    /// <summary>
    /// Domain Aggregate Root representing a Tropical Cyclone Advisory (<c>TropicalCycloneAdvisory</c>)
    /// under ICAO/WMO IWXXM 2025-2.
    /// </summary>
    public sealed class Tca : IwxxmReport
    {
        public override string Id { get; }
        public override IwxxmReportType ReportType => IwxxmReportType.TropicalCycloneAdvisory;
        public override DateTimeOffset IssueTimeUtc { get; }
        public override ReportStatus ReportStatus { get; }
        public override PermissibleUsage PermissibleUsage { get; }
        public override PermissibleUsageReason? PermissibleUsageReason { get; }
        public override string? PermissibleUsageSupplementary { get; }
        public string IssuingTcacDesignator { get; }
        public string? CycloneName { get; }
        public string? AdvisoryNumber { get; }
        public TropicalCycloneTrackPoint? Observation { get; }
        public IReadOnlyList<TropicalCycloneTrackPoint> Forecasts { get; }

        /// <summary>
        /// Observed <c>cumulonimbusCloudLocation</c> volumes (polygon or circle, e.g. "CB WI 250NM OF TC CENTRE") with
        /// their vertical limits, in document order.
        /// </summary>
        public IReadOnlyList<EnRoutePhenomenonVolume> CumulonimbusCloudLocations { get; }
        public string? Remarks { get; }
        public DateTimeOffset? NextAdvisoryTimeUtc { get; }
        public override bool IsTranslationFailed { get; }
        public override string? TranslationFailedTac { get; }

        /// <inheritdoc />
        public override TResult Accept<TResult>(IIwxxmDocumentVisitor<TResult> visitor) =>
            (visitor ?? throw new ArgumentNullException(nameof(visitor))).Visit(this);

        private Tca(
            string id,
            DateTimeOffset issueTimeUtc,
            ReportStatus reportStatus,
            PermissibleUsage permissibleUsage,
            PermissibleUsageReason? permissibleUsageReason,
            string? permissibleUsageSupplementary,
            string issuingTcacDesignator,
            string? cycloneName,
            string? advisoryNumber,
            TropicalCycloneTrackPoint? observation,
            IReadOnlyList<TropicalCycloneTrackPoint> forecasts,
            string? remarks,
            DateTimeOffset? nextAdvisoryTimeUtc,
            bool isTranslationFailed,
            string? translationFailedTac,
            IReadOnlyList<EnRoutePhenomenonVolume> cumulonimbusCloudLocations)
        {
            Id = id;
            IssueTimeUtc = issueTimeUtc;
            ReportStatus = reportStatus;
            PermissibleUsage = permissibleUsage;
            PermissibleUsageReason = permissibleUsageReason;
            PermissibleUsageSupplementary = permissibleUsageSupplementary;
            IssuingTcacDesignator = issuingTcacDesignator;
            CycloneName = cycloneName;
            AdvisoryNumber = advisoryNumber;
            Observation = observation;
            Forecasts = forecasts;
            Remarks = remarks;
            NextAdvisoryTimeUtc = nextAdvisoryTimeUtc;
            IsTranslationFailed = isTranslationFailed;
            TranslationFailedTac = translationFailedTac;
            CumulonimbusCloudLocations = cumulonimbusCloudLocations;
        }

        public static Result<Tca> Create(
            string id,
            DateTimeOffset issueTimeUtc,
            ReportStatus reportStatus,
            PermissibleUsage permissibleUsage,
            string issuingTcacDesignator,
            string? cycloneName = null,
            string? advisoryNumber = null,
            TropicalCycloneTrackPoint? observation = null,
            IEnumerable<TropicalCycloneTrackPoint>? forecasts = null,
            string? remarks = null,
            DateTimeOffset? nextAdvisoryTimeUtc = null,
            bool isTranslationFailed = false,
            string? translationFailedTac = null,
            PermissibleUsageReason? permissibleUsageReason = null,
            string? permissibleUsageSupplementary = null,
            IEnumerable<EnRoutePhenomenonVolume>? cumulonimbusCloudLocations = null)
        {
            var cbClouds = cumulonimbusCloudLocations?.ToList() ?? new List<EnRoutePhenomenonVolume>(0);
            var invariants = new DomainInvariants(id);
            invariants.RequireText(id, "DOMAIN-TCA-ID", "The TropicalCycloneAdvisory report gml:id identifier is required.");
            invariants.RequireMetadata(reportStatus, permissibleUsage, permissibleUsageReason);
            invariants.RequireIssueTime(issueTimeUtc);
            invariants.RequireUtc(nextAdvisoryTimeUtc, "next advisory time");
            invariants.Check(observation, "observed tropical cyclone");
            foreach (TropicalCycloneTrackPoint forecast in forecasts ?? Enumerable.Empty<TropicalCycloneTrackPoint>())
            {
                invariants.Check(forecast, "forecast tropical cyclone");
            }

            foreach (EnRoutePhenomenonVolume cbCloud in cbClouds)
            {
                invariants.Check(cbCloud);
            }

            if (invariants.HasErrors)
            {
                return Result<Tca>.Failure(invariants.Errors);
            }

            return Result<Tca>.Success(new Tca(
                id,
                issueTimeUtc,
                reportStatus,
                permissibleUsage,
                permissibleUsageReason,
                permissibleUsageSupplementary,
                issuingTcacDesignator ?? string.Empty,
                cycloneName,
                advisoryNumber,
                observation,
                new ReadOnlyCollection<TropicalCycloneTrackPoint>(forecasts?.ToList() ?? new List<TropicalCycloneTrackPoint>(0)),
                remarks,
                nextAdvisoryTimeUtc,
                isTranslationFailed,
                translationFailedTac,
                new ReadOnlyCollection<EnRoutePhenomenonVolume>(cbClouds)));
        }
    }
}
