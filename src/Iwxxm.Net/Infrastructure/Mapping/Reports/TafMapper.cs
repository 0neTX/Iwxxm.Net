using System;
using System.Collections.Generic;
using System.Xml.Linq;
using Iwxxm.Net.Infrastructure.Mapping.Xml;
using Iwxxm.Net.Reports;
using static Iwxxm.Net.Infrastructure.Mapping.Xml.IwxxmXmlNames;
using static Iwxxm.Net.Infrastructure.Mapping.Xml.XmlValueReader;

namespace Iwxxm.Net.Infrastructure.Mapping.Reports
{
    /// <summary>Maps <c>iwxxm:TAF</c> (<c>taf.xsd</c>) onto <see cref="Taf"/>.</summary>
    internal static class TafMapper
    {
        public static Result<Taf> Map(XElement root, UomAliasCollector aliases)
        {
            var errors = new List<ValidationError>();
            ReportHeader header = ReportHeader.Read(root, errors);
            string id = header.Id;
            bool isCancel = ParseBoolAttribute(root, "isCancelReport");

            AixmFeatureReader.ReadAerodrome(
                root.Element(IwxxmNs + "aerodrome"),
                errors,
                out string designator,
                out string? name,
                out GeoCoordinate? position);

            // validPeriod and cancelledReportValidPeriod are minOccurs="0" (taf.xsd): optional.
            GmlTimeReader.ReadPeriod(root.Element(IwxxmNs + "validPeriod"), "validPeriod", null, id, errors, out DateTimeOffset? validFrom, out DateTimeOffset? validUntil);
            GmlTimeReader.ReadPeriod(root.Element(IwxxmNs + "cancelledReportValidPeriod"), "cancelledReportValidPeriod", null, id, errors, out DateTimeOffset? cancelFrom, out DateTimeOffset? cancelUntil);

            XElement? baseProp = root.Element(IwxxmNs + "baseForecast");
            CodeListValue<NilReason>? baseNilReason = AerodromeConditionsReader.ReadNilReason(baseProp);
            XElement? baseElem = baseProp?.Element(IwxxmNs + "MeteorologicalAerodromeForecast");
            bool isNilForecast = baseElem == null && baseNilReason != null;

            AerodromeForecastGroup? baseForecast = baseElem != null ? AerodromeConditionsReader.ReadForecastGroup(baseElem, errors, aliases) : null;

            var changes = new List<AerodromeForecastGroup>();
            foreach (XElement changeProp in root.Elements(IwxxmNs + "changeForecast"))
            {
                XElement? changeElem = changeProp.Element(IwxxmNs + "MeteorologicalAerodromeForecast");
                if (changeElem != null)
                {
                    changes.Add(AerodromeConditionsReader.ReadForecastGroup(changeElem, errors, aliases));
                }
            }

            if (errors.Count > 0)
            {
                return Result<Taf>.Failure(errors);
            }

            return Taf.Create(
                id,
                header.IssueTime,
                header.Metadata.Status,
                header.Metadata.Usage,
                designator,
                name,
                position,
                validFrom,
                validUntil,
                isCancel,
                cancelFrom,
                cancelUntil,
                isNilForecast,
                baseNilReason,
                header.IsTranslationFailed,
                header.FailedTac,
                baseForecast,
                changes,
                header.Metadata.Reason,
                header.Metadata.Supplementary);
        }
    }
}
