using System;
using System.Collections.Generic;

namespace Iwxxm.Net.Reports
{
    /// <summary>
    /// Domain Aggregate Root representing an Aerodrome Special Meteorological Report (<c>SPECI</c>, root element <c>iwxxm:SPECI</c>), issued on a significant change under ICAO/WMO IWXXM 2025-2.
    /// The content and invariants are shared with the other aerodrome observation through <see cref="AerodromeObservation"/>.
    /// </summary>
    public sealed class Speci : AerodromeObservation
    {
        private Speci(Content content)
            : base(content)
        {
        }

        /// <inheritdoc />
        public override IwxxmReportType ReportType => IwxxmReportType.Speci;

        /// <inheritdoc />
        public override TResult Accept<TResult>(IIwxxmDocumentVisitor<TResult> visitor) =>
            (visitor ?? throw new ArgumentNullException(nameof(visitor))).Visit(this);

        /// <summary>Creates a SPECI report, enforcing the aerodrome observation invariants.</summary>
        public static Result<Speci> Create(
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
            string? observationNilReason = null,
            bool isTranslationFailed = false,
            string? translationFailedTac = null,
            bool cloudAndVisibilityOk = false,
            double? airTemperatureCelsius = null,
            double? dewpointTemperatureCelsius = null,
            double? qnhHpa = null,
            SurfaceWind? surfaceWind = null,
            double? prevailingVisibilityMeters = null,
            IEnumerable<RunwayVisualRange>? runwayVisualRanges = null,
            IEnumerable<AerodromeWeather>? presentWeather = null,
            IEnumerable<AerodromeCloudLayer>? cloudLayers = null,
            IEnumerable<AerodromeForecastGroup>? trendForecasts = null,
            PermissibleUsageReason? permissibleUsageReason = null,
            string? permissibleUsageSupplementary = null) =>
            Create(
                content => new Speci(content),
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
                prevailingVisibilityMeters,
                runwayVisualRanges,
                presentWeather,
                cloudLayers,
                trendForecasts,
                permissibleUsageReason,
                permissibleUsageSupplementary);
    }
}
