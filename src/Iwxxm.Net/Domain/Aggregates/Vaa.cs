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
        /// <inheritdoc />
        public override string Id { get; }
        /// <inheritdoc />
        public override IwxxmReportType ReportType => IwxxmReportType.VolcanicAshAdvisory;
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
        /// <summary>Gets the name of the issuing Volcanic Ash Advisory Centre (<c>aixm:name</c> of <c>iwxxm:issuingVolcanicAshAdvisoryCentre</c>, falling back to its designator); empty only when the document carries neither.</summary>
        public string IssuingVaacName { get; }
        /// <summary>Gets the erupting volcano that is the source of the ash (<c>iwxxm:volcano</c>), or <c>null</c> when the element is absent. The schema prescribes the name <c>UNKNOWN</c> for an unknown volcano and <c>UNNAMED</c> for an unnamed one.</summary>
        public Volcano? Volcano { get; }
        /// <summary>Gets the State, or region if the ash is not reported over a State (<c>iwxxm:stateOrRegion</c>), or <c>null</c> when absent or nil (unknown).</summary>
        public string? StateOrRegion { get; }
        /// <summary>Gets the elevation of the volcano vent above mean sea level (<c>iwxxm:sourceElevationAMSL</c>) in the unit reported, without conversion (the schema requires metres or feet; the reader also accepts kilometres, nautical miles and statute miles and keeps them as read; negative below sea level), or <c>null</c> when absent or nil (unknown).</summary>
        public Length? SourceElevation { get; }
        /// <summary>Gets the advisory number (<c>iwxxm:advisoryNumber</c>: year in full and message number, a separate sequence for each volcano), or <c>null</c> when not reported.</summary>
        public string? AdvisoryNumber { get; }
        /// <summary>Gets the free-text description of the information source (<c>iwxxm:informationSource</c>), or <c>null</c> when not reported.</summary>
        public string? InformationSource { get; }
        /// <summary>Gets the free-text eruption details, excluding the date and time of the eruptions (<c>iwxxm:eruptionDetails</c>), or <c>null</c> when absent or nil (unknown).</summary>
        public string? EruptionDetails { get; }
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

        /// <summary>Creates a volcanic ash advisory, enforcing the minimal domain invariants and collecting every violation in the returned <see cref="Result{T}"/>.</summary>
        /// <param name="id">The <c>gml:id</c> of the report root element; required (not blank).</param>
        /// <param name="issueTimeUtc">The <c>iwxxm:issueTime</c> instant; required (not <c>default</c>) and expressed in UTC (offset zero).</param>
        /// <param name="reportStatus">The <c>reportStatus</c> attribute (<c>NORMAL</c>, <c>AMENDMENT</c> or <c>CORRECTION</c>); must be a defined value.</param>
        /// <param name="permissibleUsage">The <c>permissibleUsage</c> attribute (<c>OPERATIONAL</c> or <c>NON-OPERATIONAL</c>); must be a defined value.</param>
        /// <param name="permissibleUsageReason">The optional <c>permissibleUsageReason</c> (<c>TEST</c> or <c>EXERCISE</c>), meaningful for non-operational data; <c>null</c> when not reported. When given it must be a defined value.</param>
        /// <param name="permissibleUsageSupplementary">The optional free-text <c>permissibleUsageSupplementary</c>; <c>null</c> when not reported.</param>
        /// <param name="issuingVaacName">The name of the issuing Volcanic Ash Advisory Centre (a <c>null</c> is stored as an empty string).</param>
        /// <param name="volcano">The erupting volcano; <c>null</c> when not reported.</param>
        /// <param name="stateOrRegion">The State or region; <c>null</c> when unknown.</param>
        /// <param name="sourceElevation">The vent elevation with its unit; <c>null</c> when unknown.</param>
        /// <param name="advisoryNumber">The advisory number; <c>null</c> when not reported.</param>
        /// <param name="informationSource">The information source text; <c>null</c> when not reported.</param>
        /// <param name="eruptionDetails">The eruption details text; <c>null</c> when unknown.</param>
        /// <param name="remarks">The remarks text; <c>null</c> when absent.</param>
        /// <param name="nextAdvisoryTimeUtc">The time of the next advisory in UTC; <c>null</c> when none is expected.</param>
        /// <param name="isTranslationFailed">Whether the TAC-to-XML translation failed (<c>translationFailedTAC</c> present).</param>
        /// <param name="translationFailedTac">The original TAC text that could not be translated; <c>null</c> when the translation succeeded.</param>
        /// <param name="observation">The observed or estimated ash conditions; its time must be in UTC and each ash cloud volume is checked.</param>
        /// <param name="forecasts">The forecast ash conditions in document order, each checked like <paramref name="observation"/>; <c>null</c> is treated as empty and a <c>null</c> element is an error.</param>
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
