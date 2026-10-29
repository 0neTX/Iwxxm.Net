using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Iwxxm.Net.Infrastructure.Mapping.Xml;
using Iwxxm.Net.Reports;
using static Iwxxm.Net.Infrastructure.Mapping.Xml.IwxxmXmlNames;
using static Iwxxm.Net.Infrastructure.Mapping.Xml.XmlValueReader;

namespace Iwxxm.Net.Infrastructure.Mapping.Reports
{
    /// <summary>Maps <c>iwxxm:SpaceWeatherAdvisory</c> (<c>swx.xsd</c>) onto <see cref="SpaceWeatherAdvisoryReport"/>.</summary>
    internal static class SpaceWeatherAdvisoryMapper
    {
        public static Result<SpaceWeatherAdvisoryReport> Map(XElement root)
        {
            var errors = new List<ValidationError>();
            ReportHeader header = ReportHeader.Read(root, errors);
            string id = header.Id;
            AixmFeatureReader.ReadUnit(root.Element(IwxxmNs + "issuingSpaceWeatherCentre"), out string? swxcDesig, out string? swxcName);

            List<string> replacedNumbers = ReadNonBlankValues(root, "replacedAdvisoryNumber");
            List<string> effects = ReadNonBlankValues(root, "effect");

            var analyses = new List<SpaceWeatherAnalysisRegion>();
            foreach (XElement analysisProp in root.Elements(IwxxmNs + "analysis"))
            {
                XElement? analysisElem = analysisProp.Element(IwxxmNs + "SpaceWeatherAnalysis");
                if (analysisElem == null) continue;

                ReadAnalysis(analysisElem, analyses, errors);
            }

            DateTimeOffset? nextAdvisory = GmlTimeReader.ReadOptionalInstant(root.Element(IwxxmNs + "nextAdvisoryTime"), "nextAdvisoryTime", id, errors);

            if (errors.Count > 0)
            {
                return Result<SpaceWeatherAdvisoryReport>.Failure(errors);
            }

            return SpaceWeatherAdvisoryReport.Create(
                id,
                header.IssueTime,
                header.Metadata.Status,
                header.Metadata.Usage,
                swxcName ?? swxcDesig ?? string.Empty,
                GetText(root.Element(IwxxmNs + "advisoryNumber")),
                replacedNumbers,
                effects,
                analyses,
                GetText(root.Element(IwxxmNs + "remarks")),
                nextAdvisory,
                header.IsTranslationFailed,
                header.FailedTac,
                header.Metadata.Reason,
                header.Metadata.Supplementary);
        }

        private static List<string> ReadNonBlankValues(XElement root, string elementName) =>
            root
                .Elements(IwxxmNs + elementName)
                .Select(e => e.Value)
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .ToList();

        /// <summary>Adds one <see cref="SpaceWeatherAnalysisRegion"/> per <c>iwxxm:intensityAndRegion</c> of the analysis.</summary>
        private static void ReadAnalysis(XElement analysisElem, List<SpaceWeatherAnalysisRegion> analyses, List<ValidationError> errors)
        {
            string analysisId = GetGmlId(analysisElem);
            string? timeInd = analysisElem.Attribute("timeIndicator")?.Value;
            DateTimeOffset? phenTime = GmlTimeReader.ReadOptionalInstant(analysisElem.Element(IwxxmNs + "phenomenonTime"), "phenomenonTime", analysisId, errors);

            foreach (XElement irProp in analysisElem.Elements(IwxxmNs + "intensityAndRegion"))
            {
                XElement? irElem = irProp.Element(IwxxmNs + "SpaceWeatherIntensityAndRegion");
                if (irElem == null) continue;

                string? intensity = GetText(irElem.Element(IwxxmNs + "intensity"));
                var locUris = new List<string>();
                var polygons = new List<GeoPolygon>();

                foreach (XElement regProp in irElem.Elements(IwxxmNs + "region"))
                {
                    XElement? regElem = regProp.Element(IwxxmNs + "SpaceWeatherRegion");
                    if (regElem == null) continue;

                    locUris.AddRange(CollectHrefs(regElem.Elements(IwxxmNs + "locationIndicator")));

                    XElement? patches = AixmFeatureReader.GetHorizontalProjectionPatches(
                        regElem.Element(IwxxmNs + "location")?.Element(AixmNs + "AirspaceVolume"));

                    if (patches != null)
                    {
                        GeoPolygon? poly = GmlGeometryReader.ReadPatchesPolygon(patches, GetGmlId(regElem), errors);
                        if (poly != null)
                        {
                            polygons.Add(poly);
                        }
                    }
                }

                analyses.Add(new SpaceWeatherAnalysisRegion(analysisId, timeInd, phenTime, intensity, locUris, polygons));
            }
        }
    }
}
