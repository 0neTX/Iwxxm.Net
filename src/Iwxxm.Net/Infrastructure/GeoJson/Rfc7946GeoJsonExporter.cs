using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Iwxxm.Net.Application.Ports;
using Iwxxm.Net.Domain.Aggregates;
using Iwxxm.Net.Domain.ValueObjects;

namespace Iwxxm.Net.Infrastructure.GeoJson
{
    /// <summary>
    /// Zero-dependency RFC 7946 GeoJSON exporter for all ICAO/WMO IWXXM 2025-2 Domain Aggregate Roots.
    /// Translates Domain spatial primitives (<see cref="GeoCoordinate"/>, <see cref="GeoPolygon"/> with interior holes,
    /// <see cref="FlightLevel"/>) into RFC 7946 <c>FeatureCollection</c> JSON with <c>[longitude, latitude]</c> axis order.
    /// </summary>
    internal sealed class Rfc7946GeoJsonExporter : IGeoJsonExporter
    {
        public string Export(IIwxxmReport report, GeoJsonExportOptions? options = null)
        {
            if (report == null)
                throw new ArgumentNullException(nameof(report));

            var effectiveOptions = options ?? GeoJsonExportOptions.Default;
            var features = new List<GeoJsonFeatureEntry>();
            var bbox = new BoundingBoxTracker();

            CollectReportFeatures(report, null, features, bbox);

            var sb = new StringBuilder(Math.Max(512, features.Count * 256));
            var writer = new Rfc8259JsonWriter(sb, effectiveOptions.Indented);

            writer.BeginObject();
            writer.WriteProperty("type", "FeatureCollection");

            if (effectiveOptions.IncludeBoundingBox && bbox.HasCoordinates)
            {
                writer.WritePropertyName("bbox");
                writer.BeginInlineArray();
                writer.WriteInlineNumber(bbox.MinLon);
                writer.WriteInlineNumber(bbox.MinLat);
                writer.WriteInlineNumber(bbox.MaxLon);
                writer.WriteInlineNumber(bbox.MaxLat);
                writer.EndInlineArray();
            }

            writer.WritePropertyName("metadata");
            writer.BeginObject();
            writer.WriteProperty("id", report.Id);
            writer.WriteProperty("reportType", report.ReportType.ToString());
            writer.WriteProperty("issueTimeUtc", report.IssueTimeUtc.ToString("O", CultureInfo.InvariantCulture));
            writer.WriteProperty("reportStatus", report.ReportStatus);
            writer.WriteProperty("permissibleUsage", report.PermissibleUsage);
            if (report is MeteorologicalBulletin bulletin)
            {
                writer.WriteProperty("bulletinIdentifier", bulletin.BulletinIdentifier);
                writer.WriteProperty("reportCount", bulletin.Reports.Count);
            }
            writer.EndObject();

            writer.WritePropertyName("features");
            writer.BeginArray();
            for (int i = 0; i < features.Count; i++)
            {
                WriteFeature(writer, features[i]);
            }
            writer.EndArray();
            writer.EndObject();

            return sb.ToString();
        }

        private static void CollectReportFeatures(
            IIwxxmReport report,
            string? parentBulletinIdentifier,
            List<GeoJsonFeatureEntry> features,
            BoundingBoxTracker bbox)
        {
            switch (report)
            {
                case MeteorologicalBulletin bulletin:
                    foreach (var child in bulletin.Reports)
                    {
                        CollectReportFeatures(child, bulletin.BulletinIdentifier, features, bbox);
                    }
                    break;

                case SignificantWeatherReport sigwx:
                    CollectSigwxFeatures(sigwx, parentBulletinIdentifier, features, bbox);
                    break;

                case AerodromeWeatherReport metarOrSpeci:
                    CollectAerodromeObservationFeature(metarOrSpeci, parentBulletinIdentifier, features, bbox);
                    break;

                case TafReport taf:
                    CollectTafFeature(taf, parentBulletinIdentifier, features, bbox);
                    break;

                case SigmetReport sigmet:
                    CollectSigmetFeatures(sigmet, parentBulletinIdentifier, features, bbox);
                    break;

                case AirmetReport airmet:
                    CollectAirmetFeatures(airmet, parentBulletinIdentifier, features, bbox);
                    break;

                case VonaReport vona:
                    CollectVonaFeatures(vona, parentBulletinIdentifier, features, bbox);
                    break;

                case QvaciReport qvaci:
                    CollectQvaciFeatures(qvaci, parentBulletinIdentifier, features, bbox);
                    break;

                case VolcanicAshAdvisoryReport vaa:
                    CollectVaaFeatures(vaa, parentBulletinIdentifier, features, bbox);
                    break;

                case TropicalCycloneAdvisoryReport tca:
                    CollectTcaFeatures(tca, parentBulletinIdentifier, features, bbox);
                    break;

                case SpaceWeatherAdvisoryReport swx:
                    CollectSpaceWxFeatures(swx, parentBulletinIdentifier, features, bbox);
                    break;
            }
        }

        private static void CollectSigwxFeatures(
            SignificantWeatherReport sigwx,
            string? bulletinId,
            List<GeoJsonFeatureEntry> features,
            BoundingBoxTracker bbox)
        {
            foreach (var js in sigwx.JetStreams)
            {
                var props = CreateBaseProperties(sigwx, "JETSTREAM", bulletinId);
                props.Add(("coreFlightLevel", js.CoreFlightLevel.ToString()));
                props.Add(("coreFlightLevelHundredFeet", js.CoreFlightLevel.Value));
                props.Add(("maxWindSpeedKnots", Math.Round(js.MaxWindSpeedKnots, 2)));
                features.Add(GeoJsonFeatureEntry.CreateLineString(js.Id, js.AxisTrajectory, props, bbox));

                for (int i = 0; i < js.CorePoints.Count; i++)
                {
                    var cp = js.CorePoints[i];
                    var cpProps = CreateBaseProperties(sigwx, "JETSTREAM_CORE_POINT", bulletinId);
                    cpProps.Add(("parentJetStreamId", js.Id));
                    cpProps.Add(("flightLevel", cp.FlightLevel.ToString()));
                    cpProps.Add(("windSpeedKnots", Math.Round(cp.WindSpeedKnots, 2)));
                    features.Add(GeoJsonFeatureEntry.CreatePoint($"{js.Id}#core-{i + 1}", cp.Location, cpProps, bbox));
                }
            }

            foreach (var cloud in sigwx.Clouds)
            {
                var props = CreateBaseProperties(sigwx, "CLOUD", bulletinId);
                props.Add(("cloudAmountUri", cloud.CloudAmount));
                props.Add(("distributionCode", cloud.DistributionCode.ToString()));
                props.Add(("cloudTypeUri", cloud.CloudType));
                props.Add(("typeCode", cloud.TypeCode.ToString()));
                props.Add(("baseFlightLevel", cloud.BaseFlightLevel?.ToString()));
                props.Add(("topFlightLevel", cloud.TopFlightLevel.ToString()));
                features.Add(GeoJsonFeatureEntry.CreatePolygon(cloud.Id, cloud.Area, props, bbox));
            }

            foreach (var turb in sigwx.Turbulences)
            {
                var props = CreateBaseProperties(sigwx, "TURBULENCE", bulletinId);
                props.Add(("degreeUri", turb.DegreeOfTurbulenceUri));
                props.Add(("degree", turb.Degree.ToString()));
                props.Add(("baseFlightLevel", turb.BaseFlightLevel?.ToString()));
                props.Add(("topFlightLevel", turb.TopFlightLevel.ToString()));
                features.Add(GeoJsonFeatureEntry.CreatePolygon(turb.Id, turb.Area, props, bbox));
            }

            foreach (var icing in sigwx.AirframeIcings)
            {
                var props = CreateBaseProperties(sigwx, "AIRFRAME_ICING", bulletinId);
                props.Add(("degreeUri", icing.DegreeOfIcingUri));
                props.Add(("degree", icing.Degree.ToString()));
                props.Add(("baseFlightLevel", icing.BaseFlightLevel?.ToString()));
                props.Add(("topFlightLevel", icing.TopFlightLevel.ToString()));
                features.Add(GeoJsonFeatureEntry.CreatePolygon(icing.Id, icing.Area, props, bbox));
            }

            foreach (var trop in sigwx.Tropopauses)
            {
                if (trop.ContourPolygon != null)
                {
                    var props = CreateBaseProperties(sigwx, "TROPOPAUSE_CONTOUR", bulletinId);
                    props.Add(("levelModification", trop.LevelType));
                    features.Add(GeoJsonFeatureEntry.CreatePolygon(trop.Id, trop.ContourPolygon, props, bbox));
                }

                for (int i = 0; i < trop.SpotHeights.Count; i++)
                {
                    var sh = trop.SpotHeights[i];
                    var props = CreateBaseProperties(sigwx, "TROPOPAUSE_SPOT_HEIGHT", bulletinId);
                    props.Add(("levelModification", trop.LevelType));
                    props.Add(("flightLevel", sh.FlightLevel.ToString()));
                    props.Add(("flightLevelHundredFeet", sh.FlightLevel.Value));
                    features.Add(GeoJsonFeatureEntry.CreatePoint($"{trop.Id}#spot-{i + 1}", sh.Location, props, bbox));
                }
            }

            foreach (var vol in sigwx.Volcanoes)
            {
                var props = CreateBaseProperties(sigwx, "VOLCANO", bulletinId);
                props.Add(("name", vol.Name));
                props.Add(("wmoNumber", vol.WmoNumber));
                features.Add(GeoJsonFeatureEntry.CreatePoint(vol.Id, vol.Position, props, bbox));
            }

            foreach (var tc in sigwx.TropicalCyclones)
            {
                var props = CreateBaseProperties(sigwx, "TROPICAL_CYCLONE", bulletinId);
                props.Add(("name", tc.Name));
                features.Add(GeoJsonFeatureEntry.CreatePoint(tc.Id, tc.Position, props, bbox));
            }

            foreach (var ss in sigwx.Sandstorms)
            {
                var props = CreateBaseProperties(sigwx, "SANDSTORM", bulletinId);
                features.Add(GeoJsonFeatureEntry.CreatePoint(ss.Id, ss.Position, props, bbox));
            }

            foreach (var rad in sigwx.RadiationIncidents)
            {
                var props = CreateBaseProperties(sigwx, "RADIATION_INCIDENT", bulletinId);
                props.Add(("siteName", rad.SiteName));
                features.Add(GeoJsonFeatureEntry.CreatePoint(rad.Id, rad.Position, props, bbox));
            }
        }

        private static void CollectAerodromeObservationFeature(
            AerodromeWeatherReport report,
            string? bulletinId,
            List<GeoJsonFeatureEntry> features,
            BoundingBoxTracker bbox)
        {
            var props = CreateBaseProperties(report, report.ReportType == IwxxmReportType.Speci ? "SPECI_OBSERVATION" : "METAR_OBSERVATION", bulletinId);
            props.Add(("aerodromeDesignator", report.AerodromeDesignator));
            props.Add(("aerodromeName", report.AerodromeName));
            props.Add(("observationTimeUtc", report.ObservationTimeUtc.ToString("O", CultureInfo.InvariantCulture)));
            props.Add(("isAutomatedStation", report.IsAutomatedStation));
            props.Add(("isNilObservation", report.IsNilObservation));
            props.Add(("cloudAndVisibilityOk", report.CloudAndVisibilityOk));
            props.Add(("airTemperatureCelsius", report.AirTemperatureCelsius));
            props.Add(("dewpointTemperatureCelsius", report.DewpointTemperatureCelsius));
            props.Add(("qnhHpa", report.QnhHpa));
            props.Add(("prevailingVisibilityMeters", report.PrevailingVisibilityMeters));
            if (report.SurfaceWind != null)
            {
                props.Add(("windDirectionDegrees", report.SurfaceWind.MeanDirectionDegrees));
                props.Add(("windSpeedMetersPerSecond", report.SurfaceWind.MeanSpeedMetersPerSecond));
                props.Add(("windGustMetersPerSecond", report.SurfaceWind.GustSpeedMetersPerSecond));
            }
            if (report.PresentWeatherUris.Count > 0)
            {
                props.Add(("presentWeatherUris", report.PresentWeatherUris));
            }

            features.Add(report.AerodromePosition.HasValue
                ? GeoJsonFeatureEntry.CreatePoint(report.Id, report.AerodromePosition.Value, props, bbox)
                : GeoJsonFeatureEntry.CreateNullGeometry(report.Id, props));
        }

        private static void CollectTafFeature(
            TafReport taf,
            string? bulletinId,
            List<GeoJsonFeatureEntry> features,
            BoundingBoxTracker bbox)
        {
            var props = CreateBaseProperties(taf, "TAF_FORECAST", bulletinId);
            props.Add(("aerodromeDesignator", taf.AerodromeDesignator));
            props.Add(("aerodromeName", taf.AerodromeName));
            props.Add(("validFromUtc", taf.ValidFromUtc?.ToString("O", CultureInfo.InvariantCulture)));
            props.Add(("validUntilUtc", taf.ValidUntilUtc?.ToString("O", CultureInfo.InvariantCulture)));
            props.Add(("isCancelReport", taf.IsCancelReport));
            props.Add(("isNilForecast", taf.IsNilForecast));
            props.Add(("changeForecastCount", taf.ChangeForecasts.Count));

            features.Add(taf.AerodromePosition.HasValue
                ? GeoJsonFeatureEntry.CreatePoint(taf.Id, taf.AerodromePosition.Value, props, bbox)
                : GeoJsonFeatureEntry.CreateNullGeometry(taf.Id, props));
        }

        private static void CollectSigmetFeatures(
            SigmetReport sigmet,
            string? bulletinId,
            List<GeoJsonFeatureEntry> features,
            BoundingBoxTracker bbox)
        {
            if (sigmet.EruptingVolcano != null)
            {
                var vProps = CreateBaseProperties(sigmet, "ERUPTING_VOLCANO", bulletinId);
                vProps.Add(("name", sigmet.EruptingVolcano.Name));
                vProps.Add(("wmoNumber", sigmet.EruptingVolcano.WmoNumber));
                vProps.Add(("firDesignator", sigmet.FirDesignator));
                vProps.Add(("sequenceNumber", sigmet.SequenceNumber));
                features.Add(GeoJsonFeatureEntry.CreatePoint(sigmet.EruptingVolcano.Id, sigmet.EruptingVolcano.Position, vProps, bbox));
            }

            foreach (var vol in sigmet.EvolvingConditions)
            {
                AddEnRouteVolumeFeatures(sigmet, "SIGMET_EVOLVING_VOLUME", sigmet.FirDesignator, sigmet.SequenceNumber, sigmet.PhenomenonUri, vol, bulletinId, features, bbox);
            }

            foreach (var vol in sigmet.ForecastPositions)
            {
                AddEnRouteVolumeFeatures(sigmet, "SIGMET_FORECAST_VOLUME", sigmet.FirDesignator, sigmet.SequenceNumber, sigmet.PhenomenonUri, vol, bulletinId, features, bbox);
            }
        }

        private static void CollectAirmetFeatures(
            AirmetReport airmet,
            string? bulletinId,
            List<GeoJsonFeatureEntry> features,
            BoundingBoxTracker bbox)
        {
            foreach (var vol in airmet.EvolvingConditions)
            {
                AddEnRouteVolumeFeatures(airmet, "AIRMET_EVOLVING_VOLUME", airmet.FirDesignator, airmet.SequenceNumber, airmet.PhenomenonUri, vol, bulletinId, features, bbox);
            }
        }

        private static void AddEnRouteVolumeFeatures(
            IIwxxmReport report,
            string featureType,
            string? firDesignator,
            string? sequenceNumber,
            string? phenomenonUri,
            EnRoutePhenomenonVolume vol,
            string? bulletinId,
            List<GeoJsonFeatureEntry> features,
            BoundingBoxTracker bbox)
        {
            if (vol.Area != null)
            {
                var props = CreateBaseProperties(report, featureType, bulletinId);
                props.Add(("firDesignator", firDesignator));
                props.Add(("sequenceNumber", sequenceNumber));
                props.Add(("phenomenonUri", phenomenonUri));
                props.Add(("lowerLimit", vol.LowerLimit?.ToString()));
                props.Add(("upperLimit", vol.UpperLimit?.ToString()));
                props.Add(("intensityChange", vol.IntensityChange));
                props.Add(("directionOfMotionDegrees", vol.DirectionOfMotionDegrees));
                props.Add(("speedOfMotionKnots", vol.SpeedOfMotionKnots));
                features.Add(GeoJsonFeatureEntry.CreatePolygon(vol.Id, vol.Area, props, bbox));
            }

            if (vol.TropicalCycloneCentre.HasValue)
            {
                var tcProps = CreateBaseProperties(report, "TROPICAL_CYCLONE_CENTRE", bulletinId);
                tcProps.Add(("firDesignator", firDesignator));
                tcProps.Add(("sequenceNumber", sequenceNumber));
                tcProps.Add(("phenomenonUri", phenomenonUri));
                features.Add(GeoJsonFeatureEntry.CreatePoint($"{vol.Id}#tc-centre", vol.TropicalCycloneCentre.Value, tcProps, bbox));
            }
        }

        private static void CollectVonaFeatures(
            VonaReport vona,
            string? bulletinId,
            List<GeoJsonFeatureEntry> features,
            BoundingBoxTracker bbox)
        {
            foreach (var vol in vona.Volcanoes)
            {
                var props = CreateBaseProperties(vona, "VONA_VOLCANO", bulletinId);
                props.Add(("name", vol.Name));
                props.Add(("wmoNumber", vol.WmoNumber));
                props.Add(("noticeNumber", vona.NoticeNumber));
                props.Add(("currentColourCodeUri", vona.CurrentColourCodeUri));
                props.Add(("previousColourCodeUri", vona.PreviousColourCodeUri));
                props.Add(("ashCloudElevation", vona.AshCloudElevation?.ToString()));
                features.Add(GeoJsonFeatureEntry.CreatePoint(vol.Id, vol.Position, props, bbox));
            }
        }

        private static void CollectQvaciFeatures(
            QvaciReport qvaci,
            string? bulletinId,
            List<GeoJsonFeatureEntry> features,
            BoundingBoxTracker bbox)
        {
            foreach (var vol in qvaci.Volcanoes)
            {
                var props = CreateBaseProperties(qvaci, "VOLCANO", bulletinId);
                props.Add(("name", vol.Name));
                props.Add(("wmoNumber", vol.WmoNumber));
                props.Add(("originatingCentre", qvaci.OriginatingCentre));
                features.Add(GeoJsonFeatureEntry.CreatePoint(vol.Id, vol.Position, props, bbox));
            }

            foreach (var cloud in qvaci.AshConcentrationClouds)
            {
                var props = CreateBaseProperties(qvaci, "ASH_CONCENTRATION_CLOUD", bulletinId);
                props.Add(("ashDensityMgPerM3", cloud.AshDensityMgPerM3));
                props.Add(("lowerLimit", cloud.LowerLimit?.ToString()));
                props.Add(("upperLimit", cloud.UpperLimit?.ToString()));
                props.Add(("interiorHoleCount", cloud.Area.InteriorRings.Count));
                features.Add(GeoJsonFeatureEntry.CreatePolygon(cloud.Id, cloud.Area, props, bbox));
            }
        }

        private static void CollectVaaFeatures(
            VolcanicAshAdvisoryReport vaa,
            string? bulletinId,
            List<GeoJsonFeatureEntry> features,
            BoundingBoxTracker bbox)
        {
            if (vaa.Volcano != null)
            {
                var props = CreateBaseProperties(vaa, "VOLCANO", bulletinId);
                props.Add(("name", vaa.Volcano.Name));
                props.Add(("issuingVaacName", vaa.IssuingVaacName));
                props.Add(("advisoryNumber", vaa.AdvisoryNumber));
                props.Add(("sourceElevationMeters", vaa.SourceElevationMeters));
                features.Add(GeoJsonFeatureEntry.CreatePoint(vaa.Volcano.Id, vaa.Volcano.Position, props, bbox));
            }

            foreach (var cloud in vaa.ObservedAshClouds)
            {
                if (cloud.Area == null) continue;
                var props = CreateBaseProperties(vaa, "OBSERVED_ASH_CLOUD", bulletinId);
                props.Add(("advisoryNumber", vaa.AdvisoryNumber));
                props.Add(("lowerLimit", cloud.LowerLimit?.ToString()));
                props.Add(("upperLimit", cloud.UpperLimit?.ToString()));
                props.Add(("directionOfMotionDegrees", cloud.DirectionOfMotionDegrees));
                props.Add(("speedOfMotionKnots", cloud.SpeedOfMotionKnots));
                features.Add(GeoJsonFeatureEntry.CreatePolygon(cloud.Id, cloud.Area, props, bbox));
            }

            foreach (var cloud in vaa.ForecastAshClouds)
            {
                if (cloud.Area == null) continue;
                var props = CreateBaseProperties(vaa, "FORECAST_ASH_CLOUD", bulletinId);
                props.Add(("advisoryNumber", vaa.AdvisoryNumber));
                props.Add(("lowerLimit", cloud.LowerLimit?.ToString()));
                props.Add(("upperLimit", cloud.UpperLimit?.ToString()));
                features.Add(GeoJsonFeatureEntry.CreatePolygon(cloud.Id, cloud.Area, props, bbox));
            }
        }

        private static void CollectTcaFeatures(
            TropicalCycloneAdvisoryReport tca,
            string? bulletinId,
            List<GeoJsonFeatureEntry> features,
            BoundingBoxTracker bbox)
        {
            var trackCoordinates = new List<GeoCoordinate>();

            if (tca.Observation != null)
            {
                trackCoordinates.Add(tca.Observation.Position);
                var props = CreateBaseProperties(tca, "TC_OBSERVATION", bulletinId);
                props.Add(("cycloneName", tca.CycloneName));
                props.Add(("advisoryNumber", tca.AdvisoryNumber));
                props.Add(("phenomenonTimeUtc", tca.Observation.PhenomenonTimeUtc?.ToString("O", CultureInfo.InvariantCulture)));
                props.Add(("maxSurfaceWindSpeedMetersPerSecond", tca.Observation.MaxSurfaceWindSpeedMetersPerSecond));
                props.Add(("centralPressureHpa", tca.Observation.CentralPressureHpa));
                features.Add(GeoJsonFeatureEntry.CreatePoint($"{tca.Id}#obs", tca.Observation.Position, props, bbox));
            }

            for (int i = 0; i < tca.Forecasts.Count; i++)
            {
                var fcst = tca.Forecasts[i];
                trackCoordinates.Add(fcst.Position);
                var props = CreateBaseProperties(tca, "TC_FORECAST", bulletinId);
                props.Add(("cycloneName", tca.CycloneName));
                props.Add(("advisoryNumber", tca.AdvisoryNumber));
                props.Add(("phenomenonTimeUtc", fcst.PhenomenonTimeUtc?.ToString("O", CultureInfo.InvariantCulture)));
                props.Add(("maxSurfaceWindSpeedMetersPerSecond", fcst.MaxSurfaceWindSpeedMetersPerSecond));
                features.Add(GeoJsonFeatureEntry.CreatePoint($"{tca.Id}#fcst-{i + 1}", fcst.Position, props, bbox));
            }

            if (trackCoordinates.Count >= 2)
            {
                var trackProps = CreateBaseProperties(tca, "TC_TRACK", bulletinId);
                trackProps.Add(("cycloneName", tca.CycloneName));
                trackProps.Add(("advisoryNumber", tca.AdvisoryNumber));
                features.Add(GeoJsonFeatureEntry.CreateLineString($"{tca.Id}#track", trackCoordinates, trackProps, bbox));
            }
        }

        private static void CollectSpaceWxFeatures(
            SpaceWeatherAdvisoryReport swx,
            string? bulletinId,
            List<GeoJsonFeatureEntry> features,
            BoundingBoxTracker bbox)
        {
            foreach (var region in swx.Analyses)
            {
                for (int i = 0; i < region.Polygons.Count; i++)
                {
                    var props = CreateBaseProperties(swx, "SPACE_WEATHER_REGION", bulletinId);
                    props.Add(("issuingSwxcName", swx.IssuingSwxcName));
                    props.Add(("advisoryNumber", swx.AdvisoryNumber));
                    props.Add(("timeIndicator", region.TimeIndicator));
                    props.Add(("phenomenonTimeUtc", region.PhenomenonTimeUtc?.ToString("O", CultureInfo.InvariantCulture)));
                    props.Add(("intensity", region.Intensity));
                    if (region.LocationIndicatorUris.Count > 0)
                    {
                        props.Add(("locationIndicatorUris", region.LocationIndicatorUris));
                    }
                    features.Add(GeoJsonFeatureEntry.CreatePolygon($"{region.Id}#poly-{i + 1}", region.Polygons[i], props, bbox));
                }
            }
        }

        private static List<(string Key, object? Value)> CreateBaseProperties(
            IIwxxmReport report,
            string featureType,
            string? bulletinId)
        {
            var list = new List<(string Key, object? Value)>(12)
            {
                ("featureType", featureType),
                ("reportId", report.Id),
                ("reportType", report.ReportType.ToString()),
                ("issueTimeUtc", report.IssueTimeUtc.ToString("O", CultureInfo.InvariantCulture))
            };

            if (!string.IsNullOrWhiteSpace(bulletinId))
            {
                list.Add(("bulletinIdentifier", bulletinId));
            }

            return list;
        }

        private static void WriteFeature(Rfc8259JsonWriter writer, GeoJsonFeatureEntry feature)
        {
            writer.BeginObject();
            writer.WriteProperty("type", "Feature");
            if (!string.IsNullOrWhiteSpace(feature.Id))
            {
                writer.WriteProperty("id", feature.Id);
            }

            writer.WritePropertyName("geometry");
            if (feature.GeometryType == null)
            {
                writer.WriteNullValue();
            }
            else
            {
                writer.BeginObject();
                writer.WriteProperty("type", feature.GeometryType);
                writer.WritePropertyName("coordinates");
                if (feature.GeometryType == "Point" && feature.PointCoordinate.HasValue)
                {
                    writer.WriteCoordinatePair(feature.PointCoordinate.Value);
                }
                else if (feature.GeometryType == "LineString" && feature.LineCoordinates != null)
                {
                    writer.WriteCoordinateSequence(feature.LineCoordinates);
                }
                else if (feature.GeometryType == "Polygon" && feature.Polygon != null)
                {
                    writer.BeginArray();
                    writer.WriteCoordinateRingElement(feature.Polygon.ExteriorRing);
                    for (int h = 0; h < feature.Polygon.InteriorRings.Count; h++)
                    {
                        writer.WriteCoordinateRingElement(feature.Polygon.InteriorRings[h]);
                    }
                    writer.EndArray();
                }
                writer.EndObject();
            }

            writer.WritePropertyName("properties");
            writer.BeginObject();
            for (int i = 0; i < feature.Properties.Count; i++)
            {
                var prop = feature.Properties[i];
                if (prop.Value != null)
                {
                    writer.WriteDynamicProperty(prop.Key, prop.Value);
                }
            }
            writer.EndObject();
            writer.EndObject();
        }

        private sealed class BoundingBoxTracker
        {
            public bool HasCoordinates { get; private set; }
            public double MinLon { get; private set; } = double.MaxValue;
            public double MinLat { get; private set; } = double.MaxValue;
            public double MaxLon { get; private set; } = double.MinValue;
            public double MaxLat { get; private set; } = double.MinValue;

            public void Include(GeoCoordinate coord)
            {
                HasCoordinates = true;
                if (coord.Longitude < MinLon) MinLon = coord.Longitude;
                if (coord.Longitude > MaxLon) MaxLon = coord.Longitude;
                if (coord.Latitude < MinLat) MinLat = coord.Latitude;
                if (coord.Latitude > MaxLat) MaxLat = coord.Latitude;
            }

            public void IncludeRange(IEnumerable<GeoCoordinate> coords)
            {
                foreach (var c in coords)
                {
                    Include(c);
                }
            }
        }

        private sealed class GeoJsonFeatureEntry
        {
            public string Id { get; }
            public string? GeometryType { get; }
            public GeoCoordinate? PointCoordinate { get; }
            public IReadOnlyList<GeoCoordinate>? LineCoordinates { get; }
            public GeoPolygon? Polygon { get; }
            public List<(string Key, object? Value)> Properties { get; }

            private GeoJsonFeatureEntry(
                string id,
                string? geometryType,
                GeoCoordinate? pointCoordinate,
                IReadOnlyList<GeoCoordinate>? lineCoordinates,
                GeoPolygon? polygon,
                List<(string Key, object? Value)> properties)
            {
                Id = id;
                GeometryType = geometryType;
                PointCoordinate = pointCoordinate;
                LineCoordinates = lineCoordinates;
                Polygon = polygon;
                Properties = properties;
            }

            public static GeoJsonFeatureEntry CreatePoint(
                string id,
                GeoCoordinate point,
                List<(string Key, object? Value)> properties,
                BoundingBoxTracker bbox)
            {
                bbox.Include(point);
                return new GeoJsonFeatureEntry(id, "Point", point, null, null, properties);
            }

            public static GeoJsonFeatureEntry CreateLineString(
                string id,
                IReadOnlyList<GeoCoordinate> coordinates,
                List<(string Key, object? Value)> properties,
                BoundingBoxTracker bbox)
            {
                bbox.IncludeRange(coordinates);
                return new GeoJsonFeatureEntry(id, "LineString", null, coordinates, null, properties);
            }

            public static GeoJsonFeatureEntry CreatePolygon(
                string id,
                GeoPolygon polygon,
                List<(string Key, object? Value)> properties,
                BoundingBoxTracker bbox)
            {
                bbox.IncludeRange(polygon.ExteriorRing);
                foreach (var hole in polygon.InteriorRings)
                {
                    bbox.IncludeRange(hole);
                }
                return new GeoJsonFeatureEntry(id, "Polygon", null, null, polygon, properties);
            }

            public static GeoJsonFeatureEntry CreateNullGeometry(
                string id,
                List<(string Key, object? Value)> properties)
            {
                return new GeoJsonFeatureEntry(id, null, null, null, null, properties);
            }
        }

        /// <summary>
        /// Lightweight, zero-dependency RFC 8259 JSON / RFC 7946 GeoJSON writer backed by <see cref="StringBuilder"/>.
        /// Enforces RFC 7946 Section 3.1.1 <c>[longitude, latitude]</c> coordinate tuple ordering.
        /// </summary>
        private sealed class Rfc8259JsonWriter
        {
            private readonly StringBuilder _sb;
            private readonly bool _indented;
            private int _indentLevel;
            private bool _needsComma;
            private bool _afterPropertyName;
            private bool _inlineNeedsComma;

            public Rfc8259JsonWriter(StringBuilder sb, bool indented)
            {
                _sb = sb;
                _indented = indented;
            }

            public void BeginObject()
            {
                WriteCommaIfNeeded();
                _sb.Append('{');
                _indentLevel++;
                _needsComma = false;
                _afterPropertyName = false;
            }

            public void EndObject()
            {
                _indentLevel--;
                if (_indented)
                {
                    _sb.Append('\n');
                    WriteIndent();
                }
                _sb.Append('}');
                _needsComma = true;
                _afterPropertyName = false;
            }

            public void BeginArray()
            {
                _sb.Append('[');
                _indentLevel++;
                _needsComma = false;
                _afterPropertyName = false;
            }

            public void EndArray()
            {
                _indentLevel--;
                if (_indented)
                {
                    _sb.Append('\n');
                    WriteIndent();
                }
                _sb.Append(']');
                _needsComma = true;
                _afterPropertyName = false;
            }

            public void BeginInlineArray()
            {
                _sb.Append('[');
                _inlineNeedsComma = false;
                _afterPropertyName = false;
            }

            public void WriteInlineNumber(double value)
            {
                if (_inlineNeedsComma)
                {
                    _sb.Append(_indented ? ", " : ",");
                }
                _sb.Append(value.ToString("G15", CultureInfo.InvariantCulture));
                _inlineNeedsComma = true;
            }

            public void EndInlineArray()
            {
                _sb.Append(']');
                _needsComma = true;
                _afterPropertyName = false;
            }

            public void WritePropertyName(string name)
            {
                if (_needsComma)
                {
                    _sb.Append(',');
                }
                if (_indented)
                {
                    _sb.Append('\n');
                    WriteIndent();
                }
                WriteEscapedString(name);
                _sb.Append(_indented ? ": " : ":");
                _needsComma = false;
                _afterPropertyName = true;
            }

            public void WriteProperty(string name, string? value)
            {
                if (value == null) return;
                WritePropertyName(name);
                WriteEscapedString(value);
                _needsComma = true;
                _afterPropertyName = false;
            }

            public void WriteProperty(string name, int value)
            {
                WritePropertyName(name);
                _sb.Append(value.ToString(CultureInfo.InvariantCulture));
                _needsComma = true;
                _afterPropertyName = false;
            }

            public void WriteProperty(string name, double value)
            {
                WritePropertyName(name);
                _sb.Append(value.ToString("G15", CultureInfo.InvariantCulture));
                _needsComma = true;
                _afterPropertyName = false;
            }

            public void WriteProperty(string name, bool value)
            {
                WritePropertyName(name);
                _sb.Append(value ? "true" : "false");
                _needsComma = true;
                _afterPropertyName = false;
            }

            public void WriteNullValue()
            {
                _sb.Append("null");
                _needsComma = true;
                _afterPropertyName = false;
            }

            public void WriteDynamicProperty(string name, object value)
            {
                switch (value)
                {
                    case string s:
                        WriteProperty(name, s);
                        break;
                    case int i:
                        WriteProperty(name, i);
                        break;
                    case double d:
                        WriteProperty(name, d);
                        break;
                    case bool b:
                        WriteProperty(name, b);
                        break;
                    case IReadOnlyList<string> strList:
                        WritePropertyName(name);
                        _sb.Append('[');
                        for (int idx = 0; idx < strList.Count; idx++)
                        {
                            if (idx > 0) _sb.Append(_indented ? ", " : ",");
                            WriteEscapedString(strList[idx]);
                        }
                        _sb.Append(']');
                        _needsComma = true;
                        _afterPropertyName = false;
                        break;
                    default:
                        WriteProperty(name, value.ToString());
                        break;
                }
            }

            /// <summary>
            /// Writes an RFC 7946 Section 3.1.1 <c>[longitude, latitude]</c> coordinate position.
            /// </summary>
            public void WriteCoordinatePair(GeoCoordinate coord)
            {
                _sb.Append('[');
                _sb.Append(coord.Longitude.ToString("G15", CultureInfo.InvariantCulture));
                _sb.Append(_indented ? ", " : ",");
                _sb.Append(coord.Latitude.ToString("G15", CultureInfo.InvariantCulture));
                _sb.Append(']');
                _needsComma = true;
                _afterPropertyName = false;
            }

            public void WriteCoordinateSequence(IReadOnlyList<GeoCoordinate> coords)
            {
                _sb.Append('[');
                for (int i = 0; i < coords.Count; i++)
                {
                    if (i > 0) _sb.Append(_indented ? ", " : ",");
                    _sb.Append('[');
                    _sb.Append(coords[i].Longitude.ToString("G15", CultureInfo.InvariantCulture));
                    _sb.Append(_indented ? ", " : ",");
                    _sb.Append(coords[i].Latitude.ToString("G15", CultureInfo.InvariantCulture));
                    _sb.Append(']');
                }
                _sb.Append(']');
                _needsComma = true;
                _afterPropertyName = false;
            }

            public void WriteCoordinateRingElement(IReadOnlyList<GeoCoordinate> ring)
            {
                WriteCommaIfNeeded();
                WriteCoordinateSequence(ring);
            }

            private void WriteCommaIfNeeded()
            {
                if (_needsComma)
                {
                    _sb.Append(',');
                    _needsComma = false;
                }
                if (_indented && _indentLevel > 0 && !_afterPropertyName)
                {
                    _sb.Append('\n');
                    WriteIndent();
                }
                _afterPropertyName = false;
            }

            private void WriteIndent()
            {
                _sb.Append(' ', _indentLevel * 2);
            }

            private void WriteEscapedString(string value)
            {
                _sb.Append('"');
                for (int i = 0; i < value.Length; i++)
                {
                    char c = value[i];
                    switch (c)
                    {
                        case '"':
                            _sb.Append("\\\"");
                            break;
                        case '\\':
                            _sb.Append("\\\\");
                            break;
                        case '\b':
                            _sb.Append("\\b");
                            break;
                        case '\f':
                            _sb.Append("\\f");
                            break;
                        case '\n':
                            _sb.Append("\\n");
                            break;
                        case '\r':
                            _sb.Append("\\r");
                            break;
                        case '\t':
                            _sb.Append("\\t");
                            break;
                        default:
                            if (c < 0x20)
                            {
                                _sb.Append("\\u");
                                _sb.Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                            }
                            else
                            {
                                _sb.Append(c);
                            }
                            break;
                    }
                }
                _sb.Append('"');
            }
        }
    }
}
