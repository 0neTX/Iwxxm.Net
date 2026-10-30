using System;
using System.Linq;
using System.Text.Json;
using Iwxxm.Net.Reports;
using Xunit;
using static Iwxxm.Net.Tests.DataFidelity.SampleDocuments;

namespace Iwxxm.Net.Tests.DataFidelity
{
    /// <summary>
    /// AUD-M9: time context is kept per element: SIGMET/AIRMET collection <c>@timeIndicator</c> and <c>phenomenonTime</c>,
    /// Volcanic Ash Advisory conditions per forecast offset with their <c>phenomenonTime</c> and <c>@status</c>, and the
    /// METAR/SPECI trend <c>timeIndicator</c>.
    /// </summary>
    public sealed class TimeContextTests
    {
        private static DateTimeOffset Utc(int year, int month, int day, int hour) => new DateTimeOffset(year, month, day, hour, 0, 0, TimeSpan.Zero);

        [Fact]
        public void Sigmet_ForecastCollection_KeepsTimeIndicator()
        {
            var result = IwxxmParser.ParseFileAs<Sigmet>(PathOf("sigmet-A6-1a-TS.xml"));

            Assert.True(result.IsSuccess, string.Join(" | ", result.Errors));
            Assert.All(result.Value.EvolvingConditions, v => Assert.Equal(TimeIndicator.Forecast, v.TimeIndicator));
        }

        [Fact]
        public void Sigmet_MultiLocation_KeepsObservationAndForecastTimes_IncludingReferencedTimes()
        {
            var result = IwxxmParser.ParseFileAs<Sigmet>(PathOf("sigmet-multi-location-VA.xml"));

            Assert.True(result.IsSuccess, string.Join(" | ", result.Errors));
            Sigmet sigmet = result.Value;
            Assert.Equal(2, sigmet.EvolvingConditions.Count);
            Assert.All(sigmet.EvolvingConditions, v =>
            {
                Assert.Equal(TimeIndicator.Observation, v.TimeIndicator);
                Assert.Equal(Utc(2018, 7, 10, 12), v.PhenomenonTimeUtc);
            });
            Assert.Equal(2, sigmet.ForecastPositions.Count);
            Assert.All(sigmet.ForecastPositions, v =>
            {
                Assert.Null(v.TimeIndicator);
                Assert.Equal(Utc(2018, 7, 10, 18), v.PhenomenonTimeUtc);
            });
        }

        [Fact]
        public void Vaa_ForecastsAreKeptPerOffset_WithTimeAndStatus()
        {
            var result = IwxxmParser.ParseFileAs<Vaa>(PathOf("va-advisory-A7-2.xml"));

            Assert.True(result.IsSuccess, string.Join(" | ", result.Errors));
            Vaa vaa = result.Value;
            Assert.Equal(Utc(2024, 9, 23, 1), vaa.Observation!.PhenomenonTimeUtc);
            Assert.Equal(VolcanicAshConditionsStatus.Provided, vaa.Observation.Status);
            Assert.Same(vaa.Observation.AshClouds, vaa.ObservedAshClouds);

            Assert.Equal(new[] { Utc(2024, 9, 23, 7), Utc(2024, 9, 23, 13), Utc(2024, 9, 23, 19) }, vaa.Forecasts.Select(f => f.PhenomenonTimeUtc!.Value).ToArray());
            Assert.Equal(new[] { 2, 1, 0 }, vaa.Forecasts.Select(f => f.AshClouds.Count).ToArray());
            Assert.Equal(VolcanicAshConditionsStatus.NoVolcanicAshExpected, vaa.Forecasts[2].Status);
            Assert.All(vaa.Forecasts[0].AshClouds, c => Assert.Equal(Utc(2024, 9, 23, 7), c.PhenomenonTimeUtc));
        }

        [Fact]
        public void Vaa_GeoJson_KeepsTheNoAshExpectedForecast()
        {
            var result = IwxxmParser.ParseFileToGeoJson(PathOf("va-advisory-A7-2.xml"));

            Assert.True(result.IsSuccess, string.Join(" | ", result.Errors));
            using JsonDocument json = JsonDocument.Parse(result.Value);
            JsonElement noAsh = json.RootElement.GetProperty("features").EnumerateArray()
                .Single(f => f.GetProperty("properties").GetProperty("featureType").GetString() == "FORECAST_ASH_CONDITIONS");
            Assert.Equal(JsonValueKind.Null, noAsh.GetProperty("geometry").ValueKind);
            Assert.Equal("NO_VOLCANIC_ASH_EXPECTED", noAsh.GetProperty("properties").GetProperty("status").GetString());
            Assert.Equal("2024-09-23T19:00:00.0000000+00:00", noAsh.GetProperty("properties").GetProperty("phenomenonTimeUtc").GetString());
        }

        [Fact]
        public void Metar_TrendTimeIndicators_AreMapped()
        {
            var result = IwxxmParser.ParseFileAs<Metar>(PathOf("metar-A3-1.xml"));

            Assert.True(result.IsSuccess, string.Join(" | ", result.Errors));
            Assert.Equal(TrendTimeIndicator.Until, result.Value.TrendForecasts[0].TimeIndicator);
            Assert.Equal(Utc(2012, 8, 22, 17), result.Value.TrendForecasts[0].ValidUntilUtc);
            Assert.Equal(TrendTimeIndicator.At, result.Value.TrendForecasts[1].TimeIndicator);
            Assert.Equal(Utc(2012, 8, 22, 18), result.Value.TrendForecasts[1].ValidFromUtc);
        }

        [Fact]
        public void Airmet_ObservationCollection_KeepsTimeIndicator()
        {
            var result = IwxxmParser.ParseFileAs<Airmet>(PathOf("airmet-A6-1a-TS.xml"));

            Assert.True(result.IsSuccess, string.Join(" | ", result.Errors));
            AirmetEvolvingCondition condition = Assert.Single(result.Value.EvolvingConditions);
            Assert.Equal(TimeIndicator.Observation, condition.Volume.TimeIndicator);
            Assert.Null(condition.Volume.PhenomenonTimeUtc);
        }
    }
}
