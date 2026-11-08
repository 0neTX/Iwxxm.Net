using System;
using System.Linq;
using System.Text.Json;
using Iwxxm.Net.Reports;
using Xunit;

namespace Iwxxm.Net.Tests.DataFidelity
{
    /// <summary>
    /// AUD-R3: a tropopause contour is one polygon that carries its level; spot heights exist only for genuine point
    /// spot heights, never one per ring vertex.
    /// </summary>
    public sealed class TropopauseContourTests
    {
        private static readonly DateTimeOffset T0 = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        private static readonly VerticalLimit Fl350 = VerticalLimit.Create(350, VerticalUnit.FlightLevel, VerticalReference.Std).Value;

        private static GeoPolygon Polygon() =>
            GeoPolygon.Create(new[] { new GeoCoordinate(0, 0), new GeoCoordinate(0, 10), new GeoCoordinate(10, 10), new GeoCoordinate(0, 0) }).Value;

        private static Sigwx SigwxWith(Tropopause tropopause) =>
            Sigwx.Create(
                "w1", "London", ReportStatus.Normal, PermissibleUsage.Operational, T0, T0, T0,
                VerticalLimit.Create(100, VerticalUnit.FlightLevel, VerticalReference.Std).Value,
                VerticalLimit.Create(600, VerticalUnit.FlightLevel, VerticalReference.Std).Value,
                tropopauses: new[] { tropopause }).Value;

        [Fact]
        public void Contour_CarriesItsLevel_AndHasNoSpotHeights()
        {
            Tropopause tropopause = Tropopause.Create("t1", null, Array.Empty<TropopauseSpotHeight>(), Polygon(), Fl350).Value;

            Assert.Empty(tropopause.SpotHeights);
            Assert.Equal(Fl350, tropopause.ContourLevel);
            Assert.NotNull(tropopause.ContourPolygon);
        }

        [Fact]
        public void Contour_WithoutALevel_IsRejected()
        {
            var result = Tropopause.Create("t1", null, Array.Empty<TropopauseSpotHeight>(), Polygon());

            Assert.True(result.IsFailure);
            Assert.Equal("DOMAIN-TROPOPAUSE-CONTOUR-LEVEL", Assert.Single(result.Errors).Code);
        }

        [Fact]
        public void NeitherContourNorSpotHeight_IsRejected()
        {
            var result = Tropopause.Create("t1", null, Array.Empty<TropopauseSpotHeight>());

            Assert.Equal("DOMAIN-TROPOPAUSE-EMPTY", Assert.Single(result.Errors).Code);
        }

        [Fact]
        public void ContourLevel_IsPartOfTheValueEquality()
        {
            Tropopause a = Tropopause.Create("t1", null, Array.Empty<TropopauseSpotHeight>(), Polygon(), Fl350).Value;
            Tropopause b = Tropopause.Create("t1", null, Array.Empty<TropopauseSpotHeight>(), Polygon(), Fl350).Value;
            Tropopause c = Tropopause.Create("t1", null, Array.Empty<TropopauseSpotHeight>(), Polygon(), VerticalLimit.Create(400, VerticalUnit.FlightLevel, VerticalReference.Std).Value).Value;

            Assert.Equal(a, b);
            Assert.NotEqual(a, c);
        }

        [Fact]
        public void GeoJson_OfAContour_IsOnePolygonWithTheLevel_AndNoSpotHeightPoints()
        {
            Sigwx sigwx = SigwxWith(Tropopause.Create("t1", null, Array.Empty<TropopauseSpotHeight>(), Polygon(), Fl350).Value);

            using (JsonDocument json = JsonDocument.Parse(IwxxmGeoJsonExporter.ToGeoJson(sigwx, GeoJsonExportOptions.Default).Value))
            {
                JsonElement feature = Assert.Single(json.RootElement.GetProperty("features").EnumerateArray());
                Assert.Equal("t1", feature.GetProperty("id").GetString());
                Assert.Equal("Polygon", feature.GetProperty("geometry").GetProperty("type").GetString());
                JsonElement properties = feature.GetProperty("properties");
                Assert.Equal("TROPOPAUSE_CONTOUR", properties.GetProperty("featureType").GetString());
                Assert.Equal(350, properties.GetProperty("level").GetDouble());
                Assert.Equal("STD", properties.GetProperty("levelReference").GetString());
                Assert.Equal("FL350", properties.GetProperty("levelFlightLevel").GetString());
            }
        }

        [Fact]
        public void GeoJson_OfPointSpotHeights_KeepsOnePointPerSpotHeight()
        {
            var spots = new[]
            {
                new TropopauseSpotHeight(new GeoCoordinate(1, 2), Fl350),
                new TropopauseSpotHeight(new GeoCoordinate(3, 4), VerticalLimit.Create(380, VerticalUnit.FlightLevel, VerticalReference.Std).Value),
            };
            Sigwx sigwx = SigwxWith(Tropopause.Create("t2", "SPOT", spots).Value);

            using (JsonDocument json = JsonDocument.Parse(IwxxmGeoJsonExporter.ToGeoJson(sigwx, GeoJsonExportOptions.Default).Value))
            {
                var features = json.RootElement.GetProperty("features").EnumerateArray().ToList();
                Assert.Equal(2, features.Count);
                Assert.All(features, f => Assert.Equal("TROPOPAUSE_SPOT_HEIGHT", f.GetProperty("properties").GetProperty("featureType").GetString()));
                Assert.Equal(new[] { "t2#spot-1", "t2#spot-2" }, features.Select(f => f.GetProperty("id").GetString()).ToArray());
            }
        }

        [Fact]
        public void WafsExample_MapsTheContourWithOneLevel_AndExportsNoSpotHeightsForIt()
        {
            Result<Sigwx> result = IwxxmSigwxParser.ParseFile(SampleDocuments.PathOf("WAFS-Example.xml"));

            Assert.True(result.IsSuccess, string.Join(" | ", result.Errors));
            Tropopause tropopause = Assert.Single(result.Value.Tropopauses);
            Assert.Empty(tropopause.SpotHeights);
            Assert.Equal(VerticalLimit.Create(14020, VerticalUnit.Meters, VerticalReference.Msl).Value, tropopause.ContourLevel);

            using (JsonDocument json = JsonDocument.Parse(IwxxmGeoJsonExporter.ToGeoJson(result.Value, GeoJsonExportOptions.Default).Value))
            {
                var types = json.RootElement.GetProperty("features").EnumerateArray()
                    .Select(f => f.GetProperty("properties").GetProperty("featureType").GetString()).ToList();
                Assert.Equal(1, types.Count(t => t == "TROPOPAUSE_CONTOUR"));
                Assert.DoesNotContain("TROPOPAUSE_SPOT_HEIGHT", types);
            }
        }
    }
}
