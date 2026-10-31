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
                PolygonType polygon = feature.PhenomenonGeometry.Polygon;
                SigwxGeometryReader.SrsAttributes srs = SigwxGeometryReader.SrsAttributes.From(
                    polygon.SrsName, polygon.AxisLabels, polygon.SrsDimensionSpecified, polygon.SrsDimension);

                var ringCoordsRes = SigwxGeometryReader.ReadRingCoordinates(polygon.Exterior, context, srs);
                if (ringCoordsRes.IsFailure)
                    return Result<Tropopause>.Failure(ringCoordsRes.Errors);

                // Interior rings (holes) of the contour are kept (AUD-L4).
                var holes = new List<IEnumerable<GeoCoordinate>>();
                for (int h = 0; h < polygon.Interiors.Count; h++)
                {
                    var holeRes = SigwxGeometryReader.ReadRingCoordinates(polygon.Interiors[h], $"{context}/interior[{h + 1}]", srs);
                    if (holeRes.IsFailure)
                        return Result<Tropopause>.Failure(holeRes.Errors);
                    holes.Add(holeRes.Value);
                }

                var polyRes = GeoPolygon.Create(ringCoordsRes.Value, holes, context);
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
