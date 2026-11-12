using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Iwxxm.Net.Reports
{
    /// <summary>
    /// Domain Aggregate Root representing an aeronautical Significant Weather (SIGWX) report
    /// under the ICAO/WMO IWXXM 2025-2 standard (covering both WAFSSignificantWeatherForecast and SIGWXReport).
    /// The factory enforces the forecast-level invariants (identifier, issuing centre, ordered UTC validity period,
    /// upper bounding limit above the lower one); each of the 9 WAFS SIGWX phenomenon value objects enforces its own
    /// invariants in its own <c>Create</c> factory.
    /// </summary>
    public sealed class Sigwx : IwxxmReport
    {
        /// <inheritdoc />
        public override string Id { get; }
        /// <inheritdoc />
        public override IwxxmReportType ReportType => IwxxmReportType.SignificantWeather;
        /// <summary>Gets the issuing centre as written in the document; never blank. For a WAFS SIGWX forecast it is the <c>iwxxm:originatingCentre</c> <c>WorldAreaForecastCentre</c> value (<c>Washington</c> for the NWS WAFC or <c>London</c> for the UK Met Office WAFC); for a compact SIGWX report it is the free <c>iwxxm:issuingCentre</c> string, which has no enumeration.</summary>
        public string IssuingCentre { get; }
        /// <inheritdoc />
        public override ReportStatus ReportStatus { get; }
        /// <inheritdoc />
        public override PermissibleUsage PermissibleUsage { get; }
        /// <inheritdoc />
        public override PermissibleUsageReason? PermissibleUsageReason { get; }
        /// <inheritdoc />
        public override string? PermissibleUsageSupplementary { get; }
        /// <inheritdoc />
        public override DateTimeOffset IssueTimeUtc { get; }
        /// <summary>Gets the start of the chart validity (<c>iwxxm:boundingPeriod</c> of the forecast). Mandatory: a chart without it fails to map.</summary>
        public DateTimeOffset ValidFromUtc { get; }
        /// <summary>Gets the end of the chart validity (<c>iwxxm:boundingPeriod</c>). It is never before <see cref="ValidFromUtc"/>; an equal value is accepted because operational WAFS products are valid at a single instant.</summary>
        public DateTimeOffset ValidUntilUtc { get; }

        /// <summary>Lower limit of the bounding volume as reported (value, unit, vertical reference or nil reason).</summary>
        public VerticalLimit LowerLimit { get; }

        /// <summary>Upper limit of the bounding volume as reported (value, unit, vertical reference or nil reason).</summary>
        public VerticalLimit UpperLimit { get; }
        /// <summary>Gets <c>false</c>: a SIGWX chart is not produced by TAC translation, so it never carries <c>translationFailedTAC</c>.</summary>
        public override bool IsTranslationFailed => false;
        /// <summary>Gets <c>null</c>: a SIGWX chart is not produced by TAC translation, so it never carries <c>translationFailedTAC</c>.</summary>
        public override string? TranslationFailedTac => null;

        /// <inheritdoc />
        public override TResult Accept<TResult>(IIwxxmDocumentVisitor<TResult> visitor) =>
            (visitor ?? throw new ArgumentNullException(nameof(visitor))).Visit(this);

        /// <summary>Gets the jet stream features of the chart (an axis with its maximum-wind symbols), in document order; empty when the chart carries none.</summary>
        public IReadOnlyList<JetStream> JetStreams { get; }
        /// <summary>Gets the significant cloud areas of the chart (cloud distribution and type with base and top levels), in document order; empty when none is reported.</summary>
        public IReadOnlyList<Cloud> Clouds { get; }
        /// <summary>Gets the tropopause features of the chart (spot heights or height contours), in document order; empty when none is reported.</summary>
        public IReadOnlyList<Tropopause> Tropopauses { get; }
        /// <summary>Gets the volcanic eruption symbols of the chart (name and position of the volcano), in document order; empty when none is reported.</summary>
        public IReadOnlyList<Volcano> Volcanoes { get; }
        /// <summary>Gets the areas of moderate or severe turbulence of the chart, in document order; empty when none is reported.</summary>
        public IReadOnlyList<Turbulence> Turbulences { get; }
        /// <summary>Gets the areas of moderate or severe airframe icing of the chart, in document order; empty when none is reported.</summary>
        public IReadOnlyList<AirframeIcing> AirframeIcings { get; }
        /// <summary>Gets the tropical cyclone symbols of the chart (name and position of the cyclone), in document order; empty when none is reported.</summary>
        public IReadOnlyList<TropicalCyclone> TropicalCyclones { get; }
        /// <summary>Gets the widespread sandstorm and duststorm symbols of the chart (one position each), in document order; empty when none is reported.</summary>
        public IReadOnlyList<Sandstorm> Sandstorms { get; }
        /// <summary>Gets the radiation incident symbols of the chart (location name and incident time), in document order; empty when none is reported.</summary>
        public IReadOnlyList<RadiationIncident> RadiationIncidents { get; }

        /// <summary>
        /// WAFS chart features (<c>iwxxm:feature</c>) that could not be mapped, with their position and <c>gml:id</c>; the
        /// chart keeps every other feature. Empty when every feature mapped.
        /// </summary>
        public IReadOnlyList<ItemMappingError> FeatureErrors { get; }

        private Sigwx(
            string id,
            string issuingCentre,
            ReportStatus reportStatus,
            PermissibleUsage permissibleUsage,
            PermissibleUsageReason? permissibleUsageReason,
            string? permissibleUsageSupplementary,
            DateTimeOffset issueTimeUtc,
            DateTimeOffset validFromUtc,
            DateTimeOffset validUntilUtc,
            VerticalLimit lowerLimit,
            VerticalLimit upperLimit,
            IReadOnlyList<JetStream> jetStreams,
            IReadOnlyList<Cloud> clouds,
            IReadOnlyList<Tropopause> tropopauses,
            IReadOnlyList<Volcano> volcanoes,
            IReadOnlyList<Turbulence> turbulences,
            IReadOnlyList<AirframeIcing> airframeIcings,
            IReadOnlyList<TropicalCyclone> tropicalCyclones,
            IReadOnlyList<Sandstorm> sandstorms,
            IReadOnlyList<RadiationIncident> radiationIncidents,
            IReadOnlyList<ItemMappingError> featureErrors)
        {
            Id = id;
            IssuingCentre = issuingCentre;
            ReportStatus = reportStatus;
            PermissibleUsage = permissibleUsage;
            PermissibleUsageReason = permissibleUsageReason;
            PermissibleUsageSupplementary = permissibleUsageSupplementary;
            IssueTimeUtc = issueTimeUtc;
            ValidFromUtc = validFromUtc;
            ValidUntilUtc = validUntilUtc;
            LowerLimit = lowerLimit;
            UpperLimit = upperLimit;
            JetStreams = jetStreams;
            Clouds = clouds;
            Tropopauses = tropopauses;
            Volcanoes = volcanoes;
            Turbulences = turbulences;
            AirframeIcings = airframeIcings;
            TropicalCyclones = tropicalCyclones;
            Sandstorms = sandstorms;
            RadiationIncidents = radiationIncidents;
            FeatureErrors = featureErrors;
        }

        /// <summary>Creates a SIGWX chart, enforcing the forecast-level invariants and collecting every violation in the returned <see cref="Result{T}"/>.</summary>
        /// <param name="id">The <c>gml:id</c> of the report root element; required (not blank).</param>
        /// <param name="issueTimeUtc">The <c>iwxxm:issueTime</c> instant; required (not <c>default</c>) and expressed in UTC (offset zero).</param>
        /// <param name="reportStatus">The <c>reportStatus</c> attribute (<c>NORMAL</c>, <c>AMENDMENT</c> or <c>CORRECTION</c>); must be a defined value.</param>
        /// <param name="permissibleUsage">The <c>permissibleUsage</c> attribute (<c>OPERATIONAL</c> or <c>NON-OPERATIONAL</c>); must be a defined value.</param>
        /// <param name="permissibleUsageReason">The optional <c>permissibleUsageReason</c> (<c>TEST</c> or <c>EXERCISE</c>), meaningful for non-operational data; <c>null</c> when not reported. When given it must be a defined value.</param>
        /// <param name="permissibleUsageSupplementary">The optional free-text <c>permissibleUsageSupplementary</c>; <c>null</c> when not reported.</param>
        /// <param name="issuingCentre">The World Area Forecast Centre that issued the chart; required (not blank).</param>
        /// <param name="validFromUtc">The start of the bounding period in UTC.</param>
        /// <param name="validUntilUtc">The end of the bounding period in UTC; must not be before <paramref name="validFromUtc"/>.</param>
        /// <param name="lowerLimit">The lower limit of the bounding volume (<c>lowerElevation</c>) with its unit and vertical reference.</param>
        /// <param name="upperLimit">The upper limit of the bounding volume (<c>upperElevation</c>); when it is comparable with <paramref name="lowerLimit"/> it must be strictly higher.</param>
        /// <param name="jetStreams">The jet stream features; <c>null</c> is treated as empty.</param>
        /// <param name="clouds">The cloud features; <c>null</c> is treated as empty.</param>
        /// <param name="tropopauses">The tropopause features; <c>null</c> is treated as empty.</param>
        /// <param name="volcanoes">The volcanic eruption features; <c>null</c> is treated as empty.</param>
        /// <param name="turbulences">The turbulence features; <c>null</c> is treated as empty.</param>
        /// <param name="airframeIcings">The airframe icing features; <c>null</c> is treated as empty.</param>
        /// <param name="tropicalCyclones">The tropical cyclone features; <c>null</c> is treated as empty.</param>
        /// <param name="sandstorms">The sandstorm features; <c>null</c> is treated as empty.</param>
        /// <param name="radiationIncidents">The radiation incident features; <c>null</c> is treated as empty.</param>
        /// <param name="featureErrors">The features that could not be mapped; <c>null</c> is treated as empty.</param>
        public static Result<Sigwx> Create(
            string id,
            string issuingCentre,
            ReportStatus reportStatus,
            PermissibleUsage permissibleUsage,
            DateTimeOffset issueTimeUtc,
            DateTimeOffset validFromUtc,
            DateTimeOffset validUntilUtc,
            VerticalLimit lowerLimit,
            VerticalLimit upperLimit,
            IEnumerable<JetStream>? jetStreams = null,
            IEnumerable<Cloud>? clouds = null,
            IEnumerable<Tropopause>? tropopauses = null,
            IEnumerable<Volcano>? volcanoes = null,
            IEnumerable<Turbulence>? turbulences = null,
            IEnumerable<AirframeIcing>? airframeIcings = null,
            IEnumerable<TropicalCyclone>? tropicalCyclones = null,
            IEnumerable<Sandstorm>? sandstorms = null,
            IEnumerable<RadiationIncident>? radiationIncidents = null,
            PermissibleUsageReason? permissibleUsageReason = null,
            string? permissibleUsageSupplementary = null,
            IEnumerable<ItemMappingError>? featureErrors = null)
        {
            var featureErrorList = featureErrors?.ToList() ?? new List<ItemMappingError>(0);
            var invariants = new DomainInvariants(id);
            invariants.RequireMetadata(reportStatus, permissibleUsage, permissibleUsageReason);
            invariants.RequireIssueTime(issueTimeUtc);
            invariants.RequireUtc(validFromUtc, "validity start");
            invariants.RequireUtc(validUntilUtc, "validity end");
            var errors = new List<ValidationError>(invariants.Errors);

            if (string.IsNullOrWhiteSpace(id))
            {
                errors.Add(new ValidationError(
                    "DOMAIN-SIGWX-ID",
                    "The SIGWX report gml:id identifier is required.",
                    ErrorCategory.DomainMapping));
            }

            if (string.IsNullOrWhiteSpace(issuingCentre))
            {
                errors.Add(new ValidationError(
                    "DOMAIN-SIGWX-CENTRE",
                    "The issuing World Area Forecast Centre (issuingCentre / originatingCentre) is required.",
                    ErrorCategory.DomainMapping,
                    ValidationSeverity.Error,
                    id));
            }

            // A chart may be valid at a single instant (begin == end, as the operational WAFS products publish them);
            // only an end before the start is an error.
            if (validUntilUtc < validFromUtc)
            {
                errors.Add(new ValidationError(
                    "DOMAIN-SIGWX-VALID-PERIOD",
                    $"Validity end time ({validUntilUtc:O}) must not be before validity start time ({validFromUtc:O}).",
                    ErrorCategory.DomainMapping,
                    ValidationSeverity.Error,
                    id));
            }

            if (upperLimit.TryCompareTo(lowerLimit, out int verticalOrder) && verticalOrder <= 0)
            {
                errors.Add(new ValidationError(
                    "DOMAIN-SIGWX-VERTICAL-LIMITS",
                    $"Upper vertical limit ({upperLimit}) must be strictly greater than lower vertical limit ({lowerLimit}).",
                    ErrorCategory.DomainMapping,
                    ValidationSeverity.Error,
                    id));
            }

            if (errors.Count > 0)
            {
                return Result<Sigwx>.Failure(errors);
            }

            return Result<Sigwx>.Success(new Sigwx(
                id,
                issuingCentre.Trim(),
                reportStatus,
                permissibleUsage,
                permissibleUsageReason,
                permissibleUsageSupplementary,
                issueTimeUtc,
                validFromUtc,
                validUntilUtc,
                lowerLimit,
                upperLimit,
                new ReadOnlyCollection<JetStream>(jetStreams?.ToList() ?? new List<JetStream>(0)),
                new ReadOnlyCollection<Cloud>(clouds?.ToList() ?? new List<Cloud>(0)),
                new ReadOnlyCollection<Tropopause>(tropopauses?.ToList() ?? new List<Tropopause>(0)),
                new ReadOnlyCollection<Volcano>(volcanoes?.ToList() ?? new List<Volcano>(0)),
                new ReadOnlyCollection<Turbulence>(turbulences?.ToList() ?? new List<Turbulence>(0)),
                new ReadOnlyCollection<AirframeIcing>(airframeIcings?.ToList() ?? new List<AirframeIcing>(0)),
                new ReadOnlyCollection<TropicalCyclone>(tropicalCyclones?.ToList() ?? new List<TropicalCyclone>(0)),
                new ReadOnlyCollection<Sandstorm>(sandstorms?.ToList() ?? new List<Sandstorm>(0)),
                new ReadOnlyCollection<RadiationIncident>(radiationIncidents?.ToList() ?? new List<RadiationIncident>(0)),
                new ReadOnlyCollection<ItemMappingError>(featureErrorList)),
                featureErrorList.SelectMany(e => e.ToWarnings("Feature")));
        }
    }
}
