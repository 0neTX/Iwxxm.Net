using System;
using System.Collections.Generic;
using System.Xml.Linq;
using Iwxxm.Net.Infrastructure.Mapping.Xml;
using Iwxxm.Net.Reports;
using static Iwxxm.Net.Infrastructure.Mapping.Xml.IwxxmXmlNames;
using static Iwxxm.Net.Infrastructure.Mapping.Xml.XmlValueReader;

namespace Iwxxm.Net.Infrastructure.Mapping.Reports
{
    /// <summary>Maps <c>iwxxm:VolcanoObservatoryNoticeForAviation</c> (<c>vona.xsd</c>) onto <see cref="VonaReport"/>.</summary>
    internal static class VonaMapper
    {
        public static Result<VonaReport> Map(XElement root)
        {
            var errors = new List<ValidationError>();
            ReportHeader header = ReportHeader.Read(root, errors);
            AixmFeatureReader.ReadUnit(root.Element(IwxxmNs + "originatingCentre"), out string? centreDesig, out string? centreName);

            var volcanoes = new List<Volcano>();
            VerticalLimit? ashLevel = null;

            foreach (XElement featProp in root.Elements(IwxxmNs + "feature"))
            {
                XElement? feat = featProp.Element(IwxxmNs + "MeteorologicalFeature");
                if (feat == null) continue;

                string featId = GetGmlId(feat);
                string? phenHref = VolcanoElementReader.GetPhenomenonHref(feat);

                if (phenHref != null && phenHref.EndsWith("/VOLCANO", StringComparison.OrdinalIgnoreCase))
                {
                    XElement? ptElem = feat.Element(IwxxmNs + "phenomenonGeometry")?.Element(GmlNs + "Point");
                    GeoCoordinate? pos = GmlGeometryReader.ReadPoint(ptElem, featId, errors);
                    XElement? volElem = feat.Element(IwxxmNs + "phenomenonProperty")?.Element(IwxxmNs + "Volcano");
                    if (pos.HasValue && volElem != null)
                    {
                        VolcanoElementReader.AddIwxxmVolcano(volElem, featId, pos.Value, volcanoes, errors);
                    }
                }
                else if (phenHref != null && phenHref.EndsWith("/VOLCANIC_ASH", StringComparison.OrdinalIgnoreCase))
                {
                    XElement? levelElem = feat
                        .Element(IwxxmNs + "phenomenonGeometry")
                        ?.Element(IwxxmNs + "ElevatedLevel");

                    ashLevel = VerticalLimitReader.Read(
                        levelElem?.Element(IwxxmNs + "elevation"),
                        levelElem?.Element(IwxxmNs + "verticalReference"),
                        featId,
                        errors);
                }
            }

            if (errors.Count > 0)
            {
                return Result<VonaReport>.Failure(errors);
            }

            return VonaReport.Create(
                header.Id,
                header.IssueTime,
                header.Metadata.Status,
                header.Metadata.Usage,
                centreName ?? centreDesig ?? string.Empty,
                centreDesig,
                GetText(root.Element(IwxxmNs + "stateOrRegion")),
                GetText(root.Element(IwxxmNs + "noticeNumber")),
                GetHref(root.Element(IwxxmNs + "currentColourCode")),
                GetHref(root.Element(IwxxmNs + "previousColourCode")),
                GetText(root.Element(IwxxmNs + "contacts")),
                GetText(root.Element(IwxxmNs + "remarks")),
                GetText(root.Element(IwxxmNs + "nextNotice")),
                header.IsTranslationFailed,
                header.FailedTac,
                volcanoes,
                ashLevel,
                header.Metadata.Reason,
                header.Metadata.Supplementary);
        }
    }
}
