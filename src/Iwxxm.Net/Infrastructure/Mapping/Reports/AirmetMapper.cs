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
    /// <summary>Maps <c>iwxxm:AIRMET</c> (<c>airmet.xsd</c>) onto <see cref="Airmet"/>.</summary>
    internal static class AirmetMapper
    {
        public static Result<Airmet> Map(XElement root)
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
            bool isCancel = ParseBoolAttribute(root, "isCancelReport");
            string? cancelSeqNumber = GetText(root.Element(IwxxmNs + "cancelledReportSequenceNumber"));
            GmlTimeReader.ReadPeriod(root.Element(IwxxmNs + "cancelledReportValidPeriod"), "cancelledReportValidPeriod", null, id, errors, out DateTimeOffset? cancelledFrom, out DateTimeOffset? cancelledUntil);
            CodeListValue<AirmetPhenomenon>? phenomenon = CodeListTables.AirmetPhenomena.Translate(GetHref(root.Element(IwxxmNs + "phenomenon")));

            var evolvingConditions = new List<AirmetEvolvingCondition>();
            XElement? evolvingCol = root
                .Element(IwxxmNs + "analysis")
                ?.Element(IwxxmNs + "AIRMETEvolvingConditionCollection");
            TimeIndicator? timeIndicator = null;
            DateTimeOffset? phenomenonTime = null;
            if (evolvingCol != null)
            {
                EnRouteVolumeReader.ReadCollectionTime(evolvingCol, errors, out timeIndicator, out phenomenonTime);
            }

            foreach (XElement member in evolvingCol?.Elements(IwxxmNs + "member") ?? Array.Empty<XElement>())
            {
                XElement? condition = GmlReferenceResolver.Content(member, IwxxmNs + "AIRMETEvolvingCondition");
                if (condition != null)
                {
                    evolvingConditions.Add(ReadCondition(condition, timeIndicator, phenomenonTime, errors));
                }
            }

            if (errors.Count > 0)
            {
                return Result<Airmet>.Failure(errors);
            }

            return Airmet.Create(
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
                phenomenon,
                header.IsTranslationFailed,
                header.FailedTac,
                evolvingConditions,
                header.Metadata.Reason,
                header.Metadata.Supplementary,
                isCancel,
                cancelSeqNumber,
                cancelledFrom,
                cancelledUntil);
        }

        /// <summary>Reads an <c>AIRMETEvolvingCondition</c>: shared volume plus the AIRMET-specific content (<c>airmet.xsd</c>).</summary>
        private static AirmetEvolvingCondition ReadCondition(XElement condition, TimeIndicator? timeIndicator, DateTimeOffset? phenomenonTime, List<ValidationError> errors)
        {
            EnRoutePhenomenonVolume volume = EnRouteVolumeReader.ReadConditionVolume(condition, timeIndicator, phenomenonTime, errors);
            string id = volume.Id;

            VerticalLimit? cloudBase = VerticalLimitReader.Read(
                condition.Element(IwxxmNs + "cloudBase"), condition.Element(IwxxmNs + "cloudBaseReference"), id, errors, UomMeasureReader.Subsets.CloudHeight);
            VerticalLimit? cloudTop = VerticalLimitReader.Read(
                condition.Element(IwxxmNs + "cloudTop"), condition.Element(IwxxmNs + "cloudTopReference"), id, errors, UomMeasureReader.Subsets.CloudHeight);
            string? cloudTopAboveText = GetText(condition.Element(IwxxmNs + "cloudTopAbove"));
            bool? cloudTopAbove = cloudTopAboveText == null
                ? (bool?)null
                : cloudTopAboveText == "true" || cloudTopAboveText == "1";

            Length? surfaceVisibility = UomMeasureReader.ReadLength(condition.Element(IwxxmNs + "surfaceVisibility"), id, errors, UomMeasureReader.Subsets.Visibility);
            var causes = new List<CodeListValue<VisibilityReductionCause>>();
            foreach (string href in CollectHrefs(condition.Elements(IwxxmNs + "surfaceVisibilityCause")))
            {
                CodeListValue<VisibilityReductionCause>? cause = CodeListTables.VisibilityReductionCauses.Translate(href);
                if (cause != null)
                    causes.Add(cause);
            }

            double? windDirection = UomMeasureReader.ReadFixedUnit(condition.Element(IwxxmNs + "surfaceWindDirection"), UcumUnits.Degree, id, errors);
            Speed? windSpeed = UomMeasureReader.ReadSpeed(condition.Element(IwxxmNs + "surfaceWindSpeed"), id, errors, UomMeasureReader.Subsets.WindSpeed);

            return new AirmetEvolvingCondition(volume, cloudBase, cloudTop, cloudTopAbove, surfaceVisibility, causes, windDirection, windSpeed);
        }
    }
}
