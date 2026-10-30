using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Iwxxm.Net.Reports
{
    /// <summary>
    /// Domain Aggregate Root representing a Volcanic Ash Advisory (<c>VolcanicAshAdvisory</c>)
    /// under ICAO/WMO IWXXM 2025-2.
    /// </summary>
    public sealed class Vaa : IwxxmReport
    {
        public override string Id { get; }
        public override IwxxmReportType ReportType => IwxxmReportType.VolcanicAshAdvisory;
        public override DateTimeOffset IssueTimeUtc { get; }
        public override ReportStatus ReportStatus { get; }
        public override PermissibleUsage PermissibleUsage { get; }
        public override PermissibleUsageReason? PermissibleUsageReason { get; }
        public override string? PermissibleUsageSupplementary { get; }
        public string IssuingVaacName { get; }
        public Volcano? Volcano { get; }
        public string? StateOrRegion { get; }
        public Length? SourceElevation { get; }
        public string? AdvisoryNumber { get; }
        public string? InformationSource { get; }
        public string? EruptionDetails { get; }
        public string? Remarks { get; }
        public DateTimeOffset? NextAdvisoryTimeUtc { get; }
        public override bool IsTranslationFailed { get; }
        public override string? TranslationFailedTac { get; }

        /// <inheritdoc />
        public override TResult Accept<TResult>(IIwxxmDocumentVisitor<TResult> visitor) =>
            (visitor ?? throw new ArgumentNullException(nameof(visitor))).Visit(this);

        /// <summary>Observed or estimated conditions (<c>iwxxm:observation</c>): time, status and ash clouds; <c>null</c> when absent.</summary>
        public VolcanicAshConditions? Observation { get; }

        /// <summary>Ash clouds of <see cref="Observation"/> (empty when there is no observation).</summary>
        public IReadOnlyList<EnRoutePhenomenonVolume> ObservedAshClouds => Observation?.AshClouds ?? EmptyClouds;

        /// <summary>
        /// Forecast conditions, one per <c>iwxxm:forecast</c> (+6/+12/+18 HR) in document order, each with its own
        /// <c>phenomenonTime</c>, <c>@status</c> (e.g. <c>NO_VOLCANIC_ASH_EXPECTED</c>) and ash clouds.
        /// </summary>
        public IReadOnlyList<VolcanicAshConditions> Forecasts { get; }

        private static readonly IReadOnlyList<EnRoutePhenomenonVolume> EmptyClouds = new ReadOnlyCollection<EnRoutePhenomenonVolume>(new List<EnRoutePhenomenonVolume>(0));

        private Vaa(
            string id,
            DateTimeOffset issueTimeUtc,
            ReportStatus reportStatus,
            PermissibleUsage permissibleUsage,
            PermissibleUsageReason? permissibleUsageReason,
            string? permissibleUsageSupplementary,
            string issuingVaacName,
            Volcano? volcano,
            string? stateOrRegion,
            Length? sourceElevation,
            string? advisoryNumber,
            string? informationSource,
            string? eruptionDetails,
            string? remarks,
            DateTimeOffset? nextAdvisoryTimeUtc,
            bool isTranslationFailed,
            string? translationFailedTac,
            VolcanicAshConditions? observation,
            IReadOnlyList<VolcanicAshConditions> forecasts)
        {
            Id = id;
            IssueTimeUtc = issueTimeUtc;
            ReportStatus = reportStatus;
            PermissibleUsage = permissibleUsage;
            PermissibleUsageReason = permissibleUsageReason;
            PermissibleUsageSupplementary = permissibleUsageSupplementary;
            IssuingVaacName = issuingVaacName;
            Volcano = volcano;
            StateOrRegion = stateOrRegion;
            SourceElevation = sourceElevation;
            AdvisoryNumber = advisoryNumber;
            InformationSource = informationSource;
            EruptionDetails = eruptionDetails;
            Remarks = remarks;
            NextAdvisoryTimeUtc = nextAdvisoryTimeUtc;
            IsTranslationFailed = isTranslationFailed;
            TranslationFailedTac = translationFailedTac;
            Observation = observation;
            Forecasts = forecasts;
        }

        public static Result<Vaa> Create(
            string id,
            DateTimeOffset issueTimeUtc,
            ReportStatus reportStatus,
            PermissibleUsage permissibleUsage,
            string issuingVaacName,
            Volcano? volcano = null,
            string? stateOrRegion = null,
            Length? sourceElevation = null,
            string? advisoryNumber = null,
            string? informationSource = null,
            string? eruptionDetails = null,
            string? remarks = null,
            DateTimeOffset? nextAdvisoryTimeUtc = null,
            bool isTranslationFailed = false,
            string? translationFailedTac = null,
            VolcanicAshConditions? observation = null,
            IEnumerable<VolcanicAshConditions>? forecasts = null,
            PermissibleUsageReason? permissibleUsageReason = null,
            string? permissibleUsageSupplementary = null)
        {
            var invariants = new DomainInvariants(id);
            invariants.RequireText(id, "DOMAIN-VAA-ID", "The VolcanicAshAdvisory report gml:id identifier is required.");
            invariants.RequireMetadata(reportStatus, permissibleUsage, permissibleUsageReason);
            invariants.RequireIssueTime(issueTimeUtc);
            invariants.RequireUtc(nextAdvisoryTimeUtc, "next advisory time");
            var forecastList = forecasts?.ToList() ?? new List<VolcanicAshConditions>(0);
            var allConditions = forecastList.ToList();
            if (observation != null)
                allConditions.Insert(0, observation);

            foreach (VolcanicAshConditions conditions in allConditions)
            {
                if (conditions == null)
                {
                    invariants.Add("DOMAIN-VAA-CONDITIONS-NULL", "Volcanic ash conditions cannot be null.");
                    continue;
                }

                invariants.RequireUtc(conditions.PhenomenonTimeUtc, "volcanic ash conditions time");
                foreach (EnRoutePhenomenonVolume cloud in conditions.AshClouds)
                {
                    invariants.Check(cloud);
                }
            }

            if (invariants.HasErrors)
            {
                return Result<Vaa>.Failure(invariants.Errors);
            }

            return Result<Vaa>.Success(new Vaa(
                id,
                issueTimeUtc,
                reportStatus,
                permissibleUsage,
                permissibleUsageReason,
                permissibleUsageSupplementary,
                issuingVaacName ?? string.Empty,
                volcano,
                stateOrRegion,
                sourceElevation,
                advisoryNumber,
                informationSource,
                eruptionDetails,
                remarks,
                nextAdvisoryTimeUtc,
                isTranslationFailed,
                translationFailedTac,
                observation,
                new ReadOnlyCollection<VolcanicAshConditions>(forecastList)));
        }
    }
}
