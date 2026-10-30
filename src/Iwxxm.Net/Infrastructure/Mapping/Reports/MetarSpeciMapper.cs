using System;
using System.Collections.Generic;
using System.Xml.Linq;
using Iwxxm.Net.Infrastructure.Mapping.Xml;
using Iwxxm.Net.Reports;
using static Iwxxm.Net.Infrastructure.Mapping.Xml.IwxxmXmlNames;
using static Iwxxm.Net.Infrastructure.Mapping.Xml.XmlValueReader;

namespace Iwxxm.Net.Infrastructure.Mapping.Reports
{
    /// <summary>
    /// Maps <c>iwxxm:METAR</c> and <c>iwxxm:SPECI</c> (<c>metarSpeci.xsd</c>, one shared XML type) onto <see cref="Metar"/>
    /// or <see cref="Speci"/>: the mapping is written once and the concrete aggregate is chosen by the factory passed in.
    /// </summary>
    internal static class MetarSpeciMapper
    {
        /// <summary>Signature shared by <see cref="Metar.Create"/> and <see cref="Speci.Create"/>.</summary>
        internal delegate Result<TObservation> ObservationFactory<TObservation>(
            string id,
            DateTimeOffset issueTimeUtc,
            DateTimeOffset? observationTimeUtc,
            ReportStatus reportStatus,
            PermissibleUsage permissibleUsage,
            bool isAutomatedStation,
            string aerodromeDesignator,
            string? aerodromeName,
            GeoCoordinate? aerodromePosition,
            bool isNilObservation,
            string? observationNilReason,
            bool isTranslationFailed,
            string? translationFailedTac,
            bool cloudAndVisibilityOk,
            double? airTemperatureCelsius,
            double? dewpointTemperatureCelsius,
            double? qnhHpa,
            SurfaceWind? surfaceWind,
            double? prevailingVisibilityMeters,
            IEnumerable<RunwayVisualRange>? runwayVisualRanges,
            IEnumerable<AerodromeWeather>? presentWeather,
            IEnumerable<AerodromeCloudLayer>? cloudLayers,
            IEnumerable<AerodromeForecastGroup>? trendForecasts,
            PermissibleUsageReason? permissibleUsageReason,
            string? permissibleUsageSupplementary)
            where TObservation : AerodromeObservation;

        public static Result<TObservation> Map<TObservation>(XElement root, ObservationFactory<TObservation> create)
            where TObservation : AerodromeObservation
        {
            var errors = new List<ValidationError>();
            ReportHeader header = ReportHeader.Read(root, errors);
            string id = header.Id;
            bool isAutomated = ParseBoolAttribute(root, "automatedStation");
            bool isTranslationFailed = header.IsTranslationFailed ||
                                       root.Attribute("translatedBulletinID") != null && root.Element(IwxxmNs + "observation") == null;

            // observationTime is mandatory (metarSpeci.xsd): absent -> mapping error; xlink:href only -> null (unresolved).
            DateTimeOffset? obsTime = GmlTimeReader.ReadRequiredInstant(
                root.Element(IwxxmNs + "observationTime"), "observationTime", MappingErrors.ObservationTimeMissingCode, id, errors);

            AixmFeatureReader.ReadAerodrome(
                root.Element(IwxxmNs + "aerodrome"),
                errors,
                out string designator,
                out string? name,
                out GeoCoordinate? position);

            XElement? obsProp = root.Element(IwxxmNs + "observation");
            string? obsNilReason = obsProp?.Attribute("nilReason")?.Value;
            XElement? obsElem = obsProp?.Element(IwxxmNs + "MeteorologicalAerodromeObservation");
            bool isNilObs = obsElem == null && !string.IsNullOrWhiteSpace(obsNilReason);

            bool cavok = false;
            double? airTemp = null;
            double? dewpoint = null;
            double? qnh = null;
            SurfaceWind? surfaceWind = null;
            double? visibility = null;
            var rvrs = new List<RunwayVisualRange>();
            var presentWeather = new List<AerodromeWeather>();
            var cloudLayers = new List<AerodromeCloudLayer>();

            if (obsElem != null)
            {
                cavok = ParseBoolAttribute(obsElem, "cloudAndVisibilityOK");
                airTemp = UomMeasureReader.ReadFixedUnit(obsElem.Element(IwxxmNs + "airTemperature"), UcumUnits.Celsius, id, errors);
                dewpoint = UomMeasureReader.ReadFixedUnit(obsElem.Element(IwxxmNs + "dewpointTemperature"), UcumUnits.Celsius, id, errors);
                qnh = UomMeasureReader.ReadFixedUnit(obsElem.Element(IwxxmNs + "qnh"), UcumUnits.Hectopascal, id, errors);
                surfaceWind = AerodromeConditionsReader.ReadSurfaceWind(obsElem.Element(IwxxmNs + "surfaceWind"), id, errors);
                visibility = UomMeasureReader.ReadFixedUnit(
                    obsElem.Element(IwxxmNs + "visibility")
                        ?.Element(IwxxmNs + "AerodromeHorizontalVisibility")
                        ?.Element(IwxxmNs + "prevailingVisibility"),
                    UcumUnits.Meter,
                    id,
                    errors);

                ReadRunwayVisualRanges(obsElem, rvrs, id, errors);
                presentWeather = AerodromeConditionsReader.ReadWeather(obsElem.Elements(IwxxmNs + "presentWeather"));
                AerodromeConditionsReader.ReadCloudLayers(obsElem.Element(IwxxmNs + "cloud"), cloudLayers, id, errors);
            }

            var trends = new List<AerodromeForecastGroup>();
            foreach (XElement trendProp in root.Elements(IwxxmNs + "trendForecast"))
            {
                XElement? trendElem = trendProp.Element(IwxxmNs + "MeteorologicalAerodromeTrendForecast");
                if (trendElem != null)
                {
                    trends.Add(AerodromeConditionsReader.ReadForecastGroup(trendElem, errors));
                }
            }

            if (errors.Count > 0)
            {
                return Result<TObservation>.Failure(errors);
            }

            return create(
                id,
                header.IssueTime,
                obsTime,
                header.Metadata.Status,
                header.Metadata.Usage,
                isAutomated,
                designator,
                name,
                position,
                isNilObs,
                obsNilReason,
                isTranslationFailed,
                header.FailedTac,
                cavok,
                airTemp,
                dewpoint,
                qnh,
                surfaceWind,
                visibility,
                rvrs,
                presentWeather,
                cloudLayers,
                trends,
                header.Metadata.Reason,
                header.Metadata.Supplementary);
        }

        private static void ReadRunwayVisualRanges(XElement obsElem, List<RunwayVisualRange> target, string id, List<ValidationError> errors)
        {
            foreach (XElement rvrProp in obsElem.Elements(IwxxmNs + "rvr"))
            {
                XElement? rvrElem = rvrProp.Element(IwxxmNs + "AerodromeRunwayVisualRange");
                if (rvrElem == null) continue;

                // The runway may be given by a local xlink:href reference (resolved); a remote one leaves the designator null.
                string? rwyDesig = GetText(GmlReferenceResolver.Content(rvrElem.Element(IwxxmNs + "runway"), AixmNs + "RunwayDirection")
                    ?.Element(AixmNs + "timeSlice")
                    ?.Element(AixmNs + "RunwayDirectionTimeSlice")
                    ?.Element(AixmNs + "designator"));
                double? meanRvr = UomMeasureReader.ReadFixedUnit(rvrElem.Element(IwxxmNs + "meanRVR"), UcumUnits.Meter, id, errors);
                string? tendency = rvrElem.Attribute("pastTendency")?.Value;
                target.Add(new RunwayVisualRange(rwyDesig, meanRvr, tendency));
            }
        }
    }
}
