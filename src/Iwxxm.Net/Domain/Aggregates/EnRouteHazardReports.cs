using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Iwxxm.Net.Reports
{
    /// <summary>
    /// Immutable Value Object representing an airspace volume (horizontal polygon + vertical limits + motion)
    /// used in <c>SIGMET</c>, <c>AIRMET</c>, and <c>VolcanicAshAdvisory</c> conditions.
    /// The speed of motion keeps the reported unit (<c>km/h</c> or <c>[kn_i]</c>). Vertical limits keep the reported
    /// value, unit and reference (<c>aixm:lowerLimit</c> / <c>aixm:lowerLimitReference</c>, ...): use
    /// <see cref="VerticalLimit.AsFlightLevel"/> for the flight-level view of STD limits.
    /// </summary>
    public sealed class EnRoutePhenomenonVolume
    {
        public string Id { get; }
        public GeoPolygon? Area { get; }
        public VerticalLimit? LowerLimit { get; }
        public VerticalLimit? UpperLimit { get; }
        public string? IntensityChange { get; }
        public double? DirectionOfMotionDegrees { get; }
        public Speed? SpeedOfMotion { get; }
        public GeoCoordinate? TropicalCycloneCentre { get; }

        public EnRoutePhenomenonVolume(
            string id,
            GeoPolygon? area,
            VerticalLimit? lowerLimit,
            VerticalLimit? upperLimit,
            string? intensityChange = null,
            double? directionOfMotionDegrees = null,
            Speed? speedOfMotion = null,
            GeoCoordinate? tropicalCycloneCentre = null)
        {
            Id = id ?? string.Empty;
            Area = area;
            LowerLimit = lowerLimit;
            UpperLimit = upperLimit;
            intensityChange = string.IsNullOrWhiteSpace(intensityChange) ? null : intensityChange;
            IntensityChange = intensityChange;
            DirectionOfMotionDegrees = directionOfMotionDegrees;
            SpeedOfMotion = speedOfMotion;
            TropicalCycloneCentre = tropicalCycloneCentre;
        }
    }

    /// <summary>
    /// Domain Aggregate Root representing an en-route Significant Meteorological Information report
    /// (<c>SIGMET</c>, <c>VolcanicAshSIGMET</c>, or <c>TropicalCycloneSIGMET</c>) under ICAO/WMO IWXXM 2025-2.
    /// </summary>
    public sealed class SigmetReport : IIwxxmReport
    {
        public string Id { get; }
        public IwxxmReportType ReportType { get; }
        public DateTimeOffset IssueTimeUtc { get; }
        public ReportStatus ReportStatus { get; }
        public PermissibleUsage PermissibleUsage { get; }
        public PermissibleUsageReason? PermissibleUsageReason { get; }
        public string? PermissibleUsageSupplementary { get; }
        public string AtsUnitDesignator { get; }
        public string? AtsUnitName { get; }
        public string? MwoDesignator { get; }
        public string? FirDesignator { get; }
        public string? FirName { get; }
        public string? SequenceNumber { get; }

        /// <summary>Start of <c>validPeriod</c> (mandatory in <c>sigmet.xsd</c>); <c>null</c> only when given by an unresolved <c>xlink:href</c>.</summary>
        public DateTimeOffset? ValidFromUtc { get; }

        /// <summary>End of <c>validPeriod</c> (mandatory in <c>sigmet.xsd</c>); <c>null</c> only when given by an unresolved <c>xlink:href</c>.</summary>
        public DateTimeOffset? ValidUntilUtc { get; }
        public string? PhenomenonUri { get; }
        public bool IsCancelReport { get; }
        public string? CancelledReportSequenceNumber { get; }
        public bool IsTranslationFailed { get; }
        public string? TranslationFailedTac { get; }

        /// <inheritdoc />
        public TResult Accept<TResult>(IIwxxmReportVisitor<TResult> visitor) =>
            (visitor ?? throw new ArgumentNullException(nameof(visitor))).Visit(this);

        public Volcano? EruptingVolcano { get; }
        public string? TropicalCycloneName { get; }
        public IReadOnlyList<EnRoutePhenomenonVolume> EvolvingConditions { get; }
        public IReadOnlyList<EnRoutePhenomenonVolume> ForecastPositions { get; }

        private SigmetReport(
            string id,
            IwxxmReportType reportType,
            DateTimeOffset issueTimeUtc,
            ReportStatus reportStatus,
            PermissibleUsage permissibleUsage,
            PermissibleUsageReason? permissibleUsageReason,
            string? permissibleUsageSupplementary,
            string atsUnitDesignator,
            string? atsUnitName,
            string? mwoDesignator,
            string? firDesignator,
            string? firName,
            string? sequenceNumber,
            DateTimeOffset? validFromUtc,
            DateTimeOffset? validUntilUtc,
            string? phenomenonUri,
            bool isCancelReport,
            string? cancelledReportSequenceNumber,
            bool isTranslationFailed,
            string? translationFailedTac,
            Volcano? eruptingVolcano,
            string? tropicalCycloneName,
            IReadOnlyList<EnRoutePhenomenonVolume> evolvingConditions,
            IReadOnlyList<EnRoutePhenomenonVolume> forecastPositions)
        {
            Id = id;
            ReportType = reportType;
            IssueTimeUtc = issueTimeUtc;
            ReportStatus = reportStatus;
            PermissibleUsage = permissibleUsage;
            PermissibleUsageReason = permissibleUsageReason;
            PermissibleUsageSupplementary = permissibleUsageSupplementary;
            AtsUnitDesignator = atsUnitDesignator;
            AtsUnitName = atsUnitName;
            MwoDesignator = mwoDesignator;
            FirDesignator = firDesignator;
            FirName = firName;
            SequenceNumber = sequenceNumber;
            ValidFromUtc = validFromUtc;
            ValidUntilUtc = validUntilUtc;
            PhenomenonUri = phenomenonUri;
            IsCancelReport = isCancelReport;
            CancelledReportSequenceNumber = cancelledReportSequenceNumber;
            IsTranslationFailed = isTranslationFailed;
            TranslationFailedTac = translationFailedTac;
            EruptingVolcano = eruptingVolcano;
            TropicalCycloneName = tropicalCycloneName;
            EvolvingConditions = evolvingConditions;
            ForecastPositions = forecastPositions;
        }

        public static Result<SigmetReport> Create(
            string id,
            IwxxmReportType reportType,
            DateTimeOffset issueTimeUtc,
            ReportStatus reportStatus,
            PermissibleUsage permissibleUsage,
            string atsUnitDesignator,
            string? atsUnitName,
            string? mwoDesignator,
            string? firDesignator,
            string? firName,
            string? sequenceNumber,
            DateTimeOffset? validFromUtc,
            DateTimeOffset? validUntilUtc,
            string? phenomenonUri = null,
            bool isCancelReport = false,
            string? cancelledReportSequenceNumber = null,
            bool isTranslationFailed = false,
            string? translationFailedTac = null,
            Volcano? eruptingVolcano = null,
            string? tropicalCycloneName = null,
            IEnumerable<EnRoutePhenomenonVolume>? evolvingConditions = null,
            IEnumerable<EnRoutePhenomenonVolume>? forecastPositions = null,
            PermissibleUsageReason? permissibleUsageReason = null,
            string? permissibleUsageSupplementary = null)
        {
            var evolving = evolvingConditions?.ToList() ?? new List<EnRoutePhenomenonVolume>(0);
            var positions = forecastPositions?.ToList() ?? new List<EnRoutePhenomenonVolume>(0);

            var invariants = new DomainInvariants(id);
            invariants.RequireText(id, "DOMAIN-SIGMET-ID", "The SIGMET report gml:id identifier is required.");
            invariants.RequireText(atsUnitDesignator, "DOMAIN-SIGMET-ATS-UNIT", "The issuing Air Traffic Services unit designator is required.");
            invariants.RequireMetadata(reportStatus, permissibleUsage, permissibleUsageReason);
            invariants.RequireIssueTime(issueTimeUtc);
            invariants.RequireOrderedPeriod(validFromUtc, validUntilUtc, "DOMAIN-SIGMET-VALID-PERIOD", "SIGMET validity period");
            foreach (EnRoutePhenomenonVolume volume in evolving.Concat(positions))
            {
                invariants.Check(volume);
            }

            if (invariants.HasErrors)
            {
                return Result<SigmetReport>.Failure(invariants.Errors);
            }

            return Result<SigmetReport>.Success(new SigmetReport(
                id,
                reportType,
                issueTimeUtc,
                reportStatus,
                permissibleUsage,
                permissibleUsageReason,
                permissibleUsageSupplementary,
                atsUnitDesignator.Trim(),
                atsUnitName,
                mwoDesignator,
                firDesignator,
                firName,
                sequenceNumber,
                validFromUtc,
                validUntilUtc,
                phenomenonUri,
                isCancelReport,
                cancelledReportSequenceNumber,
                isTranslationFailed,
                translationFailedTac,
                eruptingVolcano,
                tropicalCycloneName,
                new ReadOnlyCollection<EnRoutePhenomenonVolume>(evolving),
                new ReadOnlyCollection<EnRoutePhenomenonVolume>(positions)));
        }
    }

    /// <summary>
    /// Domain Aggregate Root representing an Airmen's Meteorological Information (<c>AIRMET</c>) report
    /// under ICAO/WMO IWXXM 2025-2.
    /// </summary>
    public sealed class AirmetReport : IIwxxmReport
    {
        public string Id { get; }
        public IwxxmReportType ReportType => IwxxmReportType.Airmet;
        public DateTimeOffset IssueTimeUtc { get; }
        public ReportStatus ReportStatus { get; }
        public PermissibleUsage PermissibleUsage { get; }
        public PermissibleUsageReason? PermissibleUsageReason { get; }
        public string? PermissibleUsageSupplementary { get; }
        public string AtsUnitDesignator { get; }
        public string? AtsUnitName { get; }
        public string? MwoDesignator { get; }
        public string? FirDesignator { get; }
        public string? FirName { get; }
        public string? SequenceNumber { get; }
        /// <summary>Start of <c>validPeriod</c> (mandatory in <c>airmet.xsd</c>); <c>null</c> only when given by an unresolved <c>xlink:href</c>.</summary>
        public DateTimeOffset? ValidFromUtc { get; }

        /// <summary>End of <c>validPeriod</c> (mandatory in <c>airmet.xsd</c>); <c>null</c> only when given by an unresolved <c>xlink:href</c>.</summary>
        public DateTimeOffset? ValidUntilUtc { get; }
        public string? PhenomenonUri { get; }
        public bool IsTranslationFailed { get; }
        public string? TranslationFailedTac { get; }

        /// <inheritdoc />
        public TResult Accept<TResult>(IIwxxmReportVisitor<TResult> visitor) =>
            (visitor ?? throw new ArgumentNullException(nameof(visitor))).Visit(this);

        public IReadOnlyList<EnRoutePhenomenonVolume> EvolvingConditions { get; }

        private AirmetReport(
            string id,
            DateTimeOffset issueTimeUtc,
            ReportStatus reportStatus,
            PermissibleUsage permissibleUsage,
            PermissibleUsageReason? permissibleUsageReason,
            string? permissibleUsageSupplementary,
            string atsUnitDesignator,
            string? atsUnitName,
            string? mwoDesignator,
            string? firDesignator,
            string? firName,
            string? sequenceNumber,
            DateTimeOffset? validFromUtc,
            DateTimeOffset? validUntilUtc,
            string? phenomenonUri,
            bool isTranslationFailed,
            string? translationFailedTac,
            IReadOnlyList<EnRoutePhenomenonVolume> evolvingConditions)
        {
            Id = id;
            IssueTimeUtc = issueTimeUtc;
            ReportStatus = reportStatus;
            PermissibleUsage = permissibleUsage;
            PermissibleUsageReason = permissibleUsageReason;
            PermissibleUsageSupplementary = permissibleUsageSupplementary;
            AtsUnitDesignator = atsUnitDesignator;
            AtsUnitName = atsUnitName;
            MwoDesignator = mwoDesignator;
            FirDesignator = firDesignator;
            FirName = firName;
            SequenceNumber = sequenceNumber;
            ValidFromUtc = validFromUtc;
            ValidUntilUtc = validUntilUtc;
            PhenomenonUri = phenomenonUri;
            IsTranslationFailed = isTranslationFailed;
            TranslationFailedTac = translationFailedTac;
            EvolvingConditions = evolvingConditions;
        }

        public static Result<AirmetReport> Create(
            string id,
            DateTimeOffset issueTimeUtc,
            ReportStatus reportStatus,
            PermissibleUsage permissibleUsage,
            string atsUnitDesignator,
            string? atsUnitName,
            string? mwoDesignator,
            string? firDesignator,
            string? firName,
            string? sequenceNumber,
            DateTimeOffset? validFromUtc,
            DateTimeOffset? validUntilUtc,
            string? phenomenonUri = null,
            bool isTranslationFailed = false,
            string? translationFailedTac = null,
            IEnumerable<EnRoutePhenomenonVolume>? evolvingConditions = null,
            PermissibleUsageReason? permissibleUsageReason = null,
            string? permissibleUsageSupplementary = null)
        {
            var evolving = evolvingConditions?.ToList() ?? new List<EnRoutePhenomenonVolume>(0);

            var invariants = new DomainInvariants(id);
            invariants.RequireText(id, "DOMAIN-AIRMET-ID", "The AIRMET report gml:id identifier is required.");
            invariants.RequireText(atsUnitDesignator, "DOMAIN-AIRMET-ATS-UNIT", "The issuing Air Traffic Services unit designator is required.");
            invariants.RequireMetadata(reportStatus, permissibleUsage, permissibleUsageReason);
            invariants.RequireIssueTime(issueTimeUtc);
            invariants.RequireOrderedPeriod(validFromUtc, validUntilUtc, "DOMAIN-AIRMET-VALID-PERIOD", "AIRMET validity period");
            foreach (EnRoutePhenomenonVolume volume in evolving)
            {
                invariants.Check(volume);
            }

            if (invariants.HasErrors)
            {
                return Result<AirmetReport>.Failure(invariants.Errors);
            }

            return Result<AirmetReport>.Success(new AirmetReport(
                id,
                issueTimeUtc,
                reportStatus,
                permissibleUsage,
                permissibleUsageReason,
                permissibleUsageSupplementary,
                atsUnitDesignator.Trim(),
                atsUnitName,
                mwoDesignator,
                firDesignator,
                firName,
                sequenceNumber,
                validFromUtc,
                validUntilUtc,
                phenomenonUri,
                isTranslationFailed,
                translationFailedTac,
                new ReadOnlyCollection<EnRoutePhenomenonVolume>(evolving)));
        }
    }
}
