using System.Collections.Generic;
using System.Linq;
using Iwxxm.Net.Infrastructure.Mapping.CodeLists;
using Iwxxm.Net.Reports;
using static Iwxxm.Net.Infrastructure.GeoJson.GeoJsonProperties;

namespace Iwxxm.Net.Infrastructure.GeoJson.Features
{
    /// <summary>Builds the GeoJSON features of a Space Weather Advisory: one polygon per analysed region.</summary>
    internal static class SpaceWxFeatureBuilder
    {
        public static void AddSpaceWxFeatures(
            SpaceWx swx,
            string? bulletinId,
            List<GeoJsonFeature> features)
        {
            foreach (var region in swx.Analyses)
            {
                for (int i = 0; i < region.Polygons.Count; i++)
                {
                    var props = CreateBase(swx, "SPACE_WEATHER_REGION", bulletinId);
                    props.Add(("issuingSwxcName", swx.IssuingSwxcName));
                    props.Add(("advisoryNumber", swx.AdvisoryNumber));
                    props.Add(("timeIndicator", region.TimeIndicator));
                    props.Add(("phenomenonTimeUtc", FormatInstant(region.PhenomenonTimeUtc)));
                    props.Add(("intensity", region.Intensity));
                    if (region.LocationIndicators.Count > 0)
                    {
                        // Linked-Data output: the original code-list references are rebuilt from the Domain codes.
                        props.Add(("locationIndicatorUris", region.LocationIndicators
                            .Select(location => CodeListTables.SpaceWeatherLocations.ToReference(location)!)
                            .ToList()));
                    }
                    features.Add(GeoJsonFeature.ForPolygon($"{region.Id}#poly-{i + 1}", region.Polygons[i], props));
                }
            }
        }
    }
}
