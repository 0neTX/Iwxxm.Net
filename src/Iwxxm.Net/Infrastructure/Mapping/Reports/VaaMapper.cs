using System;
using System.Collections.Generic;
using System.Xml.Linq;
using Iwxxm.Net.Infrastructure.Mapping.Xml;
using Iwxxm.Net.Reports;
using static Iwxxm.Net.Infrastructure.Mapping.Xml.IwxxmXmlNames;
using static Iwxxm.Net.Infrastructure.Mapping.Xml.XmlValueReader;

namespace Iwxxm.Net.Infrastructure.Mapping.Reports
{
    /// <summary>Maps <c>iwxxm:VolcanicAshAdvisory</c> (<c>vaa.xsd</c>) onto <see cref="Vaa"/>.</summary>
    internal static class VaaMapper
    {
        public static Result<Vaa> Map(XElement root)
        {
            var errors = new List<ValidationError>();
            ReportHeader header = ReportHeader.Read(root, errors);
            string id = header.Id;
            AixmFeatureReader.ReadUnit(root.Element(IwxxmNs + "issuingVolcanicAshAdvisoryCentre"), out string? vaacDesig, out string? vaacName);

            Volcano? volcano = VolcanoElementReader.ReadMetceVolcano(root.Element(IwxxmNs + "volcano"), errors);
            Length? sourceElev = UomMeasureReader.ReadLength(root.Element(IwxxmNs + "sourceElevationAMSL"), id, errors);

            var observedClouds = new List<EnRoutePhenomenonVolume>();
            XElement? obsCond = root
                .Element(IwxxmNs + "observation")
                ?.Element(IwxxmNs + "VolcanicAshObservedOrEstimatedConditions");

            if (obsCond != null)
            {
                ReadAshClouds(obsCond, "VolcanicAshCloudObservedOrEstimated", observedClouds, errors);
            }

            var forecastClouds = new List<EnRoutePhenomenonVolume>();
            foreach (XElement fcstProp in root.Elements(IwxxmNs + "forecast"))
            {
                XElement? fcstCond = fcstProp.Element(IwxxmNs + "VolcanicAshForecastConditions");
                if (fcstCond == null) continue;

                ReadAshClouds(fcstCond, "VolcanicAshCloudForecast", forecastClouds, errors);
            }

            DateTimeOffset? nextAdvisory = GmlTimeReader.ReadOptionalInstant(root.Element(IwxxmNs + "nextAdvisoryTime"), "nextAdvisoryTime", id, errors);

            if (errors.Count > 0)
            {
                return Result<Vaa>.Failure(errors);
            }

            return Vaa.Create(
                id,
                header.IssueTime,
                header.Metadata.Status,
                header.Metadata.Usage,
                vaacName ?? vaacDesig ?? string.Empty,
                volcano,
                GetText(root.Element(IwxxmNs + "stateOrRegion")),
                sourceElev,
                GetText(root.Element(IwxxmNs + "advisoryNumber")),
                GetText(root.Element(IwxxmNs + "informationSource")),
                GetText(root.Element(IwxxmNs + "eruptionDetails")),
                GetText(root.Element(IwxxmNs + "remarks")),
                nextAdvisory,
                header.IsTranslationFailed,
                header.FailedTac,
                observedClouds,
                forecastClouds,
                header.Metadata.Reason,
                header.Metadata.Supplementary);
        }

        private static void ReadAshClouds(XElement conditions, string cloudName, List<EnRoutePhenomenonVolume> target, List<ValidationError> errors)
        {
            foreach (XElement ashProp in conditions.Elements(IwxxmNs + "ashCloud"))
            {
                XElement? ashElem = ashProp.Element(IwxxmNs + cloudName);
                if (ashElem != null)
                {
                    target.Add(EnRouteVolumeReader.ReadAshCloudVolume(ashElem, errors));
                }
            }
        }
    }
}
