using System.Collections.Generic;
using Iwxxm.Net.Infrastructure.Mapping.CodeLists;
using Iwxxm.Net.Reports;
using static Iwxxm.Net.Infrastructure.GeoJson.GeoJsonProperties;

namespace Iwxxm.Net.Infrastructure.GeoJson.Features
{
    /// <summary>Builds the GeoJSON features of volcanic reports: VONA volcanoes, QVACI volcanoes and ash concentration clouds, and Volcanic Ash Advisory volcanoes and ash clouds.</summary>
    internal static class VolcanicFeatureBuilder
    {
        public static void AddVonaFeatures(
            Vona vona,
            string? bulletinId,
            List<GeoJsonFeature> features)
        {
            foreach (var vol in vona.Volcanoes)
            {
                var props = CreateBase(vona, "VONA_VOLCANO", bulletinId);
                props.Add(("name", vol.Name));
                props.Add(("iavceiNumber", vol.IavceiNumber));
                AddVolcanoStatus(props, vol);
                props.Add(("noticeNumber", vona.NoticeNumber));
                props.Add(("currentColourCodeUri", CodeListTables.AviationColourCodes.ToReference(vona.CurrentColourCode)));
                props.Add(("previousColourCodeUri", CodeListTables.AviationColourCodes.ToReference(vona.PreviousColourCode)));
                AddVerticalLimit(props, "ashCloudElevation", vona.AshCloudElevation);
                features.Add(GeoJsonFeature.ForPoint(vol.Id, vol.Position, props));
            }
        }

        public static void AddQvaciFeatures(
            Qvaci qvaci,
            string? bulletinId,
            List<GeoJsonFeature> features)
        {
            foreach (var vol in qvaci.Volcanoes)
            {
                var props = CreateBase(qvaci, "VOLCANO", bulletinId);
                props.Add(("name", vol.Name));
                props.Add(("iavceiNumber", vol.IavceiNumber));
                AddVolcanoStatus(props, vol);
                props.Add(("originatingCentreName", qvaci.OriginatingCentreName));
                features.Add(GeoJsonFeature.ForPoint(vol.Id, vol.Position, props));
            }

            foreach (var cloud in qvaci.AshConcentrationClouds)
            {
                var props = CreateBase(qvaci, "ASH_CONCENTRATION_CLOUD", bulletinId);
                props.Add(("ashDensityEqualsOrExceedsMgPerM3", cloud.AshDensityEqualsOrExceedsMgPerM3));
                AddVerticalLimit(props, "lowerLimit", cloud.LowerLimit);
                AddVerticalLimit(props, "upperLimit", cloud.UpperLimit);
                props.Add(("interiorHoleCount", cloud.Area.InteriorRings.Count));
                props.Add(("phenomenonTimeUtc", FormatInstant(cloud.PhenomenonTimeUtc)));
                features.Add(GeoJsonFeature.ForPolygon(cloud.Id, cloud.Area, props));
            }
        }

        private static void AddVolcanoStatus(List<(string Key, object? Value)> props, Volcano vol)
        {
            props.Add(("activityStatus", vol.ActivityStatus?.ToString()));
            AddVerticalLimit(props, "sourceElevation", vol.SourceElevation);
        }

        public static void AddVaaFeatures(
            Vaa vaa,
            string? bulletinId,
            List<GeoJsonFeature> features)
        {
            if (vaa.Volcano != null)
            {
                var props = CreateBase(vaa, "VOLCANO", bulletinId);
                props.Add(("name", vaa.Volcano.Name));
                props.Add(("issuingVaacName", vaa.IssuingVaacName));
                props.Add(("advisoryNumber", vaa.AdvisoryNumber));
                AddLength(props, "sourceElevation", vaa.SourceElevation);
                features.Add(GeoJsonFeature.ForPoint(vaa.Volcano.Id, vaa.Volcano.Position, props));
            }

            if (vaa.Observation != null)
            {
                AddConditionsFeatures(vaa, vaa.Observation, "OBSERVED_ASH_CLOUD", "OBSERVED_ASH_CONDITIONS", bulletinId, features);
            }

            foreach (VolcanicAshConditions forecast in vaa.Forecasts)
            {
                AddConditionsFeatures(vaa, forecast, "FORECAST_ASH_CLOUD", "FORECAST_ASH_CONDITIONS", bulletinId, features);
            }
        }

        // One feature per ash cloud with its time and status; conditions without any cloud (e.g. NO_VOLCANIC_ASH_EXPECTED)
        // yield one feature without geometry so that the statement is not lost.
        private static void AddConditionsFeatures(
            Vaa vaa,
            VolcanicAshConditions conditions,
            string cloudFeatureType,
            string emptyFeatureType,
            string? bulletinId,
            List<GeoJsonFeature> features)
        {
            string? status = FormatStatus(conditions.Status);
            if (conditions.AshClouds.Count == 0)
            {
                var props = CreateBase(vaa, emptyFeatureType, bulletinId);
                props.Add(("advisoryNumber", vaa.AdvisoryNumber));
                props.Add(("phenomenonTimeUtc", FormatInstant(conditions.PhenomenonTimeUtc)));
                props.Add(("status", status));
                features.Add(GeoJsonFeature.WithoutGeometry(conditions.Id, props));
                return;
            }

            foreach (EnRoutePhenomenonVolume cloud in conditions.AshClouds)
            {
                if (cloud.Area == null) continue;
                var props = CreateBase(vaa, cloudFeatureType, bulletinId);
                props.Add(("advisoryNumber", vaa.AdvisoryNumber));
                props.Add(("phenomenonTimeUtc", FormatInstant(conditions.PhenomenonTimeUtc)));
                props.Add(("status", status));
                AddVerticalLimit(props, "lowerLimit", cloud.LowerLimit);
                AddVerticalLimit(props, "upperLimit", cloud.UpperLimit);
                props.Add(("directionOfMotionDegrees", cloud.DirectionOfMotionDegrees));
                AddSpeed(props, "speedOfMotion", cloud.SpeedOfMotion);
                features.Add(GeoJsonFeature.ForSurface(cloud.Id, cloud.Area, props));
            }
        }

        private static string? FormatStatus(VolcanicAshConditionsStatus? status) => status switch
        {
            VolcanicAshConditionsStatus.Provided => "PROVIDED",
            VolcanicAshConditionsStatus.NotProvided => "NOT_PROVIDED",
            VolcanicAshConditionsStatus.NotAvailable => "NOT_AVAILABLE",
            VolcanicAshConditionsStatus.NotIdentifiable => "NOT_IDENTIFIABLE",
            VolcanicAshConditionsStatus.NoVolcanicAshExpected => "NO_VOLCANIC_ASH_EXPECTED",
            _ => null
        };
    }
}
