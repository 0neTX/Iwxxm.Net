using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Iwxxm.Net.Reports;
using Xunit;

namespace Iwxxm.Net.Tests.DataFidelity
{
    /// <summary>
    /// AUD-R1 / AUD-R4: polygons whose ring crosses ±180 more than twice, rings that wind around a pole with a lobe across
    /// the antimeridian, and the bbox rule for features spread over the whole longitude range. The invariants checked are
    /// the ones that matter to a consumer: every source vertex is kept, the pieces are closed, counterclockwise and inside
    /// [-180, 180], and their total area equals the area of the source region.
    /// </summary>
    public sealed class AntimeridianPolygonTests
    {
        private static readonly DateTimeOffset T0 = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        private static JsonDocument Export(params GeoSurface[] areas)
        {
            var volumes = areas.Select((a, i) => new EnRoutePhenomenonVolume("v" + (i + 1), a, null, null)).ToList();
            var result = Sigmet.Create(
                "s1", IwxxmReportType.Sigmet, T0, ReportStatus.Normal, PermissibleUsage.Operational,
                "NZZC", null, null, null, null, "1", T0, T0.AddHours(4), evolvingConditions: volumes);
            Assert.True(result.IsSuccess, string.Join(" | ", result.Errors));
            return JsonDocument.Parse(IwxxmGeoJsonExporter.ToGeoJson(result.Value, GeoJsonExportOptions.Default).Value);
        }

        // Vertices are given as (latitude, unwrapped longitude): longitudes beyond ±180 are wrapped back, as a source would write them.
        private static GeoPolygon Polygon(params (double Lat, double X)[] vertices) =>
            GeoPolygon.Create(vertices.Select(v => new GeoCoordinate(v.Lat, Wrap(v.X)))).Value;

        private static double Wrap(double x)
        {
            double lon = ((x + 180.0) % 360.0 + 360.0) % 360.0 - 180.0;
            return lon == -180.0 && x > 0 ? 180.0 : lon;
        }

        private static List<List<double[][]>> Polygons(JsonElement geometry)
        {
            string type = geometry.GetProperty("type").GetString()!;
            JsonElement coordinates = geometry.GetProperty("coordinates");
            IEnumerable<JsonElement> polygons = type == "Polygon" ? new[] { coordinates } : coordinates.EnumerateArray();
            return polygons.Select(p => p.EnumerateArray().Select(CircleGeometryTests.Ring).ToList()).ToList();
        }

        private static double TotalArea(List<List<double[][]>> polygons) =>
            polygons.Sum(p => p.Select((ring, i) => Math.Abs(CircleGeometryTests.SignedArea(ring)) * (i == 0 ? 1 : -1)).Sum());

        private static HashSet<(double, double)> Positions(List<List<double[][]>> polygons) =>
            new HashSet<(double, double)>(polygons.SelectMany(p => p).SelectMany(r => r).Select(c => (Math.Round(c[0], 6), Math.Round(c[1], 6))));

        private static void AssertValidPieces(List<List<double[][]>> polygons)
        {
            foreach (var polygon in polygons)
            {
                double[][] exterior = polygon[0];
                Assert.Equal(exterior[0], exterior[exterior.Length - 1]);
                Assert.True(exterior.Length >= 4);
                Assert.True(CircleGeometryTests.SignedArea(exterior) > 0, "exterior must be counterclockwise");
                Assert.All(exterior, c => Assert.InRange(c[0], -180.0, 180.0));
                Assert.Equal(exterior.Length - 1, exterior.Take(exterior.Length - 1).Select(c => (c[0], c[1])).Distinct().Count()); // no repeated vertex (no bridge)
            }
        }

        [Fact]
        public void PolygonWithoutCrossing_IsWrittenAsOneUnchangedPolygon()
        {
            using (JsonDocument json = Export(Polygon((0, 10), (0, 20), (10, 20), (10, 10), (0, 10))))
            {
                JsonElement geometry = json.RootElement.GetProperty("features")[0].GetProperty("geometry");
                Assert.Equal("Polygon", geometry.GetProperty("type").GetString());
                Assert.Equal(1, geometry.GetProperty("coordinates").GetArrayLength());
                Assert.Equal(100.0, TotalArea(Polygons(geometry)), 6);
            }
        }

        [Fact]
        public void SimpleCrossing_IsSplitIntoTwoPieces_OfTheSameTotalArea()
        {
            using (JsonDocument json = Export(Polygon((0, 170), (0, 190), (10, 190), (10, 170), (0, 170))))
            {
                var polygons = Polygons(json.RootElement.GetProperty("features")[0].GetProperty("geometry"));

                Assert.Equal(2, polygons.Count);
                AssertValidPieces(polygons);
                Assert.Equal(200.0, TotalArea(polygons), 6);
            }
        }

        [Fact]
        public void RingThatCrossesTheAntimeridianFourTimes_GivesSeparatePieces_NotOnePolygonWithAZeroWidthBridge()
        {
            // A comb: a spine west of 180 with two teeth reaching east across it (lobes of -170).
            (double, double)[] outline = { (0, 170), (0, 190), (5, 190), (5, 175), (25, 175), (25, 190), (30, 190), (30, 170), (0, 170) };
            using (JsonDocument json = Export(Polygon(outline)))
            {
                var polygons = Polygons(json.RootElement.GetProperty("features")[0].GetProperty("geometry"));

                Assert.Equal(3, polygons.Count); // west comb stub + one tooth tip on each side of the gap
                AssertValidPieces(polygons);
                Assert.Equal(300.0, TotalArea(polygons), 6);
                var east = polygons.Where(p => p[0].All(c => c[0] <= -170.0 + 1e-9 && c[0] <= 0)).ToList();
                Assert.Equal(2, east.Count);
                Assert.Contains(east, p => p[0].Max(c => c[1]) <= 5.0);
                Assert.Contains(east, p => p[0].Min(c => c[1]) >= 25.0);

                var positions = Positions(polygons);
                foreach (var v in new (double Lon, double Lat)[] { (170, 0), (-170, 0), (-170, 5), (175, 5), (175, 25), (-170, 25), (-170, 30), (170, 30) })
                    Assert.Contains((v.Lon, v.Lat), positions);
            }
        }

        // South polar ring, unwrapped longitudes 0..360, with a tongue that crosses 180 and comes back before the ring goes on.
        private static readonly (double Lat, double X)[] TongueRing =
        {
            (-70, 0), (-70, 90), (-70, 170), (-75, 200), (-80, 170), (-80, 230), (-70, 270), (-70, 360),
        };

        private static double ExpectedCapArea(IReadOnlyList<(double Lat, double X)> ring, double poleLat)
        {
            // Region between the unwrapped ring and the pole: the ring closed through the pole over one turn.
            var points = ring.Select(p => (X: p.X, Y: p.Lat)).ToList();
            points.Add((points[points.Count - 1].X, poleLat));
            points.Add((points[0].X, poleLat));
            double area = 0;
            for (int i = 0, j = points.Count - 1; i < points.Count; j = i++)
                area += (points[j].X * points[i].Y) - (points[i].X * points[j].Y);
            return Math.Abs(area) / 2.0;
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void SouthPolarRingWithALobeAcrossTheAntimeridian_KeepsEveryVertex_AndTheWholeRegion(bool reversed)
        {
            var ring = TongueRing.ToList();
            if (reversed)
                ring.Reverse();

            using (JsonDocument json = Export(Polygon(ring.ToArray())))
            {
                var polygons = Polygons(json.RootElement.GetProperty("features")[0].GetProperty("geometry"));

                AssertValidPieces(polygons);
                var positions = Positions(polygons);
                foreach ((double lat, double x) in TongueRing)
                    Assert.Contains((Math.Round(Wrap(x), 6), lat), positions);
                Assert.Contains(polygons.SelectMany(p => p[0]), c => c[1] == -90.0);
                Assert.Equal(ExpectedCapArea(TongueRing, -90), TotalArea(polygons), 3);
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void RingAlongAPoleOutAcrossTheAntimeridianAndBack_DropsTheSpurOfNoArea_AndKeepsTheRegion(bool reversed)
        {
            // On the sphere the south pole is one point, so the part of the ring along it beyond the antimeridian (the vertex 179, out
            // and back between -179 and -177) is a spur of zero width: no polygon can hold it. It is replaced by the cut at -180.
            var ring = new (double Lat, double X)[] { (-88, -178), (-90, -179), (-90, 179), (-90, -177) }.ToList();
            if (reversed)
                ring.Reverse();

            using (JsonDocument json = Export(Polygon(ring.ToArray())))
            {
                var polygons = Polygons(json.RootElement.GetProperty("features")[0].GetProperty("geometry"));

                Assert.Single(polygons);
                Assert.Equal(2.0, TotalArea(polygons), 6); // the triangle (-178, -88), (-179, -90), (-177, -90)
                var positions = Positions(polygons);
                foreach ((double lon, double lat) in new[] { (-178.0, -88.0), (-179.0, -90.0), (-177.0, -90.0), (-180.0, -90.0) })
                    Assert.Contains((lon, lat), positions);

                Assert.DoesNotContain((179.0, -90.0), positions);
                Assert.All(polygons.SelectMany(p => p).SelectMany(r => r), c => Assert.InRange(c[0], -180.0, 180.0));
            }
        }

        [Fact]
        public void LobeTwoMetersAcrossTheAntimeridian_IsAPieceOfItsOwn_NotDropped()
        {
            // The tip (179.99998, -88.2) is 2e-5 degrees across the antimeridian: the lobe has an area of 4e-10 square degrees.
            var ring = new (double Lat, double X)[] { (-88.9, -178.4), (-88.2, -180.00002), (-85.8, -178.4), (-88.3, -177.0) };
            using (JsonDocument json = Export(Polygon(ring)))
            {
                var polygons = Polygons(json.RootElement.GetProperty("features")[0].GetProperty("geometry"));

                Assert.Equal(2, polygons.Count);
                AssertValidPieces(polygons);
                Assert.Contains((179.99998, -88.2), Positions(polygons));
                double source = Math.Abs(Enumerable.Range(0, ring.Length).Sum(i => (ring[i].X * ring[(i + 1) % ring.Length].Lat) - (ring[(i + 1) % ring.Length].X * ring[i].Lat))) / 2.0;
                Assert.Equal(source, TotalArea(polygons), 6);
            }
        }

        [Fact]
        public void SouthPolarRingWithoutALobe_IsASingleCap_OfTheExpectedArea()
        {
            var ring = new (double Lat, double X)[] { (-60, 0), (-60, 90), (-60, 180), (-60, 270), (-60, 360) };
            using (JsonDocument json = Export(Polygon(ring)))
            {
                var polygons = Polygons(json.RootElement.GetProperty("features")[0].GetProperty("geometry"));

                Assert.Single(polygons);
                AssertValidPieces(polygons);
                Assert.Equal(360.0 * 30.0, TotalArea(polygons), 6);
            }
        }

        [Fact]
        public void RingAroundThePole_WithLobesBothWays_ProducesPiecesThatDoNotOverlap()
        {
            // Lobes cross 180 eastward and 0 in a ring starting on the other side: the vertices still all appear once.
            var ring = new (double Lat, double X)[]
            {
                (70, 0), (70, 100), (70, 190), (78, 175), (78, 220), (70, 250), (70, 360),
            };
            using (JsonDocument json = Export(Polygon(ring)))
            {
                var polygons = Polygons(json.RootElement.GetProperty("features")[0].GetProperty("geometry"));

                AssertValidPieces(polygons);
                var positions = Positions(polygons);
                foreach ((double lat, double x) in ring)
                    Assert.Contains((Math.Round(Wrap(x), 6), lat), positions);
                Assert.Equal(ExpectedCapArea(ring, 90), TotalArea(polygons), 3);
            }
        }

        [Fact]
        public void BoundingBox_OfAFeatureSpreadOverEveryLongitude_IsTheFullRange()
        {
            // A north polar cap reaches the pole and every longitude: [-180, 60, 180, 90], not a wrapping box.
            var cap = Polygon((60, 0), (60, 90), (60, 180), (60, 270), (60, 360));
            using (JsonDocument json = Export(cap))
            {
                double[] bbox = json.RootElement.GetProperty("bbox").EnumerateArray().Select(e => e.GetDouble()).ToArray();
                Assert.Equal(new[] { -180.0, 60.0, 180.0, 90.0 }, bbox);
            }
        }

        [Fact]
        public void BoundingBox_WhenTheNarrowestArcIsWiderThanHalfTheGlobe_IsThePlainExtent()
        {
            // One polygon crosses the antimeridian, another lies around 0: the smallest arc (170 to 0 through 180) is 190
            // degrees wide, so the plain extent is written (west <= east) rather than a box that wraps over most of the globe.
            using (JsonDocument json = Export(
                Polygon((0, 170), (0, 190), (10, 190), (10, 170), (0, 170)),
                Polygon((-10, -10), (-10, 0), (0, 0), (0, -10), (-10, -10))))
            {
                double[] bbox = json.RootElement.GetProperty("bbox").EnumerateArray().Select(e => e.GetDouble()).ToArray();
                Assert.Equal(new[] { -180.0, -10.0, 180.0, 10.0 }, bbox);
            }
        }

        [Fact]
        public void BoundingBox_WhenTheNarrowestArcIsNarrow_StaysAWrappingBox()
        {
            using (JsonDocument json = Export(Polygon((0, 170), (0, 190), (10, 190), (10, 170), (0, 170))))
            {
                double[] bbox = json.RootElement.GetProperty("bbox").EnumerateArray().Select(e => e.GetDouble()).ToArray();
                Assert.Equal(new[] { 170.0, 0.0, -170.0, 10.0 }, bbox);
            }
        }

        [Fact]
        public void BoundingBox_ReportsAPoleOnlyWhenAPositionReachesIt()
        {
            using (JsonDocument json = Export(Polygon((-80, 170), (-80, 190), (-70, 190), (-70, 170), (-80, 170))))
            {
                double[] bbox = json.RootElement.GetProperty("bbox").EnumerateArray().Select(e => e.GetDouble()).ToArray();
                Assert.Equal(-80.0, bbox[1]);
                Assert.Equal(-70.0, bbox[3]);
            }
        }
    }
}
