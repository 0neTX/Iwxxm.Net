using System.Collections.Generic;
using System.Linq;
using Iwxxm.Net.Reports;
using Iwxxm20252.Generated;

namespace Iwxxm.Net.Infrastructure.Mapping.Wafs
{
    /// <summary>
    /// Reads the GML 3.2.1 geometries of the SIGWX transport model (<c>gml:posList</c>, <c>gml:Curve</c> with
    /// <c>gml:LineStringSegment</c> / <c>gml:CubicSpline</c>, <c>gml:Ring</c>, <c>gml:pos</c>) into Domain coordinates.
    /// </summary>
    internal static class SigwxGeometryReader
    {
        /// <summary>
        /// CRS metadata attributes (<c>@srsName</c>, <c>@axisLabels</c>, <c>@srsDimension</c>) of a transport geometry,
        /// inherited by nested geometries unless they carry their own.
        /// </summary>
        internal readonly struct SrsAttributes
        {
            public static readonly SrsAttributes None = default;

            private SrsAttributes(string? srsName, string? axisLabels, string? dimension)
            {
                SrsName = srsName;
                AxisLabels = axisLabels;
                Dimension = dimension;
            }

            public string? SrsName { get; }
            public string? AxisLabels { get; }
            public string? Dimension { get; }

            public static SrsAttributes From(string? srsName, string? axisLabels, bool dimensionSpecified, int dimension) =>
                new SrsAttributes(
                    string.IsNullOrWhiteSpace(srsName) ? null : srsName,
                    string.IsNullOrWhiteSpace(axisLabels) ? null : axisLabels,
                    dimensionSpecified ? dimension.ToString(System.Globalization.CultureInfo.InvariantCulture) : null);

            public SrsAttributes Override(SrsAttributes child) =>
                new SrsAttributes(child.SrsName ?? SrsName, child.AxisLabels ?? AxisLabels, child.Dimension ?? Dimension);

            public GmlCrs? Resolve(string location, List<ValidationError> errors) =>
                GmlCrs.Resolve(SrsName, AxisLabels, Dimension, location, errors);
        }

        /// <summary>
        /// Parses a <c>gml:posList</c> (<see cref="DirectPositionListType"/>) with the CRS metadata of the list itself or,
        /// when absent there, inherited from the enclosing geometry (<paramref name="inherited"/>); see <see cref="GmlCrs"/>.
        /// </summary>
        public static Result<List<GeoCoordinate>> ParsePosList(
            DirectPositionListType? posListDto,
            string contextLocation,
            SrsAttributes inherited)
        {
            if (posListDto == null || string.IsNullOrWhiteSpace(posListDto.Value))
            {
                return Result<List<GeoCoordinate>>.Failure(
                    "GML-POSLIST-EMPTY",
                    $"Element <gml:posList> at '{contextLocation}' is empty or missing.",
                    ErrorCategory.DomainMapping,
                    contextLocation);
            }

            var errors = new List<ValidationError>();
            SrsAttributes attributes = inherited.Override(SrsAttributes.From(posListDto.SrsName, posListDto.AxisLabels, posListDto.SrsDimensionSpecified, posListDto.SrsDimension));
            GmlCrs? crs = attributes.Resolve(contextLocation, errors);
            if (!crs.HasValue)
                return Result<List<GeoCoordinate>>.Failure(errors);

            Result<IReadOnlyList<GeoCoordinate>> parsed = GmlCoordinateTokenizer.ParsePosList(posListDto.Value, crs.Value, contextLocation);
            if (parsed.IsFailure)
            {
                return Result<List<GeoCoordinate>>.Failure(parsed.Errors);
            }

            return Result<List<GeoCoordinate>>.Success(parsed.Value as List<GeoCoordinate> ?? new List<GeoCoordinate>(parsed.Value));
        }

        /// <summary>
        /// Extracts a single geographic coordinate from a <c>&lt;gml:Point&gt;/&lt;gml:pos&gt;</c>
        /// or <c>&lt;iwxxm:ElevatedPoint&gt;/&lt;gml:pos&gt;</c> node, with the axis order of its CRS metadata.
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

            SrsAttributes attributes = SrsAttributes.From(pointDto.SrsName, pointDto.AxisLabels, pointDto.SrsDimensionSpecified, pointDto.SrsDimension)
                .Override(SrsAttributes.From(pointDto.Pos.SrsName, pointDto.Pos.AxisLabels, pointDto.Pos.SrsDimensionSpecified, pointDto.Pos.SrsDimension));
            return ParsePosition(pointDto.Pos.Value, attributes, contextLocation);
        }

        private static Result<GeoCoordinate> ParsePosition(string? posText, SrsAttributes attributes, string contextLocation)
        {
            var errors = new List<ValidationError>();
            GmlCrs? crs = attributes.Resolve(contextLocation, errors);
            return crs.HasValue
                ? GmlCoordinateTokenizer.ParseSinglePoint(posText, crs.Value, contextLocation)
                : Result<GeoCoordinate>.Failure(errors);
        }

        public static Result<List<GeoCoordinate>> ReadCurveCoordinates(CurveType? curve, string context) =>
            ReadCurveCoordinates(curve, context, SrsAttributes.None);

        public static Result<List<GeoCoordinate>> ReadCurveCoordinates(CurveType? curve, string context, SrsAttributes inherited)
        {
            if (curve?.Segments == null)
            {
                return Result<List<GeoCoordinate>>.Failure(
                    "ACL-CURVE-NO-SEGMENTS",
                    $"Curve at '{context}' does not contain any <gml:segments>.",
                    ErrorCategory.DomainMapping,
                    context);
            }

            SrsAttributes curveAttributes = inherited.Override(SrsAttributes.From(curve.SrsName, curve.AxisLabels, curve.SrsDimensionSpecified, curve.SrsDimension));
            var points = new List<GeoCoordinate>();

            // gml:segments is an ordered sequence: segments are concatenated in document order, whatever their type.
            foreach (AbstractCurveSegmentType segment in curve.Segments.Items)
            {
                Result<List<GeoCoordinate>> res;
                switch (segment)
                {
                    case LineStringSegmentType line:
                        res = ParsePosList(line.PosList, $"{context}/LineStringSegment", curveAttributes);
                        break;
                    case CubicSplineType spline:
                        res = ParsePosList(spline.PosList, $"{context}/CubicSpline", curveAttributes);
                        break;
                    default:
                        return Result<List<GeoCoordinate>>.Failure(
                            Xml.MappingErrors.UnsupportedSegmentCode,
                            $"Curve at '{context}' contains a '{segment.GetType().Name}' segment, which is not a sequence of positions; it is not approximated.",
                            ErrorCategory.DomainMapping,
                            context);
                }

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

        /// <summary>
        /// Reads every <c>gml:PolygonPatch</c> of an <c>iwxxm:ElevatedVolume</c>, in document order, as surfaces: a
        /// <see cref="GeoCircle"/> when the exterior ring is a single <c>gml:CircleByCenterPoint</c>, otherwise a
        /// <see cref="GeoPolygon"/> with its interior rings (holes).
        /// </summary>
        public static Result<List<GeoSurface>> ReadElevatedVolumeSurfaces(ElevatedVolumeType volume, string context)
        {
            SrsAttributes volumeAttributes = SrsAttributes.From(volume.SrsName, volume.AxisLabels, volume.SrsDimensionSpecified, volume.SrsDimension);
            List<PolygonPatchType>? patches = volume.Patches?.PolygonPatches;
            if (patches == null || patches.Count == 0 || patches.All(p => p.Exterior == null))
            {
                return Result<List<GeoSurface>>.Failure(
                    "ACL-VOLUME-NO-PATCH",
                    $"ElevatedVolume at '{context}' does not contain an exterior PolygonPatch ring.",
                    ErrorCategory.DomainMapping,
                    context);
            }

            var surfaces = new List<GeoSurface>(patches.Count);
            for (int i = 0; i < patches.Count; i++)
            {
                string patchContext = patches.Count == 1 ? context : $"{context}/PolygonPatch[{i + 1}]";
                Result<GeoSurface> surface = ReadSurface(patches[i].Exterior, patches[i].Interiors, patchContext, volumeAttributes);
                if (surface.IsFailure)
                    return Result<List<GeoSurface>>.Failure(surface.Errors);
                surfaces.Add(surface.Value);
            }

            return Result<List<GeoSurface>>.Success(surfaces);
        }

        /// <summary>Reads an exterior ring and its interior rings (holes) as a surface.</summary>
        public static Result<GeoSurface> ReadSurface(
            AbstractRingPropertyType? exteriorRing,
            IReadOnlyList<AbstractRingPropertyType> interiors,
            string context,
            SrsAttributes inherited)
        {
            if (interiors.Count == 0)
                return ReadSurface(exteriorRing, context, inherited);

            Result<List<GeoCoordinate>> exterior = ReadRingCoordinates(exteriorRing, context, inherited);
            if (exterior.IsFailure)
                return Result<GeoSurface>.Failure(exterior.Errors);

            var holes = new List<IEnumerable<GeoCoordinate>>(interiors.Count);
            for (int h = 0; h < interiors.Count; h++)
            {
                Result<List<GeoCoordinate>> hole = ReadRingCoordinates(interiors[h], $"{context}/interior[{h + 1}]", inherited);
                if (hole.IsFailure)
                    return Result<GeoSurface>.Failure(hole.Errors);
                holes.Add(hole.Value);
            }

            Result<GeoPolygon> polygon = GeoPolygon.Create(exterior.Value, holes, context);
            return polygon.IsSuccess
                ? Result<GeoSurface>.Success(polygon.Value)
                : Result<GeoSurface>.Failure(polygon.Errors);
        }

        /// <summary>
        /// Reads a ring as a surface: a single <c>gml:CircleByCenterPoint</c> segment yields a <see cref="GeoCircle"/>;
        /// any other ring yields a <see cref="GeoPolygon"/>.
        /// </summary>
        public static Result<GeoSurface> ReadSurface(AbstractRingPropertyType? exterior, string context, SrsAttributes inherited)
        {
            CircleByCenterPointType? circle = GetSingleCircle(exterior);
            if (circle != null)
            {
                CurveType curve = exterior!.Ring!.CurveMembers[0].Curve!;
                SrsAttributes circleAttributes = inherited.Override(SrsAttributes.From(curve.SrsName, curve.AxisLabels, curve.SrsDimensionSpecified, curve.SrsDimension));
                Result<GeoCircle> circleResult = ReadCircle(circle, $"{context}/CircleByCenterPoint", circleAttributes);
                return circleResult.IsSuccess
                    ? Result<GeoSurface>.Success(circleResult.Value)
                    : Result<GeoSurface>.Failure(circleResult.Errors);
            }

            Result<List<GeoCoordinate>> ring = ReadRingCoordinates(exterior, context, inherited);
            if (ring.IsFailure)
                return Result<GeoSurface>.Failure(ring.Errors);

            Result<GeoPolygon> polygon = GeoPolygon.Create(ring.Value, context);
            return polygon.IsSuccess
                ? Result<GeoSurface>.Success(polygon.Value)
                : Result<GeoSurface>.Failure(polygon.Errors);
        }

        private static CircleByCenterPointType? GetSingleCircle(AbstractRingPropertyType? exterior)
        {
            List<GmlCurvePropertyType>? members = exterior?.Ring?.CurveMembers;
            if (members == null || members.Count != 1)
                return null;

            List<AbstractCurveSegmentType>? segments = members[0].Curve?.Segments?.Items;
            return segments != null && segments.Count == 1 ? segments[0] as CircleByCenterPointType : null;
        }

        private static Result<GeoCircle> ReadCircle(CircleByCenterPointType circle, string context, SrsAttributes inherited)
        {
            SrsAttributes posAttributes = circle.Pos == null
                ? inherited
                : inherited.Override(SrsAttributes.From(circle.Pos.SrsName, circle.Pos.AxisLabels, circle.Pos.SrsDimensionSpecified, circle.Pos.SrsDimension));
            Result<GeoCoordinate> centre = ParsePosition(circle.Pos?.Value, posAttributes, context);
            if (centre.IsFailure)
                return Result<GeoCircle>.Failure(centre.Errors);

            var errors = new List<ValidationError>();
            Length? radius = circle.Radius == null || circle.Radius.IsNil
                ? (Length?)null
                : UomMeasureReader.ReadLength(circle.Radius.RawValue, circle.Radius.Uom, $"{context}/radius", errors);
            if (errors.Count > 0)
                return Result<GeoCircle>.Failure(errors);

            if (!radius.HasValue)
            {
                return Result<GeoCircle>.Failure(
                    Xml.MappingErrors.CircleRadiusMissingCode,
                    $"The circle at '{context}' has no gml:radius; no radius is assumed.",
                    ErrorCategory.DomainMapping,
                    context);
            }

            return GeoCircle.Create(centre.Value, radius.Value, context);
        }

        public static Result<List<GeoCoordinate>> ReadRingCoordinates(
            AbstractRingPropertyType? exterior,
            string context,
            SrsAttributes inherited)
        {
            if (exterior?.LinearRing != null)
            {
                return ParsePosList(exterior.LinearRing.PosList, $"{context}/LinearRing", inherited);
            }

            if (exterior?.Ring?.CurveMembers != null && exterior.Ring.CurveMembers.Count > 0)
            {
                var coords = new List<GeoCoordinate>();
                foreach (GmlCurvePropertyType curveMember in exterior.Ring.CurveMembers)
                {
                    var curveRes = ReadCurveCoordinates(curveMember.Curve, $"{context}/Ring/curveMember", inherited);
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
