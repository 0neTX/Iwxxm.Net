using System;
using System.Collections.Generic;
using System.Xml.Linq;
using Iwxxm.Net.Infrastructure.Mapping.CodeLists;
using Iwxxm.Net.Infrastructure.Mapping.Xml;
using Iwxxm.Net.Reports;
using static Iwxxm.Net.Infrastructure.Mapping.Xml.IwxxmXmlNames;
using static Iwxxm.Net.Infrastructure.Mapping.Xml.XmlValueReader;

namespace Iwxxm.Net.Infrastructure.Mapping.Reports
{
    /// <summary>
    /// Maps <c>iwxxm:SIGMET</c>, <c>iwxxm:VolcanicAshSIGMET</c> and <c>iwxxm:TropicalCycloneSIGMET</c> (<c>sigmet.xsd</c>)
    /// onto <see cref="Sigmet"/>.
    /// </summary>
    internal static class SigmetMapper
    {
        public static Result<Sigmet> Map(XElement root, IwxxmReportType reportType, UomAliasCollector aliases)
        {
            var errors = new List<ValidationError>();
            ReportHeader header = ReportHeader.Read(root, errors);
            string id = header.Id;
            bool isCancel = ParseBoolAttribute(root, "isCancelReport");

            AixmFeatureReader.ReadUnit(root.Element(IwxxmNs + "issuingAirTrafficServicesUnit"), out string? atsDesig, out string? atsName);
            AixmFeatureReader.ReadUnit(root.Element(IwxxmNs + "originatingMeteorologicalWatchOffice"), out string? mwoDesig, out _);
            AixmFeatureReader.ReadAirspace(root.Element(IwxxmNs + "issuingAirTrafficServicesRegion"), out string? firDesig, out string? firName);

            string? seqNumber = GetText(root.Element(IwxxmNs + "sequenceNumber"));
            string? cancelSeqNumber = GetText(root.Element(IwxxmNs + "cancelledReportSequenceNumber"));

            // validPeriod is mandatory (sigmet.xsd): absent -> mapping error; xlink:href only -> null (unresolved).
            GmlTimeReader.ReadPeriod(root.Element(IwxxmNs + "validPeriod"), "validPeriod", MappingErrors.ValidPeriodMissingCode, id, errors, out DateTimeOffset? validFrom, out DateTimeOffset? validUntil);
            GmlTimeReader.ReadPeriod(root.Element(IwxxmNs + "cancelledReportValidPeriod"), "cancelledReportValidPeriod", null, id, errors, out DateTimeOffset? cancelledFrom, out DateTimeOffset? cancelledUntil);
            CodeListValue<SigmetPhenomenon>? phenomenon = CodeListTables.SigmetPhenomena.Translate(GetHref(root.Element(IwxxmNs + "phenomenon")));

            var evolvingConditions = new List<EnRoutePhenomenonVolume>();
            var forecastPositions = new List<EnRoutePhenomenonVolume>();
            ReadAnalyses(root.Elements(IwxxmNs + "analysisCollection"), evolvingConditions, forecastPositions, errors, aliases);

            // TC SIGMET supplementary analyses and VA SIGMET destination FIR (AUD-L3).
            var supplementaryEvolving = new List<EnRoutePhenomenonVolume>();
            var supplementaryPositions = new List<EnRoutePhenomenonVolume>();
            ReadAnalyses(root.Elements(IwxxmNs + "supplementaryAnalysisCollection"), supplementaryEvolving, supplementaryPositions, errors, aliases);
            AixmFeatureReader.ReadAirspace(root.Element(IwxxmNs + "volcanicAshMovedToFIR"), out string? movedToFirDesig, out string? movedToFirName);

            Volcano? eruptingVolcano = VolcanoElementReader.ReadMetceVolcano(root.Element(IwxxmNs + "eruptingVolcano"), errors);
            string? tcName = root
                .Element(IwxxmNs + "tropicalCyclone")
                ?.Element(MetceNs + "TropicalCyclone")
                ?.Element(MetceNs + "name")?.Value;

            if (errors.Count > 0)
            {
                return Result<Sigmet>.Failure(errors);
            }

            return Sigmet.Create(
                id,
                reportType,
                header.IssueTime,
                header.Metadata.Status,
                header.Metadata.Usage,
                atsDesig ?? string.Empty,
                atsName,
                mwoDesig,
                firDesig,
                firName,
                seqNumber,
                validFrom,
                validUntil,
                phenomenon,
                isCancel,
                cancelSeqNumber,
                header.IsTranslationFailed,
                header.FailedTac,
                eruptingVolcano,
                tcName,
                evolvingConditions,
                forecastPositions,
                header.Metadata.Reason,
                header.Metadata.Supplementary,
                cancelledFrom,
                cancelledUntil,
                supplementaryEvolving,
                supplementaryPositions,
                movedToFirDesig,
                movedToFirName);
        }

        private static void ReadAnalyses(
            IEnumerable<XElement> analysisCollections,
            List<EnRoutePhenomenonVolume> evolvingConditions,
            List<EnRoutePhenomenonVolume> forecastPositions,
            List<ValidationError> errors,
            UomAliasCollector aliases)
        {
            foreach (XElement analysisCol in analysisCollections)
            {
                foreach (XElement posAnalysis in analysisCol.Elements(IwxxmNs + "analysisAndForecastPositionAnalysis"))
                {
                    XElement? evolvingCol = posAnalysis
                        .Element(IwxxmNs + "analysis")
                        ?.Element(IwxxmNs + "SIGMETEvolvingConditionCollection");

                    EnRouteVolumeReader.ReadCollectionMembers(evolvingCol, "SIGMETEvolvingCondition", evolvingConditions, errors, aliases);

                    XElement? forecastCol = posAnalysis
                        .Element(IwxxmNs + "forecastPositionAnalysis")
                        ?.Element(IwxxmNs + "SIGMETPositionCollection");

                    EnRouteVolumeReader.ReadCollectionMembers(forecastCol, "SIGMETPosition", forecastPositions, errors, aliases);
                }
            }
        }
    }
}
