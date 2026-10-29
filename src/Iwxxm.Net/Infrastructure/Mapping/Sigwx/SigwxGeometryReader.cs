using System.Collections.Generic;
using System.Linq;
using Iwxxm.Net.Reports;
using Iwxxm20252.Generated;

namespace Iwxxm.Net.Infrastructure.Mapping.Sigwx
{
    /// <summary>
    /// Reads the GML 3.2.1 geometries of the SIGWX transport model (<c>gml:posList</c>, <c>gml:Curve</c> with
    /// <c>gml:LineStringSegment</c> / <c>gml:CubicSpline</c>, <c>gml:Ring</c>, <c>gml:pos</c>) into Domain coordinates.
    /// </summary>
    internal static class SigwxGeometryReader
    {
        /// <summary>
        /// Extracts and parses a flat whitespace-delimited sequence of coordinates from an XSD-generated
        /// <c>&lt;gml:posList&gt;</c> (<see cref="DirectPositionListType"/>) into a <see cref="List{GeoCoordinate}"/>.
        /// Under the ICAO/WMO IWXXM profile (CRS EPSG:4326 WGS84), axis order is strictly [Latitude Longitude].
        /// </summary>
        public static Result<List<GeoCoordinate>> ParsePosList(
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

            Result<IReadOnlyList<GeoCoordinate>> parsed = GmlCoordinateTokenizer.ParsePosList(posListDto.Value, dimension, contextLocation);
            if (parsed.IsFailure)
            {
                return Result<List<GeoCoordinate>>.Failure(parsed.Errors);
            }

            return Result<List<GeoCoordinate>>.Success(parsed.Value as List<GeoCoordinate> ?? new List<GeoCoordinate>(parsed.Value));
        }

        /// <summary>
        /// Extracts a single geographic coordinate from a <c>&lt;gml:Point&gt;/&lt;gml:pos&gt;</c>
        /// or <c>&lt;iwxxm:ElevatedPoint&gt;/&lt;gml:pos&gt;</c> node.
        /// </summary>
        public static Result<GeoCoordinate> ParsePoint(PointType? pointDto, string contextLocation)
        {
            if (pointDto?.Pos == null || string.IsNullOrWhiteSpace(pointDto.Pos.Value))
            {
                return Result<GeoCoordinate>.Failure(
                    "GML-POINT-EMPTY",
                    $"Element <gml:Point>/<gml:pos> at '{contextLocation}' is empty or missing.",
                    ErrorCategory.DomainMapping,
                    contextLocation);
            }

            return GmlCoordinateTokenizer.ParseSinglePoint(pointDto.Pos.Value, contextLocation);
        }

        public static Result<List<GeoCoordinate>> ReadCurveCoordinates(CurveType? curve, string context)
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
                var res = ParsePosList(segment.PosList, $"{context}/LineStringSegment", curveDim);
                if (res.IsFailure) return res;
                AppendDeduplicatedCoordinates(points, res.Value);
            }

            foreach (CubicSplineType spline in curve.Segments.CubicSplines)
            {
                var res = ParsePosList(spline.PosList, $"{context}/CubicSpline", curveDim);
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

        public static Result<List<GeoCoordinate>> ReadElevatedVolumeRingCoordinates(ElevatedVolumeType volume, string context)
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

            return ReadRingCoordinates(firstPatch.Exterior, context, defaultDim);
        }

        public static Result<List<GeoCoordinate>> ReadRingCoordinates(
            AbstractRingPropertyType? exterior,
            string context,
            int defaultDim)
        {
            if (exterior?.LinearRing != null)
            {
                return ParsePosList(exterior.LinearRing.PosList, $"{context}/LinearRing", defaultDim);
            }

            if (exterior?.Ring?.CurveMembers != null && exterior.Ring.CurveMembers.Count > 0)
            {
                var coords = new List<GeoCoordinate>();
                foreach (GmlCurvePropertyType curveMember in exterior.Ring.CurveMembers)
                {
                    var curveRes = ReadCurveCoordinates(curveMember.Curve, $"{context}/Ring/curveMember");
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
    }
}
