using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Iwxxm.Net.Reports
{
    /// <summary>
    /// Domain Aggregate Root representing an aeronautical Significant Weather (SIGWX) report
    /// under the ICAO/WMO IWXXM 2025-2 standard (covering both WAFSSignificantWeatherForecast and SIGWXReport).
    /// Enforces spatial, vertical, and temporal domain invariants across all 9 WAFS SIGWX phenomena.
    /// </summary>
    public sealed class SignificantWeatherReport : IwxxmReport
    {
        public override string Id { get; }
        public override IwxxmReportType ReportType => IwxxmReportType.SignificantWeather;
        public string IssuingCentre { get; }
        public override ReportStatus ReportStatus { get; }
        public override PermissibleUsage PermissibleUsage { get; }
        public override PermissibleUsageReason? PermissibleUsageReason { get; }
        public override string? PermissibleUsageSupplementary { get; }
        public override DateTimeOffset IssueTimeUtc { get; }
        public DateTimeOffset ValidFromUtc { get; }
        public DateTimeOffset ValidUntilUtc { get; }

        /// <summary>Lower limit of the bounding volume as reported (value, unit, vertical reference or nil reason).</summary>
        public VerticalLimit LowerLimit { get; }

        /// <summary>Upper limit of the bounding volume as reported (value, unit, vertical reference or nil reason).</summary>
        public VerticalLimit UpperLimit { get; }
        public override bool IsTranslationFailed => false;
        public override string? TranslationFailedTac => null;

        /// <inheritdoc />
        public override TResult Accept<TResult>(IIwxxmReportVisitor<TResult> visitor) =>
            (visitor ?? throw new ArgumentNullException(nameof(visitor))).Visit(this);

        public IReadOnlyList<JetStream> JetStreams { get; }
        public IReadOnlyList<Cloud> Clouds { get; }
        public IReadOnlyList<Tropopause> Tropopauses { get; }
        public IReadOnlyList<Volcano> Volcanoes { get; }
        public IReadOnlyList<Turbulence> Turbulences { get; }
        public IReadOnlyList<AirframeIcing> AirframeIcings { get; }
        public IReadOnlyList<TropicalCyclone> TropicalCyclones { get; }
        public IReadOnlyList<Sandstorm> Sandstorms { get; }
        public IReadOnlyList<RadiationIncident> RadiationIncidents { get; }

        private SignificantWeatherReport(
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
            IReadOnlyList<RadiationIncident> radiationIncidents)
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
        }

        public static Result<SignificantWeatherReport> Create(
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
            string? permissibleUsageSupplementary = null)
        {
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

            if (validUntilUtc <= validFromUtc)
            {
                errors.Add(new ValidationError(
                    "DOMAIN-SIGWX-VALID-PERIOD",
                    $"Validity end time ({validUntilUtc:O}) must be strictly after validity start time ({validFromUtc:O}).",
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
                return Result<SignificantWeatherReport>.Failure(errors);
            }

            return Result<SignificantWeatherReport>.Success(new SignificantWeatherReport(
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
                new ReadOnlyCollection<RadiationIncident>(radiationIncidents?.ToList() ?? new List<RadiationIncident>(0))));
        }
    }
}
