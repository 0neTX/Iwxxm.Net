using System.Collections.Generic;
using System.Linq;
using Iwxxm.Net.Infrastructure.Mapping.CodeLists;
using Iwxxm.Net.Reports;
using static Iwxxm.Net.Infrastructure.GeoJson.GeoJsonProperties;

namespace Iwxxm.Net.Infrastructure.GeoJson.Features
{
    /// <summary>
    /// Builds the GeoJSON features of a Space Weather Advisory: one polygon (or approximated circle) per region extent of
    /// every analysis, carrying only the location indicators and vertical limits of its own region; one feature without
    /// geometry per region that has no extent (e.g. NIGHTSIDE); and one feature without geometry per nil analysis
    /// (e.g. "NO SWX EXP").
    /// </summary>
    internal static class SpaceWxFeatureBuilder
    {
        public static void AddSpaceWxFeatures(
            SpaceWx swx,
            string? bulletinId,
            List<GeoJsonFeature> features)
        {
            foreach (SpaceWeatherAnalysis analysis in swx.Analyses)
            {
                if (analysis.IntensityAndRegions.Count == 0 && analysis.NilReason != null)
                {
                    var nilProps = CreateAnalysisBase(swx, analysis, "SPACE_WEATHER_ANALYSIS_NIL", bulletinId);
                    nilProps.Add(("nilReason", NilReasonText(analysis.NilReason)));
                    features.Add(GeoJsonFeature.WithoutGeometry($"{analysis.Id}#nil", nilProps));
                    continue;
                }

                foreach (SpaceWeatherIntensityAndRegion group in analysis.IntensityAndRegions)
                {
                    // Numbering runs over the whole group so that the ids stay stable: poly-N for every extent,
                    // noextent-N for every region without one (e.g. NIGHTSIDE).
                    int polygon = 0;
                    int noExtent = 0;
                    foreach (SpaceWeatherRegion region in group.Regions)
                    {
                        if (region.Areas.Count == 0)
                        {
                            // A region without extent is kept as a feature without geometry so its indicators are not lost.
                            noExtent++;
                            features.Add(GeoJsonFeature.WithoutGeometry(
                                $"{analysis.Id}#{group.Id}#noextent-{noExtent}",
                                CreateRegionProperties(swx, analysis, group, region, bulletinId)));
                            continue;
                        }

                        foreach (GeoSurface area in region.Areas)
                        {
                            polygon++;
                            features.Add(GeoJsonFeature.ForSurface(
                                $"{analysis.Id}#{group.Id}#poly-{polygon}",
                                area,
                                CreateRegionProperties(swx, analysis, group, region, bulletinId)));
                        }
                    }
                }
            }
        }

        private static List<(string Key, object? Value)> CreateRegionProperties(
            SpaceWx swx,
            SpaceWeatherAnalysis analysis,
            SpaceWeatherIntensityAndRegion group,
            SpaceWeatherRegion region,
            string? bulletinId)
        {
            var props = CreateAnalysisBase(swx, analysis, "SPACE_WEATHER_REGION", bulletinId);
            props.Add(("intensity", group.Intensity));
            if (region.LocationIndicators.Count > 0)
            {
                // Linked-Data output: the original code-list references are rebuilt from the Domain codes.
                props.Add(("locationIndicatorUris", region.LocationIndicators
                    .Select(location => CodeListTables.SpaceWeatherLocations.ToReference(location)!)
                    .ToList()));
            }

            AddVerticalLimit(props, "lowerLimit", region.LowerLimit);
            AddVerticalLimit(props, "upperLimit", region.UpperLimit);
            return props;
        }

        private static List<(string Key, object? Value)> CreateAnalysisBase(
            SpaceWx swx,
            SpaceWeatherAnalysis analysis,
            string featureType,
            string? bulletinId)
        {
            var props = CreateBase(swx, featureType, bulletinId);
            props.Add(("issuingSwxcName", swx.IssuingSwxcName));
            props.Add(("advisoryNumber", swx.AdvisoryNumber));
            props.Add(("timeIndicator", FormatTimeIndicator(analysis.TimeIndicator)));
            props.Add(("phenomenonTimeUtc", FormatInstant(analysis.PhenomenonTimeUtc)));
            return props;
        }
    }
}
