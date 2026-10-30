using System.Collections.Generic;
using Iwxxm.Net.Reports;
using static Iwxxm.Net.Infrastructure.GeoJson.GeoJsonProperties;

namespace Iwxxm.Net.Infrastructure.GeoJson.Features
{
    /// <summary>Builds the GeoJSON features of a Tropical Cyclone Advisory: observed and forecast centres, observed cumulonimbus extents and the track line.</summary>
    internal static class TcaFeatureBuilder
    {
        public static void AddTcaFeatures(
            Tca tca,
            string? bulletinId,
            List<GeoJsonFeature> features)
        {
            var trackCoordinates = new List<GeoCoordinate>();

            if (tca.Observation != null)
            {
                trackCoordinates.Add(tca.Observation.Position);
                var props = CreateBase(tca, "TC_OBSERVATION", bulletinId);
                props.Add(("cycloneName", tca.CycloneName));
                props.Add(("advisoryNumber", tca.AdvisoryNumber));
                props.Add(("phenomenonTimeUtc", FormatInstant(tca.Observation.PhenomenonTimeUtc)));
                AddSpeed(props, "maxSurfaceWindSpeed", tca.Observation.MaxSurfaceWindSpeed);
                props.Add(("centralPressureHpa", tca.Observation.CentralPressureHpa));
                features.Add(GeoJsonFeature.ForPoint($"{tca.Id}#obs", tca.Observation.Position, props));
            }

            foreach (EnRoutePhenomenonVolume cbCloud in tca.CumulonimbusCloudLocations)
            {
                if (cbCloud.Area == null) continue;
                var props = CreateBase(tca, "TC_CUMULONIMBUS_CLOUD", bulletinId);
                props.Add(("cycloneName", tca.CycloneName));
                props.Add(("advisoryNumber", tca.AdvisoryNumber));
                AddVerticalLimit(props, "lowerLimit", cbCloud.LowerLimit);
                AddVerticalLimit(props, "upperLimit", cbCloud.UpperLimit);
                features.Add(GeoJsonFeature.ForSurface(cbCloud.Id, cbCloud.Area, props));
            }

            for (int i = 0; i < tca.Forecasts.Count; i++)
            {
                var fcst = tca.Forecasts[i];
                trackCoordinates.Add(fcst.Position);
                var props = CreateBase(tca, "TC_FORECAST", bulletinId);
                props.Add(("cycloneName", tca.CycloneName));
                props.Add(("advisoryNumber", tca.AdvisoryNumber));
                props.Add(("phenomenonTimeUtc", FormatInstant(fcst.PhenomenonTimeUtc)));
                AddSpeed(props, "maxSurfaceWindSpeed", fcst.MaxSurfaceWindSpeed);
                features.Add(GeoJsonFeature.ForPoint($"{tca.Id}#fcst-{i + 1}", fcst.Position, props));
            }

            if (trackCoordinates.Count >= 2)
            {
                var trackProps = CreateBase(tca, "TC_TRACK", bulletinId);
                trackProps.Add(("cycloneName", tca.CycloneName));
                trackProps.Add(("advisoryNumber", tca.AdvisoryNumber));
                features.Add(GeoJsonFeature.ForLineString($"{tca.Id}#track", trackCoordinates, trackProps));
            }
        }
    }
}
