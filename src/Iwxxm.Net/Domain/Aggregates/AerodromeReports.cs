using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Iwxxm.Net.Reports
{
    /// <summary>
    /// Immutable Value Object representing surface wind conditions at an aerodrome.
    /// Wind speeds keep the unit reported in the document (<c>m/s</c> or <c>[kn_i]</c>);
    /// use <see cref="Speed.ToMetersPerSecond"/> / <see cref="Speed.ToKnots"/> to normalize.
    /// </summary>
    public sealed class SurfaceWindInfo
    {
        public bool IsVariableDirection { get; }
        public double? MeanDirectionDegrees { get; }
        public Speed? MeanSpeed { get; }
        public Speed? GustSpeed { get; }

        public SurfaceWindInfo(
            bool isVariableDirection,
            double? meanDirectionDegrees,
            Speed? meanSpeed,
            Speed? gustSpeed = null)
        {
            IsVariableDirection = isVariableDirection;
            MeanDirectionDegrees = meanDirectionDegrees;
            MeanSpeed = meanSpeed;
            GustSpeed = gustSpeed;
        }
    }

    /// <summary>
    /// Immutable Value Object representing Runway Visual Range (RVR) reported for a specific runway direction.
    /// </summary>
    public sealed class RunwayVisualRangeInfo
    {
        public string RunwayDesignator { get; }
        public double? MeanRvrMeters { get; }
        public string? PastTendency { get; }

        public RunwayVisualRangeInfo(string runwayDesignator, double? meanRvrMeters, string? pastTendency = null)
        {
            RunwayDesignator = runwayDesignator ?? string.Empty;
            MeanRvrMeters = meanRvrMeters;
            PastTendency = pastTendency;
        }
    }

    /// <summary>
    /// Immutable Value Object representing an aerodrome cloud layer (amount URI, base height as reported in
    /// <c>m</c> or <c>[ft_i]</c>, and optional convective cloud type URI).
    /// </summary>
    public sealed class AerodromeCloudLayerInfo
    {
        public string? AmountUri { get; }
        public Length? Base { get; }
        public string? CloudTypeUri { get; }

        public AerodromeCloudLayerInfo(string? amountUri, Length? @base, string? cloudTypeUri = null)
        {
            AmountUri = amountUri;
            Base = @base;
            CloudTypeUri = cloudTypeUri;
        }
    }

    /// <summary>
    /// Immutable Value Object representing a trend or change forecast group (e.g., BECOMING, TEMPORARY_FLUCTUATIONS, FROM).
    /// </summary>
    public sealed class AerodromeForecastGroupInfo
    {
        public string Id { get; }
        public string? ChangeIndicator { get; }
        public bool CloudAndVisibilityOk { get; }
        public DateTimeOffset? ValidFromUtc { get; }
        public DateTimeOffset? ValidUntilUtc { get; }
        public double? PrevailingVisibilityMeters { get; }
        public string? PrevailingVisibilityOperator { get; }
        public SurfaceWindInfo? SurfaceWind { get; }
        public IReadOnlyList<string> WeatherUris { get; }
        public IReadOnlyList<AerodromeCloudLayerInfo> CloudLayers { get; }

        public AerodromeForecastGroupInfo(
            string id,
            string? changeIndicator,
            bool cloudAndVisibilityOk,
            DateTimeOffset? validFromUtc,
            DateTimeOffset? validUntilUtc,
            double? prevailingVisibilityMeters,
            string? prevailingVisibilityOperator,
            SurfaceWindInfo? surfaceWind,
            IEnumerable<string>? weatherUris = null,
            IEnumerable<AerodromeCloudLayerInfo>? cloudLayers = null)
        {
            Id = id ?? string.Empty;
            ChangeIndicator = changeIndicator;
            CloudAndVisibilityOk = cloudAndVisibilityOk;
            ValidFromUtc = validFromUtc;
            ValidUntilUtc = validUntilUtc;
            PrevailingVisibilityMeters = prevailingVisibilityMeters;
            PrevailingVisibilityOperator = prevailingVisibilityOperator;
            SurfaceWind = surfaceWind;
            WeatherUris = new ReadOnlyCollection<string>(weatherUris?.ToList() ?? new List<string>(0));
            CloudLayers = new ReadOnlyCollection<AerodromeCloudLayerInfo>(cloudLayers?.ToList() ?? new List<AerodromeCloudLayerInfo>(0));
        }
    }

    /// <summary>
    /// Domain Aggregate Root representing an Aerodrome Routine (<c>METAR</c>) or Special (<c>SPECI</c>)
    /// Meteorological Report under ICAO/WMO IWXXM 2025-2.
    /// </summary>
    public sealed class AerodromeWeatherReport : IIwxxmReport
    {
        public string Id { get; }
        public IwxxmReportType ReportType { get; }
        public DateTimeOffset IssueTimeUtc { get; }
        public DateTimeOffset ObservationTimeUtc { get; }
        public string ReportStatus { get; }
        public string PermissibleUsage { get; }
        public bool IsAutomatedStation { get; }
        public string AerodromeDesignator { get; }
        public string? AerodromeName { get; }
        public GeoCoordinate? AerodromePosition { get; }
        public bool IsNilObservation { get; }
        public string? ObservationNilReason { get; }
        public bool IsTranslationFailed { get; }
        public string? TranslationFailedTac { get; }
        public bool CloudAndVisibilityOk { get; }
        public double? AirTemperatureCelsius { get; }
        public double? DewpointTemperatureCelsius { get; }
        public double? QnhHpa { get; }
        public SurfaceWindInfo? SurfaceWind { get; }
        public double? PrevailingVisibilityMeters { get; }
        public IReadOnlyList<RunwayVisualRangeInfo> RunwayVisualRanges { get; }
        public IReadOnlyList<string> PresentWeatherUris { get; }
        public IReadOnlyList<AerodromeCloudLayerInfo> CloudLayers { get; }
        public IReadOnlyList<AerodromeForecastGroupInfo> TrendForecasts { get; }

        private AerodromeWeatherReport(
            string id,
            IwxxmReportType reportType,
            DateTimeOffset issueTimeUtc,
            DateTimeOffset observationTimeUtc,
            string reportStatus,
            string permissibleUsage,
            bool isAutomatedStation,
            string aerodromeDesignator,
            string? aerodromeName,
            GeoCoordinate? aerodromePosition,
            bool isNilObservation,
            string? observationNilReason,
            bool isTranslationFailed,
            string? translationFailedTac,
            bool cloudAndVisibilityOk,
            double? airTemperatureCelsius,
            double? dewpointTemperatureCelsius,
            double? qnhHpa,
            SurfaceWindInfo? surfaceWind,
            double? prevailingVisibilityMeters,
            IReadOnlyList<RunwayVisualRangeInfo> runwayVisualRanges,
            IReadOnlyList<string> presentWeatherUris,
            IReadOnlyList<AerodromeCloudLayerInfo> cloudLayers,
            IReadOnlyList<AerodromeForecastGroupInfo> trendForecasts)
        {
            Id = id;
            ReportType = reportType;
            IssueTimeUtc = issueTimeUtc;
            ObservationTimeUtc = observationTimeUtc;
            ReportStatus = reportStatus;
            PermissibleUsage = permissibleUsage;
            IsAutomatedStation = isAutomatedStation;
            AerodromeDesignator = aerodromeDesignator;
            AerodromeName = aerodromeName;
            AerodromePosition = aerodromePosition;
            IsNilObservation = isNilObservation;
            ObservationNilReason = observationNilReason;
            IsTranslationFailed = isTranslationFailed;
            TranslationFailedTac = translationFailedTac;
            CloudAndVisibilityOk = cloudAndVisibilityOk;
            AirTemperatureCelsius = airTemperatureCelsius;
            DewpointTemperatureCelsius = dewpointTemperatureCelsius;
            QnhHpa = qnhHpa;
            SurfaceWind = surfaceWind;
            PrevailingVisibilityMeters = prevailingVisibilityMeters;
            RunwayVisualRanges = runwayVisualRanges;
            PresentWeatherUris = presentWeatherUris;
            CloudLayers = cloudLayers;
            TrendForecasts = trendForecasts;
        }

        public static Result<AerodromeWeatherReport> Create(
            string id,
            IwxxmReportType reportType,
            DateTimeOffset issueTimeUtc,
            DateTimeOffset observationTimeUtc,
            string? reportStatus,
            string? permissibleUsage,
            bool isAutomatedStation,
            string aerodromeDesignator,
            string? aerodromeName,
            GeoCoordinate? aerodromePosition,
            bool isNilObservation = false,
            string? observationNilReason = null,
            bool isTranslationFailed = false,
            string? translationFailedTac = null,
            bool cloudAndVisibilityOk = false,
            double? airTemperatureCelsius = null,
            double? dewpointTemperatureCelsius = null,
            double? qnhHpa = null,
            SurfaceWindInfo? surfaceWind = null,
            double? prevailingVisibilityMeters = null,
            IEnumerable<RunwayVisualRangeInfo>? runwayVisualRanges = null,
            IEnumerable<string>? presentWeatherUris = null,
            IEnumerable<AerodromeCloudLayerInfo>? cloudLayers = null,
            IEnumerable<AerodromeForecastGroupInfo>? trendForecasts = null)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return Result<AerodromeWeatherReport>.Failure(
                    "DOMAIN-METAR-ID",
                    "The METAR/SPECI report gml:id identifier is required.",
                    ErrorCategory.DomainMapping);
            }

            if (string.IsNullOrWhiteSpace(aerodromeDesignator))
            {
                return Result<AerodromeWeatherReport>.Failure(
                    "DOMAIN-METAR-AERODROME",
                    "The aerodrome ICAO designator is required.",
                    ErrorCategory.DomainMapping,
                    id);
            }

            return Result<AerodromeWeatherReport>.Success(new AerodromeWeatherReport(
                id,
                reportType,
                issueTimeUtc,
                observationTimeUtc,
                string.IsNullOrWhiteSpace(reportStatus) ? "NORMAL" : reportStatus!,
                string.IsNullOrWhiteSpace(permissibleUsage) ? "OPERATIONAL" : permissibleUsage!,
                isAutomatedStation,
                aerodromeDesignator.Trim(),
                aerodromeName,
                aerodromePosition,
                isNilObservation,
                observationNilReason,
                isTranslationFailed,
                translationFailedTac,
                cloudAndVisibilityOk,
                airTemperatureCelsius,
                dewpointTemperatureCelsius,
                qnhHpa,
                surfaceWind,
                prevailingVisibilityMeters,
                new ReadOnlyCollection<RunwayVisualRangeInfo>(runwayVisualRanges?.ToList() ?? new List<RunwayVisualRangeInfo>(0)),
                new ReadOnlyCollection<string>(presentWeatherUris?.ToList() ?? new List<string>(0)),
                new ReadOnlyCollection<AerodromeCloudLayerInfo>(cloudLayers?.ToList() ?? new List<AerodromeCloudLayerInfo>(0)),
                new ReadOnlyCollection<AerodromeForecastGroupInfo>(trendForecasts?.ToList() ?? new List<AerodromeForecastGroupInfo>(0))));
        }
    }

    /// <summary>
    /// Domain Aggregate Root representing a Terminal Aerodrome Forecast (<c>TAF</c>)
    /// under ICAO/WMO IWXXM 2025-2.
    /// </summary>
    public sealed class TafReport : IIwxxmReport
    {
        public string Id { get; }
        public IwxxmReportType ReportType => IwxxmReportType.Taf;
        public DateTimeOffset IssueTimeUtc { get; }
        public string ReportStatus { get; }
        public string PermissibleUsage { get; }
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
        public bool IsTranslationFailed { get; }
        public string? TranslationFailedTac { get; }
        public AerodromeForecastGroupInfo? BaseForecast { get; }
        public IReadOnlyList<AerodromeForecastGroupInfo> ChangeForecasts { get; }

        private TafReport(
            string id,
            DateTimeOffset issueTimeUtc,
            string reportStatus,
            string permissibleUsage,
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
            AerodromeForecastGroupInfo? baseForecast,
            IReadOnlyList<AerodromeForecastGroupInfo> changeForecasts)
        {
            Id = id;
            IssueTimeUtc = issueTimeUtc;
            ReportStatus = reportStatus;
            PermissibleUsage = permissibleUsage;
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

        public static Result<TafReport> Create(
            string id,
            DateTimeOffset issueTimeUtc,
            string? reportStatus,
            string? permissibleUsage,
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
            AerodromeForecastGroupInfo? baseForecast = null,
            IEnumerable<AerodromeForecastGroupInfo>? changeForecasts = null)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return Result<TafReport>.Failure(
                    "DOMAIN-TAF-ID",
                    "The TAF report gml:id identifier is required.",
                    ErrorCategory.DomainMapping);
            }

            if (string.IsNullOrWhiteSpace(aerodromeDesignator))
            {
                return Result<TafReport>.Failure(
                    "DOMAIN-TAF-AERODROME",
                    "The aerodrome ICAO designator is required.",
                    ErrorCategory.DomainMapping,
                    id);
            }

            return Result<TafReport>.Success(new TafReport(
                id,
                issueTimeUtc,
                string.IsNullOrWhiteSpace(reportStatus) ? "NORMAL" : reportStatus!,
                string.IsNullOrWhiteSpace(permissibleUsage) ? "OPERATIONAL" : permissibleUsage!,
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
                new ReadOnlyCollection<AerodromeForecastGroupInfo>(changeForecasts?.ToList() ?? new List<AerodromeForecastGroupInfo>(0))));
        }
    }
}
