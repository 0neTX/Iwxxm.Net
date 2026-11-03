using System.Collections.Generic;
using Iwxxm.Net.Reports;
using Iwxxm20252.Generated;

namespace Iwxxm.Net.Infrastructure.Mapping.Wafs
{
    /// <summary>Extracts <see cref="JetStream"/> phenomena from WAFS <c>JETSTREAM</c> features and compact <c>jetStream</c> elements.</summary>
    internal static class JetStreamExtractor
    {
        private const string CoreFlightLevelMissingCode = "ACL-JS-CORE-FL";

        public static Result<JetStream> FromWafsFeature(MeteorologicalFeatureType feature)
        {
            string context = $"MeteorologicalFeature[@gml:id='{feature.Id}']";
            CurveType? curve = feature.PhenomenonGeometry?.Curve;

            var axisCoordsResult = SigwxGeometryReader.ReadCurveCoordinates(curve, $"{context}/phenomenonGeometry");
            if (axisCoordsResult.IsFailure)
            {
                return Result<JetStream>.Failure(axisCoordsResult.Errors);
            }

            var corePoints = new List<JetStreamCorePoint>();
            Speed? maxWindSpeed = null;
            VerticalLimit? maxWindLevel = null;

            foreach (PhenomenonPropertyRoleType prop in feature.PhenomenonProperties)
            {
                WAFSJetStreamWindSymbolType? symbol = prop.JetStreamWindSymbol;
                if (symbol == null) continue;

                string symbolContext = $"{context}/WAFSJetStreamWindSymbol[@gml:id='{symbol.Id}']";
                ElevatedPointType? elevatedPt = symbol.Location?.ElevatedPoint;
                var ptCoordResult = SigwxGeometryReader.ParsePoint(elevatedPt, symbolContext);
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

                var levelErrors = new List<ValidationError>();
                VerticalLimit? level = SigwxMeasureReader.ReadVertical(elevatedPt.Elevation, elevatedPt.VerticalReference, $"{symbolContext}/elevation", levelErrors);
                if (!level.HasValue)
                    return Result<JetStream>.Failure(levelErrors);

                var speedResult = SigwxMeasureReader.ReadRequiredSpeed(symbol.WindSpeed, $"{symbolContext}/windSpeed");
                if (speedResult.IsFailure)
                    return Result<JetStream>.Failure(speedResult.Errors);
                Speed windSpeed = speedResult.Value;

                VerticalLimit? lowerIso = SigwxMeasureReader.ReadVertical(symbol.IsotachLowerElevation, symbol.IsotachLowerVerticalReference, $"{context}/IsotachLowerElevation", levelErrors);
                VerticalLimit? upperIso = SigwxMeasureReader.ReadVertical(symbol.IsotachUpperElevation, symbol.IsotachUpperVerticalReference, $"{context}/IsotachUpperElevation", levelErrors);
                if (levelErrors.Count > 0)
                    return Result<JetStream>.Failure(levelErrors);

                if (!maxWindSpeed.HasValue || windSpeed.ToMetersPerSecond() > maxWindSpeed.Value.ToMetersPerSecond())
                {
                    maxWindSpeed = windSpeed;
                    maxWindLevel = level.Value;
                }

                corePoints.Add(new JetStreamCorePoint(
                    ptCoordResult.Value,
                    level.Value,
                    windSpeed,
                    lowerIso,
                    upperIso));
            }

            if (!maxWindSpeed.HasValue || !maxWindLevel.HasValue)
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
                coreLevel: maxWindLevel.Value,
                maxWindSpeed: maxWindSpeed.Value,
                corePoints: corePoints);
        }

        public static Result<JetStream> FromCompact(JetStreamType dto)
        {
            string context = $"jetStream[@gml:id='{dto.Id}']";
            var axisCoordsResult = SigwxGeometryReader.ReadCurveCoordinates(dto.Axis?.Curve, $"{context}/axis");
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

            var levelErrors = new List<ValidationError>();
            VerticalLimit? coreLevel = SigwxMeasureReader.ReadVertical(dto.FlightLevel, null, $"{context}/flightLevel", levelErrors);
            if (!coreLevel.HasValue)
                return Result<JetStream>.Failure(levelErrors);

            var windSpeedResult = SigwxMeasureReader.ReadRequiredSpeed(dto.WindSpeed, $"{context}/windSpeed");
            if (windSpeedResult.IsFailure)
                return Result<JetStream>.Failure(windSpeedResult.Errors);
            Speed windSpeed = windSpeedResult.Value;

            var corePoints = new List<JetStreamCorePoint>();
            if (dto.CorePoints != null)
            {
                foreach (JetStreamCorePointType cpDto in dto.CorePoints)
                {
                    var corePointResult = FromCompactCorePoint(cpDto, windSpeed, context);
                    if (corePointResult.IsFailure)
                        return Result<JetStream>.Failure(corePointResult.Errors);

                    corePoints.Add(corePointResult.Value);
                }
            }

            return JetStream.Create(dto.Id, axisCoordsResult.Value, coreLevel.Value, windSpeed, corePoints);
        }

        /// <summary>Reads a compact core point; its wind speed defaults to the jet stream speed only when the point reports none.</summary>
        private static Result<JetStreamCorePoint> FromCompactCorePoint(JetStreamCorePointType cpDto, Speed jetStreamSpeed, string context)
        {
            var ptResult = SigwxGeometryReader.ParsePoint(cpDto.Location?.Point, $"{context}/corePoint");
            if (ptResult.IsFailure)
                return Result<JetStreamCorePoint>.Failure(ptResult.Errors);

            if (cpDto.FlightLevel == null)
                return Result<JetStreamCorePoint>.Failure(CoreFlightLevelMissingCode, "Missing flightLevel in corePoint.", ErrorCategory.DomainMapping, context);

            var cpErrors = new List<ValidationError>();
            VerticalLimit? cpLevel = SigwxMeasureReader.ReadVertical(cpDto.FlightLevel, null, $"{context}/corePoint/flightLevel", cpErrors);
            VerticalLimit? lowerFl = SigwxMeasureReader.ReadVertical(cpDto.LowerFlightLevel, null, $"{context}/corePoint/lowerFlightLevel", cpErrors);
            VerticalLimit? upperFl = SigwxMeasureReader.ReadVertical(cpDto.UpperFlightLevel, null, $"{context}/corePoint/upperFlightLevel", cpErrors);
            if (cpErrors.Count > 0 || !cpLevel.HasValue)
                return Result<JetStreamCorePoint>.Failure(cpErrors.Count > 0
                    ? cpErrors
                    : new List<ValidationError> { new ValidationError(CoreFlightLevelMissingCode, "Missing flightLevel in corePoint.", ErrorCategory.DomainMapping, ValidationSeverity.Error, context) });

            Speed coreSpeed = jetStreamSpeed;
            if (cpDto.WindSpeed != null)
            {
                var coreSpeedResult = SigwxMeasureReader.ReadRequiredSpeed(cpDto.WindSpeed, $"{context}/corePoint/windSpeed");
                if (coreSpeedResult.IsFailure)
                    return Result<JetStreamCorePoint>.Failure(coreSpeedResult.Errors);
                coreSpeed = coreSpeedResult.Value;
            }

            return Result<JetStreamCorePoint>.Success(new JetStreamCorePoint(
                ptResult.Value,
                cpLevel.Value,
                coreSpeed,
                lowerFl,
                upperFl));
        }
    }
}
