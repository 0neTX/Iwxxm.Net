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
    /// Maps <c>iwxxm:QuantitativeVolcanicAshConcentrationInformation</c> (<c>qvaci.xsd</c>) onto <see cref="Qvaci"/>.
    /// </summary>
    internal static class QvaciMapper
    {
        public static Result<Qvaci> Map(XElement root)
        {
            var errors = new List<ValidationError>();
            ReportHeader header = ReportHeader.Read(root, errors);
            string id = header.Id;
            AixmFeatureReader.ReadUnit(root.Element(IwxxmNs + "originatingCentre"), out string? centreDesig, out string? centreName);

            var volcanoes = new List<Volcano>();
            var ashClouds = new List<VolcanicAshConcentrationCloud>();

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
                    ReadAshCloud(feat, featId, ashClouds, errors);
                }
            }

            DateTimeOffset? modelInitTime = GmlTimeReader.ReadOptionalInstant(root.Element(IwxxmNs + "dispersionModelInitialisationTime"), "dispersionModelInitialisationTime", id, errors);

            if (errors.Count > 0)
            {
                return Result<Qvaci>.Failure(errors);
            }

            return Qvaci.Create(
                id,
                header.IssueTime,
                header.Metadata.Status,
                header.Metadata.Usage,
                centreName ?? centreDesig ?? string.Empty,
                centreDesig,
                modelInitTime,
                GetText(root.Element(IwxxmNs + "remarks")),
                header.IsTranslationFailed,
                header.FailedTac,
                volcanoes,
                ashClouds,
                header.Metadata.Reason,
                header.Metadata.Supplementary);
        }

        private static void ReadAshCloud(XElement feat, string featId, List<VolcanicAshConcentrationCloud> ashClouds, List<ValidationError> errors)
        {
            XElement? volGeom = feat.Element(IwxxmNs + "phenomenonGeometry")?.Element(IwxxmNs + "ElevatedVolume");
            XElement? concProp = feat
                .Element(IwxxmNs + "phenomenonProperty")
                ?.Element(IwxxmNs + "VolcanicAshCloudConcentration");

            if (volGeom == null || concProp == null)
                return;

            GeoPolygon? poly = GmlGeometryReader.ReadPatchesPolygon(volGeom.Element(GmlNs + "patches"), featId, errors);
            VerticalLimit? lower = VerticalLimitReader.Read(
                volGeom.Element(IwxxmNs + "lowerElevation"), volGeom.Element(IwxxmNs + "lowerVerticalReference"), featId, errors);
            VerticalLimit? upper = VerticalLimitReader.Read(
                volGeom.Element(IwxxmNs + "upperElevation"), volGeom.Element(IwxxmNs + "upperVerticalReference"), featId, errors);

            DateTimeOffset? phenomenonTime = GmlTimeReader.ReadOptionalInstant(feat.Element(IwxxmNs + "phenomenonTime"), "phenomenonTime", featId, errors);

            // ashDensityEqualsOrExceeds is a mandatory gml:MeasureType (qvaci.xsd): never defaulted to 0.
            double? density = UomMeasureReader.ReadFixedUnit(
                concProp.Element(IwxxmNs + "ashDensityEqualsOrExceeds"), UcumUnits.MilligramPerCubicMeter, featId, errors);
            if (!density.HasValue)
            {
                errors.Add(MappingErrors.Create(
                    MappingErrors.AshDensityMissingCode,
                    $"The mandatory ashDensityEqualsOrExceeds of ash cloud '{featId}' is missing or nil; no density is assumed.",
                    featId));
            }
            else if (poly != null)
            {
                ashClouds.Add(new VolcanicAshConcentrationCloud(featId, poly, lower, upper, density.Value, phenomenonTime));
            }
        }
    }
}
