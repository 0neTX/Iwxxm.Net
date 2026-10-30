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

            VolcanicAshConditions? observation = null;
            XElement? obsCond = root
                .Element(IwxxmNs + "observation")
                ?.Element(IwxxmNs + "VolcanicAshObservedOrEstimatedConditions");

            if (obsCond != null)
            {
                observation = ReadConditions(obsCond, "VolcanicAshCloudObservedOrEstimated", errors);
            }

            var forecasts = new List<VolcanicAshConditions>();
            foreach (XElement fcstProp in root.Elements(IwxxmNs + "forecast"))
            {
                XElement? fcstCond = fcstProp.Element(IwxxmNs + "VolcanicAshForecastConditions");
                if (fcstCond == null) continue;

                forecasts.Add(ReadConditions(fcstCond, "VolcanicAshCloudForecast", errors));
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
                observation,
                forecasts,
                header.Metadata.Reason,
                header.Metadata.Supplementary);
        }

        /// <summary>Reads one observed or forecast conditions element: its time, status and ash clouds.</summary>
        private static VolcanicAshConditions ReadConditions(XElement conditions, string cloudName, List<ValidationError> errors)
        {
            string id = GetGmlId(conditions);
            DateTimeOffset? phenomenonTime = GmlTimeReader.ReadOptionalInstant(conditions.Element(IwxxmNs + "phenomenonTime"), "phenomenonTime", id, errors);
            VolcanicAshConditionsStatus? status = ReadStatus(conditions, id, errors);

            var clouds = new List<EnRoutePhenomenonVolume>();
            foreach (XElement ashProp in conditions.Elements(IwxxmNs + "ashCloud"))
            {
                XElement? ashElem = GmlReferenceResolver.Content(ashProp, IwxxmNs + cloudName);
                if (ashElem != null)
                {
                    clouds.Add(EnRouteVolumeReader.ReadAshCloudVolume(ashElem, phenomenonTime, errors));
                }
            }

            return new VolcanicAshConditions(id, phenomenonTime, status, clouds);
        }

        private static VolcanicAshConditionsStatus? ReadStatus(XElement conditions, string id, List<ValidationError> errors)
        {
            string? value = conditions.Attribute("status")?.Value?.Trim();
            switch (value)
            {
                case null:
                case "":
                    return null;
                case "PROVIDED":
                    return VolcanicAshConditionsStatus.Provided;
                case "NOT_PROVIDED":
                    return VolcanicAshConditionsStatus.NotProvided;
                case "NOT_AVAILABLE":
                    return VolcanicAshConditionsStatus.NotAvailable;
                case "NOT_IDENTIFIABLE":
                    return VolcanicAshConditionsStatus.NotIdentifiable;
                case "NO_VOLCANIC_ASH_EXPECTED":
                    return VolcanicAshConditionsStatus.NoVolcanicAshExpected;
                default:
                    errors.Add(MappingErrors.Create("ACL-VAA-STATUS-INVALID", $"Unsupported volcanic ash conditions status '{value}'.", id));
                    return null;
            }
        }
    }
}
