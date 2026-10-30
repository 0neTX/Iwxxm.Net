using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Iwxxm.Net.Reports
{
    /// <summary>
    /// Shared base of the aerodrome observation reports <see cref="Metar"/> (routine) and <see cref="Speci"/> (special)
    /// under ICAO/WMO IWXXM 2025-2.
    /// </summary>
    /// <remarks>
    /// IWXXM models both reports with one type (<c>MeteorologicalAerodromeObservationReportType</c>, <c>metarSpeci.xsd</c>);
    /// they differ only in the root element and the reason for issue. The content, invariants and factory logic therefore
    /// live here once, and the two sealed subtypes only fix the report kind. The constructor is not accessible outside this
    /// assembly, so the hierarchy stays closed.
    /// </remarks>
    public abstract class AerodromeObservation : IwxxmReport
    {
        private protected AerodromeObservation(Content content)
        {
            Id = content.Id;
            IssueTimeUtc = content.IssueTimeUtc;
            ObservationTimeUtc = content.ObservationTimeUtc;
            ReportStatus = content.ReportStatus;
            PermissibleUsage = content.PermissibleUsage;
            PermissibleUsageReason = content.PermissibleUsageReason;
            PermissibleUsageSupplementary = content.PermissibleUsageSupplementary;
            IsAutomatedStation = content.IsAutomatedStation;
            AerodromeDesignator = content.AerodromeDesignator;
            AerodromeName = content.AerodromeName;
            AerodromePosition = content.AerodromePosition;
            IsNilObservation = content.IsNilObservation;
            ObservationNilReason = content.ObservationNilReason;
            IsTranslationFailed = content.IsTranslationFailed;
            TranslationFailedTac = content.TranslationFailedTac;
            CloudAndVisibilityOk = content.CloudAndVisibilityOk;
            AirTemperatureCelsius = content.AirTemperatureCelsius;
            DewpointTemperatureCelsius = content.DewpointTemperatureCelsius;
            QnhHpa = content.QnhHpa;
            SurfaceWind = content.SurfaceWind;
            PrevailingVisibilityMeters = content.PrevailingVisibilityMeters;
            RunwayVisualRanges = content.RunwayVisualRanges;
            PresentWeather = content.PresentWeather;
            CloudLayers = content.CloudLayers;
            TrendForecasts = content.TrendForecasts;
        }

        public sealed override string Id { get; }
        public sealed override DateTimeOffset IssueTimeUtc { get; }

        /// <summary>
        /// Observation time (<c>observationTime</c>, mandatory in <c>metarSpeci.xsd</c>); <c>null</c> when the document
        /// gives it only by an <c>xlink:href</c> reference, which this version does not resolve. Never defaulted to the issue time.
        /// </summary>
        public DateTimeOffset? ObservationTimeUtc { get; }
        public sealed override ReportStatus ReportStatus { get; }
        public sealed override PermissibleUsage PermissibleUsage { get; }
        public sealed override PermissibleUsageReason? PermissibleUsageReason { get; }
        public sealed override string? PermissibleUsageSupplementary { get; }
        public bool IsAutomatedStation { get; }
        public string AerodromeDesignator { get; }
        public string? AerodromeName { get; }
        public GeoCoordinate? AerodromePosition { get; }
        public bool IsNilObservation { get; }
        public string? ObservationNilReason { get; }
        public sealed override bool IsTranslationFailed { get; }
        public sealed override string? TranslationFailedTac { get; }
        public bool CloudAndVisibilityOk { get; }
        public double? AirTemperatureCelsius { get; }
        public double? DewpointTemperatureCelsius { get; }
        public double? QnhHpa { get; }
        public SurfaceWind? SurfaceWind { get; }
        public double? PrevailingVisibilityMeters { get; }
        public IReadOnlyList<RunwayVisualRange> RunwayVisualRanges { get; }

        /// <summary>Present weather codes (WMO code list <c>AerodromePresentOrForecastWeather</c>), in document order.</summary>
        public IReadOnlyList<AerodromeWeather> PresentWeather { get; }
        public IReadOnlyList<AerodromeCloudLayer> CloudLayers { get; }
        public IReadOnlyList<AerodromeForecastGroup> TrendForecasts { get; }

        /// <summary>
        /// Shared factory of <see cref="Metar"/> and <see cref="Speci"/>: applies the invariants once and builds the
        /// concrete report through <paramref name="factory"/>.
        /// </summary>
        private protected static Result<TObservation> Create<TObservation>(
            Func<Content, TObservation> factory,
            string id,
            DateTimeOffset issueTimeUtc,
            DateTimeOffset? observationTimeUtc,
            ReportStatus reportStatus,
            PermissibleUsage permissibleUsage,
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
            SurfaceWind? surfaceWind,
            double? prevailingVisibilityMeters,
            IEnumerable<RunwayVisualRange>? runwayVisualRanges,
            IEnumerable<AerodromeWeather>? presentWeather,
            IEnumerable<AerodromeCloudLayer>? cloudLayers,
            IEnumerable<AerodromeForecastGroup>? trendForecasts,
            PermissibleUsageReason? permissibleUsageReason,
            string? permissibleUsageSupplementary)
            where TObservation : AerodromeObservation
        {
            var rvrList = runwayVisualRanges?.ToList() ?? new List<RunwayVisualRange>(0);
            var trendList = trendForecasts?.ToList() ?? new List<AerodromeForecastGroup>(0);

            var invariants = new DomainInvariants(id);
            invariants.RequireText(id, "DOMAIN-METAR-ID", "The METAR/SPECI report gml:id identifier is required.");
            invariants.RequireText(aerodromeDesignator, "DOMAIN-METAR-AERODROME", "The aerodrome ICAO designator is required.");
            invariants.RequireMetadata(reportStatus, permissibleUsage, permissibleUsageReason);
            invariants.RequireIssueTime(issueTimeUtc);
            invariants.RequireUtc(observationTimeUtc, "observation time");
            invariants.Check(surfaceWind, "surface wind");
            invariants.RequireNonNegative(prevailingVisibilityMeters, DomainErrorCodes.NegativeVisibility, "prevailing visibility");
            foreach (RunwayVisualRange rvr in rvrList)
            {
                invariants.RequireNonNegative(rvr?.MeanRvrMeters, DomainErrorCodes.NegativeRvr, $"mean RVR of runway '{rvr?.RunwayDesignator}'");
            }

            foreach (AerodromeForecastGroup trend in trendList)
            {
                invariants.Check(trend);
            }

            if (invariants.HasErrors)
            {
                return Result<TObservation>.Failure(invariants.Errors);
            }

            return Result<TObservation>.Success(factory(new Content
            {
                Id = id,
                IssueTimeUtc = issueTimeUtc,
                ObservationTimeUtc = observationTimeUtc,
                ReportStatus = reportStatus,
                PermissibleUsage = permissibleUsage,
                PermissibleUsageReason = permissibleUsageReason,
                PermissibleUsageSupplementary = permissibleUsageSupplementary,
                IsAutomatedStation = isAutomatedStation,
                AerodromeDesignator = aerodromeDesignator.Trim(),
                AerodromeName = aerodromeName,
                AerodromePosition = aerodromePosition,
                IsNilObservation = isNilObservation,
                ObservationNilReason = observationNilReason,
                IsTranslationFailed = isTranslationFailed,
                TranslationFailedTac = translationFailedTac,
                CloudAndVisibilityOk = cloudAndVisibilityOk,
                AirTemperatureCelsius = airTemperatureCelsius,
                DewpointTemperatureCelsius = dewpointTemperatureCelsius,
                QnhHpa = qnhHpa,
                SurfaceWind = surfaceWind,
                PrevailingVisibilityMeters = prevailingVisibilityMeters,
                RunwayVisualRanges = new ReadOnlyCollection<RunwayVisualRange>(rvrList),
                PresentWeather = new ReadOnlyCollection<AerodromeWeather>(presentWeather?.ToList() ?? new List<AerodromeWeather>(0)),
                CloudLayers = new ReadOnlyCollection<AerodromeCloudLayer>(cloudLayers?.ToList() ?? new List<AerodromeCloudLayer>(0)),
                TrendForecasts = new ReadOnlyCollection<AerodromeForecastGroup>(trendList)
            }));
        }

        /// <summary>Validated content handed from the shared factory to the concrete report constructors.</summary>
        private protected sealed class Content
        {
            internal string Id = string.Empty;
            internal DateTimeOffset IssueTimeUtc;
            internal DateTimeOffset? ObservationTimeUtc;
            internal ReportStatus ReportStatus;
            internal PermissibleUsage PermissibleUsage;
            internal PermissibleUsageReason? PermissibleUsageReason;
            internal string? PermissibleUsageSupplementary;
            internal bool IsAutomatedStation;
            internal string AerodromeDesignator = string.Empty;
            internal string? AerodromeName;
            internal GeoCoordinate? AerodromePosition;
            internal bool IsNilObservation;
            internal string? ObservationNilReason;
            internal bool IsTranslationFailed;
            internal string? TranslationFailedTac;
            internal bool CloudAndVisibilityOk;
            internal double? AirTemperatureCelsius;
            internal double? DewpointTemperatureCelsius;
            internal double? QnhHpa;
            internal SurfaceWind? SurfaceWind;
            internal double? PrevailingVisibilityMeters;
            internal IReadOnlyList<RunwayVisualRange> RunwayVisualRanges = Array.Empty<RunwayVisualRange>();
            internal IReadOnlyList<AerodromeWeather> PresentWeather = Array.Empty<AerodromeWeather>();
            internal IReadOnlyList<AerodromeCloudLayer> CloudLayers = Array.Empty<AerodromeCloudLayer>();
            internal IReadOnlyList<AerodromeForecastGroup> TrendForecasts = Array.Empty<AerodromeForecastGroup>();
        }
    }
}
