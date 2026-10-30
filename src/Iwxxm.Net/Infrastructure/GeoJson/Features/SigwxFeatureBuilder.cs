using System.Collections.Generic;
using Iwxxm.Net.Reports;
using static Iwxxm.Net.Infrastructure.GeoJson.GeoJsonProperties;

namespace Iwxxm.Net.Infrastructure.GeoJson.Features
{
    /// <summary>Builds the GeoJSON features of a WAFS significant weather (SIGWX) forecast: jet streams and core points, clouds, turbulence, icing, tropopause and point phenomena.</summary>
    internal static class SigwxFeatureBuilder
    {
        public static void AddSigwxFeatures(
            Sigwx sigwx,
            string? bulletinId,
            List<GeoJsonFeature> features)
        {
            foreach (var js in sigwx.JetStreams)
            {
                var props = CreateBase(sigwx, "JETSTREAM", bulletinId);
                AddVerticalLimit(props, "coreLevel", js.CoreLevel);
                AddSpeed(props, "maxWindSpeed", js.MaxWindSpeed);
                features.Add(GeoJsonFeature.ForLineString(js.Id, js.AxisTrajectory, props));

                for (int i = 0; i < js.CorePoints.Count; i++)
                {
                    var cp = js.CorePoints[i];
                    var cpProps = CreateBase(sigwx, "JETSTREAM_CORE_POINT", bulletinId);
                    cpProps.Add(("parentJetStreamId", js.Id));
                    AddVerticalLimit(cpProps, "level", cp.Level);
                    AddSpeed(cpProps, "windSpeed", cp.WindSpeed);
                    features.Add(GeoJsonFeature.ForPoint($"{js.Id}#core-{i + 1}", cp.Location, cpProps));
                }
            }

            foreach (var cloud in sigwx.Clouds)
            {
                var props = CreateBase(sigwx, "CLOUD", bulletinId);
                props.Add(("cloudAmountUri", cloud.CloudAmount));
                props.Add(("distributionCode", cloud.DistributionCode.ToString()));
                props.Add(("distributionNumericCode", cloud.DistributionNumericCode));
                props.Add(("cloudTypeUri", cloud.CloudType));
                props.Add(("typeCode", cloud.TypeCode.ToString()));
                props.Add(("typeNumericCode", cloud.TypeNumericCode));
                AddVerticalLimit(props, "base", cloud.Base);
                AddVerticalLimit(props, "top", cloud.Top);
                features.Add(GeoJsonFeature.ForPolygon(cloud.Id, cloud.Area, props));
            }

            foreach (var turb in sigwx.Turbulences)
            {
                var props = CreateBase(sigwx, "TURBULENCE", bulletinId);
                props.Add(("degreeUri", turb.RawDegreeReference));
                props.Add(("degree", turb.Degree.ToString()));
                props.Add(("degreeNumericCode", turb.DegreeNumericCode));
                AddVerticalLimit(props, "base", turb.Base);
                AddVerticalLimit(props, "top", turb.Top);
                features.Add(GeoJsonFeature.ForPolygon(turb.Id, turb.Area, props));
            }

            foreach (var icing in sigwx.AirframeIcings)
            {
                var props = CreateBase(sigwx, "AIRFRAME_ICING", bulletinId);
                props.Add(("degreeUri", icing.RawDegreeReference));
                props.Add(("degree", icing.Degree.ToString()));
                props.Add(("degreeNumericCode", icing.DegreeNumericCode));
                AddVerticalLimit(props, "base", icing.Base);
                AddVerticalLimit(props, "top", icing.Top);
                features.Add(GeoJsonFeature.ForPolygon(icing.Id, icing.Area, props));
            }

            foreach (var trop in sigwx.Tropopauses)
            {
                if (trop.ContourPolygon != null)
                {
                    var props = CreateBase(sigwx, "TROPOPAUSE_CONTOUR", bulletinId);
                    props.Add(("levelModification", trop.LevelType));
                    features.Add(GeoJsonFeature.ForPolygon(trop.Id, trop.ContourPolygon, props));
                }

                for (int i = 0; i < trop.SpotHeights.Count; i++)
                {
                    var sh = trop.SpotHeights[i];
                    var props = CreateBase(sigwx, "TROPOPAUSE_SPOT_HEIGHT", bulletinId);
                    props.Add(("levelModification", trop.LevelType));
                    AddVerticalLimit(props, "level", sh.Level);
                    features.Add(GeoJsonFeature.ForPoint($"{trop.Id}#spot-{i + 1}", sh.Location, props));
                }
            }

            foreach (var vol in sigwx.Volcanoes)
            {
                var props = CreateBase(sigwx, "VOLCANO", bulletinId);
                props.Add(("name", vol.Name));
                props.Add(("wmoNumber", vol.WmoNumber));
                features.Add(GeoJsonFeature.ForPoint(vol.Id, vol.Position, props));
            }

            foreach (var tc in sigwx.TropicalCyclones)
            {
                var props = CreateBase(sigwx, "TROPICAL_CYCLONE", bulletinId);
                props.Add(("name", tc.Name));
                features.Add(GeoJsonFeature.ForPoint(tc.Id, tc.Position, props));
            }

            foreach (var ss in sigwx.Sandstorms)
            {
                var props = CreateBase(sigwx, "SANDSTORM", bulletinId);
                features.Add(GeoJsonFeature.ForPoint(ss.Id, ss.Position, props));
            }

            foreach (var rad in sigwx.RadiationIncidents)
            {
                var props = CreateBase(sigwx, "RADIATION_INCIDENT", bulletinId);
                props.Add(("siteName", rad.SiteName));
                features.Add(GeoJsonFeature.ForPoint(rad.Id, rad.Position, props));
            }
        }
    }
}
