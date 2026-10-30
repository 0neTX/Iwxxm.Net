using System.Collections.Generic;
using System.Linq;
using Iwxxm.Net.Reports;
using Iwxxm20252.Generated;

namespace Iwxxm.Net.Infrastructure.Mapping.Wafs
{
    /// <summary>Extracts <see cref="Tropopause"/> heights from WAFS <c>TROPOPAUSE</c> features and compact <c>tropopause</c> elements.</summary>
    internal static class TropopauseExtractor
    {
        public static Result<Tropopause> FromWafsFeature(MeteorologicalFeatureType feature)
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

            var levelErrors = new List<ValidationError>();
            VerticalLimit? level = SigwxMeasureReader.ReadVertical(elevatedLevel.Elevation, elevatedLevel.VerticalReference, $"{context}/ElevatedLevel/elevation", levelErrors);
            if (!level.HasValue)
                return Result<Tropopause>.Failure(levelErrors);

            var spotHeights = new List<TropopauseSpotHeight>();
            GeoPolygon? contourPolygon = null;

            if (feature.PhenomenonGeometry?.Polygon != null)
            {
                var ringCoordsRes = SigwxGeometryReader.ReadRingCoordinates(
                    feature.PhenomenonGeometry.Polygon.Exterior,
                    context,
                    SigwxGeometryReader.SrsAttributes.From(
                        feature.PhenomenonGeometry.Polygon.SrsName,
                        feature.PhenomenonGeometry.Polygon.AxisLabels,
                        feature.PhenomenonGeometry.Polygon.SrsDimensionSpecified,
                        feature.PhenomenonGeometry.Polygon.SrsDimension));

                if (ringCoordsRes.IsFailure)
                    return Result<Tropopause>.Failure(ringCoordsRes.Errors);

                var polyRes = GeoPolygon.Create(ringCoordsRes.Value, context);
                if (polyRes.IsFailure)
                    return Result<Tropopause>.Failure(polyRes.Errors);

                contourPolygon = polyRes.Value;
                foreach (GeoCoordinate vertex in ringCoordsRes.Value)
                {
                    spotHeights.Add(new TropopauseSpotHeight(vertex, level.Value));
                }
            }
            else if (feature.PhenomenonGeometry?.Point != null)
            {
                var ptRes = SigwxGeometryReader.ParsePoint(feature.PhenomenonGeometry.Point, context);
                if (ptRes.IsFailure)
                    return Result<Tropopause>.Failure(ptRes.Errors);

                spotHeights.Add(new TropopauseSpotHeight(ptRes.Value, level.Value));
            }

            // WAFSSignificantWeatherForecast does not report a level type: none is invented.
            return Tropopause.Create(feature.Id, null, spotHeights, contourPolygon);
        }

        public static Result<Tropopause> FromCompact(TropopauseType dto)
        {
            string context = $"tropopause[@gml:id='{dto.Id}']";
            var spotHeights = new List<TropopauseSpotHeight>();

            if (dto.SpotHeights != null)
            {
                foreach (TropopausePointType ptDto in dto.SpotHeights)
                {
                    var coordResult = SigwxGeometryReader.ParsePoint(ptDto.Location?.Point, $"{context}/spotHeight");
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

                    var levelErrors = new List<ValidationError>();
                    VerticalLimit? level = SigwxMeasureReader.ReadVertical(ptDto.FlightLevel, null, $"{context}/spotHeight/flightLevel", levelErrors);
                    if (!level.HasValue)
                        return Result<Tropopause>.Failure(levelErrors);

                    spotHeights.Add(new TropopauseSpotHeight(coordResult.Value, level.Value));
                }
            }

            return Tropopause.Create(dto.Id, dto.LevelType, spotHeights);
        }
    }
}
