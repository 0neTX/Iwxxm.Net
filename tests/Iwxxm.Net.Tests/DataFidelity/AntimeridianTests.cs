using System.Linq;
using System.Text.Json;
using Iwxxm.Net.Reports;
using Xunit;

namespace Iwxxm.Net.Tests.DataFidelity
{
    /// <summary>
    /// AUD-M2: geometries crossing ±180 are split per RFC 7946 §3.1.9 (MultiLineString / MultiPolygon) and the
    /// bbox has west &gt; east when the content crosses the antimeridian (§5.2). Synthetic Pacific fixtures.
    /// </summary>
    public sealed class AntimeridianTests
    {
        private static readonly System.DateTimeOffset T0 = new System.DateTimeOffset(2026, 1, 1, 0, 0, 0, System.TimeSpan.Zero);

        private static JsonElement Export(IwxxmDocument document, out JsonDocument json)
        {
            json = JsonDocument.Parse(IwxxmGeoJsonExporter.ToGeoJson(document, GeoJsonExportOptions.Default));
            return json.RootElement;
        }

        private static Sigmet SigmetWith(GeoSurface area)
        {
            var result = Sigmet.Create(
                "s1", IwxxmReportType.Sigmet, T0, ReportStatus.Normal, PermissibleUsage.Operational,
                "NZZC", null, null, null, null, "1", T0, T0.AddHours(4),
                evolvingConditions: new[] { new EnRoutePhenomenonVolume("v1", area, null, null) });
            Assert.True(result.IsSuccess, string.Join(" | ", result.Errors));
            return result.Value;
        }

        private static GeoPolygon Polygon(params (double Lat, double Lon)[] vertices) =>
            GeoPolygon.Create(vertices.Select(v => new GeoCoordinate(v.Lat, v.Lon))).Value;

        [Fact]
        public void PolygonStraddlingTheAntimeridian_IsSplitIntoAMultiPolygon_WithWestGreaterThanEast()
        {
            Sigmet sigmet = SigmetWith(Polygon((-10, 170), (-10, -170), (10, -170), (10, 170), (-10, 170)));

            JsonElement root = Export(sigmet, out JsonDocument json);
            using (json)
            {
                JsonElement geometry = root.GetProperty("features")[0].GetProperty("geometry");
                Assert.Equal("MultiPolygon", geometry.GetProperty("type").GetString());
                double[][][][] polygons = geometry.GetProperty("coordinates").EnumerateArray()
                    .Select(p => p.EnumerateArray().Select(CircleGeometryTests.Ring).ToArray()).ToArray();
                Assert.Equal(2, polygons.Length);
                Assert.All(polygons, p => Assert.True(CircleGeometryTests.SignedArea(p[0]) > 0));
                Assert.All(polygons.SelectMany(p => p[0]), position => Assert.InRange(position[0], -180.0, 180.0));
                Assert.Contains(polygons, p => p[0].All(position => position[0] >= 170.0));
                Assert.Contains(polygons, p => p[0].All(position => position[0] <= -170.0));

                double[] bbox = root.GetProperty("bbox").EnumerateArray().Select(e => e.GetDouble()).ToArray();
                Assert.Equal(new[] { 170.0, -10.0, -170.0, 10.0 }, bbox);
            }
        }

        [Fact]
        public void PacificJetStream_IsSplitIntoAMultiLineString()
        {
            var tca = Tca.Create(
                "t1", T0, ReportStatus.Normal, PermissibleUsage.Operational, "RJTD",
                observation: new TropicalCycloneTrackPoint(T0, new GeoCoordinate(20, 175), null),
                forecasts: new[]
                {
                    new TropicalCycloneTrackPoint(T0.AddHours(6), new GeoCoordinate(22, 179), null),
                    new TropicalCycloneTrackPoint(T0.AddHours(12), new GeoCoordinate(24, -177), null)
                }).Value;

            JsonElement root = Export(tca, out JsonDocument json);
            using (json)
            {
                JsonElement track = root.GetProperty("features").EnumerateArray()
                    .Single(f => f.GetProperty("properties").GetProperty("featureType").GetString() == "TC_TRACK")
                    .GetProperty("geometry");
                Assert.Equal("MultiLineString", track.GetProperty("type").GetString());
                double[][][] lines = track.GetProperty("coordinates").EnumerateArray().Select(CircleGeometryTests.Ring).ToArray();
                Assert.Equal(2, lines.Length);
                Assert.Equal(180.0, lines[0].Last()[0]);
                Assert.Equal(-180.0, lines[1].First()[0]);
                Assert.Equal(lines[0].Last()[1], lines[1].First()[1]);
                Assert.Equal(22.5, lines[0].Last()[1], 6);

                double[] bbox = root.GetProperty("bbox").EnumerateArray().Select(e => e.GetDouble()).ToArray();
                Assert.True(bbox[0] > bbox[2], "west must be greater than east when crossing the antimeridian");
                Assert.Equal(175.0, bbox[0]);
                Assert.Equal(-177.0, bbox[2]);
            }
        }

        [Fact]
        public void CircleCrossingTheAntimeridian_IsSplit()
        {
            Sigmet sigmet = SigmetWith(GeoCircle.Create(new GeoCoordinate(0, 179.5), new Length(200, LengthUnit.Kilometers)).Value);

            JsonElement root = Export(sigmet, out JsonDocument json);
            using (json)
            {
                JsonElement geometry = root.GetProperty("features")[0].GetProperty("geometry");
                Assert.Equal("MultiPolygon", geometry.GetProperty("type").GetString());
                double[] bbox = root.GetProperty("bbox").EnumerateArray().Select(e => e.GetDouble()).ToArray();
                Assert.True(bbox[0] > bbox[2]);
            }
        }

        [Fact]
        public void PolygonNotCrossing_KeepsAPlainBoundingBox()
        {
            Sigmet sigmet = SigmetWith(Polygon((-10, 10), (-10, 20), (10, 20), (10, 10), (-10, 10)));

            JsonElement root = Export(sigmet, out JsonDocument json);
            using (json)
            {
                Assert.Equal("Polygon", root.GetProperty("features")[0].GetProperty("geometry").GetProperty("type").GetString());
                Assert.Equal(new[] { 10.0, -10.0, 20.0, 10.0 }, root.GetProperty("bbox").EnumerateArray().Select(e => e.GetDouble()).ToArray());
            }
        }

        [Fact]
        public void PolarRingAroundTheNorthPole_IsWrittenAsAPolarCap()
        {
            var result = IwxxmParser.ParseFileToGeoJson(SampleDocuments.PathOf("spacewx-A7-5.xml"));

            using JsonDocument json = JsonDocument.Parse(result.Value);
            double[][] ring = CircleGeometryTests.Ring(json.RootElement.GetProperty("features")[0].GetProperty("geometry").GetProperty("coordinates")[0]);
            Assert.Contains(ring, p => p[1] == 90.0);
            Assert.Equal(-180.0, ring.Min(p => p[0]));
            Assert.Equal(180.0, ring.Max(p => p[0]));
            Assert.True(CircleGeometryTests.SignedArea(ring) > 0);
        }
    }
}
