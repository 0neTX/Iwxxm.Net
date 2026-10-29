using System.Collections.Generic;
using Iwxxm.Net.Reports;
using static Iwxxm.Net.Infrastructure.GeoJson.GeoJsonProperties;

namespace Iwxxm.Net.Infrastructure.GeoJson.Features
{
    /// <summary>Builds the GeoJSON features of volcanic reports: VONA volcanoes, QVACI volcanoes and ash concentration clouds, and Volcanic Ash Advisory volcanoes and ash clouds.</summary>
    internal static class VolcanicFeatureBuilder
    {
        public static void AddVonaFeatures(
            VonaReport vona,
            string? bulletinId,
            List<GeoJsonFeature> features)
        {
            foreach (var vol in vona.Volcanoes)
            {
                var props = CreateBase(vona, "VONA_VOLCANO", bulletinId);
                props.Add(("name", vol.Name));
                props.Add(("wmoNumber", vol.WmoNumber));
                props.Add(("noticeNumber", vona.NoticeNumber));
                props.Add(("currentColourCodeUri", vona.CurrentColourCodeUri));
                props.Add(("previousColourCodeUri", vona.PreviousColourCodeUri));
                AddVerticalLimit(props, "ashCloudElevation", vona.AshCloudElevation);
                features.Add(GeoJsonFeature.ForPoint(vol.Id, vol.Position, props));
            }
        }

        public static void AddQvaciFeatures(
            QvaciReport qvaci,
            string? bulletinId,
            List<GeoJsonFeature> features)
        {
            foreach (var vol in qvaci.Volcanoes)
            {
                var props = CreateBase(qvaci, "VOLCANO", bulletinId);
                props.Add(("name", vol.Name));
                props.Add(("wmoNumber", vol.WmoNumber));
                props.Add(("originatingCentre", qvaci.OriginatingCentre));
                features.Add(GeoJsonFeature.ForPoint(vol.Id, vol.Position, props));
            }

            foreach (var cloud in qvaci.AshConcentrationClouds)
            {
                var props = CreateBase(qvaci, "ASH_CONCENTRATION_CLOUD", bulletinId);
                props.Add(("ashDensityMgPerM3", cloud.AshDensityMgPerM3));
                AddVerticalLimit(props, "lowerLimit", cloud.LowerLimit);
                AddVerticalLimit(props, "upperLimit", cloud.UpperLimit);
                props.Add(("interiorHoleCount", cloud.Area.InteriorRings.Count));
                features.Add(GeoJsonFeature.ForPolygon(cloud.Id, cloud.Area, props));
            }
        }

        public static void AddVaaFeatures(
            VolcanicAshAdvisoryReport vaa,
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

            foreach (var cloud in vaa.ObservedAshClouds)
            {
                if (cloud.Area == null) continue;
                var props = CreateBase(vaa, "OBSERVED_ASH_CLOUD", bulletinId);
                props.Add(("advisoryNumber", vaa.AdvisoryNumber));
                AddVerticalLimit(props, "lowerLimit", cloud.LowerLimit);
                AddVerticalLimit(props, "upperLimit", cloud.UpperLimit);
                props.Add(("directionOfMotionDegrees", cloud.DirectionOfMotionDegrees));
                AddSpeed(props, "speedOfMotion", cloud.SpeedOfMotion);
                features.Add(GeoJsonFeature.ForPolygon(cloud.Id, cloud.Area, props));
            }

            foreach (var cloud in vaa.ForecastAshClouds)
            {
                if (cloud.Area == null) continue;
                var props = CreateBase(vaa, "FORECAST_ASH_CLOUD", bulletinId);
                props.Add(("advisoryNumber", vaa.AdvisoryNumber));
                AddVerticalLimit(props, "lowerLimit", cloud.LowerLimit);
                AddVerticalLimit(props, "upperLimit", cloud.UpperLimit);
                features.Add(GeoJsonFeature.ForPolygon(cloud.Id, cloud.Area, props));
            }
        }
    }
}
