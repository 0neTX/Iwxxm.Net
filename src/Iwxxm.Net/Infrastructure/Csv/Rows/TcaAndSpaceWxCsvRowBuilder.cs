using System.Collections.Generic;
using System.Linq;
using Iwxxm.Net.Infrastructure.GeoJson;
using Iwxxm.Net.Reports;

namespace Iwxxm.Net.Infrastructure.Csv.Rows
{
    /// <summary>Builds CSV rows for Tropical Cyclone Advisory (TCA) and Space Weather Advisory (SpaceWx) reports.</summary>
    internal static class TcaAndSpaceWxCsvRowBuilder
    {
        public static void AddTca(Tca tca, string? bulletinId, bool includeWkt, List<CsvRow> rows)
        {
            var trackCoordinates = new List<GeoCoordinate>();

            if (tca.Observation != null)
            {
                trackCoordinates.Add(tca.Observation.Position);
                CsvRow row = CsvRow.CreateBase(
                    tca,
                    "TC_OBSERVATION",
                    $"{tca.Id}#obs",
                    bulletinId,
                    tca.CycloneName,
                    tca.Observation.PhenomenonTimeUtc,
                    tca.Observation.PhenomenonTimeUtc);
                row.SetPoint(tca.Observation.Position, includeWkt);
                row.SetWind(null, tca.Observation.MaxSurfaceWindSpeed);
                row.SetNumeric(CsvColumns.QnhHpa, tca.Observation.CentralPressureHpa);
                row.SetText(CsvColumns.Phenomenon, "TROPICAL_CYCLONE");
                row.SetText(CsvColumns.Details, tca.AdvisoryNumber);
                rows.Add(row);
            }

            foreach (EnRoutePhenomenonVolume cb in tca.CumulonimbusCloudLocations)
            {
                if (cb.Area == null)
                    continue;

                CsvRow row = CsvRow.CreateBase(tca, "TC_CUMULONIMBUS_CLOUD", cb.Id, bulletinId, tca.CycloneName);
                row.SetSurface(cb.Area, includeWkt);
                row.SetVerticalLimits(cb.LowerLimit, cb.UpperLimit);
                row.SetText(CsvColumns.Phenomenon, "CB");
                row.SetText(CsvColumns.Details, tca.AdvisoryNumber);
                rows.Add(row);
            }

            for (int i = 0; i < tca.Forecasts.Count; i++)
            {
                var fcst = tca.Forecasts[i];
                trackCoordinates.Add(fcst.Position);
                CsvRow row = CsvRow.CreateBase(
                    tca,
                    "TC_FORECAST",
                    $"{tca.Id}#fcst-{i + 1}",
                    bulletinId,
                    tca.CycloneName,
                    fcst.PhenomenonTimeUtc,
                    fcst.PhenomenonTimeUtc);
                row.SetPoint(fcst.Position, includeWkt);
                row.SetWind(null, fcst.MaxSurfaceWindSpeed);
                row.SetText(CsvColumns.Phenomenon, "TROPICAL_CYCLONE");
                row.SetText(CsvColumns.Details, tca.AdvisoryNumber);
                rows.Add(row);
            }

            if (trackCoordinates.Count >= 2)
            {
                CsvRow trackRow = CsvRow.CreateBase(tca, "TC_TRACK", $"{tca.Id}#track", bulletinId, tca.CycloneName);
                trackRow.SetLineString(trackCoordinates, includeWkt);
                trackRow.SetText(CsvColumns.Phenomenon, "TROPICAL_CYCLONE");
                trackRow.SetText(CsvColumns.Details, tca.AdvisoryNumber);
                rows.Add(trackRow);
            }

            if (tca.IsTranslationFailed && rows.Count == 0)
            {
                CsvRow row = CsvRow.CreateBase(tca, "TCA_TRANSLATION_FAILED", tca.Id, bulletinId, tca.CycloneName);
                row.SetText(CsvColumns.Details, tca.TranslationFailedTac);
                rows.Add(row);
            }
        }

        public static void AddSpaceWx(SpaceWx swx, string? bulletinId, bool includeWkt, List<CsvRow> rows)
        {
            foreach (SpaceWeatherAnalysis analysis in swx.Analyses)
            {
                if (analysis.IntensityAndRegions.Count == 0 && analysis.NilReason != null)
                {
                    CsvRow nilRow = CsvRow.CreateBase(
                        swx,
                        "SPACE_WEATHER_ANALYSIS_NIL",
                        $"{analysis.Id}#nil",
                        bulletinId,
                        swx.IssuingSwxcName,
                        analysis.PhenomenonTimeUtc,
                        analysis.PhenomenonTimeUtc);
                    nilRow.SetText(CsvColumns.Phenomenon, analysis.TimeIndicator.ToString());
                    nilRow.SetText(CsvColumns.Details, GeoJsonProperties.NilReasonText(analysis.NilReason));
                    rows.Add(nilRow);
                    continue;
                }

                foreach (SpaceWeatherIntensityAndRegion group in analysis.IntensityAndRegions)
                {
                    int polygon = 0;
                    int noExtent = 0;
                    foreach (SpaceWeatherRegion region in group.Regions)
                    {
                        string? locations = region.LocationIndicators.Count > 0
                            ? string.Join(" ", region.LocationIndicators.Select(l => l.Notation))
                            : null;

                        if (region.Areas.Count == 0)
                        {
                            noExtent++;
                            CsvRow row = CreateRegionRow(swx, analysis, group, region, $"{analysis.Id}#{group.Id}#noextent-{noExtent}", locations, bulletinId);
                            rows.Add(row);
                            continue;
                        }

                        foreach (GeoSurface area in region.Areas)
                        {
                            polygon++;
                            CsvRow row = CreateRegionRow(swx, analysis, group, region, $"{analysis.Id}#{group.Id}#poly-{polygon}", locations, bulletinId);
                            row.SetSurface(area, includeWkt);
                            rows.Add(row);
                        }
                    }
                }
            }

            if (swx.IsTranslationFailed && rows.Count == 0)
            {
                CsvRow row = CsvRow.CreateBase(swx, "SPACEWX_TRANSLATION_FAILED", swx.Id, bulletinId, swx.IssuingSwxcName);
                row.SetText(CsvColumns.Details, swx.TranslationFailedTac);
                rows.Add(row);
            }
        }

        private static CsvRow CreateRegionRow(
            SpaceWx swx,
            SpaceWeatherAnalysis analysis,
            SpaceWeatherIntensityAndRegion group,
            SpaceWeatherRegion region,
            string featureId,
            string? locations,
            string? bulletinId)
        {
            CsvRow row = CsvRow.CreateBase(
                swx,
                "SPACE_WEATHER_REGION",
                featureId,
                bulletinId,
                swx.IssuingSwxcName,
                analysis.PhenomenonTimeUtc,
                analysis.PhenomenonTimeUtc);
            row.SetVerticalLimits(region.LowerLimit, region.UpperLimit);
            row.SetText(CsvColumns.Phenomenon, analysis.TimeIndicator.ToString());
            row.SetText(CsvColumns.Intensity, group.Intensity);
            row.SetText(CsvColumns.Details, locations);
            return row;
        }
    }
}
