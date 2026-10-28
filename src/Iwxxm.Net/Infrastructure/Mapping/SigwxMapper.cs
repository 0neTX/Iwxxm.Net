using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Iwxxm.Net.Application.Ports;
using Iwxxm.Net.Domain.Aggregates;
using Iwxxm.Net.Domain.Common;
using Iwxxm.Net.Domain.ValueObjects;
using Iwxxm20252.Generated;

namespace Iwxxm.Net.Infrastructure.Mapping
{
    /// <summary>
    /// Anti-Corruption Layer (ACL) mapper that translates the XSD-generated XML object graph
    /// (<see cref="WAFSSignificantWeatherForecastType"/> or <see cref="SIGWXReportType"/>)
    /// into pure Domain Entities and Value Objects, isolating the Domain from OGC GML 3.2.1
    /// primitives (<c>gml:posList</c>, <c>gml:CubicSpline</c>, <c>gml:PolygonPatch</c>, <c>gml:pos</c>).
    /// </summary>
    internal sealed class SigwxMapper : ISigwxAclMapper<SigwxDocumentEnvelope>
    {
        private const double KnotsPerMeterPerSecond = 1.9438444924406;
        private static readonly char[] WhitespaceDelimiters = { ' ', '\t', '\r', '\n' };

        public Result<SignificantWeatherReport> MapToDomain(SigwxDocumentEnvelope envelope)
        {
            if (envelope == null)
                throw new ArgumentNullException(nameof(envelope));

            if (envelope.WafsForecast != null)
            {
                return MapWafsForecastToDomain(envelope.WafsForecast);
            }

            if (envelope.CompactReport != null)
            {
                return MapCompactReportToDomain(envelope.CompactReport);
            }

            return Result<SignificantWeatherReport>.Failure(
                "ACL-EMPTY-ENVELOPE",
                "The deserialized SIGWX document envelope does not contain a report instance.",
                ErrorCategory.DomainMapping);
        }

        // =====================================================================
        // Official WMO IWXXM 2025-2 WAFSSignificantWeatherForecast Mapping
        // =====================================================================

        private static Result<SignificantWeatherReport> MapWafsForecastToDomain(WAFSSignificantWeatherForecastType dto)
        {
            var errors = new List<ValidationError>();

            DateTimeOffset issueTime = ExtractTimeInstant(dto.IssueTime, "issueTime", errors);
            (DateTimeOffset validFrom, DateTimeOffset validUntil) = ExtractTimePeriod(dto.BoundingPeriod, "boundingPeriod", errors);

            string issuingCentre = dto.OriginatingCentre?.WorldAreaForecastCentre ?? string.Empty;

            FlightLevel lowerLimit = ExtractFlightLevel(
                dto.BoundingVolume?.ElevatedEnvelope?.LowerElevation,
                "boundingVolume/ElevatedEnvelope/lowerElevation",
                errors);
            FlightLevel upperLimit = ExtractFlightLevel(
                dto.BoundingVolume?.ElevatedEnvelope?.UpperElevation,
                "boundingVolume/ElevatedEnvelope/UpperElevation",
                errors);

            var jetStreams = new List<JetStream>();
            var clouds = new List<Cloud>();
            var tropopauses = new List<Tropopause>();
            var volcanoes = new List<Volcano>();
            var turbulences = new List<Turbulence>();
            var airframeIcings = new List<AirframeIcing>();
            var tropicalCyclones = new List<TropicalCyclone>();
            var sandstorms = new List<Sandstorm>();
            var radiationIncidents = new List<RadiationIncident>();

            if (dto.Features != null)
            {
                foreach (MeteorologicalFeaturePropertyType featureProp in dto.Features)
                {
                    MeteorologicalFeatureType? feature = featureProp.MeteorologicalFeature;
                    if (feature == null)
                    {
                        // External xlink:href feature reference without inline payload
                        continue;
                    }

                    string phenomenonHref = feature.Phenomenon?.Href ?? string.Empty;

                    if (phenomenonHref.EndsWith("/JETSTREAM", StringComparison.OrdinalIgnoreCase))
                    {
                        var jsResult = MapWafsJetStreamFeature(feature);
                        if (jsResult.IsSuccess) jetStreams.Add(jsResult.Value);
                        else errors.AddRange(jsResult.Errors);
                    }
                    else if (phenomenonHref.EndsWith("/CLOUD", StringComparison.OrdinalIgnoreCase))
                    {
                        var cloudResult = MapWafsCloudFeature(feature);
                        if (cloudResult.IsSuccess) clouds.Add(cloudResult.Value);
                        else errors.AddRange(cloudResult.Errors);
                    }
                    else if (phenomenonHref.EndsWith("/TROPOPAUSE", StringComparison.OrdinalIgnoreCase))
                    {
                        var tropoResult = MapWafsTropopauseFeature(feature);
                        if (tropoResult.IsSuccess) tropopauses.Add(tropoResult.Value);
                        else errors.AddRange(tropoResult.Errors);
                    }
                    else if (phenomenonHref.EndsWith("/VOLCANO", StringComparison.OrdinalIgnoreCase))
                    {
                        var volcanoResult = MapWafsVolcanoFeature(feature);
                        if (volcanoResult.IsSuccess) volcanoes.Add(volcanoResult.Value);
                        else errors.AddRange(volcanoResult.Errors);
                    }
                    else if (phenomenonHref.EndsWith("/TURBULENCE", StringComparison.OrdinalIgnoreCase))
                    {
                        var turbResult = MapWafsTurbulenceFeature(feature);
                        if (turbResult.IsSuccess) turbulences.Add(turbResult.Value);
                        else errors.AddRange(turbResult.Errors);
                    }
                    else if (phenomenonHref.EndsWith("/AIRFRAME_ICING", StringComparison.OrdinalIgnoreCase))
                    {
                        var icingResult = MapWafsAirframeIcingFeature(feature);
                        if (icingResult.IsSuccess) airframeIcings.Add(icingResult.Value);
                        else errors.AddRange(icingResult.Errors);
                    }
                    else if (phenomenonHref.EndsWith("/TROPICAL_CYCLONE", StringComparison.OrdinalIgnoreCase))
                    {
                        var tcResult = MapWafsTropicalCycloneFeature(feature);
                        if (tcResult.IsSuccess) tropicalCyclones.Add(tcResult.Value);
                        else errors.AddRange(tcResult.Errors);
                    }
                    else if (phenomenonHref.EndsWith("/SANDSTORM", StringComparison.OrdinalIgnoreCase))
                    {
                        var sandResult = MapWafsSandstormFeature(feature);
                        if (sandResult.IsSuccess) sandstorms.Add(sandResult.Value);
                        else errors.AddRange(sandResult.Errors);
                    }
                    else if (phenomenonHref.EndsWith("/RADIATION", StringComparison.OrdinalIgnoreCase))
                    {
                        var radResult = MapWafsRadiationIncidentFeature(feature);
                        if (radResult.IsSuccess) radiationIncidents.Add(radResult.Value);
                        else errors.AddRange(radResult.Errors);
                    }
                }
            }

            if (errors.Count > 0)
            {
                return Result<SignificantWeatherReport>.Failure(errors);
            }

            return SignificantWeatherReport.Create(
                id: dto.Id,
                issuingCentre: issuingCentre,
                reportStatus: dto.ReportStatus,
                permissibleUsage: dto.PermissibleUsage,
                issueTimeUtc: issueTime,
                validFromUtc: validFrom,
                validUntilUtc: validUntil,
                lowerLimit: lowerLimit,
                upperLimit: upperLimit,
                jetStreams: jetStreams,
                clouds: clouds,
                tropopauses: tropopauses,
                volcanoes: volcanoes,
                turbulences: turbulences,
                airframeIcings: airframeIcings,
                tropicalCyclones: tropicalCyclones,
                sandstorms: sandstorms,
                radiationIncidents: radiationIncidents);
        }

        private static Result<JetStream> MapWafsJetStreamFeature(MeteorologicalFeatureType feature)
        {
            string context = $"MeteorologicalFeature[@gml:id='{feature.Id}']";
            CurveType? curve = feature.PhenomenonGeometry?.Curve;

            var axisCoordsResult = ExtractCurveCoordinates(curve, $"{context}/phenomenonGeometry");
            if (axisCoordsResult.IsFailure)
            {
                return Result<JetStream>.Failure(axisCoordsResult.Errors);
            }

            var corePoints = new List<JetStreamCorePoint>();
            double maxWindSpeedKnots = 0.0;
            FlightLevel maxWindFlightLevel = default;
            bool hasCoreLevel = false;

            foreach (PhenomenonPropertyRoleType prop in feature.PhenomenonProperties)
            {
                WAFSJetStreamWindSymbolType? symbol = prop.JetStreamWindSymbol;
                if (symbol == null) continue;

                ElevatedPointType? elevatedPt = symbol.Location?.ElevatedPoint;
                var ptCoordResult = ParseGmlPoint(elevatedPt, $"{context}/WAFSJetStreamWindSymbol[@gml:id='{symbol.Id}']");
                if (ptCoordResult.IsFailure)
                    return Result<JetStream>.Failure(ptCoordResult.Errors);

                if (elevatedPt?.Elevation == null || elevatedPt.Elevation.IsNil)
                {
                    return Result<JetStream>.Failure(
                        "ACL-WAFS-JS-MISSING-ELEVATION",
                        $"Missing elevation on WAFSJetStreamWindSymbol '{symbol.Id}'.",
                        ErrorCategory.DomainMapping,
                        context);
                }

                var flResult = FlightLevel.Create(elevatedPt.Elevation.Value, elevatedPt.Elevation.Uom, context);
                if (flResult.IsFailure)
                    return Result<JetStream>.Failure(flResult.Errors);

                double speedKt = ConvertSpeedToKnots(symbol.WindSpeed);

                FlightLevel? lowerIso = null;
                if (symbol.IsotachLowerElevation != null && !symbol.IsotachLowerElevation.IsNil)
                {
                    var lowRes = FlightLevel.Create(symbol.IsotachLowerElevation.Value, symbol.IsotachLowerElevation.Uom, context);
                    if (lowRes.IsFailure) return Result<JetStream>.Failure(lowRes.Errors);
                    lowerIso = lowRes.Value;
                }

                FlightLevel? upperIso = null;
                if (symbol.IsotachUpperElevation != null && !symbol.IsotachUpperElevation.IsNil)
                {
                    var upRes = FlightLevel.Create(symbol.IsotachUpperElevation.Value, symbol.IsotachUpperElevation.Uom, context);
                    if (upRes.IsFailure) return Result<JetStream>.Failure(upRes.Errors);
                    upperIso = upRes.Value;
                }

                if (!hasCoreLevel || speedKt > maxWindSpeedKnots)
                {
                    maxWindSpeedKnots = speedKt;
                    maxWindFlightLevel = flResult.Value;
                    hasCoreLevel = true;
                }

                corePoints.Add(new JetStreamCorePoint(
                    ptCoordResult.Value,
                    flResult.Value,
                    speedKt,
                    lowerIso,
                    upperIso));
            }

            if (!hasCoreLevel)
            {
                return Result<JetStream>.Failure(
                    "ACL-WAFS-JS-NO-WIND-SYMBOLS",
                    $"JetStream feature '{feature.Id}' does not contain any WAFSJetStreamWindSymbol properties.",
                    ErrorCategory.DomainMapping,
                    context);
            }

            return JetStream.Create(
                id: feature.Id,
                axisCoordinates: axisCoordsResult.Value,
                coreFlightLevel: maxWindFlightLevel,
                maxWindSpeedKnots: maxWindSpeedKnots,
                corePoints: corePoints);
        }

        private static Result<Cloud> MapWafsCloudFeature(MeteorologicalFeatureType feature)
        {
            string context = $"MeteorologicalFeature[@gml:id='{feature.Id}']";
            var volumeRes = ExtractElevatedVolumeGeometryAndLimits(feature.PhenomenonGeometry?.ElevatedVolume, context);
            if (volumeRes.IsFailure)
                return Result<Cloud>.Failure(volumeRes.Errors);

            string? cloudAmount = feature.PhenomenonProperties
                .Select(p => p.CloudDistribution?.Href)
                .FirstOrDefault(h => !string.IsNullOrWhiteSpace(h));

            string? cloudType = feature.PhenomenonProperties
                .Select(p => p.CloudType?.Href)
                .FirstOrDefault(h => !string.IsNullOrWhiteSpace(h));

            return Cloud.Create(
                id: feature.Id,
                cloudAmount: cloudAmount ?? "UNKNOWN",
                cloudType: cloudType ?? "UNKNOWN",
                baseFlightLevel: volumeRes.Value.BaseFl,
                topFlightLevel: volumeRes.Value.TopFl,
                area: volumeRes.Value.Polygon);
        }

        private static Result<Turbulence> MapWafsTurbulenceFeature(MeteorologicalFeatureType feature)
        {
            string context = $"MeteorologicalFeature[@gml:id='{feature.Id}']";
            var volumeRes = ExtractElevatedVolumeGeometryAndLimits(feature.PhenomenonGeometry?.ElevatedVolume, context);
            if (volumeRes.IsFailure)
                return Result<Turbulence>.Failure(volumeRes.Errors);

            string? degreeHref = feature.PhenomenonProperties
                .Select(p => p.DegreeOfTurbulence?.Href)
                .FirstOrDefault(h => !string.IsNullOrWhiteSpace(h));

            return Turbulence.Create(
                id: feature.Id,
                rawDegreeReference: degreeHref ?? "UNKNOWN",
                baseFlightLevel: volumeRes.Value.BaseFl,
                topFlightLevel: volumeRes.Value.TopFl,
                area: volumeRes.Value.Polygon);
        }

        private static Result<AirframeIcing> MapWafsAirframeIcingFeature(MeteorologicalFeatureType feature)
        {
            string context = $"MeteorologicalFeature[@gml:id='{feature.Id}']";
            var volumeRes = ExtractElevatedVolumeGeometryAndLimits(feature.PhenomenonGeometry?.ElevatedVolume, context);
            if (volumeRes.IsFailure)
                return Result<AirframeIcing>.Failure(volumeRes.Errors);

            string? degreeHref = feature.PhenomenonProperties
                .Select(p => p.DegreeOfIcing?.Href)
                .FirstOrDefault(h => !string.IsNullOrWhiteSpace(h));

            return AirframeIcing.Create(
                id: feature.Id,
                rawDegreeReference: degreeHref ?? "UNKNOWN",
                baseFlightLevel: volumeRes.Value.BaseFl,
                topFlightLevel: volumeRes.Value.TopFl,
                area: volumeRes.Value.Polygon);
        }

        private static Result<Tropopause> MapWafsTropopauseFeature(MeteorologicalFeatureType feature)
        {
            string context = $"MeteorologicalFeature[@gml:id='{feature.Id}']";
            ElevatedLevelType? elevatedLevel = feature.PhenomenonProperties
                .Select(p => p.ElevatedLevel)
                .FirstOrDefault(l => l != null);

            if (elevatedLevel?.Elevation == null || elevatedLevel.Elevation.IsNil)
            {
                return Result<Tropopause>.Failure(
                    "ACL-WAFS-TROPO-MISSING-LEVEL",
                    $"Tropopause feature '{feature.Id}' is missing an <iwxxm:ElevatedLevel> property.",
                    ErrorCategory.DomainMapping,
                    context);
            }

            var flResult = FlightLevel.Create(elevatedLevel.Elevation.Value, elevatedLevel.Elevation.Uom, context);
            if (flResult.IsFailure)
                return Result<Tropopause>.Failure(flResult.Errors);

            var spotHeights = new List<TropopauseSpotHeight>();
            GeoPolygon? contourPolygon = null;

            if (feature.PhenomenonGeometry?.Polygon != null)
            {
                var ringCoordsRes = ExtractRingCoordinates(
                    feature.PhenomenonGeometry.Polygon.Exterior,
                    context,
                    feature.PhenomenonGeometry.Polygon.SrsDimensionSpecified ? feature.PhenomenonGeometry.Polygon.SrsDimension : 2);

                if (ringCoordsRes.IsFailure)
                    return Result<Tropopause>.Failure(ringCoordsRes.Errors);

                var polyRes = GeoPolygon.Create(ringCoordsRes.Value, context);
                if (polyRes.IsFailure)
                    return Result<Tropopause>.Failure(polyRes.Errors);

                contourPolygon = polyRes.Value;
                foreach (GeoCoordinate vertex in ringCoordsRes.Value)
                {
                    spotHeights.Add(new TropopauseSpotHeight(vertex, flResult.Value));
                }
            }
            else if (feature.PhenomenonGeometry?.Point != null)
            {
                var ptRes = ParseGmlPoint(feature.PhenomenonGeometry.Point, context);
                if (ptRes.IsFailure)
                    return Result<Tropopause>.Failure(ptRes.Errors);

                spotHeights.Add(new TropopauseSpotHeight(ptRes.Value, flResult.Value));
            }

            return Tropopause.Create(feature.Id, "CONTOUR", spotHeights, contourPolygon);
        }

        private static Result<Volcano> MapWafsVolcanoFeature(MeteorologicalFeatureType feature)
        {
            string context = $"MeteorologicalFeature[@gml:id='{feature.Id}']";
            var ptRes = ParseGmlPoint(feature.PhenomenonGeometry?.Point, $"{context}/phenomenonGeometry");
            if (ptRes.IsFailure)
                return Result<Volcano>.Failure(ptRes.Errors);

            WAFSVolcanoType? volcanoProp = feature.PhenomenonProperties
                .Select(p => p.Volcano)
                .FirstOrDefault(v => v != null);

            string name = volcanoProp?.Name?.Value ?? string.Empty;
            string? wmoNumber = volcanoProp?.IAVCEINumber?.Value ?? volcanoProp?.WmoNumber;

            return Volcano.Create(
                id: feature.Id,
                name: name,
                wmoNumber: wmoNumber,
                position: ptRes.Value,
                eruptionTimeUtc: null);
        }

        private static Result<TropicalCyclone> MapWafsTropicalCycloneFeature(MeteorologicalFeatureType feature)
        {
            string context = $"MeteorologicalFeature[@gml:id='{feature.Id}']";
            var ptRes = ParseGmlPoint(feature.PhenomenonGeometry?.Point, $"{context}/phenomenonGeometry");
            if (ptRes.IsFailure)
                return Result<TropicalCyclone>.Failure(ptRes.Errors);

            TropicalCycloneType? tcProp = feature.PhenomenonProperties
                .Select(p => p.TropicalCyclone)
                .FirstOrDefault(tc => tc != null);

            string name = tcProp?.Name?.Value ?? string.Empty;
            return TropicalCyclone.Create(feature.Id, name, ptRes.Value);
        }

        private static Result<Sandstorm> MapWafsSandstormFeature(MeteorologicalFeatureType feature)
        {
            string context = $"MeteorologicalFeature[@gml:id='{feature.Id}']";
            var ptRes = ParseGmlPoint(feature.PhenomenonGeometry?.Point, $"{context}/phenomenonGeometry");
            if (ptRes.IsFailure)
                return Result<Sandstorm>.Failure(ptRes.Errors);

            return Sandstorm.Create(feature.Id, ptRes.Value);
        }

        private static Result<RadiationIncident> MapWafsRadiationIncidentFeature(MeteorologicalFeatureType feature)
        {
            string context = $"MeteorologicalFeature[@gml:id='{feature.Id}']";
            var ptRes = ParseGmlPoint(feature.PhenomenonGeometry?.Point, $"{context}/phenomenonGeometry");
            if (ptRes.IsFailure)
                return Result<RadiationIncident>.Failure(ptRes.Errors);

            RadiationIncidentType? radProp = feature.PhenomenonProperties
                .Select(p => p.RadiationIncident)
                .FirstOrDefault(r => r != null);

            string? siteName = radProp?.SiteName != null && string.IsNullOrWhiteSpace(radProp.SiteName.NilReason)
                ? radProp.SiteName.Value
                : null;

            DateTimeOffset? incidentTime = radProp?.IncidentTime?.TimeInstant != null
                ? NormalizeToUtc(radProp.IncidentTime.TimeInstant.TimePosition)
                : null;

            return RadiationIncident.Create(feature.Id, siteName, ptRes.Value, incidentTime);
        }

        private static Result<(GeoPolygon Polygon, FlightLevel? BaseFl, FlightLevel TopFl)> ExtractElevatedVolumeGeometryAndLimits(
            ElevatedVolumeType? volume,
            string context)
        {
            if (volume == null)
            {
                return Result<(GeoPolygon, FlightLevel?, FlightLevel)>.Failure(
                    "ACL-WAFS-MISSING-VOLUME",
                    $"Feature at '{context}' is missing an <iwxxm:ElevatedVolume> geometry.",
                    ErrorCategory.DomainMapping,
                    context);
            }

            var ringCoordsResult = ExtractElevatedVolumeRingCoordinates(volume, context);
            if (ringCoordsResult.IsFailure)
                return Result<(GeoPolygon, FlightLevel?, FlightLevel)>.Failure(ringCoordsResult.Errors);

            var polyResult = GeoPolygon.Create(ringCoordsResult.Value, context);
            if (polyResult.IsFailure)
                return Result<(GeoPolygon, FlightLevel?, FlightLevel)>.Failure(polyResult.Errors);

            FlightLevel? baseFl = null;
            if (volume.LowerElevation != null && !volume.LowerElevation.IsNil)
            {
                var baseRes = FlightLevel.Create(volume.LowerElevation.Value, volume.LowerElevation.Uom, $"{context}/lowerElevation");
                if (baseRes.IsFailure)
                    return Result<(GeoPolygon, FlightLevel?, FlightLevel)>.Failure(baseRes.Errors);
                baseFl = baseRes.Value;
            }

            if (volume.UpperElevation == null || volume.UpperElevation.IsNil)
            {
                return Result<(GeoPolygon, FlightLevel?, FlightLevel)>.Failure(
                    "ACL-WAFS-MISSING-UPPER-ELEV",
                    $"ElevatedVolume at '{context}' is missing a valid <iwxxm:upperElevation>.",
                    ErrorCategory.DomainMapping,
                    context);
            }

            var topRes = FlightLevel.Create(volume.UpperElevation.Value, volume.UpperElevation.Uom, $"{context}/upperElevation");
            if (topRes.IsFailure)
                return Result<(GeoPolygon, FlightLevel?, FlightLevel)>.Failure(topRes.Errors);

            return Result<(GeoPolygon, FlightLevel?, FlightLevel)>.Success((polyResult.Value, baseFl, topRes.Value));
        }

        // =====================================================================
        // Compact SIGWXReport Mapping
        // =====================================================================

        private static Result<SignificantWeatherReport> MapCompactReportToDomain(SIGWXReportType dto)
        {
            var errors = new List<ValidationError>();

            DateTimeOffset issueTime = ExtractTimeInstant(dto.IssueTime, "issueTime", errors);
            (DateTimeOffset validFrom, DateTimeOffset validUntil) = ExtractTimePeriod(dto.ValidPeriod, "validPeriod", errors);

            FlightLevel lowerLimit = ExtractFlightLevel(dto.LowerLimit, "lowerLimit", errors);
            FlightLevel upperLimit = ExtractFlightLevel(dto.UpperLimit, "upperLimit", errors);

            var jetStreams = new List<JetStream>();
            if (dto.JetStreams != null)
            {
                foreach (JetStreamType jsDto in dto.JetStreams)
                {
                    var jsResult = MapCompactJetStream(jsDto);
                    if (jsResult.IsSuccess) jetStreams.Add(jsResult.Value);
                    else errors.AddRange(jsResult.Errors);
                }
            }

            var clouds = new List<Cloud>();
            if (dto.Clouds != null)
            {
                foreach (CloudDistributionFeatureType cloudDto in dto.Clouds)
                {
                    var cloudResult = MapCompactCloud(cloudDto);
                    if (cloudResult.IsSuccess) clouds.Add(cloudResult.Value);
                    else errors.AddRange(cloudResult.Errors);
                }
            }

            var tropopauses = new List<Tropopause>();
            if (dto.Tropopauses != null)
            {
                foreach (TropopauseType tropoDto in dto.Tropopauses)
                {
                    var tropoResult = MapCompactTropopause(tropoDto);
                    if (tropoResult.IsSuccess) tropopauses.Add(tropoResult.Value);
                    else errors.AddRange(tropoResult.Errors);
                }
            }

            var volcanoes = new List<Volcano>();
            if (dto.Volcanoes != null)
            {
                foreach (WAFSVolcanoType volcanoDto in dto.Volcanoes)
                {
                    var volcanoResult = MapCompactVolcano(volcanoDto);
                    if (volcanoResult.IsSuccess) volcanoes.Add(volcanoResult.Value);
                    else errors.AddRange(volcanoResult.Errors);
                }
            }

            if (errors.Count > 0)
            {
                return Result<SignificantWeatherReport>.Failure(errors);
            }

            return SignificantWeatherReport.Create(
                id: dto.Id,
                issuingCentre: dto.IssuingCentre,
                reportStatus: dto.ReportStatus,
                permissibleUsage: dto.PermissibleUsage,
                issueTimeUtc: issueTime,
                validFromUtc: validFrom,
                validUntilUtc: validUntil,
                lowerLimit: lowerLimit,
                upperLimit: upperLimit,
                jetStreams: jetStreams,
                clouds: clouds,
                tropopauses: tropopauses,
                volcanoes: volcanoes);
        }

        // =====================================================================
        // Core GML <gml:posList>, <gml:Curve>, <gml:Ring>, and <gml:pos> Parsers
        // =====================================================================

        /// <summary>
        /// Extracts and parses a flat whitespace-delimited sequence of coordinates from an XSD-generated
        /// <c>&lt;gml:posList&gt;</c> (<see cref="DirectPositionListType"/>) into a <see cref="List{GeoCoordinate}"/>.
        /// Under the ICAO/WMO IWXXM profile (CRS EPSG:4326 WGS84), axis order is strictly [Latitude Longitude].
        /// </summary>
        internal static Result<List<GeoCoordinate>> ParseGmlPosList(
            DirectPositionListType? posListDto,
            string contextLocation,
            int defaultDimension = 2)
        {
            if (posListDto == null || string.IsNullOrWhiteSpace(posListDto.Value))
            {
                return Result<List<GeoCoordinate>>.Failure(
                    "GML-POSLIST-EMPTY",
                    $"Element <gml:posList> at '{contextLocation}' is empty or missing.",
                    ErrorCategory.DomainMapping,
                    contextLocation);
            }

            int dimension = posListDto.SrsDimensionSpecified && posListDto.SrsDimension > 0
                ? posListDto.SrsDimension
                : (defaultDimension > 0 ? defaultDimension : 2);

            if (dimension < 2)
            {
                return Result<List<GeoCoordinate>>.Failure(
                    "GML-SRS-DIMENSION-INVALID",
                    $"Spatial dimension srsDimension={dimension} at '{contextLocation}' is invalid (minimum is 2).",
                    ErrorCategory.DomainMapping,
                    contextLocation);
            }

            string[] tokens = posListDto.Value.Split(WhitespaceDelimiters, StringSplitOptions.RemoveEmptyEntries);

            if (tokens.Length == 0 || tokens.Length % dimension != 0)
            {
                return Result<List<GeoCoordinate>>.Failure(
                    "GML-POSLIST-TUPLE-MISMATCH",
                    $"Element <gml:posList> at '{contextLocation}' contains {tokens.Length} scalar values, which is not a multiple of srsDimension ({dimension}).",
                    ErrorCategory.DomainMapping,
                    contextLocation);
            }

            int pointCount = tokens.Length / dimension;
            var coordinates = new List<GeoCoordinate>(pointCount);

            for (int i = 0; i < tokens.Length; i += dimension)
            {
                if (!double.TryParse(tokens[i], NumberStyles.Float, CultureInfo.InvariantCulture, out double lat))
                {
                    return Result<List<GeoCoordinate>>.Failure(
                        "GML-POSLIST-PARSE-LAT",
                        $"Failed to parse latitude token '{tokens[i]}' as a floating-point number at '{contextLocation}'.",
                        ErrorCategory.DomainMapping,
                        contextLocation);
                }

                if (!double.TryParse(tokens[i + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out double lon))
                {
                    return Result<List<GeoCoordinate>>.Failure(
                        "GML-POSLIST-PARSE-LON",
                        $"Failed to parse longitude token '{tokens[i + 1]}' as a floating-point number at '{contextLocation}'.",
                        ErrorCategory.DomainMapping,
                        contextLocation);
                }

                Result<GeoCoordinate> coordResult = GeoCoordinate.Create(lat, lon, contextLocation);
                if (coordResult.IsFailure)
                {
                    return Result<List<GeoCoordinate>>.Failure(coordResult.Errors);
                }

                coordinates.Add(coordResult.Value);
            }

            return Result<List<GeoCoordinate>>.Success(coordinates);
        }

        /// <summary>
        /// Extracts a single geographic coordinate from a <c>&lt;gml:Point&gt;/&lt;gml:pos&gt;</c>
        /// or <c>&lt;iwxxm:ElevatedPoint&gt;/&lt;gml:pos&gt;</c> node.
        /// </summary>
        internal static Result<GeoCoordinate> ParseGmlPoint(PointType? pointDto, string contextLocation)
        {
            if (pointDto?.Pos == null || string.IsNullOrWhiteSpace(pointDto.Pos.Value))
            {
                return Result<GeoCoordinate>.Failure(
                    "GML-POINT-EMPTY",
                    $"Element <gml:Point>/<gml:pos> at '{contextLocation}' is empty or missing.",
                    ErrorCategory.DomainMapping,
                    contextLocation);
            }

            string[] tokens = pointDto.Pos.Value.Split(WhitespaceDelimiters, StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length < 2)
            {
                return Result<GeoCoordinate>.Failure(
                    "GML-POS-TUPLE-MISMATCH",
                    $"Element <gml:pos> at '{contextLocation}' requires at least 2 values (lat lon), but found {tokens.Length}.",
                    ErrorCategory.DomainMapping,
                    contextLocation);
            }

            if (!double.TryParse(tokens[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double lat) ||
                !double.TryParse(tokens[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double lon))
            {
                return Result<GeoCoordinate>.Failure(
                    "GML-POS-PARSE-ERROR",
                    $"Invalid numeric coordinates '{pointDto.Pos.Value}' at '{contextLocation}'.",
                    ErrorCategory.DomainMapping,
                    contextLocation);
            }

            return GeoCoordinate.Create(lat, lon, contextLocation);
        }

        private static Result<List<GeoCoordinate>> ExtractCurveCoordinates(CurveType? curve, string context)
        {
            if (curve?.Segments == null)
            {
                return Result<List<GeoCoordinate>>.Failure(
                    "ACL-CURVE-NO-SEGMENTS",
                    $"Curve at '{context}' does not contain any <gml:segments>.",
                    ErrorCategory.DomainMapping,
                    context);
            }

            int curveDim = curve.SrsDimensionSpecified && curve.SrsDimension > 0 ? curve.SrsDimension : 2;
            var points = new List<GeoCoordinate>();

            foreach (LineStringSegmentType segment in curve.Segments.LineStringSegments)
            {
                var res = ParseGmlPosList(segment.PosList, $"{context}/LineStringSegment", curveDim);
                if (res.IsFailure) return res;
                AppendDeduplicatedCoordinates(points, res.Value);
            }

            foreach (CubicSplineType spline in curve.Segments.CubicSplines)
            {
                var res = ParseGmlPosList(spline.PosList, $"{context}/CubicSpline", curveDim);
                if (res.IsFailure) return res;
                AppendDeduplicatedCoordinates(points, res.Value);
            }

            if (points.Count == 0)
            {
                return Result<List<GeoCoordinate>>.Failure(
                    "ACL-CURVE-EMPTY",
                    $"Curve at '{context}' contains no LineStringSegment or CubicSpline coordinates.",
                    ErrorCategory.DomainMapping,
                    context);
            }

            return Result<List<GeoCoordinate>>.Success(points);
        }

        private static Result<List<GeoCoordinate>> ExtractElevatedVolumeRingCoordinates(ElevatedVolumeType volume, string context)
        {
            int defaultDim = volume.SrsDimensionSpecified && volume.SrsDimension > 0 ? volume.SrsDimension : 2;
            PolygonPatchType? firstPatch = volume.Patches?.PolygonPatches?.FirstOrDefault();

            if (firstPatch?.Exterior == null)
            {
                return Result<List<GeoCoordinate>>.Failure(
                    "ACL-VOLUME-NO-PATCH",
                    $"ElevatedVolume at '{context}' does not contain an exterior PolygonPatch ring.",
                    ErrorCategory.DomainMapping,
                    context);
            }

            return ExtractRingCoordinates(firstPatch.Exterior, context, defaultDim);
        }

        private static Result<List<GeoCoordinate>> ExtractRingCoordinates(
            AbstractRingPropertyType? exterior,
            string context,
            int defaultDim)
        {
            if (exterior?.LinearRing != null)
            {
                return ParseGmlPosList(exterior.LinearRing.PosList, $"{context}/LinearRing", defaultDim);
            }

            if (exterior?.Ring?.CurveMembers != null && exterior.Ring.CurveMembers.Count > 0)
            {
                var coords = new List<GeoCoordinate>();
                foreach (GmlCurvePropertyType curveMember in exterior.Ring.CurveMembers)
                {
                    var curveRes = ExtractCurveCoordinates(curveMember.Curve, $"{context}/Ring/curveMember");
                    if (curveRes.IsFailure) return curveRes;
                    AppendDeduplicatedCoordinates(coords, curveRes.Value);
                }
                return Result<List<GeoCoordinate>>.Success(coords);
            }

            return Result<List<GeoCoordinate>>.Failure(
                "ACL-RING-EMPTY",
                $"Polygon exterior ring at '{context}' is missing <gml:LinearRing> or <gml:Ring>.",
                ErrorCategory.DomainMapping,
                context);
        }

        private static void AppendDeduplicatedCoordinates(List<GeoCoordinate> target, List<GeoCoordinate> source)
        {
            foreach (GeoCoordinate pt in source)
            {
                if (target.Count == 0 || target[target.Count - 1] != pt)
                {
                    target.Add(pt);
                }
            }
        }

        private static double ConvertSpeedToKnots(MeasureType? speedMeasure)
        {
            if (speedMeasure == null || speedMeasure.IsNil)
                return 0.0;

            if (string.Equals(speedMeasure.Uom, "m/s", StringComparison.OrdinalIgnoreCase))
            {
                return speedMeasure.Value * KnotsPerMeterPerSecond;
            }

            return speedMeasure.Value;
        }

        private static Result<JetStream> MapCompactJetStream(JetStreamType dto)
        {
            string context = $"jetStream[@gml:id='{dto.Id}']";
            var axisCoordsResult = ExtractCurveCoordinates(dto.Axis?.Curve, $"{context}/axis");
            if (axisCoordsResult.IsFailure)
                return Result<JetStream>.Failure(axisCoordsResult.Errors);

            if (dto.FlightLevel == null)
            {
                return Result<JetStream>.Failure(
                    "ACL-JETSTREAM-MISSING-FL",
                    $"Missing flightLevel at '{context}'.",
                    ErrorCategory.DomainMapping,
                    context);
            }

            var flResult = FlightLevel.Create(dto.FlightLevel.Value, dto.FlightLevel.Uom, context);
            if (flResult.IsFailure)
                return Result<JetStream>.Failure(flResult.Errors);

            double windSpeedKnots = ConvertSpeedToKnots(dto.WindSpeed);

            var corePoints = new List<JetStreamCorePoint>();
            if (dto.CorePoints != null)
            {
                foreach (JetStreamCorePointType cpDto in dto.CorePoints)
                {
                    var ptResult = ParseGmlPoint(cpDto.Location?.Point, $"{context}/corePoint");
                    if (ptResult.IsFailure)
                        return Result<JetStream>.Failure(ptResult.Errors);

                    if (cpDto.FlightLevel == null)
                        return Result<JetStream>.Failure("ACL-JS-CORE-FL", "Missing flightLevel in corePoint.", ErrorCategory.DomainMapping, context);

                    var cpFlResult = FlightLevel.Create(cpDto.FlightLevel.Value, cpDto.FlightLevel.Uom, $"{context}/corePoint");
                    if (cpFlResult.IsFailure)
                        return Result<JetStream>.Failure(cpFlResult.Errors);

                    FlightLevel? lowerFl = null;
                    if (cpDto.LowerFlightLevel != null && !cpDto.LowerFlightLevel.IsNil)
                    {
                        var lowRes = FlightLevel.Create(cpDto.LowerFlightLevel.Value, cpDto.LowerFlightLevel.Uom, $"{context}/corePoint/lowerFlightLevel");
                        if (lowRes.IsFailure) return Result<JetStream>.Failure(lowRes.Errors);
                        lowerFl = lowRes.Value;
                    }

                    FlightLevel? upperFl = null;
                    if (cpDto.UpperFlightLevel != null && !cpDto.UpperFlightLevel.IsNil)
                    {
                        var upRes = FlightLevel.Create(cpDto.UpperFlightLevel.Value, cpDto.UpperFlightLevel.Uom, $"{context}/corePoint/upperFlightLevel");
                        if (upRes.IsFailure) return Result<JetStream>.Failure(upRes.Errors);
                        upperFl = upRes.Value;
                    }

                    corePoints.Add(new JetStreamCorePoint(
                        ptResult.Value,
                        cpFlResult.Value,
                        cpDto.WindSpeed != null ? ConvertSpeedToKnots(cpDto.WindSpeed) : windSpeedKnots,
                        lowerFl,
                        upperFl));
                }
            }

            return JetStream.Create(dto.Id, axisCoordsResult.Value, flResult.Value, windSpeedKnots, corePoints);
        }

        private static Result<Cloud> MapCompactCloud(CloudDistributionFeatureType dto)
        {
            string context = $"cloud[@gml:id='{dto.Id}']";
            PolygonType? polygonDto = dto.Geometry?.Polygon;

            int polyDim = polygonDto != null && polygonDto.SrsDimensionSpecified && polygonDto.SrsDimension > 0
                ? polygonDto.SrsDimension
                : 2;

            var coordsResult = ExtractRingCoordinates(polygonDto?.Exterior, $"{context}/geometry", polyDim);
            if (coordsResult.IsFailure)
                return Result<Cloud>.Failure(coordsResult.Errors);

            var polygonResult = GeoPolygon.Create(coordsResult.Value, context);
            if (polygonResult.IsFailure)
                return Result<Cloud>.Failure(polygonResult.Errors);

            FlightLevel? baseFl = null;
            if (dto.Base != null && !dto.Base.IsNil)
            {
                var baseRes = FlightLevel.Create(dto.Base.Value, dto.Base.Uom, $"{context}/base");
                if (baseRes.IsFailure)
                    return Result<Cloud>.Failure(baseRes.Errors);
                baseFl = baseRes.Value;
            }

            if (dto.Top == null || dto.Top.IsNil)
            {
                return Result<Cloud>.Failure(
                    "ACL-CLOUD-MISSING-TOP",
                    $"Element <iwxxm:top> is required at '{context}'.",
                    ErrorCategory.DomainMapping,
                    context);
            }

            var topRes = FlightLevel.Create(dto.Top.Value, dto.Top.Uom, $"{context}/top");
            if (topRes.IsFailure)
                return Result<Cloud>.Failure(topRes.Errors);

            return Cloud.Create(
                id: dto.Id,
                cloudAmount: dto.CloudAmount,
                cloudType: dto.CloudType,
                baseFlightLevel: baseFl,
                topFlightLevel: topRes.Value,
                area: polygonResult.Value);
        }

        private static Result<Tropopause> MapCompactTropopause(TropopauseType dto)
        {
            string context = $"tropopause[@gml:id='{dto.Id}']";
            var spotHeights = new List<TropopauseSpotHeight>();

            if (dto.SpotHeights != null)
            {
                foreach (TropopausePointType ptDto in dto.SpotHeights)
                {
                    var coordResult = ParseGmlPoint(ptDto.Location?.Point, $"{context}/spotHeight");
                    if (coordResult.IsFailure)
                        return Result<Tropopause>.Failure(coordResult.Errors);

                    if (ptDto.FlightLevel == null || ptDto.FlightLevel.IsNil)
                    {
                        return Result<Tropopause>.Failure(
                            "ACL-TROPOPAUSE-MISSING-FL",
                            $"Missing flightLevel at '{context}/spotHeight'.",
                            ErrorCategory.DomainMapping,
                            context);
                    }

                    var flResult = FlightLevel.Create(ptDto.FlightLevel.Value, ptDto.FlightLevel.Uom, $"{context}/spotHeight");
                    if (flResult.IsFailure)
                        return Result<Tropopause>.Failure(flResult.Errors);

                    spotHeights.Add(new TropopauseSpotHeight(coordResult.Value, flResult.Value));
                }
            }

            return Tropopause.Create(dto.Id, dto.LevelType, spotHeights);
        }

        private static Result<Volcano> MapCompactVolcano(WAFSVolcanoType dto)
        {
            string context = $"volcano[@gml:id='{dto.Id}']";
            var posResult = ParseGmlPoint(dto.Location?.Point, $"{context}/location");
            if (posResult.IsFailure)
                return Result<Volcano>.Failure(posResult.Errors);

            DateTimeOffset? eruptionUtc = null;
            if (dto.EruptionTimeSpecified)
            {
                eruptionUtc = NormalizeToUtc(dto.EruptionTime);
            }

            return Volcano.Create(
                id: dto.Id,
                name: dto.Name?.Value ?? string.Empty,
                wmoNumber: dto.WmoNumber ?? dto.IAVCEINumber?.Value,
                position: posResult.Value,
                eruptionTimeUtc: eruptionUtc);
        }

        private static DateTimeOffset ExtractTimeInstant(
            TimeInstantPropertyType? prop,
            string fieldName,
            List<ValidationError> errors)
        {
            if (prop?.TimeInstant == null)
            {
                errors.Add(new ValidationError(
                    "ACL-MISSING-TIME-INSTANT",
                    $"Required temporal element '{fieldName}' is missing.",
                    ErrorCategory.DomainMapping,
                    ValidationSeverity.Error,
                    fieldName));
                return DateTimeOffset.MinValue;
            }

            return NormalizeToUtc(prop.TimeInstant.TimePosition);
        }

        private static (DateTimeOffset From, DateTimeOffset Until) ExtractTimePeriod(
            TimePeriodPropertyType? prop,
            string fieldName,
            List<ValidationError> errors)
        {
            if (prop?.TimePeriod == null)
            {
                errors.Add(new ValidationError(
                    "ACL-MISSING-TIME-PERIOD",
                    $"Required validity period '{fieldName}' is missing.",
                    ErrorCategory.DomainMapping,
                    ValidationSeverity.Error,
                    fieldName));
                return (DateTimeOffset.MinValue, DateTimeOffset.MinValue);
            }

            return (
                NormalizeToUtc(prop.TimePeriod.BeginPosition),
                NormalizeToUtc(prop.TimePeriod.EndPosition));
        }

        private static FlightLevel ExtractFlightLevel(
            MeasureType? measure,
            string fieldName,
            List<ValidationError> errors)
        {
            if (measure == null || measure.IsNil)
            {
                errors.Add(new ValidationError(
                    "ACL-MISSING-FLIGHT-LEVEL",
                    $"Required vertical limit '{fieldName}' is missing.",
                    ErrorCategory.DomainMapping,
                    ValidationSeverity.Error,
                    fieldName));
                return default;
            }

            var flRes = FlightLevel.Create(measure.Value, measure.Uom, fieldName);
            if (flRes.IsFailure)
            {
                errors.AddRange(flRes.Errors);
                return default;
            }

            return flRes.Value;
        }

        private static DateTimeOffset NormalizeToUtc(DateTime dt)
        {
            if (dt.Kind == DateTimeKind.Unspecified)
            {
                return new DateTimeOffset(DateTime.SpecifyKind(dt, DateTimeKind.Utc));
            }
            return new DateTimeOffset(dt.ToUniversalTime());
        }
    }
}
