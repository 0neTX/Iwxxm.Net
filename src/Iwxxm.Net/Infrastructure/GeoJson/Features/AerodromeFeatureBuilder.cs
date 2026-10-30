using System.Collections.Generic;
using System.Linq;
using Iwxxm.Net.Infrastructure.Mapping.CodeLists;
using Iwxxm.Net.Reports;
using static Iwxxm.Net.Infrastructure.GeoJson.GeoJsonProperties;

namespace Iwxxm.Net.Infrastructure.GeoJson.Features
{
    /// <summary>Builds the GeoJSON feature of METAR/SPECI observations and TAF forecasts (aerodrome reference point, or a null geometry).</summary>
    internal static class AerodromeFeatureBuilder
    {
        public static void AddAerodromeObservationFeature(
            AerodromeObservation report,
            string? bulletinId,
            List<GeoJsonFeature> features)
        {
            var props = CreateBase(report, report.ReportType == IwxxmReportType.Speci ? "SPECI_OBSERVATION" : "METAR_OBSERVATION", bulletinId);
            props.Add(("aerodromeDesignator", report.AerodromeDesignator));
            props.Add(("aerodromeName", report.AerodromeName));
            props.Add(("observationTimeUtc", FormatInstant(report.ObservationTimeUtc)));
            props.Add(("isAutomatedStation", report.IsAutomatedStation));
            props.Add(("isNilObservation", report.IsNilObservation));
            props.Add(("cloudAndVisibilityOk", report.CloudAndVisibilityOk));
            props.Add(("airTemperatureCelsius", report.AirTemperatureCelsius));
            props.Add(("dewpointTemperatureCelsius", report.DewpointTemperatureCelsius));
            props.Add(("qnhHpa", report.QnhHpa));
            props.Add(("prevailingVisibilityMeters", report.PrevailingVisibilityMeters));
            if (report.SurfaceWind != null)
            {
                props.Add(("windDirectionDegrees", report.SurfaceWind.MeanDirectionDegrees));
                AddSpeed(props, "windSpeed", report.SurfaceWind.MeanSpeed);
                AddSpeed(props, "windGust", report.SurfaceWind.GustSpeed);
            }
            if (report.PresentWeather.Count > 0)
            {
                // Linked-Data output: the original code-list references are rebuilt from the Domain codes.
                props.Add(("presentWeatherUris", report.PresentWeather.Select(AerodromeWeatherCodeList.ToReference).ToList()));
            }

            features.Add(report.AerodromePosition.HasValue
                ? GeoJsonFeature.ForPoint(report.Id, report.AerodromePosition.Value, props)
                : GeoJsonFeature.WithoutGeometry(report.Id, props));
        }

        public static void AddTafFeature(
            Taf taf,
            string? bulletinId,
            List<GeoJsonFeature> features)
        {
            var props = CreateBase(taf, "TAF_FORECAST", bulletinId);
            props.Add(("aerodromeDesignator", taf.AerodromeDesignator));
            props.Add(("aerodromeName", taf.AerodromeName));
            props.Add(("validFromUtc", FormatInstant(taf.ValidFromUtc)));
            props.Add(("validUntilUtc", FormatInstant(taf.ValidUntilUtc)));
            props.Add(("isCancelReport", taf.IsCancelReport));
            props.Add(("isNilForecast", taf.IsNilForecast));
            props.Add(("changeForecastCount", taf.ChangeForecasts.Count));

            features.Add(taf.AerodromePosition.HasValue
                ? GeoJsonFeature.ForPoint(taf.Id, taf.AerodromePosition.Value, props)
                : GeoJsonFeature.WithoutGeometry(taf.Id, props));
        }
    }
}
