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
            Visibility = content.Visibility;
            RunwayVisualRanges = content.RunwayVisualRanges;
            PresentWeather = content.PresentWeather;
            PresentWeatherNilReason = content.PresentWeatherNilReason;
            Cloud = content.Cloud;
            TrendForecasts = content.TrendForecasts;
        }

        /// <inheritdoc />
        public sealed override string Id { get; }
        /// <inheritdoc />
        public sealed override DateTimeOffset IssueTimeUtc { get; }

        /// <summary>
        /// Observation time (<c>observationTime</c>, mandatory in <c>metarSpeci.xsd</c>); <c>null</c> when the document
        /// gives it only by an <c>xlink:href</c> reference, which this version does not resolve. Never defaulted to the issue time.
        /// </summary>
        public DateTimeOffset? ObservationTimeUtc { get; }
        /// <inheritdoc />
        public sealed override ReportStatus ReportStatus { get; }
        /// <inheritdoc />
        public sealed override PermissibleUsage PermissibleUsage { get; }
        /// <inheritdoc />
        public sealed override PermissibleUsageReason? PermissibleUsageReason { get; }
        /// <inheritdoc />
        public sealed override string? PermissibleUsageSupplementary { get; }
        /// <summary>Gets a value indicating whether the report comes from an automated observing station (<c>automatedStation="true"</c>); <c>false</c> when the attribute is false or absent.</summary>
        public bool IsAutomatedStation { get; }
        /// <summary>Gets the ICAO location indicator of the aerodrome (<c>aixm:designator</c> of the <c>iwxxm:aerodrome</c> feature, falling back to <c>aixm:locationIndicatorICAO</c>), trimmed and never blank.</summary>
        public string AerodromeDesignator { get; }
        /// <summary>Gets the aerodrome name (<c>aixm:name</c>), or <c>null</c> when the document does not carry it.</summary>
        public string? AerodromeName { get; }
        /// <summary>Gets the aerodrome reference point (<c>aixm:ARP</c> of the <c>iwxxm:aerodrome</c> feature), or <c>null</c> when the document does not carry it.</summary>
        public GeoCoordinate? AerodromePosition { get; }
        /// <summary>Gets a value indicating whether <c>iwxxm:observation</c> is nil (a missing report): the element carries a <c>nilReason</c> and no observed conditions, see <see cref="ObservationNilReason"/>.</summary>
        public bool IsNilObservation { get; }
        /// <summary>Gets the <c>nilReason</c> of a nil <c>iwxxm:observation</c> as a code of the WMO nil-reason register <c>http://codes.wmo.int/common/nil</c> (typically <see cref="NilReason.Missing"/>: the whole report is missing), or <c>null</c> when conditions are reported. A reason outside the official register is kept as <c>Unknown</c> with its raw value in <see cref="CodeListValue{TCode}.Notation"/>.</summary>
        public CodeListValue<NilReason>? ObservationNilReason { get; }
        /// <inheritdoc />
        public sealed override bool IsTranslationFailed { get; }
        /// <inheritdoc />
        public sealed override string? TranslationFailedTac { get; }
        /// <summary>
        /// Gets a value indicating whether the observation is <c>CAVOK</c> (<c>cloudAndVisibilityOK="true"</c>): visibility 10 km or more, no cloud of operational significance and no weather of significance to aviation.
        /// In that case visibility, RVR, present weather and cloud are not reported (ICAO PANS-MET, Doc 10157, Section 2.2).
        /// </summary>
        public bool CloudAndVisibilityOk { get; }
        /// <summary>Gets the observed air temperature in degrees Celsius (<c>iwxxm:airTemperature</c>, UCUM <c>Cel</c>), or <c>null</c> when not reported.</summary>
        public double? AirTemperatureCelsius { get; }
        /// <summary>Gets the observed dew-point temperature in degrees Celsius (<c>iwxxm:dewpointTemperature</c>, UCUM <c>Cel</c>), or <c>null</c> when not reported.</summary>
        public double? DewpointTemperatureCelsius { get; }
        /// <summary>Gets the observed QNH altimeter setting (barometric pressure adjusted to sea level) in hectopascals (<c>iwxxm:qnh</c>, UCUM <c>hPa</c>), or <c>null</c> when not reported.</summary>
        public double? QnhHpa { get; }
        /// <summary>Gets the representative surface wind (<c>iwxxm:surfaceWind</c>), or <c>null</c> when not reported.</summary>
        public SurfaceWind? SurfaceWind { get; }

        /// <summary>Horizontal visibility (prevailing with operator, minimum with direction); <c>null</c> when not reported.</summary>
        public AerodromeHorizontalVisibility? Visibility { get; }

        /// <summary>Prevailing visibility in metres (<see cref="AerodromeHorizontalVisibility.PrevailingMeters"/> of <see cref="Visibility"/>).</summary>
        public double? PrevailingVisibilityMeters => Visibility?.PrevailingMeters;

        /// <summary>Gets the runway visual range observations (<c>iwxxm:rvr</c>), one per runway, in document order; empty when none is reported. The schema requires them when visibility or RVR is below 1500 m.</summary>
        public IReadOnlyList<RunwayVisualRange> RunwayVisualRanges { get; }

        /// <summary>Present weather codes (WMO code list <c>AerodromePresentOrForecastWeather</c>), in document order.</summary>
        public IReadOnlyList<AerodromeWeather> PresentWeather { get; }

        /// <summary>
        /// Reason why present weather is nil (e.g. <see cref="NilReason.NotObservable"/> for TAC <c>//</c>); <c>null</c> when
        /// weather codes are given or present weather is not reported.
        /// </summary>
        public CodeListValue<NilReason>? PresentWeatherNilReason { get; }

        /// <summary>Cloud group (layers, vertical visibility, <c>NSC</c>/<c>NCD</c>); <c>null</c> when not reported.</summary>
        public AerodromeCloud? Cloud { get; }

        /// <summary>Cloud layers of <see cref="Cloud"/> (empty when no cloud group is reported).</summary>
        public IReadOnlyList<AerodromeCloudLayer> CloudLayers => Cloud?.Layers ?? Array.Empty<AerodromeCloudLayer>();
        /// <summary>Gets the trend forecasts appended to the observation (<c>iwxxm:trendForecast</c>, the TAC <c>BECMG</c>/<c>TEMPO</c> trend groups), in document order; empty when none is reported. A nil <c>NOSIG</c> trend yields no group.</summary>
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
            CodeListValue<NilReason>? observationNilReason,
            bool isTranslationFailed,
            string? translationFailedTac,
            bool cloudAndVisibilityOk,
            double? airTemperatureCelsius,
            double? dewpointTemperatureCelsius,
            double? qnhHpa,
            SurfaceWind? surfaceWind,
            AerodromeHorizontalVisibility? visibility,
            IEnumerable<RunwayVisualRange>? runwayVisualRanges,
            IEnumerable<AerodromeWeather>? presentWeather,
            CodeListValue<NilReason>? presentWeatherNilReason,
            AerodromeCloud? cloud,
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
            invariants.Check(visibility);
            invariants.Check(cloud, "cloud");
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
                Visibility = visibility,
                RunwayVisualRanges = new ReadOnlyCollection<RunwayVisualRange>(rvrList),
                PresentWeather = new ReadOnlyCollection<AerodromeWeather>(presentWeather?.ToList() ?? new List<AerodromeWeather>(0)),
                PresentWeatherNilReason = presentWeatherNilReason,
                Cloud = cloud,
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
            internal CodeListValue<NilReason>? ObservationNilReason;
            internal bool IsTranslationFailed;
            internal string? TranslationFailedTac;
            internal bool CloudAndVisibilityOk;
            internal double? AirTemperatureCelsius;
            internal double? DewpointTemperatureCelsius;
            internal double? QnhHpa;
            internal SurfaceWind? SurfaceWind;
            internal AerodromeHorizontalVisibility? Visibility;
            internal IReadOnlyList<RunwayVisualRange> RunwayVisualRanges = Array.Empty<RunwayVisualRange>();
            internal IReadOnlyList<AerodromeWeather> PresentWeather = Array.Empty<AerodromeWeather>();
            internal CodeListValue<NilReason>? PresentWeatherNilReason;
            internal AerodromeCloud? Cloud;
            internal IReadOnlyList<AerodromeForecastGroup> TrendForecasts = Array.Empty<AerodromeForecastGroup>();
        }
    }
}
