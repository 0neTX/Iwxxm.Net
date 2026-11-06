using System;
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
        /// <summary>Reads a SIGMET/AIRMET evolving condition or SIGMET forecast position with the time context of its collection.</summary>
        public static EnRoutePhenomenonVolume ReadConditionVolume(
            XElement condElem,
            TimeIndicator? timeIndicator,
            DateTimeOffset? phenomenonTimeUtc,
            List<ValidationError> errors,
            UomAliasCollector aliases)
        {
            string id = GetGmlId(condElem);
            string? intensityChange = condElem.Attribute("intensityChange")?.Value;
            double? dir = UomMeasureReader.ReadFixedUnit(condElem.Element(IwxxmNs + "directionOfMotion"), UcumUnits.Degree, id, errors);
            Speed? spd = UomMeasureReader.ReadSpeed(condElem.Element(IwxxmNs + "speedOfMotion"), id, errors, UomMeasureReader.Subsets.SpeedOfMotion, aliases);

            GeoCoordinate? tcPos = GmlGeometryReader.ReadPoint(
                condElem.Element(IwxxmNs + "tropicalCyclonePosition")?.Element(GmlNs + "Point"),
                id,
                errors);

            XElement? volElem = condElem.Element(IwxxmNs + "geometry")?.Element(AixmNs + "AirspaceVolume");
            AixmFeatureReader.ReadAirspaceVolumeLimits(volElem, id, errors, out VerticalLimit? lower, out VerticalLimit? upper);
            GeoSurface? area = ReadArea(volElem, id, errors);

            return new EnRoutePhenomenonVolume(id, area, lower, upper, intensityChange, dir, spd, tcPos, timeIndicator, phenomenonTimeUtc);
        }

        /// <summary>
        /// Reads the <c>iwxxm:member</c> volumes named <paramref name="memberName"/> of a SIGMET/AIRMET collection, each
        /// with the collection <c>@timeIndicator</c> and <c>phenomenonTime</c> (inline or by local reference).
        /// </summary>
        public static void ReadCollectionMembers(XElement? collection, string memberName, List<EnRoutePhenomenonVolume> target, List<ValidationError> errors, UomAliasCollector aliases)
        {
            if (collection == null)
                return;

            ReadCollectionTime(collection, errors, out TimeIndicator? timeIndicator, out DateTimeOffset? phenomenonTime);
            foreach (XElement member in collection.Elements(IwxxmNs + "member"))
            {
                XElement? volume = GmlReferenceResolver.Content(member, IwxxmNs + memberName);
                if (volume != null)
                {
                    target.Add(ReadConditionVolume(volume, timeIndicator, phenomenonTime, errors, aliases));
                }
            }
        }

        /// <summary>Reads the <c>@timeIndicator</c> and <c>phenomenonTime</c> of a SIGMET/AIRMET condition collection.</summary>
        public static void ReadCollectionTime(XElement collection, List<ValidationError> errors, out TimeIndicator? timeIndicator, out DateTimeOffset? phenomenonTime)
        {
            string id = GetGmlId(collection);
            timeIndicator = TimeIndicatorReader.Read(collection, id, errors);
            phenomenonTime = GmlTimeReader.ReadOptionalInstant(collection.Element(IwxxmNs + "phenomenonTime"), "phenomenonTime", id, errors);
        }

        /// <summary>Reads a Volcanic Ash Advisory observed/estimated or forecast ash cloud.</summary>
        public static EnRoutePhenomenonVolume ReadAshCloudVolume(XElement ashElem, DateTimeOffset? phenomenonTimeUtc, List<ValidationError> errors, UomAliasCollector aliases)
        {
            string id = GetGmlId(ashElem);
            double? dir = UomMeasureReader.ReadFixedUnit(ashElem.Element(IwxxmNs + "directionOfMotion"), UcumUnits.Degree, id, errors);
            Speed? spd = UomMeasureReader.ReadSpeed(ashElem.Element(IwxxmNs + "speedOfMotion"), id, errors, UomMeasureReader.Subsets.SpeedOfMotion, aliases);

            XElement? volElem = ashElem.Element(IwxxmNs + "ashCloudExtent")?.Element(AixmNs + "AirspaceVolume");
            AixmFeatureReader.ReadAirspaceVolumeLimits(volElem, id, errors, out VerticalLimit? lower, out VerticalLimit? upper);
            GeoSurface? area = ReadArea(volElem, id, errors);

            return new EnRoutePhenomenonVolume(id, area, lower, upper, null, dir, spd, null, null, phenomenonTimeUtc);
        }

        /// <summary>Reads an <c>aixm:AirspaceVolume</c> (vertical limits and horizontal projection, polygon or circle).</summary>
        public static EnRoutePhenomenonVolume ReadAirspaceVolume(XElement volElem, List<ValidationError> errors)
        {
            string id = GetGmlId(volElem);
            AixmFeatureReader.ReadAirspaceVolumeLimits(volElem, id, errors, out VerticalLimit? lower, out VerticalLimit? upper);
            return new EnRoutePhenomenonVolume(id, ReadArea(volElem, id, errors), lower, upper);
        }

        private static GeoSurface? ReadArea(XElement? volElem, string id, List<ValidationError> errors)
        {
            XElement? patches = AixmFeatureReader.GetHorizontalProjectionPatches(volElem);
            return patches != null ? GmlGeometryReader.ReadPatchesSurface(patches, id, errors) : null;
        }
    }
}
