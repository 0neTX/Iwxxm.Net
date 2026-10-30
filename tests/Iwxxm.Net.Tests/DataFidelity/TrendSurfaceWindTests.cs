using System.Linq;
using System.Xml.Linq;
using Iwxxm.Net.Reports;
using Xunit;
using static Iwxxm.Net.Tests.DataFidelity.SampleDocuments;

namespace Iwxxm.Net.Tests.DataFidelity
{
    /// <summary>
    /// AUD-H4: a METAR/SPECI trend carries its wind as <c>iwxxm:AerodromeSurfaceWindTrendForecast</c>
    /// (<c>metarSpeci.xsd</c> MeteorologicalAerodromeTrendForecastType/surfaceWind, <c>common.xsd</c>).
    /// </summary>
    public sealed class TrendSurfaceWindTests
    {
        private static XDocument MetarWithTrendWind(string uom, string speed)
        {
            XDocument doc = Load("metar-A3-1.xml");
            XElement trend = doc.Root!.Elements(IwxxmNs + "trendForecast").First()
                .Element(IwxxmNs + "MeteorologicalAerodromeTrendForecast")!;
            trend.Element(IwxxmNs + "prevailingVisibility")!.AddAfterSelf(
                new XElement(IwxxmNs + "surfaceWind",
                    new XElement(IwxxmNs + "AerodromeSurfaceWindTrendForecast",
                        new XElement(IwxxmNs + "meanWindDirection", new XAttribute("uom", "deg"), "270"),
                        new XElement(IwxxmNs + "meanWindSpeed", new XAttribute("uom", uom), speed))));
            return doc;
        }

        [Fact]
        public void MetarTrend_AerodromeSurfaceWindTrendForecast_IsMapped()
        {
            var result = IwxxmParser.ParseAs<Metar>(MetarWithTrendWind("[kn_i]", "15").ToString());

            Assert.True(result.IsSuccess, string.Join(" | ", result.Errors));
            SurfaceWind? wind = result.Value.TrendForecasts[0].SurfaceWind;
            Assert.NotNull(wind);
            Assert.Equal(270.0, wind!.MeanDirectionDegrees);
            Assert.Equal(new Speed(15.0, SpeedUnit.Knots), wind.MeanSpeed);
            Assert.Null(wind.GustSpeed);
            Assert.Null(result.Value.TrendForecasts[1].SurfaceWind);
        }

        [Fact]
        public void SpeciTrend_AerodromeSurfaceWindTrendForecast_IsMapped()
        {
            XDocument doc = MetarWithTrendWind("m/s", "8");
            doc.Root!.Name = IwxxmNs + "SPECI";

            var result = MapDirectly(doc);

            Assert.True(result.IsSuccess, string.Join(" | ", result.Errors));
            var speci = Assert.IsType<Speci>(result.Value);
            Assert.Equal(new Speed(8.0, SpeedUnit.MetersPerSecond), speci.TrendForecasts[0].SurfaceWind!.MeanSpeed);
        }
    }
}
