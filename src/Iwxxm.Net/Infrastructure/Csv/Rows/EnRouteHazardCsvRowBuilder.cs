using System.Collections.Generic;
using Iwxxm.Net.Reports;

namespace Iwxxm.Net.Infrastructure.Csv.Rows
{
    /// <summary>Builds CSV rows for SIGMET and AIRMET en-route hazard reports.</summary>
    internal static class EnRouteHazardCsvRowBuilder
    {
        public static void AddSigmet(
            Sigmet sigmet,
            string? bulletinId,
            bool includeWkt,
            List<CsvRow> rows)
        {
            if (sigmet.EruptingVolcano != null)
            {
                CsvRow vRow = CsvRow.CreateBase(
                    sigmet,
                    "ERUPTING_VOLCANO",
                    sigmet.EruptingVolcano.Id,
                    bulletinId,
                    sigmet.FirDesignator,
                    sigmet.ValidFromUtc,
                    sigmet.ValidUntilUtc);
                vRow.SetPoint(sigmet.EruptingVolcano.Position, includeWkt);
                vRow.SetText(CsvColumns.Phenomenon, sigmet.EruptingVolcano.Name);
                vRow.SetText(CsvColumns.Details, sigmet.EruptingVolcano.IavceiNumber);
                rows.Add(vRow);
            }

            string? phenomenon = sigmet.Phenomenon?.Notation;
            foreach (EnRoutePhenomenonVolume vol in sigmet.EvolvingConditions)
            {
                AddVolume(sigmet, "SIGMET_EVOLVING_VOLUME", sigmet.FirDesignator, sigmet.ValidFromUtc, sigmet.ValidUntilUtc, phenomenon, vol, bulletinId, includeWkt, rows);
            }

            foreach (EnRoutePhenomenonVolume vol in sigmet.ForecastPositions)
            {
                AddVolume(sigmet, "SIGMET_FORECAST_VOLUME", sigmet.FirDesignator, sigmet.ValidFromUtc, sigmet.ValidUntilUtc, phenomenon, vol, bulletinId, includeWkt, rows);
            }

            if (sigmet.IsCancelReport || sigmet.IsTranslationFailed)
            {
                CsvRow meta = CsvRow.CreateBase(
                    sigmet,
                    sigmet.IsCancelReport ? "SIGMET_CANCELLATION" : "SIGMET_TRANSLATION_FAILED",
                    sigmet.Id,
                    bulletinId,
                    sigmet.FirDesignator,
                    sigmet.ValidFromUtc,
                    sigmet.ValidUntilUtc);
                meta.SetText(CsvColumns.Details, sigmet.IsTranslationFailed ? sigmet.TranslationFailedTac : $"CANCEL {sigmet.SequenceNumber}");
                rows.Add(meta);
            }
        }

        public static void AddAirmet(
            Airmet airmet,
            string? bulletinId,
            bool includeWkt,
            List<CsvRow> rows)
        {
            if (airmet.IsCancelReport || airmet.IsTranslationFailed)
            {
                CsvRow meta = CsvRow.CreateBase(
                    airmet,
                    airmet.IsCancelReport ? "AIRMET_CANCELLATION" : "AIRMET_TRANSLATION_FAILED",
                    airmet.Id,
                    bulletinId,
                    airmet.FirDesignator,
                    airmet.ValidFromUtc,
                    airmet.ValidUntilUtc);
                meta.SetText(CsvColumns.Details, airmet.IsTranslationFailed ? airmet.TranslationFailedTac : $"CANCEL {airmet.SequenceNumber}");
                rows.Add(meta);
                return;
            }

            string? phenomenon = airmet.Phenomenon?.Notation;
            foreach (AirmetEvolvingCondition condition in airmet.EvolvingConditions)
            {
                CsvRow row = CreateVolumeRow(
                    airmet,
                    "AIRMET_EVOLVING_VOLUME",
                    airmet.FirDesignator,
                    airmet.ValidFromUtc,
                    airmet.ValidUntilUtc,
                    phenomenon,
                    condition.Volume,
                    bulletinId,
                    includeWkt);

                if (condition.SurfaceVisibility.HasValue)
                    row.SetNumeric(CsvColumns.VisibilityM, condition.SurfaceVisibility.Value.ToMeters());

                if (condition.SurfaceWindDirectionDegrees.HasValue || condition.SurfaceWindSpeed.HasValue)
                    row.SetWind(condition.SurfaceWindDirectionDegrees, condition.SurfaceWindSpeed);

                rows.Add(row);
            }
        }

        private static void AddVolume(
            IwxxmReport report,
            string featureType,
            string? firDesignator,
            System.DateTimeOffset? validFrom,
            System.DateTimeOffset? validUntil,
            string? phenomenon,
            EnRoutePhenomenonVolume vol,
            string? bulletinId,
            bool includeWkt,
            List<CsvRow> rows)
        {
            if (vol.Area != null)
            {
                rows.Add(CreateVolumeRow(report, featureType, firDesignator, validFrom, validUntil, phenomenon, vol, bulletinId, includeWkt));
            }

            if (vol.TropicalCycloneCentre.HasValue)
            {
                CsvRow tcRow = CsvRow.CreateBase(
                    report,
                    "TROPICAL_CYCLONE_CENTRE",
                    $"{vol.Id}#tc",
                    bulletinId,
                    firDesignator,
                    validFrom,
                    validUntil);
                tcRow.SetPoint(vol.TropicalCycloneCentre.Value, includeWkt);
                tcRow.SetText(CsvColumns.Phenomenon, phenomenon);
                rows.Add(tcRow);
            }
        }

        private static CsvRow CreateVolumeRow(
            IwxxmReport report,
            string featureType,
            string? firDesignator,
            System.DateTimeOffset? validFrom,
            System.DateTimeOffset? validUntil,
            string? phenomenon,
            EnRoutePhenomenonVolume vol,
            string? bulletinId,
            bool includeWkt)
        {
            CsvRow row = CsvRow.CreateBase(
                report,
                featureType,
                vol.Id,
                bulletinId,
                firDesignator,
                validFrom,
                validUntil);

            row.SetSurface(vol.Area, includeWkt);
            row.SetVerticalLimits(vol.LowerLimit, vol.UpperLimit);
            row.SetWind(vol.DirectionOfMotionDegrees, vol.SpeedOfMotion);
            row.SetText(CsvColumns.Phenomenon, phenomenon);
            row.SetText(CsvColumns.Intensity, vol.IntensityChange?.ToString());
            return row;
        }
    }
}
