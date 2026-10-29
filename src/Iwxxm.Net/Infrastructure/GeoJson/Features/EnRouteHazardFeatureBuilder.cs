using System.Collections.Generic;
using Iwxxm.Net.Reports;
using static Iwxxm.Net.Infrastructure.GeoJson.GeoJsonProperties;

namespace Iwxxm.Net.Infrastructure.GeoJson.Features
{
    /// <summary>Builds the GeoJSON features of SIGMET and AIRMET reports: phenomenon volumes, tropical cyclone centres and erupting volcanoes.</summary>
    internal static class EnRouteHazardFeatureBuilder
    {
        public static void AddSigmetFeatures(
            SigmetReport sigmet,
            string? bulletinId,
            List<GeoJsonFeature> features)
        {
            if (sigmet.EruptingVolcano != null)
            {
                var vProps = CreateBase(sigmet, "ERUPTING_VOLCANO", bulletinId);
                vProps.Add(("name", sigmet.EruptingVolcano.Name));
                vProps.Add(("wmoNumber", sigmet.EruptingVolcano.WmoNumber));
                vProps.Add(("firDesignator", sigmet.FirDesignator));
                vProps.Add(("sequenceNumber", sigmet.SequenceNumber));
                features.Add(GeoJsonFeature.ForPoint(sigmet.EruptingVolcano.Id, sigmet.EruptingVolcano.Position, vProps));
            }

            foreach (var vol in sigmet.EvolvingConditions)
            {
                AddVolumeFeatures(sigmet, "SIGMET_EVOLVING_VOLUME", sigmet.FirDesignator, sigmet.SequenceNumber, sigmet.PhenomenonUri, vol, bulletinId, features);
            }

            foreach (var vol in sigmet.ForecastPositions)
            {
                AddVolumeFeatures(sigmet, "SIGMET_FORECAST_VOLUME", sigmet.FirDesignator, sigmet.SequenceNumber, sigmet.PhenomenonUri, vol, bulletinId, features);
            }
        }

        public static void AddAirmetFeatures(
            AirmetReport airmet,
            string? bulletinId,
            List<GeoJsonFeature> features)
        {
            foreach (var vol in airmet.EvolvingConditions)
            {
                AddVolumeFeatures(airmet, "AIRMET_EVOLVING_VOLUME", airmet.FirDesignator, airmet.SequenceNumber, airmet.PhenomenonUri, vol, bulletinId, features);
            }
        }

        private static void AddVolumeFeatures(
            IIwxxmReport report,
            string featureType,
            string? firDesignator,
            string? sequenceNumber,
            string? phenomenonUri,
            EnRoutePhenomenonVolume vol,
            string? bulletinId,
            List<GeoJsonFeature> features)
        {
            if (vol.Area != null)
            {
                var props = CreateBase(report, featureType, bulletinId);
                props.Add(("firDesignator", firDesignator));
                props.Add(("sequenceNumber", sequenceNumber));
                props.Add(("phenomenonUri", phenomenonUri));
                AddVerticalLimit(props, "lowerLimit", vol.LowerLimit);
                AddVerticalLimit(props, "upperLimit", vol.UpperLimit);
                props.Add(("intensityChange", vol.IntensityChange));
                props.Add(("directionOfMotionDegrees", vol.DirectionOfMotionDegrees));
                AddSpeed(props, "speedOfMotion", vol.SpeedOfMotion);
                features.Add(GeoJsonFeature.ForPolygon(vol.Id, vol.Area, props));
            }

            if (vol.TropicalCycloneCentre.HasValue)
            {
                var tcProps = CreateBase(report, "TROPICAL_CYCLONE_CENTRE", bulletinId);
                tcProps.Add(("firDesignator", firDesignator));
                tcProps.Add(("sequenceNumber", sequenceNumber));
                tcProps.Add(("phenomenonUri", phenomenonUri));
                features.Add(GeoJsonFeature.ForPoint($"{vol.Id}#tc-centre", vol.TropicalCycloneCentre.Value, tcProps));
            }
        }
    }
}
