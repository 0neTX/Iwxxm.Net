using System.Collections.Generic;
using System.Linq;
using Iwxxm.Net.Reports;

namespace Iwxxm.Net.Infrastructure.Csv.Rows
{
    /// <summary>Builds CSV rows for METAR/SPECI aerodrome observations and TAF forecasts.</summary>
    internal static class AerodromeCsvRowBuilder
    {
        public static void AddAerodromeObservation(
            AerodromeObservation report,
            string? bulletinId,
            bool includeWkt,
            List<CsvRow> rows)
        {
            string featureType = report.ReportType == IwxxmReportType.Speci
                ? "SPECI_OBSERVATION"
                : "METAR_OBSERVATION";

            CsvRow row = CsvRow.CreateBase(
                report,
                featureType,
                report.Id,
                bulletinId,
                report.AerodromeDesignator,
                report.ObservationTimeUtc,
                report.ObservationTimeUtc);

            if (report.AerodromePosition.HasValue)
                row.SetPoint(report.AerodromePosition.Value, includeWkt);

            if (report.SurfaceWind != null)
                row.SetWind(report.SurfaceWind.MeanDirectionDegrees, report.SurfaceWind.MeanSpeed);

            row.SetNumeric(CsvColumns.AirTemperatureC, report.AirTemperatureCelsius);
            row.SetNumeric(CsvColumns.QnhHpa, report.QnhHpa);
            row.SetNumeric(CsvColumns.VisibilityM, report.PrevailingVisibilityMeters);

            if (report.PresentWeather.Count > 0)
                row.SetText(CsvColumns.Phenomenon, string.Join(" ", report.PresentWeather.Select(w => w.Notation)));

            if (report.IsTranslationFailed)
                row.SetText(CsvColumns.Details, report.TranslationFailedTac);
            else if (report.IsNilObservation)
                row.SetText(CsvColumns.Details, "NIL");
            else if (report.CloudAndVisibilityOk)
                row.SetText(CsvColumns.Details, "CAVOK");

            rows.Add(row);
        }

        public static void AddTaf(
            Taf taf,
            string? bulletinId,
            bool includeWkt,
            List<CsvRow> rows)
        {
            CsvRow row = CsvRow.CreateBase(
                taf,
                "TAF_FORECAST",
                taf.Id,
                bulletinId,
                taf.AerodromeDesignator,
                taf.ValidFromUtc,
                taf.ValidUntilUtc);

            if (taf.AerodromePosition.HasValue)
                row.SetPoint(taf.AerodromePosition.Value, includeWkt);

            if (taf.BaseForecast != null)
            {
                if (taf.BaseForecast.SurfaceWind != null)
                    row.SetWind(taf.BaseForecast.SurfaceWind.MeanDirectionDegrees, taf.BaseForecast.SurfaceWind.MeanSpeed);
                row.SetNumeric(CsvColumns.VisibilityM, taf.BaseForecast.PrevailingVisibilityMeters);
                if (taf.BaseForecast.Weather.Count > 0)
                    row.SetText(CsvColumns.Phenomenon, string.Join(" ", taf.BaseForecast.Weather.Select(w => w.Notation)));
            }

            if (taf.IsTranslationFailed)
                row.SetText(CsvColumns.Details, taf.TranslationFailedTac);
            else if (taf.IsCancelReport)
                row.SetText(CsvColumns.Details, "CANCEL");
            else if (taf.IsNilForecast)
                row.SetText(CsvColumns.Details, "NIL");
            else if (taf.ChangeForecasts.Count > 0)
                row.SetText(CsvColumns.Details, $"changes={taf.ChangeForecasts.Count}");

            rows.Add(row);
        }
    }
}
