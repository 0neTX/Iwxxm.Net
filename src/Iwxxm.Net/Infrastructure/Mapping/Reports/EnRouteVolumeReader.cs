using System.Collections.Generic;
using System.Xml.Linq;
using Iwxxm.Net.Infrastructure.Mapping.Xml;
using Iwxxm.Net.Reports;
using static Iwxxm.Net.Infrastructure.Mapping.Xml.IwxxmXmlNames;
using static Iwxxm.Net.Infrastructure.Mapping.Xml.XmlValueReader;

namespace Iwxxm.Net.Infrastructure.Mapping.Reports
{
    /// <summary>
    /// Reads the en-route phenomenon volumes shared by SIGMET, AIRMET and Volcanic Ash Advisory reports: an
    /// <c>aixm:AirspaceVolume</c> (horizontal projection plus vertical limits) with optional motion.
    /// </summary>
    internal static class EnRouteVolumeReader
    {
        /// <summary>Reads a SIGMET/AIRMET evolving condition or SIGMET forecast position.</summary>
        public static EnRoutePhenomenonVolume ReadConditionVolume(XElement condElem, List<ValidationError> errors)
        {
            string id = GetGmlId(condElem);
            string? intensityChange = condElem.Attribute("intensityChange")?.Value;
            double? dir = UomMeasureReader.ReadFixedUnit(condElem.Element(IwxxmNs + "directionOfMotion"), UcumUnits.Degree, id, errors);
            Speed? spd = UomMeasureReader.ReadSpeed(condElem.Element(IwxxmNs + "speedOfMotion"), id, errors);

            GeoCoordinate? tcPos = GmlGeometryReader.ReadPoint(
                condElem.Element(IwxxmNs + "tropicalCyclonePosition")?.Element(GmlNs + "Point"),
                id,
                errors);

            XElement? volElem = condElem.Element(IwxxmNs + "geometry")?.Element(AixmNs + "AirspaceVolume");
            AixmFeatureReader.ReadAirspaceVolumeLimits(volElem, id, errors, out VerticalLimit? lower, out VerticalLimit? upper);
            GeoPolygon? area = ReadArea(volElem, id, errors);

            return new EnRoutePhenomenonVolume(id, area, lower, upper, intensityChange, dir, spd, tcPos);
        }

        /// <summary>Reads the <c>iwxxm:member</c> volumes named <paramref name="memberName"/> of a SIGMET/AIRMET collection.</summary>
        public static void ReadCollectionMembers(XElement? collection, string memberName, List<EnRoutePhenomenonVolume> target, List<ValidationError> errors)
        {
            if (collection == null)
                return;

            foreach (XElement member in collection.Elements(IwxxmNs + "member"))
            {
                XElement? volume = member.Element(IwxxmNs + memberName);
                if (volume != null)
                {
                    target.Add(ReadConditionVolume(volume, errors));
                }
            }
        }

        /// <summary>Reads a Volcanic Ash Advisory observed/estimated or forecast ash cloud.</summary>
        public static EnRoutePhenomenonVolume ReadAshCloudVolume(XElement ashElem, List<ValidationError> errors)
        {
            string id = GetGmlId(ashElem);
            double? dir = UomMeasureReader.ReadFixedUnit(ashElem.Element(IwxxmNs + "directionOfMotion"), UcumUnits.Degree, id, errors);
            Speed? spd = UomMeasureReader.ReadSpeed(ashElem.Element(IwxxmNs + "speedOfMotion"), id, errors);

            XElement? volElem = ashElem.Element(IwxxmNs + "ashCloudExtent")?.Element(AixmNs + "AirspaceVolume");
            AixmFeatureReader.ReadAirspaceVolumeLimits(volElem, id, errors, out VerticalLimit? lower, out VerticalLimit? upper);
            GeoPolygon? area = ReadArea(volElem, id, errors);

            return new EnRoutePhenomenonVolume(id, area, lower, upper, null, dir, spd);
        }

        private static GeoPolygon? ReadArea(XElement? volElem, string id, List<ValidationError> errors)
        {
            XElement? patches = AixmFeatureReader.GetHorizontalProjectionPatches(volElem);
            return patches != null ? GmlGeometryReader.ReadPatchesPolygon(patches, id, errors) : null;
        }
    }
}
