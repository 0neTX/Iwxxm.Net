using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Iwxxm.Net.Reports;
using static Iwxxm.Net.Infrastructure.Mapping.Xml.IwxxmXmlNames;

namespace Iwxxm.Net.Infrastructure.Mapping.Xml
{
    /// <summary>
    /// Reads GML 3.2 geometries (<c>gml:Point</c>, <c>gml:PolygonPatch</c> with <c>gml:LinearRing</c> or
    /// <c>gml:Ring</c> curve members, <c>gml:CircleByCenterPoint</c>) into Domain spatial value objects. The axis order
    /// and dimension come from the inherited <c>@srsName</c> / <c>@axisLabels</c> / <c>@srsDimension</c> (<see cref="GmlCrs"/>).
    /// </summary>
    internal static class GmlGeometryReader
    {
        public static GeoCoordinate? ReadPoint(XElement? pointElem, string contextLocation, List<ValidationError> errors)
        {
            XElement? posElem = pointElem?.Element(GmlNs + "pos");
            string? posText = posElem?.Value;
            if (posElem == null || string.IsNullOrWhiteSpace(posText))
                return null;

            GmlCrs? crs = GmlCrs.For(posElem, contextLocation, errors);
            if (!crs.HasValue)
                return null;

            var coordRes = GmlCoordinateTokenizer.ParseSinglePoint(posText!, crs.Value, contextLocation);
            if (coordRes.IsFailure)
            {
                errors.AddRange(coordRes.Errors);
                return null;
            }

            return coordRes.Value;
        }

        /// <summary>
        /// Reads the single <c>gml:PolygonPatch</c> of a <c>gml:patches</c> element as a polygon. A patch whose exterior is
        /// a <c>gml:CircleByCenterPoint</c> is not a polygon: use <see cref="ReadPatchesSurface"/> for surfaces that may be
        /// circles.
        /// </summary>
        public static GeoPolygon? ReadPatchesPolygon(XElement? patchesElem, string contextLocation, List<ValidationError> errors) =>
            ReadPatchesSurface(patchesElem, contextLocation, errors) as GeoPolygon;

        /// <summary>
        /// Reads the single <c>gml:PolygonPatch</c> of a <c>gml:patches</c> element as a <see cref="GeoSurface"/>: a
        /// <see cref="GeoCircle"/> when the exterior ring is a single <c>gml:CircleByCenterPoint</c>, otherwise a
        /// <see cref="GeoPolygon"/>. Returns <c>null</c> when there is no patch, or when the circle centre is the
        /// unspecified placeholder position <c>9999 9999</c> (official space weather NIGHTSIDE example).
        /// </summary>
        /// <remarks>
        /// For consumers whose Domain model holds one surface (an en-route volume, an ash cloud contour). A
        /// <c>gml:patches</c> element with several patches is reported as <see cref="MappingErrors.MultiPatchUnsupportedCode"/> instead of
        /// silently keeping the first patch; use <see cref="ReadPatchesSurfaces"/> where the Domain holds several surfaces.
        /// </remarks>
        public static GeoSurface? ReadPatchesSurface(XElement? patchesElem, string contextLocation, List<ValidationError> errors)
        {
            if (patchesElem == null)
                return null;

            List<XElement> patches = patchesElem.Elements(GmlNs + "PolygonPatch").ToList();
            if (patches.Count > 1)
            {
                errors.Add(MappingErrors.Create(
                    MappingErrors.MultiPatchUnsupportedCode,
                    $"The surface at '{contextLocation}' has {patches.Count} gml:PolygonPatch elements, but a single surface is expected here; the patches are not merged or truncated.",
                    contextLocation));
                return null;
            }

            return patches.Count == 1 ? ReadPatch(patches[0], contextLocation, errors) : null;
        }

        /// <summary>
        /// Reads every <c>gml:PolygonPatch</c> of a <c>gml:patches</c> element, in document order (a patch that is the
        /// unspecified circle placeholder is skipped). For Domain models that hold several surfaces (space weather regions).
        /// </summary>
        public static IReadOnlyList<GeoSurface> ReadPatchesSurfaces(XElement? patchesElem, string contextLocation, List<ValidationError> errors)
        {
            var surfaces = new List<GeoSurface>();
            if (patchesElem == null)
                return surfaces;

            foreach (XElement patch in patchesElem.Elements(GmlNs + "PolygonPatch"))
            {
                GeoSurface? surface = ReadPatch(patch, contextLocation, errors);
                if (surface != null)
                    surfaces.Add(surface);
            }

            return surfaces;
        }

        private static GeoSurface? ReadPatch(XElement patch, string contextLocation, List<ValidationError> errors)
        {
            XElement? extRing = patch.Element(GmlNs + "exterior");
            XElement? circle = GetSingleCircleSegment(extRing);
            if (circle != null)
            {
                return ReadCircle(circle, contextLocation, errors);
            }

            List<GeoCoordinate>? extCoords = ReadRingCoordinates(extRing, contextLocation, errors);
            if (extCoords == null || extCoords.Count == 0)
                return null;

            var interiorRings = new List<IReadOnlyList<GeoCoordinate>>();
            foreach (XElement intRing in patch.Elements(GmlNs + "interior"))
            {
                int errorsBefore = errors.Count;
                List<GeoCoordinate>? holeCoords = ReadRingCoordinates(intRing, contextLocation, errors);
                if (holeCoords == null || holeCoords.Count == 0)
                {
                    // An interior ring that cannot be read is an error, never a silently dropped hole. The reading
                    // problem, when there is one, is already reported by the ring reader.
                    if (errors.Count == errorsBefore)
                    {
                        errors.Add(MappingErrors.Create(
                            MappingErrors.InteriorRingUnreadableCode,
                            $"A gml:interior ring at '{contextLocation}' has no readable positions; the hole is not dropped silently.",
                            contextLocation));
                    }

                    return null;
                }

                // Rings with too few positions are rejected by GeoPolygon.Create (GEO-POLYGON-HOLE-DEGENERATE).
                interiorRings.Add(holeCoords);
            }

            var polyRes = GeoPolygon.Create(extCoords, interiorRings, contextLocation);
            if (polyRes.IsFailure)
            {
                errors.AddRange(polyRes.Errors);
                return null;
            }

            return polyRes.Value;
        }

        /// <summary>
        /// Reads a <c>gml:CircleByCenterPoint</c> (<c>gml:pos</c> centre and <c>gml:radius</c> with <c>@uom</c>).
        /// </summary>
        public static GeoCircle? ReadCircle(XElement circleElem, string contextLocation, List<ValidationError> errors)
        {
            string location = contextLocation + "/CircleByCenterPoint";
            string? posText = circleElem.Element(GmlNs + "pos")?.Value;
            if (IsUnspecifiedPosition(posText))
                return null;

            GeoCoordinate? centre = ReadPoint(circleElem, location, errors);
            XElement? radiusElem = circleElem.Element(GmlNs + "radius");
            if (radiusElem == null || string.IsNullOrWhiteSpace(radiusElem.Value))
            {
                errors.Add(MappingErrors.Create(MappingErrors.CircleRadiusMissingCode, $"The circle at '{location}' has no gml:radius; no radius is assumed.", location));
                return null;
            }

            Length? radius = UomMeasureReader.ReadLength(radiusElem, location, errors);
            if (!centre.HasValue || !radius.HasValue)
                return null;

            Result<GeoCircle> circle = GeoCircle.Create(centre.Value, radius.Value, location);
            if (circle.IsFailure)
            {
                errors.AddRange(circle.Errors);
                return null;
            }

            return circle.Value;
        }

        /// <summary>
        /// Returns the <c>gml:CircleByCenterPoint</c> of a ring made of exactly one curve segment, or <c>null</c>.
        /// </summary>
        private static XElement? GetSingleCircleSegment(XElement? ringPropElem)
        {
            XElement? ring = ringPropElem?.Element(GmlNs + "Ring");
            if (ring == null)
                return null;

            var segments = new List<XElement>();
            foreach (XElement curveMember in ring.Elements(GmlNs + "curveMember"))
            {
                XElement? segmentsElem = curveMember.Element(GmlNs + "Curve")?.Element(GmlNs + "segments");
                if (segmentsElem != null)
                    segments.AddRange(segmentsElem.Elements());
            }

            return segments.Count == 1 && segments[0].Name == GmlNs + "CircleByCenterPoint" ? segments[0] : null;
        }

        private static bool IsUnspecifiedPosition(string? posText)
        {
            if (string.IsNullOrWhiteSpace(posText))
                return false;

            string[] tokens = posText!.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length < 2)
                return false;

            foreach (string token in tokens)
            {
                if (token != "9999")
                    return false;
            }

            return true;
        }

        private static List<GeoCoordinate>? ReadRingCoordinates(XElement? ringPropElem, string contextLocation, List<ValidationError> errors)
        {
            if (ringPropElem == null)
                return null;

            XElement? linearPosListElem = ringPropElem.Element(GmlNs + "LinearRing")?.Element(GmlNs + "posList");
            string? linearPosList = linearPosListElem?.Value;
            if (linearPosListElem != null && !string.IsNullOrWhiteSpace(linearPosList))
            {
                GmlCrs? crs = GmlCrs.For(linearPosListElem, contextLocation, errors);
                if (!crs.HasValue)
                    return null;

                var res = GmlCoordinateTokenizer.ParsePosList(linearPosList!, crs.Value, contextLocation);
                if (res.IsFailure)
                {
                    errors.AddRange(res.Errors);
                    return null;
                }
                return res.Value as List<GeoCoordinate> ?? new List<GeoCoordinate>(res.Value);
            }

            XElement? ringElem = ringPropElem.Element(GmlNs + "Ring");
            if (ringElem != null)
            {
                var allCoords = new List<GeoCoordinate>();
                foreach (XElement curveMember in ringElem.Elements(GmlNs + "curveMember"))
                {
                    XElement? segments = curveMember.Element(GmlNs + "Curve")?.Element(GmlNs + "segments");
                    if (segments == null) continue;

                    foreach (XElement seg in segments.Elements())
                    {
                        if (!IsLinearSegment(seg))
                        {
                            errors.Add(MappingErrors.Create(
                                MappingErrors.UnsupportedSegmentCode,
                                $"The curve segment 'gml:{seg.Name.LocalName}' at '{contextLocation}' cannot be combined with other segments of a ring; the ring is not approximated.",
                                contextLocation));
                            return null;
                        }

                        XElement? segPosListElem = seg.Element(GmlNs + "posList");
                        string? segPosList = segPosListElem?.Value;
                        if (segPosListElem != null && !string.IsNullOrWhiteSpace(segPosList))
                        {
                            GmlCrs? crs = GmlCrs.For(segPosListElem, contextLocation, errors);
                            if (!crs.HasValue)
                                return null;

                            var res = GmlCoordinateTokenizer.ParsePosList(segPosList!, crs.Value, contextLocation);
                            if (res.IsFailure)
                            {
                                errors.AddRange(res.Errors);
                                return null;
                            }
                            AppendJoined(allCoords, res.Value);
                        }
                    }
                }

                return allCoords.Count > 0 ? allCoords : null;
            }

            return null;
        }

        /// <summary>
        /// Appends the positions of a segment to a ring (AUD-L2): consecutive curve members and segments share their joint
        /// vertex (the end of one is the start of the next), so that shared vertex is kept once instead of being repeated.
        /// </summary>
        private static void AppendJoined(List<GeoCoordinate> ring, IReadOnlyList<GeoCoordinate> segment)
        {
            int start = ring.Count > 0 && segment.Count > 0 && ring[ring.Count - 1] == segment[0] ? 1 : 0;
            for (int i = start; i < segment.Count; i++)
            {
                ring.Add(segment[i]);
            }
        }

        /// <summary>Segments interpolated through their control points (<c>gml:posList</c>).</summary>
        private static bool IsLinearSegment(XElement segment) =>
            segment.Name == GmlNs + "LineStringSegment" ||
            segment.Name == GmlNs + "GeodesicString" ||
            segment.Name == GmlNs + "CubicSpline";
    }
}
