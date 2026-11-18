using System.Collections.Generic;
using Iwxxm.Net.Reports;

namespace Iwxxm.Net.Infrastructure.Csv.Rows
{
    /// <summary>Builds CSV rows for all 9 WAFS SIGWX phenomena.</summary>
    internal static class SigwxCsvRowBuilder
    {
        public static void AddSigwx(Sigwx sigwx, string? bulletinId, bool includeWkt, List<CsvRow> rows)
        {
            foreach (JetStream js in sigwx.JetStreams)
            {
                CsvRow jsRow = CreateSigwxBase(sigwx, "JETSTREAM", js.Id, bulletinId);
                jsRow.SetLineString(js.AxisTrajectory, includeWkt);
                jsRow.SetVerticalLimits(js.CoreLevel, js.CoreLevel);
                jsRow.SetWind(null, js.MaxWindSpeed);
                jsRow.SetText(CsvColumns.Phenomenon, "JETSTREAM");
                rows.Add(jsRow);

                for (int i = 0; i < js.CorePoints.Count; i++)
                {
                    JetStreamCorePoint cp = js.CorePoints[i];
                    CsvRow cpRow = CreateSigwxBase(sigwx, "JETSTREAM_CORE_POINT", $"{js.Id}#core-{i + 1}", bulletinId);
                    cpRow.SetPoint(cp.Location, includeWkt);
                    cpRow.SetVerticalLimits(cp.Level, cp.Level);
                    cpRow.SetWind(null, cp.WindSpeed);
                    cpRow.SetText(CsvColumns.Phenomenon, "JETSTREAM_CORE_POINT");
                    cpRow.SetText(CsvColumns.Details, js.Id);
                    rows.Add(cpRow);
                }
            }

            foreach (Cloud cloud in sigwx.Clouds)
            {
                CsvRow row = CreateSigwxBase(sigwx, "CLOUD", cloud.Id, bulletinId);
                row.SetSurfaces(cloud.Areas, includeWkt);
                row.SetVerticalLimits(cloud.Base, cloud.Top);
                row.SetText(CsvColumns.Phenomenon, cloud.CloudType?.ToString() ?? "CLOUD");
                row.SetText(CsvColumns.Intensity, cloud.Distribution?.ToString());
                rows.Add(row);
            }

            foreach (Turbulence turb in sigwx.Turbulences)
            {
                CsvRow row = CreateSigwxBase(sigwx, "TURBULENCE", turb.Id, bulletinId);
                row.SetSurfaces(turb.Areas, includeWkt);
                row.SetVerticalLimits(turb.Base, turb.Top);
                row.SetText(CsvColumns.Phenomenon, "TURBULENCE");
                row.SetText(CsvColumns.Intensity, turb.Degree?.ToString());
                rows.Add(row);
            }

            foreach (AirframeIcing icing in sigwx.AirframeIcings)
            {
                CsvRow row = CreateSigwxBase(sigwx, "AIRFRAME_ICING", icing.Id, bulletinId);
                row.SetSurfaces(icing.Areas, includeWkt);
                row.SetVerticalLimits(icing.Base, icing.Top);
                row.SetText(CsvColumns.Phenomenon, "AIRFRAME_ICING");
                row.SetText(CsvColumns.Intensity, icing.Degree?.ToString());
                rows.Add(row);
            }

            foreach (Tropopause trop in sigwx.Tropopauses)
            {
                if (trop.ContourPolygon != null)
                {
                    CsvRow row = CreateSigwxBase(sigwx, "TROPOPAUSE_CONTOUR", trop.Id, bulletinId);
                    row.SetSurface(trop.ContourPolygon, includeWkt);
                    row.SetVerticalLimits(trop.ContourLevel, trop.ContourLevel);
                    row.SetText(CsvColumns.Phenomenon, "TROPOPAUSE");
                    row.SetText(CsvColumns.Details, trop.LevelType);
                    rows.Add(row);
                }

                for (int i = 0; i < trop.SpotHeights.Count; i++)
                {
                    TropopauseSpotHeight sh = trop.SpotHeights[i];
                    CsvRow row = CreateSigwxBase(sigwx, "TROPOPAUSE_SPOT_HEIGHT", $"{trop.Id}#spot-{i + 1}", bulletinId);
                    row.SetPoint(sh.Location, includeWkt);
                    row.SetVerticalLimits(sh.Level, sh.Level);
                    row.SetText(CsvColumns.Phenomenon, "TROPOPAUSE");
                    row.SetText(CsvColumns.Details, trop.LevelType);
                    rows.Add(row);
                }
            }

            foreach (Volcano vol in sigwx.Volcanoes)
            {
                CsvRow row = CreateSigwxBase(sigwx, "VOLCANO", vol.Id, bulletinId);
                row.SetPoint(vol.Position, includeWkt);
                row.SetText(CsvColumns.Phenomenon, vol.Name);
                row.SetText(CsvColumns.Details, vol.IavceiNumber);
                rows.Add(row);
            }

            foreach (TropicalCyclone tc in sigwx.TropicalCyclones)
            {
                CsvRow row = CreateSigwxBase(sigwx, "TROPICAL_CYCLONE", tc.Id, bulletinId);
                row.SetPoint(tc.Position, includeWkt);
                row.SetText(CsvColumns.Phenomenon, tc.Name);
                rows.Add(row);
            }

            foreach (Sandstorm ss in sigwx.Sandstorms)
            {
                CsvRow row = CreateSigwxBase(sigwx, "SANDSTORM", ss.Id, bulletinId);
                row.SetPoint(ss.Position, includeWkt);
                row.SetText(CsvColumns.Phenomenon, "SANDSTORM");
                rows.Add(row);
            }

            foreach (RadiationIncident rad in sigwx.RadiationIncidents)
            {
                CsvRow row = CreateSigwxBase(sigwx, "RADIATION_INCIDENT", rad.Id, bulletinId);
                row.SetPoint(rad.Position, includeWkt);
                row.SetText(CsvColumns.Phenomenon, "RADIATION");
                row.SetText(CsvColumns.Details, rad.SiteName);
                rows.Add(row);
            }
        }

        private static CsvRow CreateSigwxBase(Sigwx sigwx, string featureType, string featureId, string? bulletinId) =>
            CsvRow.CreateBase(
                sigwx,
                featureType,
                featureId,
                bulletinId,
                sigwx.IssuingCentre,
                sigwx.ValidFromUtc,
                sigwx.ValidUntilUtc);
    }
}
