using System;
using System.Linq;
using Iwxxm.Net.Reports;
using Xunit;

namespace Iwxxm.Net.Tests.DataFidelity
{
    /// <summary>
    /// TAC qualifiers and nil-reason semantics (AUD-M11): operators (<c>P</c>/<c>M</c>), variable wind extremes, minimum
    /// visibility, vertical visibility, RVR tendency, TAF <c>TX/TN</c> and <c>NSW</c>/<c>NSC</c>/<c>NCD</c>/<c>SKC</c>.
    /// Synthetic variants of the official examples go through the full pipeline, so they are also schema-valid.
    /// </summary>
    public sealed class TacQualifierTests
    {
        private const string Nil = "http://codes.wmo.int/common/nil/";

        [Fact]
        public void SpeciA32_MinimumVisibilityAndDirection_AreRead()
        {
            Speci speci = Parse<Speci>(SampleDocuments.Text("speci-A3-2.xml"));

            Assert.Equal(new AerodromeHorizontalVisibility(3000, null, 1200, 45), speci.Visibility);
            Assert.Equal(3000, speci.PrevailingVisibilityMeters);
        }

        [Fact]
        public void SpeciA32_TrendNsw_AndNsc_AreDistinguishableFromNotReported()
        {
            Speci speci = Parse<Speci>(SampleDocuments.Text("speci-A3-2.xml"));
            AerodromeForecastGroup trend = speci.TrendForecasts.Last();

            Assert.True(trend.IsNoSignificantWeather);
            Assert.Empty(trend.Weather);
            Assert.Equal(NilReason.NothingOfOperationalSignificance, trend.WeatherNilReason!.Code);
            Assert.NotNull(trend.Cloud);
            Assert.True(trend.Cloud!.IsNoSignificantCloud);
            Assert.Empty(trend.CloudLayers);

            // The observation reports weather and cloud: no nil reason.
            Assert.Null(speci.PresentWeatherNilReason);
            Assert.Null(speci.Cloud!.NilReason);
        }

        [Fact]
        public void MetarA31_RvrTendency_AndTrendVisibilityOperator_AreTyped()
        {
            Metar metar = Parse<Metar>(SampleDocuments.Text("metar-A3-1.xml"));

            RunwayVisualRange rvr = Assert.Single(metar.RunwayVisualRanges);
            Assert.Equal(RunwayVisualRangeTendency.Upward, rvr.PastTendency);
            Assert.Null(rvr.MeanRvrOperator);

            AerodromeForecastGroup trend = metar.TrendForecasts.Last();
            Assert.Equal(RelationalOperator.Above, trend.PrevailingVisibilityOperator);
            Assert.Null(metar.TrendForecasts.First().PrevailingVisibilityOperator);
            Assert.True(trend.IsNoSignificantWeather);
            Assert.Null(trend.Cloud);
        }

        [Fact]
        public void Metar_WindOperatorsAndExtremeDirections_AreRead()
        {
            string xml = SampleDocuments.Text("metar-A3-1.xml").Replace(
                "<iwxxm:meanWindSpeed uom=\"m/s\">4.0</iwxxm:meanWindSpeed>",
                "<iwxxm:meanWindSpeed uom=\"m/s\">49</iwxxm:meanWindSpeed>" +
                "<iwxxm:meanWindSpeedOperator>ABOVE</iwxxm:meanWindSpeedOperator>" +
                "<iwxxm:windGustSpeed uom=\"m/s\">60</iwxxm:windGustSpeed>" +
                "<iwxxm:windGustSpeedOperator>ABOVE</iwxxm:windGustSpeedOperator>" +
                "<iwxxm:extremeClockwiseWindDirection uom=\"deg\">280</iwxxm:extremeClockwiseWindDirection>" +
                "<iwxxm:extremeCounterClockwiseWindDirection uom=\"deg\">200</iwxxm:extremeCounterClockwiseWindDirection>");

            SurfaceWind wind = Parse<Metar>(xml).SurfaceWind!;

            Assert.Equal(new Speed(49, SpeedUnit.MetersPerSecond), wind.MeanSpeed);
            Assert.Equal(RelationalOperator.Above, wind.MeanSpeedOperator);
            Assert.Equal(RelationalOperator.Above, wind.GustSpeedOperator);
            Assert.Equal(280, wind.ExtremeClockwiseDirectionDegrees);
            Assert.Equal(200, wind.ExtremeCounterClockwiseDirectionDegrees);
        }

        [Fact]
        public void Metar_RvrOperatorAndVisibilityOperator_AreRead()
        {
            string xml = SampleDocuments.Text("metar-A3-1.xml")
                .Replace(
                    "<iwxxm:meanRVR uom=\"m\">1000</iwxxm:meanRVR>",
                    "<iwxxm:meanRVR uom=\"m\">50</iwxxm:meanRVR><iwxxm:meanRVROperator>BELOW</iwxxm:meanRVROperator>")
                .Replace(
                    "<iwxxm:prevailingVisibility uom=\"m\">600</iwxxm:prevailingVisibility>",
                    "<iwxxm:prevailingVisibility uom=\"m\">10000</iwxxm:prevailingVisibility><iwxxm:prevailingVisibilityOperator>ABOVE</iwxxm:prevailingVisibilityOperator>");

            Metar metar = Parse<Metar>(xml);

            Assert.Equal(RelationalOperator.Below, Assert.Single(metar.RunwayVisualRanges).MeanRvrOperator);
            Assert.Equal(RelationalOperator.Above, metar.Visibility!.PrevailingOperator);
            Assert.Null(metar.Visibility.MinimumMeters);
        }

        [Fact]
        public void Metar_VerticalVisibility_IsReadWithItsUnit()
        {
            Metar metar = Parse<Metar>(ReplaceCloud(
                "<iwxxm:cloud><iwxxm:AerodromeCloud><iwxxm:verticalVisibility uom=\"[ft_i]\">200</iwxxm:verticalVisibility></iwxxm:AerodromeCloud></iwxxm:cloud>"));

            Assert.Equal(new Length(200, LengthUnit.Feet), metar.Cloud!.VerticalVisibility);
            Assert.Null(metar.Cloud.VerticalVisibilityNilReason);
            Assert.Empty(metar.CloudLayers);
        }

        [Fact]
        public void Metar_VerticalVisibilityNotObservable_KeepsItsNilReason()
        {
            Metar metar = Parse<Metar>(ReplaceCloud(
                "<iwxxm:cloud><iwxxm:AerodromeCloud><iwxxm:verticalVisibility xsi:nil=\"true\" uom=\"N/A\" nilReason=\"" + Nil +
                "notObservable\"/></iwxxm:AerodromeCloud></iwxxm:cloud>"));

            Assert.Null(metar.Cloud!.VerticalVisibility);
            Assert.Equal(NilReason.NotObservable, metar.Cloud.VerticalVisibilityNilReason!.Code);
        }

        [Fact]
        public void Metar_NoCloudDetected_IsNcd_AndPresentWeatherNotObservable_IsKept()
        {
            string xml = ReplaceCloud("<iwxxm:cloud xsi:nil=\"true\" nilReason=\"" + Nil + "notDetectedByAutoSystem\"/>")
                .Replace(
                    "<iwxxm:presentWeather xlink:href=\"http://codes.wmo.int/306/4678/DZ\"/>\n            <iwxxm:presentWeather xlink:href=\"http://codes.wmo.int/306/4678/FG\"/>",
                    "<iwxxm:presentWeather nilReason=\"" + Nil + "notObservable\"/>");

            Metar metar = Parse<Metar>(xml);

            Assert.True(metar.Cloud!.IsNoCloudDetected);
            Assert.False(metar.Cloud.IsNoSignificantCloud);
            Assert.Empty(metar.PresentWeather);
            Assert.Equal(NilReason.NotObservable, metar.PresentWeatherNilReason!.Code);
        }

        [Fact]
        public void Metar_ClearSky_IsSkcAmountWithInapplicableBase()
        {
            Metar metar = Parse<Metar>(ReplaceCloud(
                "<iwxxm:cloud><iwxxm:AerodromeCloud><iwxxm:layer><iwxxm:CloudLayer>" +
                "<iwxxm:amount xlink:href=\"http://codes.wmo.int/49-2/CloudAmountReportedAtAerodrome/SKC\"/>" +
                "<iwxxm:base xsi:nil=\"true\" uom=\"N/A\" nilReason=\"" + Nil + "inapplicable\"/>" +
                "</iwxxm:CloudLayer></iwxxm:layer></iwxxm:AerodromeCloud></iwxxm:cloud>"));

            AerodromeCloudLayer layer = Assert.Single(metar.CloudLayers);
            Assert.Equal(AerodromeCloudAmount.SkyClear, layer.Amount!.Code);
            Assert.Null(layer.Base);
            Assert.Equal(NilReason.Inapplicable, layer.BaseNilReason!.Code);
        }

        [Fact]
        public void Metar_NilCloudLayer_IsKeptWithItsReason()
        {
            Metar metar = Parse<Metar>(ReplaceCloud(
                "<iwxxm:cloud><iwxxm:AerodromeCloud><iwxxm:layer xsi:nil=\"true\" nilReason=\"" + Nil + "notObservable\"/></iwxxm:AerodromeCloud></iwxxm:cloud>"));

            AerodromeCloudLayer layer = Assert.Single(metar.CloudLayers);
            Assert.Null(layer.Amount);
            Assert.Equal(NilReason.NotObservable, layer.NilReason!.Code);
        }

        [Fact]
        public void Taf_MaximumAndMinimumTemperatures_AreRead()
        {
            string taf = SampleDocuments.Text("taf-A5-1.xml");
            int baseEnd = taf.IndexOf("</iwxxm:cloud>", StringComparison.Ordinal) + "</iwxxm:cloud>".Length;
            string xml = taf.Insert(baseEnd,
                "<iwxxm:temperature><iwxxm:AerodromeAirTemperatureForecast>" +
                "<iwxxm:maximumAirTemperature uom=\"Cel\">27</iwxxm:maximumAirTemperature>" +
                "<iwxxm:maximumAirTemperatureTime><gml:TimeInstant gml:id=\"tx-1\"><gml:timePosition>2012-08-16T14:00:00Z</gml:timePosition></gml:TimeInstant></iwxxm:maximumAirTemperatureTime>" +
                "<iwxxm:minimumAirTemperature uom=\"Cel\">-2</iwxxm:minimumAirTemperature>" +
                "<iwxxm:minimumAirTemperatureTime><gml:TimeInstant gml:id=\"tn-1\"><gml:timePosition>2012-08-16T05:00:00Z</gml:timePosition></gml:TimeInstant></iwxxm:minimumAirTemperatureTime>" +
                "</iwxxm:AerodromeAirTemperatureForecast></iwxxm:temperature>");

            Taf result = Parse<Taf>(xml);

            AerodromeTemperatureForecast temperature = Assert.Single(result.BaseForecast!.Temperatures);
            Assert.Equal(27, temperature.MaximumCelsius);
            Assert.Equal(new DateTimeOffset(2012, 8, 16, 14, 0, 0, TimeSpan.Zero), temperature.MaximumTimeUtc);
            Assert.Equal(-2, temperature.MinimumCelsius);
            Assert.Equal(new DateTimeOffset(2012, 8, 16, 5, 0, 0, TimeSpan.Zero), temperature.MinimumTimeUtc);
            Assert.All(result.ChangeForecasts, change => Assert.Empty(change.Temperatures));
        }

        [Fact]
        public void Taf_ChangeGroupNsw_IsDistinguishableFromNoWeatherGroup()
        {
            string source = SampleDocuments.Text("taf-A5-1.xml");
            int secondCloud = source.IndexOf("<iwxxm:cloud>", source.IndexOf("<iwxxm:changeForecast>", StringComparison.Ordinal), StringComparison.Ordinal);
            Taf taf = Parse<Taf>(source.Insert(secondCloud, "<iwxxm:weather nilReason=\"" + Nil + "nothingOfOperationalSignificance\"/>"));

            Assert.True(taf.ChangeForecasts[0].IsNoSignificantWeather);
            Assert.Empty(taf.ChangeForecasts[0].Weather);
            Assert.False(taf.BaseForecast!.IsNoSignificantWeather);
            Assert.Null(taf.BaseForecast.WeatherNilReason);
        }

        [Fact]
        public void Operators_OutsideTheEnumeration_AreMappingErrors()
        {
            string xml = SampleDocuments.Text("metar-A3-1.xml").Replace(
                "<iwxxm:prevailingVisibilityOperator>ABOVE</iwxxm:prevailingVisibilityOperator>",
                "<iwxxm:prevailingVisibilityOperator>OVER</iwxxm:prevailingVisibilityOperator>");

            Result<IwxxmDocument> result = SampleDocuments.MapDirectly(xml);

            Assert.True(result.IsFailure);
            Assert.Contains(result.Errors, e => e.Code == "ACL-OPERATOR-INVALID");
        }

        private static string ReplaceCloud(string replacement)
        {
            string metar = SampleDocuments.Text("metar-A3-1.xml");
            int start = metar.IndexOf("<iwxxm:cloud>", StringComparison.Ordinal);
            int end = metar.IndexOf("</iwxxm:cloud>", StringComparison.Ordinal) + "</iwxxm:cloud>".Length;
            return metar.Substring(0, start) + replacement + metar.Substring(end);
        }

        private static T Parse<T>(string xml)
            where T : IwxxmDocument
        {
            Result<IwxxmDocument> result = IwxxmParser.Parse(xml);
            Assert.True(result.IsSuccess, string.Join(" | ", result.Errors));
            return Assert.IsType<T>(result.Value);
        }
    }
}
