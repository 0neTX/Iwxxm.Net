using System;
using System.Collections.Generic;
using System.Linq;
using Iwxxm.Net.Infrastructure.Mapping.CodeLists;
using Iwxxm.Net.Reports;
using static Iwxxm.Net.Infrastructure.GeoJson.GeoJsonProperties;

namespace Iwxxm.Net.Infrastructure.GeoJson.Features
{
    /// <summary>Builds the GeoJSON features of SIGMET and AIRMET reports: phenomenon volumes, tropical cyclone centres and erupting volcanoes.</summary>
    internal static class EnRouteHazardFeatureBuilder
    {
        public static void AddSigmetFeatures(
            Sigmet sigmet,
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

            string? phenomenonUri = CodeListTables.SigmetPhenomena.ToReference(sigmet.Phenomenon);
            foreach (var vol in sigmet.EvolvingConditions)
            {
                AddVolumeFeatures(sigmet, "SIGMET_EVOLVING_VOLUME", sigmet.FirDesignator, sigmet.SequenceNumber, phenomenonUri, vol, bulletinId, features);
            }

            foreach (var vol in sigmet.ForecastPositions)
            {
                AddVolumeFeatures(sigmet, "SIGMET_FORECAST_VOLUME", sigmet.FirDesignator, sigmet.SequenceNumber, phenomenonUri, vol, bulletinId, features);
            }
        }

        public static void AddAirmetFeatures(
            Airmet airmet,
            string? bulletinId,
            List<GeoJsonFeature> features)
        {
            // A cancellation carries no evolving condition (AIRMET.AIRMET-3): it never yields an active hazard volume.
            if (airmet.IsCancelReport)
                return;

            string? phenomenonUri = CodeListTables.AirmetPhenomena.ToReference(airmet.Phenomenon);
            foreach (AirmetEvolvingCondition condition in airmet.EvolvingConditions)
            {
                AddVolumeFeatures(
                    airmet,
                    "AIRMET_EVOLVING_VOLUME",
                    airmet.FirDesignator,
                    airmet.SequenceNumber,
                    phenomenonUri,
                    condition.Volume,
                    bulletinId,
                    features,
                    props => AddAirmetContent(props, condition));
            }
        }

        private static void AddAirmetContent(List<(string Key, object? Value)> props, AirmetEvolvingCondition condition)
        {
            AddVerticalLimit(props, "cloudBase", condition.CloudBase);
            AddVerticalLimit(props, "cloudTop", condition.CloudTop);
            if (condition.IsCloudTopAbove.HasValue)
                props.Add(("cloudTopAbove", condition.IsCloudTopAbove.Value));
            if (condition.SurfaceVisibility.HasValue)
                AddLength(props, "surfaceVisibility", condition.SurfaceVisibility);
            if (condition.SurfaceVisibilityCauses.Count > 0)
            {
                IReadOnlyList<string> uris = condition.SurfaceVisibilityCauses
                    .Select(c => CodeListTables.VisibilityReductionCauses.ToReference(c) ?? c.Notation)
                    .ToList();
                props.Add(("surfaceVisibilityCauseUris", uris));
            }

            if (condition.SurfaceWindDirectionDegrees.HasValue)
                props.Add(("surfaceWindDirectionDegrees", condition.SurfaceWindDirectionDegrees));
            if (condition.SurfaceWindSpeed.HasValue)
                AddSpeed(props, "surfaceWindSpeed", condition.SurfaceWindSpeed);
        }

        // phenomenonUri: code-list reference rebuilt from the Domain phenomenon (Linked-Data output).
        private static void AddVolumeFeatures(
            IwxxmReport report,
            string featureType,
            string? firDesignator,
            string? sequenceNumber,
            string? phenomenonUri,
            EnRoutePhenomenonVolume vol,
            string? bulletinId,
            List<GeoJsonFeature> features,
            Action<List<(string Key, object? Value)>>? addSpecificContent = null)
        {
            if (vol.Area != null)
            {
                var props = CreateBase(report, featureType, bulletinId);
                props.Add(("firDesignator", firDesignator));
                props.Add(("sequenceNumber", sequenceNumber));
                props.Add(("phenomenonUri", phenomenonUri));
                props.Add(("timeIndicator", FormatTimeIndicator(vol.TimeIndicator)));
                props.Add(("phenomenonTimeUtc", FormatInstant(vol.PhenomenonTimeUtc)));
                AddVerticalLimit(props, "lowerLimit", vol.LowerLimit);
                AddVerticalLimit(props, "upperLimit", vol.UpperLimit);
                props.Add(("intensityChange", vol.IntensityChange));
                props.Add(("directionOfMotionDegrees", vol.DirectionOfMotionDegrees));
                AddSpeed(props, "speedOfMotion", vol.SpeedOfMotion);
                addSpecificContent?.Invoke(props);
                features.Add(GeoJsonFeature.ForSurface(vol.Id, vol.Area, props));
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
