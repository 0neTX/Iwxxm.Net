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
        /// <inheritdoc />
        public override string Id { get; }
        /// <inheritdoc />
        public override IwxxmReportType ReportType => IwxxmReportType.TropicalCycloneAdvisory;
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
        /// <summary>Gets the designator of the issuing Tropical Cyclone Advisory Centre (<c>aixm:designator</c> of <c>iwxxm:issuingTropicalCycloneAdvisoryCentre</c>, falling back to its name); empty only when the document carries neither.</summary>
        public string IssuingTcacDesignator { get; }
        /// <summary>Gets the tropical cyclone name (<c>metce:name</c> of <c>iwxxm:tropicalCycloneName</c>; <c>NN</c> may be used when the cyclone is unnamed), or <c>null</c> when not reported.</summary>
        public string? CycloneName { get; }
        /// <summary>Gets the advisory number (<c>iwxxm:advisoryNumber</c>: year in full and message number, a separate sequence for each cyclone), or <c>null</c> when not reported.</summary>
        public string? AdvisoryNumber { get; }
        /// <summary>Gets the observed or estimated position and intensity of the cyclone (<c>iwxxm:observation</c>), or <c>null</c> when the observation is absent or has no position.</summary>
        public TropicalCycloneTrackPoint? Observation { get; }
        /// <summary>Gets the forecast positions of the cyclone (<c>iwxxm:forecast</c>, one per forecast time) in document order; empty when none is reported.</summary>
        public IReadOnlyList<TropicalCycloneTrackPoint> Forecasts { get; }

        /// <summary>
        /// Observed <c>cumulonimbusCloudLocation</c> volumes (polygon or circle, e.g. "CB WI 250NM OF TC CENTRE") with
        /// their vertical limits, in document order.
        /// </summary>
        public IReadOnlyList<EnRoutePhenomenonVolume> CumulonimbusCloudLocations { get; }
        /// <summary>Gets the free-text remarks (<c>iwxxm:remarks</c>), or <c>null</c> when absent or nil (nil reason <c>inapplicable</c>).</summary>
        public string? Remarks { get; }
        /// <summary>Gets the time the next advisory is expected to be issued (<c>iwxxm:nextAdvisoryTime</c>), or <c>null</c> when absent or nil (nil reason <c>inapplicable</c>, no subsequent advisory expected). The <c>indeterminatePosition</c> qualifier (before or after the stated time) that the schema allows is not read, so it is dropped.</summary>
        public DateTimeOffset? NextAdvisoryTimeUtc { get; }
        /// <inheritdoc />
        public override bool IsTranslationFailed { get; }
        /// <inheritdoc />
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

        /// <summary>Creates a tropical cyclone advisory, enforcing the minimal domain invariants and collecting every violation in the returned <see cref="Result{T}"/>.</summary>
        /// <param name="id">The <c>gml:id</c> of the report root element; required (not blank).</param>
        /// <param name="issueTimeUtc">The <c>iwxxm:issueTime</c> instant; required (not <c>default</c>) and expressed in UTC (offset zero).</param>
        /// <param name="reportStatus">The <c>reportStatus</c> attribute (<c>NORMAL</c>, <c>AMENDMENT</c> or <c>CORRECTION</c>); must be a defined value.</param>
        /// <param name="permissibleUsage">The <c>permissibleUsage</c> attribute (<c>OPERATIONAL</c> or <c>NON-OPERATIONAL</c>); must be a defined value.</param>
        /// <param name="permissibleUsageReason">The optional <c>permissibleUsageReason</c> (<c>TEST</c> or <c>EXERCISE</c>), meaningful for non-operational data; <c>null</c> when not reported. When given it must be a defined value.</param>
        /// <param name="permissibleUsageSupplementary">The optional free-text <c>permissibleUsageSupplementary</c>; <c>null</c> when not reported.</param>
        /// <param name="issuingTcacDesignator">The designator of the issuing Tropical Cyclone Advisory Centre.</param>
        /// <param name="cycloneName">The tropical cyclone name; <c>null</c> when not reported.</param>
        /// <param name="advisoryNumber">The advisory number; <c>null</c> when not reported.</param>
        /// <param name="observation">The observed position and intensity; its time must be in UTC and its maximum surface wind speed non-negative.</param>
        /// <param name="forecasts">The forecast positions in document order, each checked like <paramref name="observation"/>; <c>null</c> is treated as empty.</param>
        /// <param name="remarks">The free-text remarks; <c>null</c> when absent.</param>
        /// <param name="nextAdvisoryTimeUtc">The time of the next advisory in UTC; <c>null</c> when none is expected.</param>
        /// <param name="isTranslationFailed">Whether the TAC-to-XML translation failed (<c>translationFailedTAC</c> present).</param>
        /// <param name="translationFailedTac">The original TAC text that could not be translated; <c>null</c> when the translation succeeded.</param>
        /// <param name="cumulonimbusCloudLocations">The observed cumulonimbus cloud volumes (<c>iwxxm:cumulonimbusCloudLocation</c>); <c>null</c> is treated as empty. Direction and speed of motion of each volume are checked.</param>
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
