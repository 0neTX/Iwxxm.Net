using System;
using System.Collections.Generic;
using System.Xml.Linq;
using Iwxxm.Net.Infrastructure.Mapping.Xml;
using Iwxxm.Net.Reports;
using static Iwxxm.Net.Infrastructure.Mapping.Xml.IwxxmXmlNames;
using static Iwxxm.Net.Infrastructure.Mapping.Xml.XmlValueReader;

namespace Iwxxm.Net.Infrastructure.Mapping.Reports
{
    /// <summary>Maps <c>iwxxm:TropicalCycloneAdvisory</c> (<c>tca.xsd</c>) onto <see cref="Tca"/>.</summary>
    internal static class TcaMapper
    {
        public static Result<Tca> Map(XElement root, UomAliasCollector aliases)
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
            var cbClouds = new List<EnRoutePhenomenonVolume>();
            XElement? obsElem = root
                .Element(IwxxmNs + "observation")
                ?.Element(IwxxmNs + "TropicalCycloneObservedConditions");

            if (obsElem != null)
            {
                DateTimeOffset? obsTime = GmlTimeReader.ReadOptionalInstant(obsElem.Element(IwxxmNs + "phenomenonTime"), "phenomenonTime", id, errors);
                GeoCoordinate? pos = ReadCyclonePosition(obsElem, errors);
                Speed? maxWind = UomMeasureReader.ReadSpeed(obsElem.Element(IwxxmNs + "maximumSurfaceWindSpeed"), id, errors, UomMeasureReader.Subsets.WindSpeed, aliases);
                double? pressure = UomMeasureReader.ReadFixedUnit(obsElem.Element(IwxxmNs + "centralPressure"), UcumUnits.Hectopascal, id, errors);

                // Movement and intensity change of the observed position (AUD-L3).
                TropicalCycloneMovement? movement = ReadEnum(GetText(obsElem.Element(IwxxmNs + "movement")), "movement", id, errors,
                    ("MOVING", TropicalCycloneMovement.Moving), ("STATIONARY", TropicalCycloneMovement.Stationary));
                double? movementDirection = UomMeasureReader.ReadFixedUnit(obsElem.Element(IwxxmNs + "movementDirection"), UcumUnits.Degree, id, errors);
                Speed? movementSpeed = UomMeasureReader.ReadSpeed(obsElem.Element(IwxxmNs + "movementSpeed"), id, errors, UomMeasureReader.Subsets.SpeedOfMotion, aliases);
                TropicalCycloneIntensityChange? intensityChange = ReadEnum(GetText(obsElem.Element(IwxxmNs + "intensityChange")), "intensityChange", id, errors,
                    ("NO_CHANGE", TropicalCycloneIntensityChange.NoChange), ("WEAKEN", TropicalCycloneIntensityChange.Weaken), ("INTENSIFY", TropicalCycloneIntensityChange.Intensify));

                if (pos.HasValue)
                {
                    obsPoint = new TropicalCycloneTrackPoint(obsTime, pos.Value, maxWind, pressure, movement, movementDirection, movementSpeed, intensityChange);
                }

                // cumulonimbusCloudLocation (0..n, nillable): CB extent, polygon or circle ("WI 250NM OF TC CENTRE").
                foreach (XElement cbProp in obsElem.Elements(IwxxmNs + "cumulonimbusCloudLocation"))
                {
                    XElement? volume = cbProp.Element(AixmNs + "AirspaceVolume");
                    if (volume != null)
                    {
                        cbClouds.Add(EnRouteVolumeReader.ReadAirspaceVolume(volume, errors));
                    }
                }
            }

            var forecasts = new List<TropicalCycloneTrackPoint>();
            foreach (XElement fcstProp in root.Elements(IwxxmNs + "forecast"))
            {
                XElement? fcstElem = fcstProp.Element(IwxxmNs + "TropicalCycloneForecastConditions");
                if (fcstElem == null) continue;

                DateTimeOffset? fcstTime = GmlTimeReader.ReadOptionalInstant(fcstElem.Element(IwxxmNs + "phenomenonTime"), "phenomenonTime", id, errors);
                GeoCoordinate? pos = ReadCyclonePosition(fcstElem, errors);
                Speed? maxWind = UomMeasureReader.ReadSpeed(fcstElem.Element(IwxxmNs + "maximumSurfaceWindSpeed"), id, errors, UomMeasureReader.Subsets.WindSpeed, aliases);

                if (pos.HasValue)
                {
                    forecasts.Add(new TropicalCycloneTrackPoint(fcstTime, pos.Value, maxWind));
                }
            }

            DateTimeOffset? nextAdvisory = GmlTimeReader.ReadOptionalInstant(root.Element(IwxxmNs + "nextAdvisoryTime"), "nextAdvisoryTime", id, errors);

            if (errors.Count > 0)
            {
                return Result<Tca>.Failure(errors);
            }

            return Tca.Create(
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
                header.Metadata.Supplementary,
                cbClouds);
        }

        private static TEnum? ReadEnum<TEnum>(string? value, string field, string id, List<ValidationError> errors, params (string Code, TEnum Member)[] members)
            where TEnum : struct
        {
            if (value == null)
                return null;

            foreach ((string code, TEnum member) in members)
            {
                if (string.Equals(code, value, StringComparison.Ordinal))
                    return member;
            }

            errors.Add(MappingErrors.Create("ACL-TCA-VALUE-INVALID", $"Unsupported {field} value '{value}'.", id));
            return null;
        }

        private static GeoCoordinate? ReadCyclonePosition(XElement conditions, List<ValidationError> errors) =>
            GmlGeometryReader.ReadPoint(
                conditions.Element(IwxxmNs + "tropicalCyclonePosition")?.Element(GmlNs + "Point"),
                GetGmlId(conditions),
                errors);
    }
}
