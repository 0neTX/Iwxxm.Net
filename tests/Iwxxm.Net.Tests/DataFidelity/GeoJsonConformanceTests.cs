using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Iwxxm.Net.Reports;
using Xunit;
using static Iwxxm.Net.Tests.DataFidelity.SampleDocuments;

namespace Iwxxm.Net.Tests.DataFidelity
{
    /// <summary>
    /// RFC 7946 output conformance: right-hand rule (AUD-M1), applied in the writer only.
    /// </summary>
    public sealed class GeoJsonConformanceTests
    {
        public static IEnumerable<object[]> AllSamples() =>
            Directory.GetFiles(Path.Combine("Samples", "wmo-examples"), "*.xml")
                .Where(f => !f.EndsWith("WAFS-Example.xml"))
                .OrderBy(f => f)
                .Select(f => new object[] { Path.GetFileName(f) });

        internal static IEnumerable<double[][][]> Polygons(JsonElement root)
        {
            foreach (JsonElement feature in root.GetProperty("features").EnumerateArray())
            {
                JsonElement geometry = feature.GetProperty("geometry");
                if (geometry.ValueKind == JsonValueKind.Null) continue;
                string type = geometry.GetProperty("type").GetString()!;
                JsonElement coordinates = geometry.GetProperty("coordinates");
                if (type == "Polygon")
                    yield return Rings(coordinates);
                else if (type == "MultiPolygon")
                    foreach (JsonElement polygon in coordinates.EnumerateArray())
                        yield return Rings(polygon);
            }
        }

        private static double[][][] Rings(JsonElement polygon) =>
            polygon.EnumerateArray().Select(CircleGeometryTests.Ring).ToArray();

        [Theory]
        [MemberData(nameof(AllSamples))]
        public void Polygons_FollowTheRightHandRule(string sample)
        {
            var result = IwxxmParser.ParseFileToGeoJson(PathOf(sample));
            Assert.True(result.IsSuccess, string.Join(" | ", result.Errors));

            using JsonDocument json = JsonDocument.Parse(result.Value);
            foreach (double[][][] polygon in Polygons(json.RootElement))
            {
                Assert.True(CircleGeometryTests.SignedArea(polygon[0]) > 0, $"{sample}: exterior ring must be counterclockwise.");
                Assert.All(polygon.Skip(1), hole => Assert.True(CircleGeometryTests.SignedArea(hole) < 0, $"{sample}: holes must be clockwise."));
            }
        }

        [Fact]
        public void SigmetVaEggx_ClockwiseSourceRing_IsReversedInGeoJsonOnly()
        {
            var report = IwxxmParser.ParseFileAs<Sigmet>(PathOf("sigmet-VA-EGGX.xml"));
            Assert.True(report.IsSuccess, string.Join(" | ", report.Errors));
            GeoPolygon forecast = Assert.IsType<GeoPolygon>(report.Value.ForecastPositions[0].Area);
            Assert.Equal(new GeoCoordinate(58.0, -14.0), forecast.ExteriorRing[1]);

            var result = IwxxmParser.ParseFileToGeoJson(PathOf("sigmet-VA-EGGX.xml"));
            using JsonDocument json = JsonDocument.Parse(result.Value);
            JsonElement feature = json.RootElement.GetProperty("features").EnumerateArray()
                .Single(f => f.GetProperty("properties").GetProperty("featureType").GetString() == "SIGMET_FORECAST_VOLUME");
            double[][] ring = CircleGeometryTests.Ring(feature.GetProperty("geometry").GetProperty("coordinates")[0]);
            Assert.Equal(new[] { -12.0, 60.0 }, ring[0]);
            Assert.Equal(new[] { -15.58, 60.0 }, ring[1]);
            Assert.True(CircleGeometryTests.SignedArea(ring) > 0);
        }

        [Fact]
        public void Qvaci_Hole_IsClockwise()
        {
            var result = IwxxmParser.ParseFileToGeoJson(PathOf("qvaci-Example.xml"));
            using JsonDocument json = JsonDocument.Parse(result.Value);

            double[][][] withHole = Polygons(json.RootElement).Single(p => p.Length > 1);
            Assert.True(CircleGeometryTests.SignedArea(withHole[0]) > 0);
            Assert.True(CircleGeometryTests.SignedArea(withHole[1]) < 0);
        }
    }
}
