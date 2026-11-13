using System.Collections.Generic;
using System.Xml.Linq;
using Iwxxm.Net.Infrastructure.Mapping.Xml;
using Iwxxm.Net.Reports;
using static Iwxxm.Net.Infrastructure.Mapping.Xml.IwxxmXmlNames;
using static Iwxxm.Net.Infrastructure.Mapping.Xml.XmlValueReader;

namespace Iwxxm.Net.Infrastructure.Mapping.Reports
{
    /// <summary>Reads volcano descriptions: <c>iwxxm:Volcano</c> (VONA, QVACI) and <c>metce:Volcano</c> (SIGMET, VAA).</summary>
    internal static class VolcanoElementReader
    {
        /// <summary>
        /// Maps an <c>iwxxm:Volcano</c> (VONA / QVACI). <c>iwxxm:name</c> is mandatory but nillable (<c>metFeature.xsd</c>):
        /// an absent name is a mapping error, a nil name maps to <c>null</c>.
        /// </summary>
        public static void AddIwxxmVolcano(XElement volElem, string featId, GeoCoordinate position, List<Volcano> volcanoes, List<ValidationError> errors)
        {
            XElement? nameElem = volElem.Element(IwxxmNs + "name");
            if (nameElem == null)
            {
                errors.Add(MappingErrors.Create(MappingErrors.VolcanoNameMissingCode, $"The mandatory element iwxxm:name of volcano '{featId}' is missing; no name is assumed.", featId));
                return;
            }

            // activityStatus and sourceElevation are optional; they are read exactly as the WAFS SIGWX path does:
            // an unsupported status, unit or reference is a mapping error, a nil elevation maps to null.
            int errorsBefore = errors.Count;
            VolcanoActivityStatus? status = VolcanoActivityStatusReader.Read(GetText(volElem.Element(IwxxmNs + "activityStatus")), featId, errors);
            XElement? levelElem = volElem.Element(IwxxmNs + "sourceElevation")?.Element(IwxxmNs + "ElevatedLevel");
            XElement? elevationElem = levelElem?.Element(IwxxmNs + "elevation");
            VerticalLimit? sourceElevation = elevationElem == null
                ? (VerticalLimit?)null
                : VerticalLimitReader.Read(elevationElem, levelElem!.Element(IwxxmNs + "verticalReference"), $"MeteorologicalFeature[@gml:id='{featId}']/Volcano/sourceElevation", errors);
            if (sourceElevation.HasValue && sourceElevation.Value.IsNil)
                sourceElevation = null;
            if (errors.Count > errorsBefore)
                return;

            var vRes = Volcano.Create(featId, GetText(nameElem), GetText(volElem.Element(IwxxmNs + "IAVCEINumber")), position, null, status, sourceElevation);
            if (vRes.IsSuccess) volcanoes.Add(vRes.Value);
            else errors.AddRange(vRes.Errors);
        }

        /// <summary>Maps a <c>metce:Volcano</c>; <c>metce:name</c> is a mandatory, non-nillable string (<c>phenomena.xsd</c>).</summary>
        public static Volcano? ReadMetceVolcano(XElement? propElem, List<ValidationError> errors)
        {
            XElement? volElem = propElem?.Element(MetceNs + "Volcano")
                                ?? propElem?.Element(MetceNs + "EruptingVolcano");
            if (volElem == null)
                return null;

            string id = GetGmlId(volElem);
            string? name = GetText(volElem.Element(MetceNs + "name"));
            if (name == null)
            {
                errors.Add(MappingErrors.Create(MappingErrors.VolcanoNameMissingCode, $"The mandatory element metce:name of volcano '{id}' is missing or empty; no name is assumed.", id));
                return null;
            }

            XElement? ptElem = volElem.Element(MetceNs + "position")?.Element(GmlNs + "Point");
            GeoCoordinate? pos = GmlGeometryReader.ReadPoint(ptElem, id, errors);
            if (!pos.HasValue)
                return null;

            var res = Volcano.Create(id, name, null, pos.Value);
            if (res.IsFailure)
            {
                errors.AddRange(res.Errors);
                return null;
            }

            return res.Value;
        }

        /// <summary>Returns the <c>iwxxm:MeteorologicalFeature</c> phenomenon reference (<c>xlink:href</c>), or <c>null</c>.</summary>
        public static string? GetPhenomenonHref(XElement feature) => GetHref(feature.Element(IwxxmNs + "phenomenon"));
    }
}
