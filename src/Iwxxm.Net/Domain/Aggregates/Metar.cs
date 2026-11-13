using System;
using System.Collections.Generic;

namespace Iwxxm.Net.Reports
{
    /// <summary>
    /// Domain Aggregate Root representing an Aerodrome Routine Meteorological Report (<c>METAR</c>, root element <c>iwxxm:METAR</c>) under ICAO/WMO IWXXM 2025-2.
    /// The content and invariants are shared with the other aerodrome observation through <see cref="AerodromeObservation"/>.
    /// </summary>
    public sealed class Metar : AerodromeObservation
    {
        private Metar(Content content)
            : base(content)
        {
        }

        /// <inheritdoc />
        public override IwxxmReportType ReportType => IwxxmReportType.Metar;

        /// <inheritdoc />
        public override TResult Accept<TResult>(IIwxxmDocumentVisitor<TResult> visitor) =>
            (visitor ?? throw new ArgumentNullException(nameof(visitor))).Visit(this);

        /// <summary>Creates a METAR report, enforcing the aerodrome observation invariants.</summary>
        public static Result<Metar> Create(
            string id,
            DateTimeOffset issueTimeUtc,
            DateTimeOffset? observationTimeUtc,
            ReportStatus reportStatus,
            PermissibleUsage permissibleUsage,
            bool isAutomatedStation,
            string aerodromeDesignator,
            string? aerodromeName,
            GeoCoordinate? aerodromePosition,
            bool isNilObservation = false,
            CodeListValue<NilReason>? observationNilReason = null,
            bool isTranslationFailed = false,
            string? translationFailedTac = null,
            bool cloudAndVisibilityOk = false,
            double? airTemperatureCelsius = null,
            double? dewpointTemperatureCelsius = null,
            double? qnhHpa = null,
            SurfaceWind? surfaceWind = null,
            AerodromeHorizontalVisibility? visibility = null,
            IEnumerable<RunwayVisualRange>? runwayVisualRanges = null,
            IEnumerable<AerodromeWeather>? presentWeather = null,
            CodeListValue<NilReason>? presentWeatherNilReason = null,
            AerodromeCloud? cloud = null,
            IEnumerable<AerodromeForecastGroup>? trendForecasts = null,
            PermissibleUsageReason? permissibleUsageReason = null,
            string? permissibleUsageSupplementary = null) =>
            Create(
                content => new Metar(content),
                id,
                issueTimeUtc,
                observationTimeUtc,
                reportStatus,
                permissibleUsage,
                isAutomatedStation,
                aerodromeDesignator,
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
                visibility,
                runwayVisualRanges,
                presentWeather,
                presentWeatherNilReason,
                cloud,
                trendForecasts,
                permissibleUsageReason,
                permissibleUsageSupplementary);
    }
}
