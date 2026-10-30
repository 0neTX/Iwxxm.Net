using System.Collections.Generic;
using System.Xml.Linq;
using Iwxxm.Net.Reports;
using static Iwxxm.Net.Infrastructure.Mapping.Xml.IwxxmXmlNames;
using static Iwxxm.Net.Infrastructure.Mapping.Xml.XmlValueReader;

namespace Iwxxm.Net.Infrastructure.Mapping.Xml
{
    /// <summary>
    /// Reads the AIXM 5.1.1 features embedded in IWXXM reports (<c>aixm:AirportHeliport</c>, <c>aixm:Unit</c>,
    /// <c>aixm:Airspace</c>, <c>aixm:AirspaceVolume</c>) exactly as reported, inline or by local <c>xlink:href="#id"</c>.
    /// </summary>
    internal static class AixmFeatureReader
    {
        public static void ReadAerodrome(
            XElement? aerodromeProp,
            List<ValidationError> errors,
            out string designator,
            out string? name,
            out GeoCoordinate? position)
        {
            designator = string.Empty;
            name = null;
            position = null;

            XElement? slice = GmlReferenceResolver.Content(aerodromeProp, AixmNs + "AirportHeliport")
                ?.Element(AixmNs + "timeSlice")
                ?.Element(AixmNs + "AirportHeliportTimeSlice");

            if (slice == null)
                return;

            designator = GetText(slice.Element(AixmNs + "designator"))
                         ?? GetText(slice.Element(AixmNs + "locationIndicatorICAO"))
                         ?? string.Empty;
            name = GetText(slice.Element(AixmNs + "name"));

            XElement? arpPt = slice
                .Element(AixmNs + "ARP")
                ?.Element(AixmNs + "ElevatedPoint");

            if (arpPt != null)
            {
                position = GmlGeometryReader.ReadPoint(arpPt, GetGmlId(arpPt), errors);
            }
        }

        /// <summary>
        /// Reads an <c>aixm:Unit</c> time slice. The designator is <c>null</c> when <c>aixm:designator</c> is absent
        /// (it is not replaced by the unit name).
        /// </summary>
        public static void ReadUnit(
            XElement? unitProp,
            out string? designator,
            out string? name)
        {
            designator = null;
            name = null;

            XElement? slice = GmlReferenceResolver.Content(unitProp, AixmNs + "Unit")
                ?.Element(AixmNs + "timeSlice")
                ?.Element(AixmNs + "UnitTimeSlice");

            if (slice == null)
                return;

            name = GetText(slice.Element(AixmNs + "name"));
            designator = GetText(slice.Element(AixmNs + "designator"));
        }

        public static void ReadAirspace(
            XElement? airspaceProp,
            out string? designator,
            out string? name)
        {
            designator = null;
            name = null;

            XElement? slice = GmlReferenceResolver.Content(airspaceProp, AixmNs + "Airspace")
                ?.Element(AixmNs + "timeSlice")
                ?.Element(AixmNs + "AirspaceTimeSlice");

            if (slice == null)
                return;

            designator = GetText(slice.Element(AixmNs + "designator"));
            name = GetText(slice.Element(AixmNs + "name"));
        }

        /// <summary>
        /// Reads <c>aixm:lowerLimit</c> / <c>aixm:lowerLimitReference</c> and <c>aixm:upperLimit</c> /
        /// <c>aixm:upperLimitReference</c> exactly as reported (no flight-level conversion, no assumed reference).
        /// </summary>
        public static void ReadAirspaceVolumeLimits(
            XElement? volElem,
            string context,
            List<ValidationError> errors,
            out VerticalLimit? lower,
            out VerticalLimit? upper)
        {
            lower = VerticalLimitReader.Read(volElem?.Element(AixmNs + "lowerLimit"), volElem?.Element(AixmNs + "lowerLimitReference"), context, errors);
            upper = VerticalLimitReader.Read(volElem?.Element(AixmNs + "upperLimit"), volElem?.Element(AixmNs + "upperLimitReference"), context, errors);
        }

        /// <summary>Returns the <c>gml:patches</c> of the horizontal projection of an <c>aixm:AirspaceVolume</c>, or <c>null</c>.</summary>
        public static XElement? GetHorizontalProjectionPatches(XElement? volElem) =>
            volElem
                ?.Element(AixmNs + "horizontalProjection")
                ?.Element(AixmNs + "Surface")
                ?.Element(GmlNs + "patches");
    }
}
