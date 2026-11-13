using System.Collections.Generic;
using System.Linq;
using Iwxxm.Net.Infrastructure.Mapping.CodeLists;
using Iwxxm.Net.Reports;
using static Iwxxm.Net.Infrastructure.GeoJson.GeoJsonProperties;

namespace Iwxxm.Net.Infrastructure.GeoJson.Features
{
    /// <summary>
    /// Builds the GeoJSON features of a Space Weather Advisory: one polygon (or approximated circle) per region extent of
    /// every analysis, and one feature without geometry per nil analysis (e.g. "NO SWX EXP").
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
                if (analysis.Regions.Count == 0 && analysis.NilReason != null)
                {
                    var nilProps = CreateAnalysisBase(swx, analysis, "SPACE_WEATHER_ANALYSIS_NIL", bulletinId);
                    nilProps.Add(("nilReason", NilReasonText(analysis.NilReason)));
                    features.Add(GeoJsonFeature.WithoutGeometry($"{analysis.Id}#nil", nilProps));
                    continue;
                }

                foreach (SpaceWeatherAnalysisRegion region in analysis.Regions)
                {
                    for (int i = 0; i < region.Areas.Count; i++)
                    {
                        var props = CreateAnalysisBase(swx, analysis, "SPACE_WEATHER_REGION", bulletinId);
                        props.Add(("intensity", region.Intensity));
                        if (region.LocationIndicators.Count > 0)
                        {
                            // Linked-Data output: the original code-list references are rebuilt from the Domain codes.
                            props.Add(("locationIndicatorUris", region.LocationIndicators
                                .Select(location => CodeListTables.SpaceWeatherLocations.ToReference(location)!)
                                .ToList()));
                        }
                        features.Add(GeoJsonFeature.ForSurface($"{analysis.Id}#{region.Id}#poly-{i + 1}", region.Areas[i], props));
                    }
                }
            }
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
