using System;
using System.Collections.Generic;
using System.Xml.Linq;
using Iwxxm.Net.Infrastructure.Mapping.Xml;
using Iwxxm.Net.Reports;
using static Iwxxm.Net.Infrastructure.Mapping.Xml.IwxxmXmlNames;
using static Iwxxm.Net.Infrastructure.Mapping.Xml.XmlValueReader;

namespace Iwxxm.Net.Infrastructure.Mapping.Reports
{
    /// <summary>Maps <c>iwxxm:TropicalCycloneAdvisory</c> (<c>tca.xsd</c>) onto <see cref="TropicalCycloneAdvisoryReport"/>.</summary>
    internal static class TropicalCycloneAdvisoryMapper
    {
        public static Result<TropicalCycloneAdvisoryReport> Map(XElement root)
        {
            var errors = new List<ValidationError>();
            ReportHeader header = ReportHeader.Read(root, errors);
            string id = header.Id;
            AixmFeatureReader.ReadUnit(root.Element(IwxxmNs + "issuingTropicalCycloneAdvisoryCentre"), out string? tcacDesig, out string? tcacName);

            string? cycloneName = root
                .Element(IwxxmNs + "tropicalCycloneName")
                ?.Element(MetceNs + "TropicalCyclone")
                ?.Element(MetceNs + "name")?.Value;

            TropicalCycloneTrackPoint? obsPoint = null;
            XElement? obsElem = root
                .Element(IwxxmNs + "observation")
                ?.Element(IwxxmNs + "TropicalCycloneObservedConditions");

            if (obsElem != null)
            {
                DateTimeOffset? obsTime = GmlTimeReader.ReadOptionalInstant(obsElem.Element(IwxxmNs + "phenomenonTime"), "phenomenonTime", id, errors);
                GeoCoordinate? pos = ReadCyclonePosition(obsElem, errors);
                Speed? maxWind = UomMeasureReader.ReadSpeed(obsElem.Element(IwxxmNs + "maximumSurfaceWindSpeed"), id, errors);
                double? pressure = UomMeasureReader.ReadFixedUnit(obsElem.Element(IwxxmNs + "centralPressure"), UcumUnits.Hectopascal, id, errors);

                if (pos.HasValue)
                {
                    obsPoint = new TropicalCycloneTrackPoint(obsTime, pos.Value, maxWind, pressure);
                }
            }

            var forecasts = new List<TropicalCycloneTrackPoint>();
            foreach (XElement fcstProp in root.Elements(IwxxmNs + "forecast"))
            {
                XElement? fcstElem = fcstProp.Element(IwxxmNs + "TropicalCycloneForecastConditions");
                if (fcstElem == null) continue;

                DateTimeOffset? fcstTime = GmlTimeReader.ReadOptionalInstant(fcstElem.Element(IwxxmNs + "phenomenonTime"), "phenomenonTime", id, errors);
                GeoCoordinate? pos = ReadCyclonePosition(fcstElem, errors);
                Speed? maxWind = UomMeasureReader.ReadSpeed(fcstElem.Element(IwxxmNs + "maximumSurfaceWindSpeed"), id, errors);

                if (pos.HasValue)
                {
                    forecasts.Add(new TropicalCycloneTrackPoint(fcstTime, pos.Value, maxWind));
                }
            }

            DateTimeOffset? nextAdvisory = GmlTimeReader.ReadOptionalInstant(root.Element(IwxxmNs + "nextAdvisoryTime"), "nextAdvisoryTime", id, errors);

            if (errors.Count > 0)
            {
                return Result<TropicalCycloneAdvisoryReport>.Failure(errors);
            }

            return TropicalCycloneAdvisoryReport.Create(
                id,
                header.IssueTime,
                header.Metadata.Status,
                header.Metadata.Usage,
                tcacDesig ?? tcacName ?? string.Empty,
                cycloneName,
                GetText(root.Element(IwxxmNs + "advisoryNumber")),
                obsPoint,
                forecasts,
                GetText(root.Element(IwxxmNs + "remarks")),
                nextAdvisory,
                header.IsTranslationFailed,
                header.FailedTac,
                header.Metadata.Reason,
                header.Metadata.Supplementary);
        }

        private static GeoCoordinate? ReadCyclonePosition(XElement conditions, List<ValidationError> errors) =>
            GmlGeometryReader.ReadPoint(
                conditions.Element(IwxxmNs + "tropicalCyclonePosition")?.Element(GmlNs + "Point"),
                GetGmlId(conditions),
                errors);
    }
}
