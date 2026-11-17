using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Iwxxm.Net.Infrastructure.GeoJson.Writing;
using Iwxxm.Net.Reports;
using Xunit;

namespace Iwxxm.Net.Tests.DataFidelity
{
    /// <summary>
    /// Interior rings of polygons that cross the antimeridian (not polar caps), and lines with a vertex exactly on ±180.
    /// The assertions describe the behaviour a consumer needs: the exported area equals the exterior minus the holes, no hole
    /// shares a segment with the exterior of its polygon (a hole that touches its shell along an edge is invalid for GEOS and
    /// PostGIS: the correct output is a notch in the exterior), and a hole that cannot be attached to a piece is counted in
    /// <c>droppedInteriorRings</c> (docs/REFERENCE.md), never lost silently. They pin the antimeridian hole and line fixes.
    /// </summary>
    public sealed class AntimeridianHoleAndLineTests
    {
        private static readonly DateTimeOffset T0 = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        private const double Tolerance = 1e-9;

        // Vertices are (latitude, unwrapped longitude); longitudes beyond ±180 are wrapped back, as a GML source would write them.
        private static double Wrap(double x)
        {
            double lon = ((x + 180.0) % 360.0 + 360.0) % 360.0 - 180.0;
            return lon == -180.0 && x > 0 ? 180.0 : lon;
        }

        private static IEnumerable<GeoCoordinate> Coordinates((double Lat, double X)[] ring) =>
            ring.Select(v => new GeoCoordinate(v.Lat, Wrap(v.X)));

        private static GeoPolygon Polygon((double Lat, double X)[] exterior, params (double Lat, double X)[][] holes)
        {
            var result = GeoPolygon.Create(Coordinates(exterior), holes.Select(Coordinates));
            Assert.True(result.IsSuccess, string.Join(" | ", result.Errors));
            return result.Value;
        }

        private static JsonDocument Export(IwxxmDocument document) =>
            JsonDocument.Parse(IwxxmGeoJsonExporter.ToGeoJson(document, GeoJsonExportOptions.Default).Value);

        private static JsonDocument ExportArea(GeoPolygon area)
        {
            var volumes = new List<EnRoutePhenomenonVolume> { new EnRoutePhenomenonVolume("v1", area, null, null) };
            var result = Sigmet.Create(
                "s1", IwxxmReportType.Sigmet, T0, ReportStatus.Normal, PermissibleUsage.Operational,
                "NZZC", null, null, null, null, "1", T0, T0.AddHours(4), evolvingConditions: volumes);
            Assert.True(result.IsSuccess, string.Join(" | ", result.Errors));
            return Export(result.Value);
        }

        private static List<List<double[][]>> Polygons(JsonElement geometry)
        {
            string type = geometry.GetProperty("type").GetString()!;
            JsonElement coordinates = geometry.GetProperty("coordinates");
            IEnumerable<JsonElement> polygons = type == "Polygon" ? new[] { coordinates } : coordinates.EnumerateArray();
            return polygons.Select(p => p.EnumerateArray().Select(CircleGeometryTests.Ring).ToList()).ToList();
        }

        private static double PlanarArea(List<List<double[][]>> polygons) =>
            polygons.Sum(p => p.Select((ring, i) => Math.Abs(CircleGeometryTests.SignedArea(ring)) * (i == 0 ? 1 : -1)).Sum());

        private static double SourceArea((double Lat, double X)[] ring) =>
            Math.Abs(CircleGeometryTests.SignedArea(ring.Select(v => new[] { v.X, v.Lat }).ToArray()));

        private static double ExpectedArea((double Lat, double X)[] exterior, params (double Lat, double X)[][] holes) =>
            SourceArea(exterior) - holes.Sum(SourceArea);

        private static int Dropped(JsonDocument json) =>
            json.RootElement.GetProperty("features")[0].GetProperty("properties").TryGetProperty("droppedInteriorRings", out JsonElement n) ? n.GetInt32() : 0;

        private static int HoleCount(List<List<double[][]>> polygons) => polygons.Sum(p => p.Count - 1);

        /// <summary>Length of the common part of two segments when they are collinear (0 when they only cross or touch at a point).</summary>
        private static double SharedLength(double[] a1, double[] a2, double[] b1, double[] b2)
        {
            double dx = a2[0] - a1[0];
            double dy = a2[1] - a1[1];
            double length = Math.Sqrt((dx * dx) + (dy * dy));
            if (length < Tolerance)
                return 0.0;

            double Cross(double[] p) => ((p[0] - a1[0]) * dy - (p[1] - a1[1]) * dx) / length;
            if (Math.Abs(Cross(b1)) > Tolerance || Math.Abs(Cross(b2)) > Tolerance)
                return 0.0;

            double Along(double[] p) => (((p[0] - a1[0]) * dx) + ((p[1] - a1[1]) * dy)) / length;
            double low = Math.Max(0.0, Math.Min(Along(b1), Along(b2)));
            double high = Math.Min(length, Math.Max(Along(b1), Along(b2)));
            return Math.Max(0.0, high - low);
        }

        private static List<string> HolesSharingAnEdgeWithTheirShell(List<List<double[][]>> polygons)
        {
            var shared = new List<string>();
            for (int p = 0; p < polygons.Count; p++)
            {
                double[][] shell = polygons[p][0];
                for (int h = 1; h < polygons[p].Count; h++)
                {
                    double[][] hole = polygons[p][h];
                    for (int i = 1; i < hole.Length; i++)
                    {
                        for (int j = 1; j < shell.Length; j++)
                        {
                            double length = SharedLength(shell[j - 1], shell[j], hole[i - 1], hole[i]);
                            if (length > Tolerance)
                                shared.Add($"polygon {p}, hole {h}: segment {i} shares {length:0.######} with shell segment {j}");
                        }
                    }
                }
            }

            return shared;
        }

        // ---------------------------------------------------------------- a hole that crosses the antimeridian (TD-GEO-1)

        private static readonly (double Lat, double X)[] Box = { (0, 170), (0, 190), (10, 190), (10, 170), (0, 170) };
        private static readonly (double Lat, double X)[] BoxHole = { (4, 175), (4, 185), (6, 185), (6, 175), (4, 175) };

        [Fact]
        public void HoleCrossingTheAntimeridian_KeepsTheTotalArea_OfExteriorMinusHole()
        {
            using (JsonDocument json = ExportArea(Polygon(Box, BoxHole)))
            {
                var polygons = Polygons(json.RootElement.GetProperty("features")[0].GetProperty("geometry"));

                Assert.Equal(ExpectedArea(Box, BoxHole), PlanarArea(polygons), 6);
                Assert.Equal(0, Dropped(json));
            }
        }

        [Fact]
        public void HoleCrossingTheAntimeridian_DoesNotShareASegmentWithTheExterior()
        {
            using (JsonDocument json = ExportArea(Polygon(Box, BoxHole)))
            {
                var polygons = Polygons(json.RootElement.GetProperty("features")[0].GetProperty("geometry"));

                Assert.Equal(2, polygons.Count);
                Assert.Empty(HolesSharingAnEdgeWithTheirShell(polygons));
            }
        }

        private static List<double[][]> Rings(List<List<double[][]>> polygons) => polygons.SelectMany(p => p).ToList();

        private static string Describe(double[][] ring) => string.Join(" ", ring.Select(c => $"({c[0]},{c[1]})"));

        /// <summary>Asserts that every ring is closed with at least 4 positions, that shells are counterclockwise and holes clockwise.</summary>
        private static void AssertValidRings(List<List<double[][]>> polygons)
        {
            foreach (List<double[][]> polygon in polygons)
            {
                for (int i = 0; i < polygon.Count; i++)
                {
                    double[][] ring = polygon[i];
                    Assert.True(ring.Length >= 4, "ring with fewer than 4 positions: " + Describe(ring));
                    Assert.Equal(ring[0], ring[ring.Length - 1]);
                    Assert.All(ring, c => Assert.InRange(c[0], -180.0, 180.0));
                    double area = CircleGeometryTests.SignedArea(ring);
                    Assert.True(i == 0 ? area > 0 : area < 0, $"wrong orientation of ring {i}: {Describe(ring)}");
                }
            }
        }

        private static List<List<double[][]>> Export(GeoPolygon polygon, out JsonDocument json)
        {
            json = ExportArea(polygon);
            return Polygons(json.RootElement.GetProperty("features")[0].GetProperty("geometry"));
        }

        [Fact]
        public void HoleCrossingTheAntimeridian_BecomesANotchOfEachPiece()
        {
            using (JsonDocument json = ExportArea(Polygon(Box, BoxHole)))
            {
                var polygons = Polygons(json.RootElement.GetProperty("features")[0].GetProperty("geometry"));

                Assert.Equal(2, polygons.Count);
                Assert.Equal(0, HoleCount(polygons));
                AssertValidRings(polygons);
                double[][] west = polygons.Select(p => p[0]).Single(r => r.All(c => c[0] > 0));
                double[][] east = polygons.Select(p => p[0]).Single(r => r.All(c => c[0] < 0));
                Assert.Equal(8, west.Length - 1);
                Assert.Equal(8, east.Length - 1);
                Assert.Contains(west, c => c[0] == 175.0 && c[1] == 4.0);
                Assert.Contains(west, c => c[0] == 175.0 && c[1] == 6.0);
                Assert.Contains(east, c => c[0] == -175.0 && c[1] == 4.0);
                Assert.Contains(east, c => c[0] == -175.0 && c[1] == 6.0);
                Assert.Equal(90.0, Math.Abs(CircleGeometryTests.SignedArea(west)), 6);
                Assert.Equal(90.0, Math.Abs(CircleGeometryTests.SignedArea(east)), 6);
            }
        }

        [Theory]
        [MemberData(nameof(HoleStarts))]
        public void HoleCrossingTheAntimeridian_GivesTheSameNotches_WhicheverVertexItStartsAt(int rotation, bool reversed)
        {
            var corners = new List<(double Lat, double X)> { (4, 175), (4, 185), (6, 185), (6, 175) };
            if (reversed)
                corners.Reverse();

            var ring = corners.Skip(rotation).Concat(corners.Take(rotation)).ToList();
            ring.Add(ring[0]);
            using (JsonDocument json = ExportArea(Polygon(Box, ring.ToArray())))
            {
                var polygons = Polygons(json.RootElement.GetProperty("features")[0].GetProperty("geometry"));

                Assert.Equal(2, polygons.Count);
                Assert.Equal(0, HoleCount(polygons));
                Assert.Equal(180.0, PlanarArea(polygons), 6);
                Assert.Equal(0, Dropped(json));
                AssertValidRings(polygons);
            }
        }

        [Fact]
        public void HoleCrossingTheAntimeridian_InAShellWrittenClockwise_IsStillANotch()
        {
            (double Lat, double X)[] clockwiseBox = Enumerable.Reverse(Box).ToArray();
            using (JsonDocument json = ExportArea(Polygon(clockwiseBox, BoxHole)))
            {
                var polygons = Polygons(json.RootElement.GetProperty("features")[0].GetProperty("geometry"));

                Assert.Equal(2, polygons.Count);
                Assert.Equal(0, HoleCount(polygons));
                Assert.Equal(180.0, PlanarArea(polygons), 6);
                AssertValidRings(polygons);
            }
        }

        [Fact]
        public void HoleTouchingTheAntimeridianAlongAnEdge_FromTheEastSide_IsANotchOfThatPiece()
        {
            (double Lat, double X)[] h = { (4, 180), (4, 183), (6, 183), (6, 180), (4, 180) };
            using (JsonDocument json = ExportArea(Polygon(Box, h)))
            {
                var polygons = Polygons(json.RootElement.GetProperty("features")[0].GetProperty("geometry"));

                Assert.Equal(2, polygons.Count);
                Assert.Equal(0, HoleCount(polygons));
                Assert.Equal(ExpectedArea(Box, h), PlanarArea(polygons), 6);
                Assert.Equal(0, Dropped(json));
                AssertValidRings(polygons);
                Assert.Empty(HolesSharingAnEdgeWithTheirShell(polygons));
                double[][] east = polygons.Select(p => p[0]).Single(r => r.All(c => c[0] < 0));
                Assert.Equal(8, east.Length - 1);
            }
        }

        [Fact]
        public void HoleTouchingTheAntimeridianAlongAnEdge_FromTheWestSide_IsANotchOfThatPiece()
        {
            (double Lat, double X)[] h = { (4, 177), (4, 180), (6, 180), (6, 177), (4, 177) };
            using (JsonDocument json = ExportArea(Polygon(Box, h)))
            {
                var polygons = Polygons(json.RootElement.GetProperty("features")[0].GetProperty("geometry"));

                Assert.Equal(2, polygons.Count);
                Assert.Equal(0, HoleCount(polygons));
                Assert.Equal(ExpectedArea(Box, h), PlanarArea(polygons), 6);
                Assert.Equal(0, Dropped(json));
                AssertValidRings(polygons);
                Assert.Empty(HolesSharingAnEdgeWithTheirShell(polygons));
            }
        }

        [Fact]
        public void TwoHoles_OneCrossingAndOneInside_ArePartlyNotchAndPartlyHole()
        {
            (double Lat, double X)[] inside = { (1, 182), (1, 184), (2, 184), (2, 182), (1, 182) };
            using (JsonDocument json = ExportArea(Polygon(Box, BoxHole, inside)))
            {
                var polygons = Polygons(json.RootElement.GetProperty("features")[0].GetProperty("geometry"));

                Assert.Equal(2, polygons.Count);
                Assert.Equal(1, HoleCount(polygons));
                Assert.Equal(ExpectedArea(Box, BoxHole, inside), PlanarArea(polygons), 6);
                Assert.Equal(0, Dropped(json));
                AssertValidRings(polygons);
                Assert.Empty(HolesSharingAnEdgeWithTheirShell(polygons));
            }
        }

        [Fact]
        public void TwoHolesCrossingTheAntimeridian_GiveTwoNotchesInEachPiece()
        {
            (double Lat, double X)[] second = { (7, 176), (7, 184), (8, 184), (8, 176), (7, 176) };
            using (JsonDocument json = ExportArea(Polygon(Box, BoxHole, second)))
            {
                var polygons = Polygons(json.RootElement.GetProperty("features")[0].GetProperty("geometry"));

                Assert.Equal(2, polygons.Count);
                Assert.Equal(0, HoleCount(polygons));
                Assert.Equal(ExpectedArea(Box, BoxHole, second), PlanarArea(polygons), 6);
                Assert.Equal(0, Dropped(json));
                AssertValidRings(polygons);
                Assert.All(polygons, p => Assert.Equal(12, p[0].Length - 1));
            }
        }

        [Fact]
        public void HoleCrossingTheAntimeridian_AsATriangleWithItsApexOnTheBorder_KeepsTheArea()
        {
            // The apex at 180 touches the border at one vertex while the base is on the east side only: a plain hole of the east piece.
            (double Lat, double X)[] h = { (2, 180), (1, 184), (4, 184), (2, 180) };
            using (JsonDocument json = ExportArea(Polygon(Box, h)))
            {
                var polygons = Polygons(json.RootElement.GetProperty("features")[0].GetProperty("geometry"));

                Assert.Equal(2, polygons.Count);
                Assert.Equal(1, HoleCount(polygons));
                Assert.Equal(ExpectedArea(Box, h), PlanarArea(polygons), 6);
                Assert.Equal(0, Dropped(json));
                AssertValidRings(polygons);
            }
        }

        [Fact]
        public void HoleInTheWesternPartOfThePolygon_StaysAHole_AndIsNotCounted()
        {
            (double Lat, double X)[] h = { (4, 172), (4, 174), (6, 174), (6, 172), (4, 172) };
            using (JsonDocument json = ExportArea(Polygon(Box, h)))
            {
                var polygons = Polygons(json.RootElement.GetProperty("features")[0].GetProperty("geometry"));

                Assert.Equal(1, HoleCount(polygons));
                Assert.Equal(ExpectedArea(Box, h), PlanarArea(polygons), 6);
                Assert.Equal(0, Dropped(json));
                AssertValidRings(polygons);
            }
        }

        [Fact]
        public void PolarCap_HoleCrossingTheAntimeridian_IsStillCountedNotWritten()
        {
            var exterior = new[] { (60.0, 0.0), (60.0, 90.0), (60.0, 180.0), (60.0, -90.0), (60.0, 0.0) };
            var crossing = new[] { (80.0, 170.0), (80.0, -170.0), (85.0, -170.0), (85.0, 170.0), (80.0, 170.0) };
            var inside = new[] { (80.0, 10.0), (80.0, 40.0), (85.0, 40.0), (85.0, 10.0), (80.0, 10.0) };
            var result = GeoPolygon.Create(
                exterior.Select(v => new GeoCoordinate(v.Item1, v.Item2)),
                new[] { crossing, inside }.Select(h => h.Select(v => new GeoCoordinate(v.Item1, v.Item2))));
            Assert.True(result.IsSuccess, string.Join(" | ", result.Errors));

            using (JsonDocument json = ExportArea(result.Value))
            {
                var polygons = Polygons(json.RootElement.GetProperty("features")[0].GetProperty("geometry"));

                Assert.Equal(1, Dropped(json));
                Assert.Equal(1, HoleCount(polygons));
            }
        }

        // ---------------------------------------------------------------- holes in a polygon that splits into several pieces

        // A comb: a spine west of 180 and two teeth that reach east across it. East of 180 the teeth are two separate pieces.
        private static readonly (double Lat, double X)[] Comb =
            { (0, 170), (0, 190), (5, 190), (5, 175), (25, 175), (25, 190), (30, 190), (30, 170), (0, 170) };

        [Fact]
        public void Comb_HoleFullyInsideTheEastStripNextToTheBorder_IsAttachedToItsPiece()
        {
            (double Lat, double X)[] h = { (1, 181), (1, 184), (4, 184), (4, 181), (1, 181) };
            using (JsonDocument json = ExportArea(Polygon(Comb, h)))
            {
                var polygons = Polygons(json.RootElement.GetProperty("features")[0].GetProperty("geometry"));

                Assert.Equal(3, polygons.Count);
                Assert.Equal(1, HoleCount(polygons));
                Assert.Equal(ExpectedArea(Comb, h), PlanarArea(polygons), 6);
                Assert.Equal(0, Dropped(json));
                Assert.Empty(HolesSharingAnEdgeWithTheirShell(polygons));
            }
        }

        [Fact]
        public void Comb_HoleFarFromTheBorderInTheUpperTooth_IsAttachedToTheUpperTooth()
        {
            (double Lat, double X)[] h = { (26, 185), (26, 189), (29, 189), (29, 185), (26, 185) };
            using (JsonDocument json = ExportArea(Polygon(Comb, h)))
            {
                var polygons = Polygons(json.RootElement.GetProperty("features")[0].GetProperty("geometry"));

                Assert.Equal(ExpectedArea(Comb, h), PlanarArea(polygons), 6);
                Assert.Equal(0, Dropped(json));
                var owner = Assert.Single(polygons, p => p.Count == 2);
                Assert.True(owner[0].Min(c => c[1]) >= 25.0, "the hole belongs to the piece of the upper tooth");
            }
        }

        [Fact]
        public void Comb_HoleInTheWesternSpine_IsKeptWithoutBeingCounted()
        {
            (double Lat, double X)[] h = { (10, 171), (10, 174), (20, 174), (20, 171), (10, 171) };
            using (JsonDocument json = ExportArea(Polygon(Comb, h)))
            {
                var polygons = Polygons(json.RootElement.GetProperty("features")[0].GetProperty("geometry"));

                Assert.Equal(ExpectedArea(Comb, h), PlanarArea(polygons), 6);
                Assert.Equal(0, Dropped(json));
                Assert.Equal(1, HoleCount(polygons));
            }
        }

        [Fact]
        public void Comb_HoleTouchingTheAntimeridianAtAVertex_IsAttachedOrCounted_NeverLostSilently()
        {
            (double Lat, double X)[] h = { (2, 180), (1, 184), (4, 184), (2, 180) };
            using (JsonDocument json = ExportArea(Polygon(Comb, h)))
            {
                var polygons = Polygons(json.RootElement.GetProperty("features")[0].GetProperty("geometry"));

                bool kept = Math.Abs(PlanarArea(polygons) - ExpectedArea(Comb, h)) < 1e-6;
                Assert.True(kept || Dropped(json) == 1, $"hole lost silently: area {PlanarArea(polygons)}, dropped {Dropped(json)}");
                Assert.InRange(Dropped(json), 0, 1);
            }
        }

        // A serpentine: three horizontal bars joined alternately at the east and at the west end, so both strips hold two pieces.
        private static readonly (double Lat, double X)[] Serpentine =
        {
            (0, 170), (0, 190), (10, 190), (10, 173), (17, 173), (17, 190), (20, 190), (20, 170), (7, 170), (7, 187), (3, 187), (3, 170), (0, 170),
        };

        [Fact]
        public void Serpentine_HoleCrossingTheAntimeridian_IsCountedAtMostOnce_AndNeverLostSilently()
        {
            // Inside the middle bar (latitude 7 to 10), astride 180.
            (double Lat, double X)[] h = { (8, 177), (8, 183), (9, 183), (9, 177), (8, 177) };
            using (JsonDocument json = ExportArea(Polygon(Serpentine, h)))
            {
                var polygons = Polygons(json.RootElement.GetProperty("features")[0].GetProperty("geometry"));

                Assert.Equal(4, polygons.Count);
                bool kept = Math.Abs(PlanarArea(polygons) - ExpectedArea(Serpentine, h)) < 1e-6;
                Assert.True(kept || Dropped(json) >= 1, $"hole lost silently: area {PlanarArea(polygons)} instead of {ExpectedArea(Serpentine, h)}, dropped {Dropped(json)}");
                Assert.InRange(Dropped(json), 0, 1); // one source hole is never counted more than once
            }
        }

        public static IEnumerable<object[]> HoleStarts()
        {
            foreach (bool reversed in new[] { false, true })
            {
                for (int rotation = 0; rotation < 4; rotation++)
                    yield return new object[] { rotation, reversed };
            }
        }

        // Hole astride 180 in the middle bar of the serpentine, written from a given start vertex and direction.
        private static (double Lat, double X)[] StraddlingHole(int rotation, bool reversed)
        {
            var corners = new List<(double Lat, double X)> { (8, 177), (8, 183), (9, 183), (9, 177) };
            if (reversed)
                corners.Reverse();

            var ring = corners.Skip(rotation).Concat(corners.Take(rotation)).ToList();
            ring.Add(ring[0]);
            return ring.ToArray();
        }

        [Theory]
        [MemberData(nameof(HoleStarts))]
        public void Serpentine_HoleCrossingTheAntimeridian_IsNeverLostSilently_WhicheverVertexItStartsAt(int rotation, bool reversed)
        {
            (double Lat, double X)[] h = StraddlingHole(rotation, reversed);
            using (JsonDocument json = ExportArea(Polygon(Serpentine, h)))
            {
                var polygons = Polygons(json.RootElement.GetProperty("features")[0].GetProperty("geometry"));

                bool kept = Math.Abs(PlanarArea(polygons) - ExpectedArea(Serpentine, h)) < 1e-6;
                Assert.True(kept || Dropped(json) >= 1, $"hole lost silently: area {PlanarArea(polygons)} instead of {ExpectedArea(Serpentine, h)}, dropped {Dropped(json)}");
                Assert.InRange(Dropped(json), 0, 1);
            }
        }

        [Theory]
        [MemberData(nameof(HoleStarts))]
        public void Serpentine_HoleCrossingTheAntimeridian_KeepsTheWholeAreaInEveryVertexOrder(int rotation, bool reversed)
        {
            (double Lat, double X)[] h = StraddlingHole(rotation, reversed);
            using (JsonDocument json = ExportArea(Polygon(Serpentine, h)))
            {
                var polygons = Polygons(json.RootElement.GetProperty("features")[0].GetProperty("geometry"));

                Assert.Equal(ExpectedArea(Serpentine, h), PlanarArea(polygons), 6);
                Assert.Equal(0, Dropped(json));
                Assert.Empty(HolesSharingAnEdgeWithTheirShell(polygons));
                Assert.Equal(0, HoleCount(polygons));
                AssertValidRings(polygons);
            }
        }

        [Fact]
        public void Comb_HoleCrossingTheAntimeridianInTheUpperTooth_IsANotchOfThePieceOnEachSide()
        {
            (double Lat, double X)[] h = { (26, 178), (26, 185), (29, 185), (29, 178), (26, 178) };
            using (JsonDocument json = ExportArea(Polygon(Comb, h)))
            {
                var polygons = Polygons(json.RootElement.GetProperty("features")[0].GetProperty("geometry"));

                Assert.Equal(3, polygons.Count);
                Assert.Equal(0, HoleCount(polygons));
                Assert.Equal(ExpectedArea(Comb, h), PlanarArea(polygons), 6);
                Assert.Equal(0, Dropped(json));
                AssertValidRings(polygons);
                Assert.Empty(HolesSharingAnEdgeWithTheirShell(polygons));
            }
        }

        [Fact]
        public void Comb_HoleCrossingTheAntimeridian_AndAnotherInsideTheOtherTooth_AreBothKept()
        {
            (double Lat, double X)[] crossing = { (26, 178), (26, 185), (29, 185), (29, 178), (26, 178) };
            (double Lat, double X)[] lower = { (1, 181), (1, 184), (4, 184), (4, 181), (1, 181) };
            using (JsonDocument json = ExportArea(Polygon(Comb, crossing, lower)))
            {
                var polygons = Polygons(json.RootElement.GetProperty("features")[0].GetProperty("geometry"));

                Assert.Equal(3, polygons.Count);
                Assert.Equal(1, HoleCount(polygons));
                Assert.Equal(ExpectedArea(Comb, crossing, lower), PlanarArea(polygons), 6);
                Assert.Equal(0, Dropped(json));
                AssertValidRings(polygons);
                Assert.Empty(HolesSharingAnEdgeWithTheirShell(polygons));
            }
        }

        [Fact]
        public void Serpentine_HoleFullyInsideOneBar_IsAttachedWithoutBeingCounted()
        {
            (double Lat, double X)[] h = { (8, 176), (8, 178), (9, 178), (9, 176), (8, 176) };
            using (JsonDocument json = ExportArea(Polygon(Serpentine, h)))
            {
                var polygons = Polygons(json.RootElement.GetProperty("features")[0].GetProperty("geometry"));

                Assert.Equal(ExpectedArea(Serpentine, h), PlanarArea(polygons), 6);
                Assert.Equal(0, Dropped(json));
                Assert.Equal(1, HoleCount(polygons));
            }
        }

        // ---------------------------------------------------------------- interior rings that are not inside the exterior

        /// <summary>
        /// The interior ring is not inside the exterior (GeoPolygon only checks its length): it must be left out and counted once,
        /// never cut out of the strip as a notch, which would close through the corners of the strip (a world-sized ring).
        /// </summary>
        private static void AssertHoleDroppedAndExteriorKept((double Lat, double X)[] exterior, (double Lat, double X)[] hole, int expectedMembers)
        {
            using (JsonDocument json = ExportArea(Polygon(exterior, hole)))
            {
                var polygons = Polygons(json.RootElement.GetProperty("features")[0].GetProperty("geometry"));

                Assert.Equal(expectedMembers, polygons.Count);
                Assert.Equal(SourceArea(exterior), PlanarArea(polygons), 6);
                Assert.Equal(1, Dropped(json));
                Assert.Equal(0, HoleCount(polygons));
                Assert.All(Rings(polygons), ring => Assert.DoesNotContain(ring, c => Math.Abs(c[1]) == 90.0));
                Assert.All(polygons, p => Assert.All(p[0], c => Assert.InRange(c[1], exterior.Min(v => v.Lat), exterior.Max(v => v.Lat))));
                AssertValidRings(polygons);
            }
        }

        [Fact]
        public void HoleEntirelyOutsideTheExterior_AcrossTheAntimeridian_IsDroppedAndCounted()
        {
            (double Lat, double X)[] h = { (20, 175), (20, 185), (22, 185), (22, 175), (20, 175) };
            AssertHoleDroppedAndExteriorKept(Box, h, 2);
        }

        [Fact]
        public void HolePokingOutOfTheTopEdge_AcrossTheAntimeridian_IsDroppedAndCounted()
        {
            (double Lat, double X)[] h = { (8, 175), (8, 185), (12, 185), (12, 175), (8, 175) };
            AssertHoleDroppedAndExteriorKept(Box, h, 2);
        }

        [Fact]
        public void HolePokingOutSideways_WhileCrossingTheAntimeridian_IsDroppedAndCounted()
        {
            // Longitude 195 is beyond the exterior (170 to 190) on the east side.
            (double Lat, double X)[] h = { (4, 175), (4, 195), (6, 195), (6, 175), (4, 175) };
            AssertHoleDroppedAndExteriorKept(Box, h, 2);
        }

        [Fact]
        public void HoleWithASingleVertexOutsideTheExteriorBoundingBox_IsDroppedAndCounted()
        {
            // Latitude 11 is above the box of the exterior (0 to 10): the bounding-box prefilter rejects the ring, not the even-odd test.
            (double Lat, double X)[] h = { (4, 175), (4, 185), (11, 183), (6, 175), (4, 175) };
            AssertHoleDroppedAndExteriorKept(Box, h, 2);
        }

        [Fact]
        public void HoleWithASingleVertexOutsideAConcaveExterior_ButInsideItsBoundingBox_IsDroppedAndCounted()
        {
            // (8, 183) lies in the gap between the teeth of the comb (latitude 5 to 25, east of 175): outside the exterior but inside its
            // bounding box, so the even-odd test is what rejects this ring. The other vertices are inside the lower tooth.
            (double Lat, double X)[] h = { (1, 175), (1, 185), (8, 183), (3, 175), (1, 175) };
            AssertHoleDroppedAndExteriorKept(Comb, h, 3);
        }

        [Fact]
        public void HoleWithAVertexOnASlopedShellEdge_IsKeptAsANotchAndNotCounted()
        {
            // The edge (10,190)-(20,170) is sloped; (13,184) lies exactly on it (latitude = 10 + (190 - longitude) / 2) and is on the
            // boundary, which the containment test allows. The other vertices are inside, so the ring crosses 180 and is cut out.
            (double Lat, double X)[] exterior = { (0, 170), (0, 190), (10, 190), (20, 170), (0, 170) };
            (double Lat, double X)[] h = { (4, 175), (4, 185), (13, 184), (6, 175), (4, 175) };
            using (JsonDocument json = ExportArea(Polygon(exterior, h)))
            {
                var polygons = Polygons(json.RootElement.GetProperty("features")[0].GetProperty("geometry"));

                Assert.Equal(0, Dropped(json));
                Assert.Equal(ExpectedArea(exterior, h), PlanarArea(polygons), 6);
                Assert.All(Rings(polygons), ring => Assert.DoesNotContain(ring, c => Math.Abs(c[1]) == 90.0));
                AssertValidRings(polygons);
            }
        }

        [Fact]
        public void HoleOutsideTheExterior_InAPolygonThatSplitsIntoSeveralPieces_IsDroppedAndCounted()
        {
            // The gap between the teeth of the comb (latitude 5 to 25, east of 175) is outside the exterior; the hole is astride 180.
            (double Lat, double X)[] h = { (10, 177), (10, 183), (15, 183), (15, 177), (10, 177) };
            AssertHoleDroppedAndExteriorKept(Comb, h, 3);
        }

        [Fact]
        public void HoleOutsideTheExterior_ReachingTheBorderFromTheEastOnly_IsDroppedAndCounted()
        {
            (double Lat, double X)[] h = { (20, 180), (20, 185), (22, 185), (22, 180), (20, 180) };
            AssertHoleDroppedAndExteriorKept(Box, h, 2);
        }

        [Fact]
        public void HoleCrossingTheBorderSeveralTimes_InsideTheExterior_IsCutOutAsNotches()
        {
            // A "C" whose bars end beyond 180 on the east: the ring crosses 180 four times.
            (double Lat, double X)[] h = { (2, 175), (2, 185), (4, 185), (4, 177), (6, 177), (6, 185), (8, 185), (8, 175), (2, 175) };
            using (JsonDocument json = ExportArea(Polygon(Box, h)))
            {
                var polygons = Polygons(json.RootElement.GetProperty("features")[0].GetProperty("geometry"));

                Assert.Equal(ExpectedArea(Box, h), PlanarArea(polygons), 6);
                Assert.Equal(0, Dropped(json));
                Assert.Equal(0, HoleCount(polygons));
                Assert.All(Rings(polygons), ring => Assert.DoesNotContain(ring, c => Math.Abs(c[1]) == 90.0));
                AssertValidRings(polygons);
            }
        }

        [Fact]
        public void HoleCrossingTheBorderSeveralTimes_WithAVertexTouchingTheShell_IsStillANotch()
        {
            // Same "C", with its lower bar on the lower edge of the exterior (an edge shared with the shell is invalid GML, a vertex is not).
            (double Lat, double X)[] h = { (0, 176), (2, 185), (4, 185), (4, 177), (6, 177), (6, 185), (8, 185), (8, 175), (0, 176) };
            using (JsonDocument json = ExportArea(Polygon(Box, h)))
            {
                var polygons = Polygons(json.RootElement.GetProperty("features")[0].GetProperty("geometry"));

                Assert.Equal(0, Dropped(json));
                Assert.Equal(ExpectedArea(Box, h), PlanarArea(polygons), 6);
                Assert.All(Rings(polygons), ring => Assert.DoesNotContain(ring, c => Math.Abs(c[1]) == 90.0));
            }
        }

        [Fact]
        public void StripClipper_NeverClosesARingMadeOnlyOfNotchArcs_AndReportsTheirSource()
        {
            // The exterior has no arc in the strip; the notch chain (it enters at latitude 5 and leaves at 3 on the west border, so the
            // arc can only be closed round the whole rectangle) has one.
            var exterior = new List<StripClipper.Node> { new StripClipper.Node(200.0, 0.0, 0), new StripClipper.Node(210.0, 5.0, 1), new StripClipper.Node(200.0, 0.0, 0) };
            var notch = new List<StripClipper.Node>
            {
                new StripClipper.Node(-190.0, 5.0, -1), new StripClipper.Node(-170.0, 5.0, 1), new StripClipper.Node(-170.0, 3.0, 0), new StripClipper.Node(-190.0, 3.0, -1),
            };
            var source = new List<GeoJsonPosition> { new GeoJsonPosition(-170.0, 3.0), new GeoJsonPosition(-170.0, 5.0) };
            var orphans = new List<IReadOnlyList<GeoJsonPosition>>();

            List<List<GeoJsonPosition>> rings = StripClipper.Clip(
                new (IReadOnlyList<StripClipper.Node> Chain, IReadOnlyList<GeoJsonPosition> Source)[] { (exterior, source), (notch, source) },
                -180.0, 180.0, 0.0, orphans);

            Assert.Empty(rings);
            Assert.Single(orphans);
            Assert.Same(source, orphans[0]);
        }

        // ---------------------------------------------------------------- lines with a vertex exactly on the antimeridian (TD-GEO-2)

        private static List<double[][]> ExportTrack(params (double Lat, double Lon)[] points)
        {
            var tca = Tca.Create(
                "t1", T0, ReportStatus.Normal, PermissibleUsage.Operational, "RJTD",
                observation: new TropicalCycloneTrackPoint(T0, new GeoCoordinate(points[0].Lat, points[0].Lon), null),
                forecasts: points.Skip(1).Select((p, i) => new TropicalCycloneTrackPoint(T0.AddHours(6 * (i + 1)), new GeoCoordinate(p.Lat, p.Lon), null)).ToArray()).Value;

            using (JsonDocument json = Export(tca))
            {
                JsonElement track = json.RootElement.GetProperty("features").EnumerateArray()
                    .Single(f => f.GetProperty("properties").GetProperty("featureType").GetString() == "TC_TRACK")
                    .GetProperty("geometry");
                JsonElement coordinates = track.GetProperty("coordinates");
                return track.GetProperty("type").GetString() == "LineString"
                    ? new List<double[][]> { CircleGeometryTests.Ring(coordinates) }
                    : coordinates.EnumerateArray().Select(CircleGeometryTests.Ring).ToList();
            }
        }

        private static double LineLength(IEnumerable<double[][]> parts) =>
            parts.Sum(part => Enumerable.Range(1, part.Length - 1).Sum(i => Math.Sqrt(Math.Pow(part[i][0] - part[i - 1][0], 2) + Math.Pow(part[i][1] - part[i - 1][1], 2))));

        private static double SourceLength((double Lat, double Lon)[] points)
        {
            double total = 0.0;
            for (int i = 1; i < points.Length; i++)
            {
                double dLon = points[i].Lon - points[i - 1].Lon;
                dLon = dLon > 180.0 ? dLon - 360.0 : dLon < -180.0 ? dLon + 360.0 : dLon;
                double dLat = points[i].Lat - points[i - 1].Lat;
                total += Math.Sqrt((dLon * dLon) + (dLat * dLat));
            }

            return total;
        }

        public static IEnumerable<object[]> LinesWithAMiddleVertexOnTheAntimeridian()
        {
            yield return new object[] { new[] { (8.0, 170.0), (10.0, 180.0), (12.0, -170.0) } };   // on +180, going east
            yield return new object[] { new[] { (12.0, -170.0), (10.0, -180.0), (8.0, 170.0) } };   // on -180, going west
        }

        public static IEnumerable<object[]> LinesWithAnEndVertexOnTheAntimeridian()
        {
            yield return new object[] { new[] { (10.0, 180.0), (12.0, -170.0) } };                  // starts on +180
            yield return new object[] { new[] { (8.0, 170.0), (10.0, -180.0) } };                   // ends on -180 (reached the short way east)
            yield return new object[] { new[] { (10.0, -180.0), (8.0, 170.0) } };                   // starts on -180
            yield return new object[] { new[] { (12.0, -170.0), (10.0, 180.0) } };                  // ends on +180 (reached the short way west)
        }

        private static void AssertNoDegeneratePart_AndLengthKept((double Lat, double Lon)[] points)
        {
            List<double[][]> parts = ExportTrack(points);

            string described = string.Join(" | ", parts.Select(p => string.Join(" ", p.Select(c => $"({c[0]},{c[1]})"))));
            Assert.All(parts, part => Assert.True(part.Select(c => (c[0], c[1])).Distinct().Count() >= 2, "degenerate part in " + described));
            Assert.Equal(SourceLength(points), LineLength(parts), 6);
        }

        [Fact]
        public void LineWithAnEndVertexOnTheAntimeridian_IsASingleLineString_WithoutTheStub()
        {
            List<double[][]> parts = ExportTrack((8.0, 170.0), (10.0, -180.0));
            double[][] line = Assert.Single(parts);
            Assert.Equal(new[] { new[] { 170.0, 8.0 }, new[] { 180.0, 10.0 } }, line);

            parts = ExportTrack((10.0, 180.0), (12.0, -170.0));
            line = Assert.Single(parts);
            Assert.Equal(new[] { new[] { -180.0, 10.0 }, new[] { -170.0, 12.0 } }, line);
        }

        [Fact]
        public void LineWithAMiddleVertexOnTheAntimeridian_DoesNotRepeatThePosition()
        {
            List<double[][]> parts = ExportTrack((8.0, 170.0), (10.0, 180.0), (12.0, -170.0));
            Assert.Equal(2, parts.Count);
            Assert.Equal(new[] { new[] { 170.0, 8.0 }, new[] { 180.0, 10.0 } }, parts[0]);
            Assert.Equal(new[] { new[] { -180.0, 10.0 }, new[] { -170.0, 12.0 } }, parts[1]);
        }

        [Fact]
        public void LineThatCrossesTwice_KeepsThreePartsAndItsLength()
        {
            (double Lat, double Lon)[] points = { (8.0, 170.0), (10.0, -175.0), (12.0, -170.0), (14.0, 175.0) };
            List<double[][]> parts = ExportTrack(points);
            Assert.Equal(3, parts.Count);
            Assert.All(parts, part => Assert.True(part.Select(c => (c[0], c[1])).Distinct().Count() >= 2));
            Assert.Equal(SourceLength(points), LineLength(parts), 6);
        }

        [Fact]
        public void LineThatEndsOnTheAntimeridianAfterCrossing_KeepsBothParts()
        {
            (double Lat, double Lon)[] points = { (8.0, 170.0), (10.0, -175.0), (12.0, -180.0) };
            List<double[][]> parts = ExportTrack(points);
            Assert.Equal(2, parts.Count);
            Assert.All(parts, part => Assert.True(part.Select(c => (c[0], c[1])).Distinct().Count() >= 2));
            Assert.Equal(SourceLength(points), LineLength(parts), 6);
        }

        [Theory]
        [MemberData(nameof(LinesWithAMiddleVertexOnTheAntimeridian))]
        public void LineWithAMiddleVertexOnTheAntimeridian_HasNoDegeneratePart_AndKeepsItsLength((double Lat, double Lon)[] points) =>
            AssertNoDegeneratePart_AndLengthKept(points);

        [Theory]
        [MemberData(nameof(LinesWithAnEndVertexOnTheAntimeridian))]
        public void LineWithAnEndVertexOnTheAntimeridian_HasNoDegeneratePart_AndKeepsItsLength((double Lat, double Lon)[] points) =>
            AssertNoDegeneratePart_AndLengthKept(points);
    }
}
