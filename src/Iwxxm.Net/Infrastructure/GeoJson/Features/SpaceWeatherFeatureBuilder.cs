using System.Collections.Generic;
using Iwxxm.Net.Reports;
using static Iwxxm.Net.Infrastructure.GeoJson.GeoJsonProperties;

namespace Iwxxm.Net.Infrastructure.GeoJson.Features
{
    /// <summary>Builds the GeoJSON features of a Space Weather Advisory: one polygon per analysed region.</summary>
    internal static class SpaceWeatherFeatureBuilder
    {
        public static void AddSpaceWxFeatures(
            SpaceWeatherAdvisoryReport swx,
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
                    if (region.LocationIndicatorUris.Count > 0)
                    {
                        props.Add(("locationIndicatorUris", region.LocationIndicatorUris));
                    }
                    features.Add(GeoJsonFeature.ForPolygon($"{region.Id}#poly-{i + 1}", region.Polygons[i], props));
                }
            }
        }
    }
}
