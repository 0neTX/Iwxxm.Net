using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Iwxxm.Net.Reports
{
    /// <summary>
    /// Immutable Value Object representing a quantitative volcanic ash concentration cloud contour
    /// inside a <see cref="QvaciReport"/> (<c>QuantitativeVolcanicAshConcentrationInformation</c>).
    /// <see cref="AshDensityMgPerM3"/> is the reported <c>ashDensityEqualsOrExceeds</c> (mandatory in <c>qvaci.xsd</c>);
    /// vertical limits keep the reported value, unit and reference.
    /// </summary>
    public sealed class VolcanicAshConcentrationCloud
    {
        public string Id { get; }
        public GeoPolygon Area { get; }
        public VerticalLimit? LowerLimit { get; }
        public VerticalLimit? UpperLimit { get; }
        public double AshDensityMgPerM3 { get; }

        public VolcanicAshConcentrationCloud(
            string id,
            GeoPolygon area,
            VerticalLimit? lowerLimit,
            VerticalLimit? upperLimit,
            double ashDensityMgPerM3)
        {
            Id = id ?? string.Empty;
            Area = area ?? throw new ArgumentNullException(nameof(area));
            LowerLimit = lowerLimit;
            UpperLimit = upperLimit;
            AshDensityMgPerM3 = ashDensityMgPerM3;
        }
    }

    /// <summary>
    /// Domain Aggregate Root representing a Volcano Observatory Notice for Aviation (<c>VONA</c>)
    /// under ICAO/WMO IWXXM 2025-2.
    /// </summary>
    public sealed class VonaReport : IIwxxmReport
    {
        public string Id { get; }
        public IwxxmReportType ReportType => IwxxmReportType.VolcanoObservatoryNotice;
        public DateTimeOffset IssueTimeUtc { get; }
        public ReportStatus ReportStatus { get; }
        public PermissibleUsage PermissibleUsage { get; }
        public PermissibleUsageReason? PermissibleUsageReason { get; }
        public string? PermissibleUsageSupplementary { get; }
        public string OriginatingCentre { get; }
        public string? OriginatingCentreDesignator { get; }
        public string? StateOrRegion { get; }
        public string? NoticeNumber { get; }
        public string? CurrentColourCodeUri { get; }
        public string? PreviousColourCodeUri { get; }
        public string? Contacts { get; }
        public string? Remarks { get; }
        public string? NextNotice { get; }
        public bool IsTranslationFailed { get; }
        public string? TranslationFailedTac { get; }

        /// <inheritdoc />
        public TResult Accept<TResult>(IIwxxmReportVisitor<TResult> visitor) =>
            (visitor ?? throw new ArgumentNullException(nameof(visitor))).Visit(this);

        public IReadOnlyList<Volcano> Volcanoes { get; }

        /// <summary>Ash cloud elevation as reported (value, unit and vertical reference), or <c>null</c> when not reported.</summary>
        public VerticalLimit? AshCloudElevation { get; }

        private VonaReport(
            string id,
            DateTimeOffset issueTimeUtc,
            ReportStatus reportStatus,
            PermissibleUsage permissibleUsage,
            PermissibleUsageReason? permissibleUsageReason,
            string? permissibleUsageSupplementary,
            string originatingCentre,
            string? originatingCentreDesignator,
            string? stateOrRegion,
            string? noticeNumber,
            string? currentColourCodeUri,
            string? previousColourCodeUri,
            string? contacts,
            string? remarks,
            string? nextNotice,
            bool isTranslationFailed,
            string? translationFailedTac,
            IReadOnlyList<Volcano> volcanoes,
            VerticalLimit? ashCloudElevation)
        {
            Id = id;
            IssueTimeUtc = issueTimeUtc;
            ReportStatus = reportStatus;
            PermissibleUsage = permissibleUsage;
            PermissibleUsageReason = permissibleUsageReason;
            PermissibleUsageSupplementary = permissibleUsageSupplementary;
            OriginatingCentre = originatingCentre;
            OriginatingCentreDesignator = originatingCentreDesignator;
            StateOrRegion = stateOrRegion;
            NoticeNumber = noticeNumber;
            CurrentColourCodeUri = currentColourCodeUri;
            PreviousColourCodeUri = previousColourCodeUri;
            Contacts = contacts;
            Remarks = remarks;
            NextNotice = nextNotice;
            IsTranslationFailed = isTranslationFailed;
            TranslationFailedTac = translationFailedTac;
            Volcanoes = volcanoes;
            AshCloudElevation = ashCloudElevation;
        }

        public static Result<VonaReport> Create(
            string id,
            DateTimeOffset issueTimeUtc,
            ReportStatus reportStatus,
            PermissibleUsage permissibleUsage,
            string originatingCentre,
            string? originatingCentreDesignator = null,
            string? stateOrRegion = null,
            string? noticeNumber = null,
            string? currentColourCodeUri = null,
            string? previousColourCodeUri = null,
            string? contacts = null,
            string? remarks = null,
            string? nextNotice = null,
            bool isTranslationFailed = false,
            string? translationFailedTac = null,
            IEnumerable<Volcano>? volcanoes = null,
            VerticalLimit? ashCloudElevation = null,
            PermissibleUsageReason? permissibleUsageReason = null,
            string? permissibleUsageSupplementary = null)
        {
            var invariants = new DomainInvariants(id);
            invariants.RequireText(id, "DOMAIN-VONA-ID", "The VONA report gml:id identifier is required.");
            invariants.RequireMetadata(reportStatus, permissibleUsage, permissibleUsageReason);
            invariants.RequireIssueTime(issueTimeUtc);

            if (invariants.HasErrors)
            {
                return Result<VonaReport>.Failure(invariants.Errors);
            }

            return Result<VonaReport>.Success(new VonaReport(
                id,
                issueTimeUtc,
                reportStatus,
                permissibleUsage,
                permissibleUsageReason,
                permissibleUsageSupplementary,
                originatingCentre ?? string.Empty,
                originatingCentreDesignator,
                stateOrRegion,
                noticeNumber,
                currentColourCodeUri,
                previousColourCodeUri,
                contacts,
                remarks,
                nextNotice,
                isTranslationFailed,
                translationFailedTac,
                new ReadOnlyCollection<Volcano>(volcanoes?.ToList() ?? new List<Volcano>(0)),
                ashCloudElevation));
        }
    }

    /// <summary>
    /// Domain Aggregate Root representing Quantitative Volcanic Ash Concentration Information (<c>QVACI</c>),
    /// introduced in ICAO/WMO IWXXM 2025-2.
    /// </summary>
    public sealed class QvaciReport : IIwxxmReport
    {
        public string Id { get; }
        public IwxxmReportType ReportType => IwxxmReportType.QuantitativeVolcanicAshConcentration;
        public DateTimeOffset IssueTimeUtc { get; }
        public ReportStatus ReportStatus { get; }
        public PermissibleUsage PermissibleUsage { get; }
        public PermissibleUsageReason? PermissibleUsageReason { get; }
        public string? PermissibleUsageSupplementary { get; }
        public string OriginatingCentre { get; }
        public string? OriginatingCentreDesignator { get; }
        public DateTimeOffset? DispersionModelInitialisationTimeUtc { get; }
        public string? Remarks { get; }
        public bool IsTranslationFailed { get; }
        public string? TranslationFailedTac { get; }

        /// <inheritdoc />
        public TResult Accept<TResult>(IIwxxmReportVisitor<TResult> visitor) =>
            (visitor ?? throw new ArgumentNullException(nameof(visitor))).Visit(this);

        public IReadOnlyList<Volcano> Volcanoes { get; }
        public IReadOnlyList<VolcanicAshConcentrationCloud> AshConcentrationClouds { get; }

        private QvaciReport(
            string id,
            DateTimeOffset issueTimeUtc,
            ReportStatus reportStatus,
            PermissibleUsage permissibleUsage,
            PermissibleUsageReason? permissibleUsageReason,
            string? permissibleUsageSupplementary,
            string originatingCentre,
            string? originatingCentreDesignator,
            DateTimeOffset? dispersionModelInitialisationTimeUtc,
            string? remarks,
            bool isTranslationFailed,
            string? translationFailedTac,
            IReadOnlyList<Volcano> volcanoes,
            IReadOnlyList<VolcanicAshConcentrationCloud> ashConcentrationClouds)
        {
            Id = id;
            IssueTimeUtc = issueTimeUtc;
            ReportStatus = reportStatus;
            PermissibleUsage = permissibleUsage;
            PermissibleUsageReason = permissibleUsageReason;
            PermissibleUsageSupplementary = permissibleUsageSupplementary;
            OriginatingCentre = originatingCentre;
            OriginatingCentreDesignator = originatingCentreDesignator;
            DispersionModelInitialisationTimeUtc = dispersionModelInitialisationTimeUtc;
            Remarks = remarks;
            IsTranslationFailed = isTranslationFailed;
            TranslationFailedTac = translationFailedTac;
            Volcanoes = volcanoes;
            AshConcentrationClouds = ashConcentrationClouds;
        }

        public static Result<QvaciReport> Create(
            string id,
            DateTimeOffset issueTimeUtc,
            ReportStatus reportStatus,
            PermissibleUsage permissibleUsage,
            string originatingCentre,
            string? originatingCentreDesignator = null,
            DateTimeOffset? dispersionModelInitialisationTimeUtc = null,
            string? remarks = null,
            bool isTranslationFailed = false,
            string? translationFailedTac = null,
            IEnumerable<Volcano>? volcanoes = null,
            IEnumerable<VolcanicAshConcentrationCloud>? ashConcentrationClouds = null,
            PermissibleUsageReason? permissibleUsageReason = null,
            string? permissibleUsageSupplementary = null)
        {
            var invariants = new DomainInvariants(id);
            invariants.RequireText(id, "DOMAIN-QVACI-ID", "The QVACI report gml:id identifier is required.");
            invariants.RequireMetadata(reportStatus, permissibleUsage, permissibleUsageReason);
            invariants.RequireIssueTime(issueTimeUtc);
            invariants.RequireUtc(dispersionModelInitialisationTimeUtc, "dispersion model initialisation time");

            if (invariants.HasErrors)
            {
                return Result<QvaciReport>.Failure(invariants.Errors);
            }

            return Result<QvaciReport>.Success(new QvaciReport(
                id,
                issueTimeUtc,
                reportStatus,
                permissibleUsage,
                permissibleUsageReason,
                permissibleUsageSupplementary,
                originatingCentre ?? string.Empty,
                originatingCentreDesignator,
                dispersionModelInitialisationTimeUtc,
                remarks,
                isTranslationFailed,
                translationFailedTac,
                new ReadOnlyCollection<Volcano>(volcanoes?.ToList() ?? new List<Volcano>(0)),
                new ReadOnlyCollection<VolcanicAshConcentrationCloud>(ashConcentrationClouds?.ToList() ?? new List<VolcanicAshConcentrationCloud>(0))));
        }
    }

    /// <summary>
    /// Domain Aggregate Root representing a Volcanic Ash Advisory (<c>VolcanicAshAdvisory</c>)
    /// under ICAO/WMO IWXXM 2025-2.
    /// </summary>
    public sealed class VolcanicAshAdvisoryReport : IIwxxmReport
    {
        public string Id { get; }
        public IwxxmReportType ReportType => IwxxmReportType.VolcanicAshAdvisory;
        public DateTimeOffset IssueTimeUtc { get; }
        public ReportStatus ReportStatus { get; }
        public PermissibleUsage PermissibleUsage { get; }
        public PermissibleUsageReason? PermissibleUsageReason { get; }
        public string? PermissibleUsageSupplementary { get; }
        public string IssuingVaacName { get; }
        public Volcano? Volcano { get; }
        public string? StateOrRegion { get; }
        public Length? SourceElevation { get; }
        public string? AdvisoryNumber { get; }
        public string? InformationSource { get; }
        public string? EruptionDetails { get; }
        public string? Remarks { get; }
        public DateTimeOffset? NextAdvisoryTimeUtc { get; }
        public bool IsTranslationFailed { get; }
        public string? TranslationFailedTac { get; }

        /// <inheritdoc />
        public TResult Accept<TResult>(IIwxxmReportVisitor<TResult> visitor) =>
            (visitor ?? throw new ArgumentNullException(nameof(visitor))).Visit(this);

        public IReadOnlyList<EnRoutePhenomenonVolume> ObservedAshClouds { get; }
        public IReadOnlyList<EnRoutePhenomenonVolume> ForecastAshClouds { get; }

        private VolcanicAshAdvisoryReport(
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
            IReadOnlyList<EnRoutePhenomenonVolume> observedAshClouds,
            IReadOnlyList<EnRoutePhenomenonVolume> forecastAshClouds)
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
            ObservedAshClouds = observedAshClouds;
            ForecastAshClouds = forecastAshClouds;
        }

        public static Result<VolcanicAshAdvisoryReport> Create(
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
            IEnumerable<EnRoutePhenomenonVolume>? observedAshClouds = null,
            IEnumerable<EnRoutePhenomenonVolume>? forecastAshClouds = null,
            PermissibleUsageReason? permissibleUsageReason = null,
            string? permissibleUsageSupplementary = null)
        {
            var invariants = new DomainInvariants(id);
            invariants.RequireText(id, "DOMAIN-VAA-ID", "The VolcanicAshAdvisory report gml:id identifier is required.");
            invariants.RequireMetadata(reportStatus, permissibleUsage, permissibleUsageReason);
            invariants.RequireIssueTime(issueTimeUtc);
            invariants.RequireUtc(nextAdvisoryTimeUtc, "next advisory time");
            foreach (EnRoutePhenomenonVolume cloud in (observedAshClouds ?? Enumerable.Empty<EnRoutePhenomenonVolume>()).Concat(forecastAshClouds ?? Enumerable.Empty<EnRoutePhenomenonVolume>()))
            {
                invariants.Check(cloud);
            }

            if (invariants.HasErrors)
            {
                return Result<VolcanicAshAdvisoryReport>.Failure(invariants.Errors);
            }

            return Result<VolcanicAshAdvisoryReport>.Success(new VolcanicAshAdvisoryReport(
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
                new ReadOnlyCollection<EnRoutePhenomenonVolume>(observedAshClouds?.ToList() ?? new List<EnRoutePhenomenonVolume>(0)),
                new ReadOnlyCollection<EnRoutePhenomenonVolume>(forecastAshClouds?.ToList() ?? new List<EnRoutePhenomenonVolume>(0))));
        }
    }

    /// <summary>
    /// Immutable Value Object representing an observed or forecast tropical cyclone position and intensity.
    /// </summary>
    public sealed class TropicalCycloneTrackPoint
    {
        public DateTimeOffset? PhenomenonTimeUtc { get; }
        public GeoCoordinate Position { get; }
        public Speed? MaxSurfaceWindSpeed { get; }
        public double? CentralPressureHpa { get; }

        public TropicalCycloneTrackPoint(
            DateTimeOffset? phenomenonTimeUtc,
            GeoCoordinate position,
            Speed? maxSurfaceWindSpeed,
            double? centralPressureHpa = null)
        {
            PhenomenonTimeUtc = phenomenonTimeUtc;
            Position = position;
            MaxSurfaceWindSpeed = maxSurfaceWindSpeed;
            CentralPressureHpa = centralPressureHpa;
        }
    }

    /// <summary>
    /// Domain Aggregate Root representing a Tropical Cyclone Advisory (<c>TropicalCycloneAdvisory</c>)
    /// under ICAO/WMO IWXXM 2025-2.
    /// </summary>
    public sealed class TropicalCycloneAdvisoryReport : IIwxxmReport
    {
        public string Id { get; }
        public IwxxmReportType ReportType => IwxxmReportType.TropicalCycloneAdvisory;
        public DateTimeOffset IssueTimeUtc { get; }
        public ReportStatus ReportStatus { get; }
        public PermissibleUsage PermissibleUsage { get; }
        public PermissibleUsageReason? PermissibleUsageReason { get; }
        public string? PermissibleUsageSupplementary { get; }
        public string IssuingTcacDesignator { get; }
        public string? CycloneName { get; }
        public string? AdvisoryNumber { get; }
        public TropicalCycloneTrackPoint? Observation { get; }
        public IReadOnlyList<TropicalCycloneTrackPoint> Forecasts { get; }
        public string? Remarks { get; }
        public DateTimeOffset? NextAdvisoryTimeUtc { get; }
        public bool IsTranslationFailed { get; }
        public string? TranslationFailedTac { get; }

        /// <inheritdoc />
        public TResult Accept<TResult>(IIwxxmReportVisitor<TResult> visitor) =>
            (visitor ?? throw new ArgumentNullException(nameof(visitor))).Visit(this);

        private TropicalCycloneAdvisoryReport(
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
            string? translationFailedTac)
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
        }

        public static Result<TropicalCycloneAdvisoryReport> Create(
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
            string? permissibleUsageSupplementary = null)
        {
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

            if (invariants.HasErrors)
            {
                return Result<TropicalCycloneAdvisoryReport>.Failure(invariants.Errors);
            }

            return Result<TropicalCycloneAdvisoryReport>.Success(new TropicalCycloneAdvisoryReport(
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
                translationFailedTac));
        }
    }

    /// <summary>
    /// Immutable Value Object representing an analyzed or forecast Space Weather intensity and geographic region.
    /// </summary>
    public sealed class SpaceWeatherAnalysisRegion
    {
        public string Id { get; }
        public string? TimeIndicator { get; }
        public DateTimeOffset? PhenomenonTimeUtc { get; }
        public string? Intensity { get; }
        public IReadOnlyList<string> LocationIndicatorUris { get; }
        public IReadOnlyList<GeoPolygon> Polygons { get; }

        public SpaceWeatherAnalysisRegion(
            string id,
            string? timeIndicator,
            DateTimeOffset? phenomenonTimeUtc,
            string? intensity,
            IEnumerable<string>? locationIndicatorUris = null,
            IEnumerable<GeoPolygon>? polygons = null)
        {
            Id = id ?? string.Empty;
            TimeIndicator = timeIndicator;
            PhenomenonTimeUtc = phenomenonTimeUtc;
            Intensity = intensity;
            LocationIndicatorUris = new ReadOnlyCollection<string>(locationIndicatorUris?.ToList() ?? new List<string>(0));
            Polygons = new ReadOnlyCollection<GeoPolygon>(polygons?.ToList() ?? new List<GeoPolygon>(0));
        }
    }

    /// <summary>
    /// Domain Aggregate Root representing a Space Weather Advisory (<c>SpaceWeatherAdvisory</c>)
    /// under ICAO/WMO IWXXM 2025-2.
    /// </summary>
    public sealed class SpaceWeatherAdvisoryReport : IIwxxmReport
    {
        public string Id { get; }
        public IwxxmReportType ReportType => IwxxmReportType.SpaceWeatherAdvisory;
        public DateTimeOffset IssueTimeUtc { get; }
        public ReportStatus ReportStatus { get; }
        public PermissibleUsage PermissibleUsage { get; }
        public PermissibleUsageReason? PermissibleUsageReason { get; }
        public string? PermissibleUsageSupplementary { get; }
        public string IssuingSwxcName { get; }
        public string? AdvisoryNumber { get; }
        public IReadOnlyList<string> ReplacedAdvisoryNumbers { get; }
        public IReadOnlyList<string> Effects { get; }
        public IReadOnlyList<SpaceWeatherAnalysisRegion> Analyses { get; }
        public string? Remarks { get; }
        public DateTimeOffset? NextAdvisoryTimeUtc { get; }
        public bool IsTranslationFailed { get; }
        public string? TranslationFailedTac { get; }

        /// <inheritdoc />
        public TResult Accept<TResult>(IIwxxmReportVisitor<TResult> visitor) =>
            (visitor ?? throw new ArgumentNullException(nameof(visitor))).Visit(this);

        private SpaceWeatherAdvisoryReport(
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
            IReadOnlyList<SpaceWeatherAnalysisRegion> analyses,
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

        public static Result<SpaceWeatherAdvisoryReport> Create(
            string id,
            DateTimeOffset issueTimeUtc,
            ReportStatus reportStatus,
            PermissibleUsage permissibleUsage,
            string issuingSwxcName,
            string? advisoryNumber = null,
            IEnumerable<string>? replacedAdvisoryNumbers = null,
            IEnumerable<string>? effects = null,
            IEnumerable<SpaceWeatherAnalysisRegion>? analyses = null,
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
            foreach (SpaceWeatherAnalysisRegion analysis in analyses ?? Enumerable.Empty<SpaceWeatherAnalysisRegion>())
            {
                invariants.RequireUtc(analysis?.PhenomenonTimeUtc, "space weather analysis time");
            }

            if (invariants.HasErrors)
            {
                return Result<SpaceWeatherAdvisoryReport>.Failure(invariants.Errors);
            }

            return Result<SpaceWeatherAdvisoryReport>.Success(new SpaceWeatherAdvisoryReport(
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
                new ReadOnlyCollection<SpaceWeatherAnalysisRegion>(analyses?.ToList() ?? new List<SpaceWeatherAnalysisRegion>(0)),
                remarks,
                nextAdvisoryTimeUtc,
                isTranslationFailed,
                translationFailedTac));
        }
    }
}
