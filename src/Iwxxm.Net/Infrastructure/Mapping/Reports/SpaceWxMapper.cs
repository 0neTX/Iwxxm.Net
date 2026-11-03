using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Iwxxm.Net.Infrastructure.Mapping.CodeLists;
using Iwxxm.Net.Infrastructure.Mapping.Xml;
using Iwxxm.Net.Reports;
using static Iwxxm.Net.Infrastructure.Mapping.Xml.IwxxmXmlNames;
using static Iwxxm.Net.Infrastructure.Mapping.Xml.XmlValueReader;

namespace Iwxxm.Net.Infrastructure.Mapping.Reports
{
    /// <summary>Maps <c>iwxxm:SpaceWeatherAdvisory</c> (<c>swx.xsd</c>) onto <see cref="SpaceWx"/>.</summary>
    internal static class SpaceWxMapper
    {
        public static Result<SpaceWx> Map(XElement root)
        {
            var errors = new List<ValidationError>();
            ReportHeader header = ReportHeader.Read(root, errors);
            string id = header.Id;
            AixmFeatureReader.ReadUnit(root.Element(IwxxmNs + "issuingSpaceWeatherCentre"), out string? swxcDesig, out string? swxcName);

            List<string> replacedNumbers = ReadNonBlankValues(root, "replacedAdvisoryNumber");
            List<string> effects = ReadNonBlankValues(root, "effect");

            var analyses = new List<SpaceWeatherAnalysis>();
            foreach (XElement analysisProp in root.Elements(IwxxmNs + "analysis"))
            {
                XElement? analysisElem = analysisProp.Element(IwxxmNs + "SpaceWeatherAnalysis");
                if (analysisElem == null) continue;

                analyses.Add(ReadAnalysis(analysisElem, errors));
            }

            DateTimeOffset? nextAdvisory = GmlTimeReader.ReadOptionalInstant(root.Element(IwxxmNs + "nextAdvisoryTime"), "nextAdvisoryTime", id, errors);

            if (errors.Count > 0)
            {
                return Result<SpaceWx>.Failure(errors);
            }

            return SpaceWx.Create(
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

        /// <summary>
        /// Reads one <c>SpaceWeatherAnalysis</c>: one <see cref="SpaceWeatherAnalysisRegion"/> per
        /// <c>iwxxm:intensityAndRegion</c>, inline or by local reference; a nil group keeps its nil reason.
        /// </summary>
        private static SpaceWeatherAnalysis ReadAnalysis(XElement analysisElem, List<ValidationError> errors)
        {
            string analysisId = GetGmlId(analysisElem);
            TimeIndicator? timeIndicator = TimeIndicatorReader.Read(analysisElem, analysisId, errors);
            DateTimeOffset? phenTime = GmlTimeReader.ReadOptionalInstant(analysisElem.Element(IwxxmNs + "phenomenonTime"), "phenomenonTime", analysisId, errors);

            var regions = new List<SpaceWeatherAnalysisRegion>();
            string? nilReason = null;
            foreach (XElement irProp in analysisElem.Elements(IwxxmNs + "intensityAndRegion"))
            {
                XElement? irElem = GmlReferenceResolver.Content(irProp, IwxxmNs + "SpaceWeatherIntensityAndRegion");
                if (irElem == null)
                {
                    nilReason ??= irProp.Attribute("nilReason")?.Value;
                    continue;
                }

                string? intensity = GetText(irElem.Element(IwxxmNs + "intensity"));
                var locations = new List<CodeListValue<SpaceWeatherLocation>>();
                var areas = new List<GeoSurface>();

                foreach (XElement regProp in irElem.Elements(IwxxmNs + "region"))
                {
                    XElement? regElem = GmlReferenceResolver.Content(regProp, IwxxmNs + "SpaceWeatherRegion");
                    if (regElem == null) continue;

                    foreach (string href in CollectHrefs(regElem.Elements(IwxxmNs + "locationIndicator")))
                    {
                        locations.Add(CodeListTables.SpaceWeatherLocations.Translate(href)!);
                    }

                    XElement? patches = AixmFeatureReader.GetHorizontalProjectionPatches(
                        GmlReferenceResolver.Content(regElem.Element(IwxxmNs + "location"), AixmNs + "AirspaceVolume"));

                    if (patches != null)
                    {
                        areas.AddRange(GmlGeometryReader.ReadPatchesSurfaces(patches, GetGmlId(regElem), errors));
                    }
                }

                regions.Add(new SpaceWeatherAnalysisRegion(GetGmlId(irElem), intensity, locations, areas));
            }

            return new SpaceWeatherAnalysis(analysisId, timeIndicator, phenTime, regions, regions.Count == 0 ? nilReason : null);
        }
    }
}
