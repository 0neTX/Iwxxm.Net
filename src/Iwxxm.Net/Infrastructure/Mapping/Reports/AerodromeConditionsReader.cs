using System;
using System.Collections.Generic;
using System.Xml.Linq;
using System.Linq;
using Iwxxm.Net.Infrastructure.Mapping.CodeLists;
using Iwxxm.Net.Infrastructure.Mapping.Xml;
using Iwxxm.Net.Reports;
using static Iwxxm.Net.Infrastructure.Mapping.Xml.IwxxmXmlNames;
using static Iwxxm.Net.Infrastructure.Mapping.Xml.XmlValueReader;

namespace Iwxxm.Net.Infrastructure.Mapping.Reports
{
    /// <summary>
    /// Reads the aerodrome weather groups shared by METAR/SPECI observations and trends and TAF forecasts: surface wind,
    /// cloud layers and forecast groups (<c>MeteorologicalAerodromeTrendForecast</c> / <c>MeteorologicalAerodromeForecast</c>).
    /// </summary>
    internal static class AerodromeConditionsReader
    {
        internal const string OperatorInvalidCode = "ACL-OPERATOR-INVALID";

        /// <summary>Reads an IWXXM <c>RelationalOperatorType</c> (<c>ABOVE</c> / <c>BELOW</c>); <c>null</c> when absent or nil.</summary>
        public static RelationalOperator? ReadOperator(XElement? element, string context, List<ValidationError> errors)
        {
            string? value = GetText(element);
            switch (value)
            {
                case null:
                    return null;
                case "ABOVE":
                    return RelationalOperator.Above;
                case "BELOW":
                    return RelationalOperator.Below;
                default:
                    errors.Add(MappingErrors.Create(OperatorInvalidCode, $"Unsupported relational operator '{value}' on '{element!.Name.LocalName}' (expected ABOVE or BELOW).", context));
                    return null;
            }
        }

        /// <summary>Translates the <c>@nilReason</c> of an element (WMO register <c>common/nil</c>); <c>null</c> when absent.</summary>
        public static CodeListValue<NilReason>? ReadNilReason(XElement? element) =>
            CodeListTables.NilReasons.Translate(element?.Attribute("nilReason")?.Value);

        /// <summary>Translates the first <c>@nilReason</c> found among <paramref name="elements"/>; <c>null</c> when none has one.</summary>
        public static CodeListValue<NilReason>? ReadFirstNilReason(IEnumerable<XElement> elements) =>
            elements.Select(ReadNilReason).FirstOrDefault(reason => reason != null);

        public static SurfaceWind? ReadSurfaceWind(XElement? surfaceWindProp, string context, List<ValidationError> errors)
        {
            // Observation (metarSpeci.xsd), TAF forecast (taf.xsd) and METAR/SPECI trend (metarSpeci.xsd, common.xsd) winds.
            XElement? windElem = surfaceWindProp?.Element(IwxxmNs + "AerodromeSurfaceWind")
                                 ?? surfaceWindProp?.Element(IwxxmNs + "AerodromeSurfaceWindForecast")
                                 ?? surfaceWindProp?.Element(IwxxmNs + "AerodromeSurfaceWindTrendForecast");
            if (windElem == null)
                return null;

            bool isVar = ParseBoolAttribute(windElem, "variableWindDirection");
            double? dir = UomMeasureReader.ReadFixedUnit(windElem.Element(IwxxmNs + "meanWindDirection"), UcumUnits.Degree, context, errors);
            Speed? spd = UomMeasureReader.ReadSpeed(windElem.Element(IwxxmNs + "meanWindSpeed"), context, errors, UomMeasureReader.Subsets.WindSpeed);
            Speed? gust = UomMeasureReader.ReadSpeed(windElem.Element(IwxxmNs + "windGustSpeed"), context, errors, UomMeasureReader.Subsets.WindSpeed);
            RelationalOperator? spdOp = ReadOperator(windElem.Element(IwxxmNs + "meanWindSpeedOperator"), context, errors);
            RelationalOperator? gustOp = ReadOperator(windElem.Element(IwxxmNs + "windGustSpeedOperator"), context, errors);
            double? extremeCw = UomMeasureReader.ReadFixedUnit(windElem.Element(IwxxmNs + "extremeClockwiseWindDirection"), UcumUnits.Degree, context, errors);
            double? extremeCcw = UomMeasureReader.ReadFixedUnit(windElem.Element(IwxxmNs + "extremeCounterClockwiseWindDirection"), UcumUnits.Degree, context, errors);
            return new SurfaceWind(isVar, dir, spd, gust, spdOp, gustOp, extremeCw, extremeCcw);
        }

        /// <summary>
        /// Reads the cloud group (<c>AerodromeCloud</c> / <c>AerodromeCloudForecast</c>): <c>null</c> when the property is
        /// absent (or only an unresolved reference); a nil group keeps its reason (<c>NSC</c>, <c>NCD</c>); otherwise the
        /// layers (including nil layers and nil amount/base, e.g. <c>//////</c>, <c>SKC</c>) and the vertical visibility.
        /// </summary>
        public static AerodromeCloud? ReadCloud(XElement? cloudProp, string context, List<ValidationError> errors)
        {
            if (cloudProp == null)
                return null;

            XElement? cloudElem = cloudProp.Element(IwxxmNs + "AerodromeCloud")
                                  ?? cloudProp.Element(IwxxmNs + "AerodromeCloudForecast");
            if (cloudElem == null)
            {
                CodeListValue<NilReason>? groupNilReason = ReadNilReason(cloudProp);
                return groupNilReason == null ? null : new AerodromeCloud(nilReason: groupNilReason);
            }

            XElement? vvElem = cloudElem.Element(IwxxmNs + "verticalVisibility");
            Length? verticalVisibility = UomMeasureReader.ReadLength(vvElem, context, errors, UomMeasureReader.Subsets.CloudHeight);
            CodeListValue<NilReason>? vvNilReason = ReadNilReason(vvElem);

            var layers = new List<AerodromeCloudLayer>();
            foreach (XElement layerProp in cloudElem.Elements(IwxxmNs + "layer"))
            {
                XElement? cl = layerProp.Element(IwxxmNs + "CloudLayer");
                if (cl == null)
                {
                    CodeListValue<NilReason>? layerNilReason = ReadNilReason(layerProp);
                    if (layerNilReason != null)
                    {
                        layers.Add(new AerodromeCloudLayer(null, null, nilReason: layerNilReason));
                    }

                    continue;
                }

                XElement? amountElem = cl.Element(IwxxmNs + "amount");
                XElement? baseElem = cl.Element(IwxxmNs + "base");
                CodeListValue<AerodromeCloudAmount>? amount = CodeListTables.AerodromeCloudAmounts.Translate(GetHref(amountElem));
                Length? cloudBase = UomMeasureReader.ReadLength(baseElem, context, errors, UomMeasureReader.Subsets.CloudHeight);
                CodeListValue<ConvectiveCloudType>? cloudType =
                    CodeListTables.ConvectiveCloudTypes.Translate(GetHref(cl.Element(IwxxmNs + "cloudType")));
                layers.Add(new AerodromeCloudLayer(amount, cloudBase, cloudType, ReadNilReason(amountElem), ReadNilReason(baseElem)));
            }

            return new AerodromeCloud(layers, verticalVisibility, vvNilReason);
        }

        /// <summary>
        /// Translates the <c>xlink:href</c> codes of present / forecast weather elements (WMO code list
        /// <c>AerodromePresentOrForecastWeather</c>), in document order; elements without a reference (nil) are skipped.
        /// </summary>
        public static List<AerodromeWeather> ReadWeather(IEnumerable<XElement> weatherElements) =>
            CollectHrefs(weatherElements).Select(AerodromeWeatherCodeList.Translate).ToList();

        public static AerodromeForecastGroup ReadForecastGroup(XElement fcstElem, List<ValidationError> errors)
        {
            string id = GetGmlId(fcstElem);
            string? changeInd = fcstElem.Attribute("changeIndicator")?.Value;
            TrendTimeIndicator? timeIndicator = ReadTrendTimeIndicator(fcstElem.Element(IwxxmNs + "timeIndicator"), id, errors);
            bool cavok = ParseBoolAttribute(fcstElem, "cloudAndVisibilityOK");

            // phenomenonTime may be a period, an instant, or an unresolved xlink:href reference (-> null bounds).
            XElement? phenTime = fcstElem.Element(IwxxmNs + "phenomenonTime");
            GmlTimeReader.ReadPeriod(phenTime, "phenomenonTime", null, id, errors, out DateTimeOffset? validFrom, out DateTimeOffset? validUntil);
            if (!validFrom.HasValue && !validUntil.HasValue)
            {
                validFrom = GmlTimeReader.ReadOptionalInstant(phenTime, "phenomenonTime", id, errors);
            }

            double? vis = UomMeasureReader.ReadFixedUnit(fcstElem.Element(IwxxmNs + "prevailingVisibility"), UcumUnits.Meter, id, errors);
            RelationalOperator? visOp = ReadOperator(fcstElem.Element(IwxxmNs + "prevailingVisibilityOperator"), id, errors);
            SurfaceWind? wind = ReadSurfaceWind(fcstElem.Element(IwxxmNs + "surfaceWind"), id, errors);
            List<AerodromeWeather> wxList = ReadWeather(fcstElem.Elements(IwxxmNs + "weather"));
            CodeListValue<NilReason>? weatherNilReason = wxList.Count == 0 ? ReadFirstNilReason(fcstElem.Elements(IwxxmNs + "weather")) : null;
            AerodromeCloud? cloud = ReadCloud(fcstElem.Element(IwxxmNs + "cloud"), id, errors);
            List<AerodromeTemperatureForecast> temperatures = ReadTemperatures(fcstElem.Elements(IwxxmNs + "temperature"), id, errors);

            return new AerodromeForecastGroup(
                id,
                changeInd,
                cavok,
                validFrom,
                validUntil,
                vis,
                visOp,
                wind,
                wxList,
                cloud,
                timeIndicator,
                weatherNilReason,
                temperatures);
        }

        /// <summary>Reads the TAF <c>TX/TN</c> groups (<c>AerodromeAirTemperatureForecast</c>, temperatures in <c>Cel</c>).</summary>
        private static List<AerodromeTemperatureForecast> ReadTemperatures(IEnumerable<XElement> temperatureProps, string id, List<ValidationError> errors)
        {
            var temperatures = new List<AerodromeTemperatureForecast>();
            foreach (XElement prop in temperatureProps)
            {
                XElement? forecast = prop.Element(IwxxmNs + "AerodromeAirTemperatureForecast");
                if (forecast == null)
                    continue;

                temperatures.Add(new AerodromeTemperatureForecast(
                    UomMeasureReader.ReadFixedUnit(forecast.Element(IwxxmNs + "maximumAirTemperature"), UcumUnits.Celsius, id, errors),
                    GmlTimeReader.ReadOptionalInstant(forecast.Element(IwxxmNs + "maximumAirTemperatureTime"), "maximumAirTemperatureTime", id, errors),
                    UomMeasureReader.ReadFixedUnit(forecast.Element(IwxxmNs + "minimumAirTemperature"), UcumUnits.Celsius, id, errors),
                    GmlTimeReader.ReadOptionalInstant(forecast.Element(IwxxmNs + "minimumAirTemperatureTime"), "minimumAirTemperatureTime", id, errors)));
            }

            return temperatures;
        }

        /// <summary>Reads a METAR/SPECI trend <c>timeIndicator</c> (<c>AT</c>, <c>UNTIL</c>, <c>FROM</c>).</summary>
        private static TrendTimeIndicator? ReadTrendTimeIndicator(XElement? element, string id, List<ValidationError> errors)
        {
            string? value = GetText(element);
            switch (value)
            {
                case null:
                    return null;
                case "AT":
                    return TrendTimeIndicator.At;
                case "UNTIL":
                    return TrendTimeIndicator.Until;
                case "FROM":
                    return TrendTimeIndicator.From;
                default:
                    errors.Add(MappingErrors.Create(TimeIndicatorReader.InvalidCode, $"Unsupported trend timeIndicator '{value}' (expected AT, UNTIL or FROM).", id));
                    return null;
            }
        }
    }
}
