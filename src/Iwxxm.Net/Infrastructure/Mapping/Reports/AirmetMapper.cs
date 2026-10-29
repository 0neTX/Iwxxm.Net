using System;
using System.Collections.Generic;
using System.Xml.Linq;
using Iwxxm.Net.Infrastructure.Mapping.Xml;
using Iwxxm.Net.Reports;
using static Iwxxm.Net.Infrastructure.Mapping.Xml.IwxxmXmlNames;
using static Iwxxm.Net.Infrastructure.Mapping.Xml.XmlValueReader;

namespace Iwxxm.Net.Infrastructure.Mapping.Reports
{
    /// <summary>Maps <c>iwxxm:AIRMET</c> (<c>airmet.xsd</c>) onto <see cref="AirmetReport"/>.</summary>
    internal static class AirmetMapper
    {
        public static Result<AirmetReport> Map(XElement root)
        {
            var errors = new List<ValidationError>();
            ReportHeader header = ReportHeader.Read(root, errors);
            string id = header.Id;

            AixmFeatureReader.ReadUnit(root.Element(IwxxmNs + "issuingAirTrafficServicesUnit"), out string? atsDesig, out string? atsName);
            AixmFeatureReader.ReadUnit(root.Element(IwxxmNs + "originatingMeteorologicalWatchOffice"), out string? mwoDesig, out _);
            AixmFeatureReader.ReadAirspace(root.Element(IwxxmNs + "issuingAirTrafficServicesRegion"), out string? firDesig, out string? firName);

            string? seqNumber = GetText(root.Element(IwxxmNs + "sequenceNumber"));

            // validPeriod is mandatory (airmet.xsd): absent -> mapping error; xlink:href only -> null (unresolved).
            GmlTimeReader.ReadPeriod(root.Element(IwxxmNs + "validPeriod"), "validPeriod", MappingErrors.ValidPeriodMissingCode, id, errors, out DateTimeOffset? validFrom, out DateTimeOffset? validUntil);
            string? phenomenonUri = GetHref(root.Element(IwxxmNs + "phenomenon"));

            var evolvingConditions = new List<EnRoutePhenomenonVolume>();
            XElement? evolvingCol = root
                .Element(IwxxmNs + "analysis")
                ?.Element(IwxxmNs + "AIRMETEvolvingConditionCollection");
            EnRouteVolumeReader.ReadCollectionMembers(evolvingCol, "AIRMETEvolvingCondition", evolvingConditions, errors);

            if (errors.Count > 0)
            {
                return Result<AirmetReport>.Failure(errors);
            }

            return AirmetReport.Create(
                id,
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
                phenomenonUri,
                header.IsTranslationFailed,
                header.FailedTac,
                evolvingConditions,
                header.Metadata.Reason,
                header.Metadata.Supplementary);
        }
    }
}
