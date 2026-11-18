using System.Collections.Generic;
using System.Globalization;
using Iwxxm.Net.Infrastructure.GeoJson;
using Iwxxm.Net.Reports;

namespace Iwxxm.Net.Infrastructure.Csv.Rows
{
    /// <summary>Builds CSV rows for VONA, QVACI, and Volcanic Ash Advisory (VAA) reports.</summary>
    internal static class VolcanicCsvRowBuilder
    {
        public static void AddVona(Vona vona, string? bulletinId, bool includeWkt, List<CsvRow> rows)
        {
            if (vona.Volcanoes.Count == 0)
            {
                CsvRow row = CsvRow.CreateBase(vona, "VONA_NOTICE", vona.Id, bulletinId);
                row.SetText(CsvColumns.Intensity, vona.CurrentColourCode?.Notation);
                row.SetText(CsvColumns.Details, vona.IsTranslationFailed ? vona.TranslationFailedTac : vona.NoticeNumber);
                rows.Add(row);
                return;
            }

            foreach (Volcano vol in vona.Volcanoes)
            {
                CsvRow row = CsvRow.CreateBase(vona, "VONA_VOLCANO", vol.Id, bulletinId, vol.Name);
                row.SetPoint(vol.Position, includeWkt);
                row.SetVerticalLimits(vol.SourceElevation, vona.AshCloudElevation);
                row.SetText(CsvColumns.Phenomenon, vol.Name);
                row.SetText(CsvColumns.Intensity, vona.CurrentColourCode?.Notation);
                row.SetText(CsvColumns.Details, vona.NoticeNumber);
                rows.Add(row);
            }
        }

        public static void AddQvaci(Qvaci qvaci, string? bulletinId, bool includeWkt, List<CsvRow> rows)
        {
            foreach (Volcano vol in qvaci.Volcanoes)
            {
                CsvRow row = CsvRow.CreateBase(qvaci, "VOLCANO", vol.Id, bulletinId, qvaci.OriginatingCentreName);
                row.SetPoint(vol.Position, includeWkt);
                row.SetVerticalLimits(vol.SourceElevation, null);
                row.SetText(CsvColumns.Phenomenon, vol.Name);
                row.SetText(CsvColumns.Details, vol.IavceiNumber);
                rows.Add(row);
            }

            foreach (var cloud in qvaci.AshConcentrationClouds)
            {
                CsvRow row = CsvRow.CreateBase(
                    qvaci,
                    "ASH_CONCENTRATION_CLOUD",
                    cloud.Id,
                    bulletinId,
                    qvaci.OriginatingCentreName,
                    cloud.PhenomenonTimeUtc,
                    cloud.PhenomenonTimeUtc);
                row.SetSurface(cloud.Area, includeWkt);
                row.SetVerticalLimits(cloud.LowerLimit, cloud.UpperLimit);
                row.SetText(CsvColumns.Phenomenon, "VOLCANIC_ASH");
                row.SetText(
                    CsvColumns.Intensity,
                    cloud.AshDensityEqualsOrExceedsMgPerM3.ToString("G17", CultureInfo.InvariantCulture) + " mg/m3");
                rows.Add(row);
            }

            if (qvaci.IsTranslationFailed && rows.Count == 0)
            {
                CsvRow row = CsvRow.CreateBase(qvaci, "QVACI_TRANSLATION_FAILED", qvaci.Id, bulletinId, qvaci.OriginatingCentreName);
                row.SetText(CsvColumns.Details, qvaci.TranslationFailedTac);
                rows.Add(row);
            }
        }

        public static void AddVaa(Vaa vaa, string? bulletinId, bool includeWkt, List<CsvRow> rows)
        {
            if (vaa.Volcano != null)
            {
                CsvRow volRow = CsvRow.CreateBase(vaa, "VOLCANO", vaa.Volcano.Id, bulletinId, vaa.IssuingVaacName);
                volRow.SetPoint(vaa.Volcano.Position, includeWkt);
                volRow.SetText(CsvColumns.Phenomenon, vaa.Volcano.Name);
                volRow.SetText(CsvColumns.Details, vaa.AdvisoryNumber);
                rows.Add(volRow);
            }

            if (vaa.Observation != null)
                AddConditions(vaa, vaa.Observation, "OBSERVED_ASH_CLOUD", "OBSERVED_ASH_CONDITIONS", bulletinId, includeWkt, rows);

            foreach (VolcanicAshConditions forecast in vaa.Forecasts)
                AddConditions(vaa, forecast, "FORECAST_ASH_CLOUD", "FORECAST_ASH_CONDITIONS", bulletinId, includeWkt, rows);

            if (vaa.IsTranslationFailed && rows.Count == 0)
            {
                CsvRow row = CsvRow.CreateBase(vaa, "VAA_TRANSLATION_FAILED", vaa.Id, bulletinId, vaa.IssuingVaacName);
                row.SetText(CsvColumns.Details, vaa.TranslationFailedTac);
                rows.Add(row);
            }
        }

        private static void AddConditions(
            Vaa vaa,
            VolcanicAshConditions conditions,
            string cloudFeatureType,
            string emptyFeatureType,
            string? bulletinId,
            bool includeWkt,
            List<CsvRow> rows)
        {
            string? status = conditions.Status?.ToString();
            if (conditions.AshClouds.Count == 0)
            {
                CsvRow row = CsvRow.CreateBase(
                    vaa,
                    emptyFeatureType,
                    conditions.Id,
                    bulletinId,
                    vaa.IssuingVaacName,
                    conditions.PhenomenonTimeUtc,
                    conditions.PhenomenonTimeUtc);
                row.SetText(CsvColumns.Phenomenon, "VOLCANIC_ASH");
                row.SetText(CsvColumns.Details, status);
                rows.Add(row);
                return;
            }

            foreach (EnRoutePhenomenonVolume cloud in conditions.AshClouds)
            {
                if (cloud.Area == null)
                    continue;

                CsvRow row = CsvRow.CreateBase(
                    vaa,
                    cloudFeatureType,
                    cloud.Id,
                    bulletinId,
                    vaa.IssuingVaacName,
                    conditions.PhenomenonTimeUtc,
                    conditions.PhenomenonTimeUtc);
                row.SetSurface(cloud.Area, includeWkt);
                row.SetVerticalLimits(cloud.LowerLimit, cloud.UpperLimit);
                row.SetWind(cloud.DirectionOfMotionDegrees, cloud.SpeedOfMotion);
                row.SetText(CsvColumns.Phenomenon, "VOLCANIC_ASH");
                row.SetText(CsvColumns.Details, status);
                rows.Add(row);
            }
        }
    }
}
