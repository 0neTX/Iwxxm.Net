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
    /// Reads the aerodrome weather groups shared by METAR/SPECI observations and trends and TAF forecasts: surface wind,
    /// cloud layers and forecast groups (<c>MeteorologicalAerodromeTrendForecast</c> / <c>MeteorologicalAerodromeForecast</c>).
    /// </summary>
    internal static class AerodromeConditionsReader
    {
        public static SurfaceWindInfo? ReadSurfaceWind(XElement? surfaceWindProp, string context, List<ValidationError> errors)
        {
            XElement? windElem = surfaceWindProp?.Element(IwxxmNs + "AerodromeSurfaceWind")
                                 ?? surfaceWindProp?.Element(IwxxmNs + "AerodromeSurfaceWindForecast");
            if (windElem == null)
                return null;

            bool isVar = ParseBoolAttribute(windElem, "variableWindDirection");
            double? dir = UomMeasureReader.ReadFixedUnit(windElem.Element(IwxxmNs + "meanWindDirection"), UcumUnits.Degree, context, errors);
            Speed? spd = UomMeasureReader.ReadSpeed(windElem.Element(IwxxmNs + "meanWindSpeed"), context, errors);
            Speed? gust = UomMeasureReader.ReadSpeed(windElem.Element(IwxxmNs + "windGustSpeed"), context, errors);
            return new SurfaceWindInfo(isVar, dir, spd, gust);
        }

        public static void ReadCloudLayers(
            XElement? cloudProp,
            List<AerodromeCloudLayerInfo> target,
            string context,
            List<ValidationError> errors)
        {
            XElement? cloudElem = cloudProp?.Element(IwxxmNs + "AerodromeCloud")
                                  ?? cloudProp?.Element(IwxxmNs + "AerodromeCloudForecast");
            if (cloudElem == null)
                return;

            foreach (XElement layerProp in cloudElem.Elements(IwxxmNs + "layer"))
            {
                XElement? cl = layerProp.Element(IwxxmNs + "CloudLayer");
                if (cl == null) continue;

                string? amount = GetHref(cl.Element(IwxxmNs + "amount"));
                Length? cloudBase = UomMeasureReader.ReadLength(cl.Element(IwxxmNs + "base"), context, errors);
                string? cloudType = GetHref(cl.Element(IwxxmNs + "cloudType"));
                target.Add(new AerodromeCloudLayerInfo(amount, cloudBase, cloudType));
            }
        }

        public static AerodromeForecastGroupInfo ReadForecastGroup(XElement fcstElem, List<ValidationError> errors)
        {
            string id = GetGmlId(fcstElem);
            string? changeInd = fcstElem.Attribute("changeIndicator")?.Value;
            bool cavok = ParseBoolAttribute(fcstElem, "cloudAndVisibilityOK");

            // phenomenonTime may be a period, an instant, or an unresolved xlink:href reference (-> null bounds).
            XElement? phenTime = fcstElem.Element(IwxxmNs + "phenomenonTime");
            GmlTimeReader.ReadPeriod(phenTime, "phenomenonTime", null, id, errors, out DateTimeOffset? validFrom, out DateTimeOffset? validUntil);
            if (!validFrom.HasValue && !validUntil.HasValue)
            {
                validFrom = GmlTimeReader.ReadOptionalInstant(phenTime, "phenomenonTime", id, errors);
            }

            double? vis = UomMeasureReader.ReadFixedUnit(fcstElem.Element(IwxxmNs + "prevailingVisibility"), UcumUnits.Meter, id, errors);
            string? visOp = GetText(fcstElem.Element(IwxxmNs + "prevailingVisibilityOperator"));
            SurfaceWindInfo? wind = ReadSurfaceWind(fcstElem.Element(IwxxmNs + "surfaceWind"), id, errors);
            List<string> wxList = CollectHrefs(fcstElem.Elements(IwxxmNs + "weather"));

            var layers = new List<AerodromeCloudLayerInfo>();
            ReadCloudLayers(fcstElem.Element(IwxxmNs + "cloud"), layers, id, errors);

            return new AerodromeForecastGroupInfo(
                id,
                changeInd,
                cavok,
                validFrom,
                validUntil,
                vis,
                visOp,
                wind,
                wxList,
                layers);
        }
    }
}
