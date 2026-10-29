using System.Collections.Generic;
using System.Xml.Linq;
using Iwxxm.Net.Reports;
using static Iwxxm.Net.Infrastructure.Mapping.Xml.IwxxmXmlNames;

namespace Iwxxm.Net.Infrastructure.Mapping.Xml
{
    /// <summary>
    /// Reads GML 3.2 geometries (<c>gml:Point</c>, <c>gml:PolygonPatch</c> with <c>gml:LinearRing</c> or
    /// <c>gml:Ring</c> curve members) into Domain spatial value objects (<c>[latitude longitude]</c> axis order).
    /// </summary>
    internal static class GmlGeometryReader
    {
        public static GeoCoordinate? ReadPoint(XElement? pointElem, string contextLocation, List<ValidationError> errors)
        {
            string? posText = pointElem?.Element(GmlNs + "pos")?.Value;
            if (string.IsNullOrWhiteSpace(posText))
                return null;

            var coordRes = GmlCoordinateTokenizer.ParseSinglePoint(posText!, contextLocation);
            if (coordRes.IsFailure)
            {
                errors.AddRange(coordRes.Errors);
                return null;
            }

            return coordRes.Value;
        }

        public static GeoPolygon? ReadPatchesPolygon(XElement? patchesElem, string contextLocation, List<ValidationError> errors)
        {
            XElement? patch = patchesElem?.Element(GmlNs + "PolygonPatch");
            if (patch == null)
                return null;

            XElement? extRing = patch.Element(GmlNs + "exterior");
            List<GeoCoordinate>? extCoords = ReadRingCoordinates(extRing, contextLocation, errors);
            if (extCoords == null || extCoords.Count == 0)
                return null;

            var interiorRings = new List<IReadOnlyList<GeoCoordinate>>();
            foreach (XElement intRing in patch.Elements(GmlNs + "interior"))
            {
                List<GeoCoordinate>? holeCoords = ReadRingCoordinates(intRing, contextLocation, errors);
                if (holeCoords != null && holeCoords.Count >= 3)
                {
                    interiorRings.Add(holeCoords);
                }
            }

            var polyRes = GeoPolygon.Create(extCoords, interiorRings, contextLocation);
            if (polyRes.IsFailure)
            {
                errors.AddRange(polyRes.Errors);
                return null;
            }

            return polyRes.Value;
        }

        private static List<GeoCoordinate>? ReadRingCoordinates(XElement? ringPropElem, string contextLocation, List<ValidationError> errors)
        {
            if (ringPropElem == null)
                return null;

            string? linearPosList = ringPropElem.Element(GmlNs + "LinearRing")?.Element(GmlNs + "posList")?.Value;
            if (!string.IsNullOrWhiteSpace(linearPosList))
            {
                var res = GmlCoordinateTokenizer.ParsePosList(linearPosList!, 2, contextLocation);
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
                        string? segPosList = seg.Element(GmlNs + "posList")?.Value;
                        if (!string.IsNullOrWhiteSpace(segPosList))
                        {
                            var res = GmlCoordinateTokenizer.ParsePosList(segPosList!, 2, contextLocation);
                            if (res.IsFailure)
                            {
                                errors.AddRange(res.Errors);
                                return null;
                            }
                            allCoords.AddRange(res.Value);
                        }
                    }
                }

                return allCoords.Count > 0 ? allCoords : null;
            }

            return null;
        }
    }
}
