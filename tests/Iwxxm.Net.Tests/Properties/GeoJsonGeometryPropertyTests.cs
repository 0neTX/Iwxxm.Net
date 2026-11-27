using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using CsCheck;
using Iwxxm.Net.Reports;
using Xunit;

namespace Iwxxm.Net.Tests.Properties
{
    /// <summary>
    /// Properties of the GeoJSON export of surfaces (polygons and circles), through the public exporter
    /// (<see cref="IwxxmGeoJsonExporter"/>, on a SIGMET whose evolving condition carries the surface), so that ring orientation,
    /// antimeridian splitting and polar caps are exercised exactly as in production.
    /// <para>
    /// Definitions taken from the code (not from an outside idea of "valid"):
    /// <list type="bullet">
    /// <item>Orientation is the sign of the planar shoelace area on (longitude, latitude), as in the writer; RFC 7946 §3.1.6 asks
    /// for counterclockwise exterior rings and clockwise holes in that sense. It is only defined for rings with a non-zero
    /// area, which every generated shape has.</item>
    /// <item>An edge "jumps" when its two longitudes differ by more than 180 degrees. The writer deliberately keeps two kinds of
    /// edge: an edge between two positions that are both ON the antimeridian (|lon| = 180, e.g. a full-globe band edge) and
    /// the edge that closes a polar cap along the pole itself (both ends at |lat| = 90, zero length on the sphere). Both are
    /// exempt; every other edge must be at most 180 degrees wide.</item>
    /// <item>Sources are simple (non self-intersecting) closed rings that go the short way between consecutive vertices, with
    /// no two consecutive vertices more than 180 degrees apart in longitude, which is how a GML source writes them. Rings that
    /// enclose a pole are included.</item>
    /// </list>
    /// </para>
    /// </summary>
    public sealed class GeoJsonGeometryPropertyTests
    {
        private static readonly DateTimeOffset T0 = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        // ---- cases ----

        /// <summary>A generated polygon: exterior vertices (latitude, longitude), an optional hole and its source orientation.</summary>
        public sealed class PolygonCase
        {
            public PolygonCase(string family, (double Lat, double Lon)[] exterior, (double Lat, double Lon)[]? hole)
            {
                Family = family;
                Exterior = exterior;
                Hole = hole;
            }

            public string Family { get; }

            public (double Lat, double Lon)[] Exterior { get; }

            public (double Lat, double Lon)[]? Hole { get; }

            public GeoPolygon ToPolygon()
            {
                Result<GeoPolygon> result = GeoPolygon.Create(
                    Exterior.Select(v => new GeoCoordinate(v.Lat, v.Lon)),
                    Hole == null ? null : new[] { Hole.Select(v => new GeoCoordinate(v.Lat, v.Lon)) });
                Assert.True(result.IsSuccess, string.Join(" | ", result.Errors));
                return result.Value;
            }

            public override string ToString() =>
                Family + " exterior=[" + Format(Exterior) + "]" + (Hole == null ? string.Empty : " hole=[" + Format(Hole) + "]");

            private static string Format((double Lat, double Lon)[] vertices) =>
                string.Join("; ", vertices.Select(v => "(" + v.Lat.ToString("R", CultureInfo.InvariantCulture) + ", " + v.Lon.ToString("R", CultureInfo.InvariantCulture) + ")"));
        }

        /// <summary>A generated circle: centre, and the radius in metres and as reported (value + unit).</summary>
        public sealed class CircleCase
        {
            public CircleCase(double lat, double lon, double radiusMeters, LengthUnit unit)
            {
                Lat = lat;
                Lon = lon;
                RadiusMeters = radiusMeters;
                Unit = unit;
            }

            public double Lat { get; }

            public double Lon { get; }

            public double RadiusMeters { get; }

            public LengthUnit Unit { get; }

            public GeoCircle ToCircle()
            {
                var radius = new Length(RadiusMeters, LengthUnit.Meters);
                Result<GeoCircle> result = GeoCircle.Create(new GeoCoordinate(Lat, Lon), new Length(radius.To(Unit), Unit));
                Assert.True(result.IsSuccess, string.Join(" | ", result.Errors));
                return result.Value;
            }

            public override string ToString() =>
                string.Format(CultureInfo.InvariantCulture, "circle centre=({0:R}, {1:R}) radius={2:R} m (reported in {3})", Lat, Lon, RadiusMeters, Unit);
        }

        // ---- generators ----

        private static double Wrap(double lon)
        {
            double wrapped = (((lon + 180.0) % 360.0) + 360.0) % 360.0 - 180.0;
            return wrapped == -180.0 && lon > 0 ? 180.0 : wrapped;
        }

        private static double ClampLat(double lat) => Math.Max(-90.0, Math.Min(90.0, lat));

        // Half-width of a shape in degrees: tiny (about 10 m), small (a few km), medium (a FIR sector) and wide (up to 60 degrees,
        // so that no two consecutive vertices are more than 180 degrees apart).
        private static readonly Gen<double> Extent = Gen.Frequency(
            (2, (IGen<double>)Gen.Double[1e-4, 1e-2]),
            (3, (IGen<double>)Gen.Double[1e-2, 2.0]),
            (3, (IGen<double>)Gen.Double[2.0, 20.0]),
            (2, (IGen<double>)Gen.Double[20.0, 60.0]));

        // Star-shaped ring around a centre: strictly increasing angles, so the ring is simple whatever the radii. A centre near a
        // pole or on the antimeridian gives rings that touch the pole or cross +-180 (also exactly on it, through the edge values).
        private static readonly Gen<PolygonCase> Star = Gen.Select(
            DomainGenerators.Latitude, DomainGenerators.Longitude, Extent, Gen.Int[4, 12],
            Gen.Double[0.35, 1.0].Array[12], Gen.Double[-1.0, 1.0].Array[12], Gen.Bool, Gen.Bool,
            (cLat, cLon, extent, n, radii, jitter, reversed, withHole) =>
            {
                var ring = new List<(double Lat, double Lon)>(n);
                for (int i = 0; i < n; i++)
                {
                    double angle = 2.0 * Math.PI * (i + (0.4 * jitter[i])) / n;
                    double r = extent * radii[i];
                    ring.Add((ClampLat(cLat + (r * Math.Sin(angle))), Wrap(cLon + (r * Math.Cos(angle)))));
                }

                // A diamond hole around the centre, well inside the star (the closest an edge can come to the centre is 0.05 * extent).
                // Only where nothing is clamped at a pole, so that the hole is really inside the exterior ring.
                (double Lat, double Lon)[]? hole = null;
                if (withHole && extent <= 20.0 && Math.Abs(cLat) <= 60.0)
                {
                    double h = extent * 0.04;
                    hole = new[] { (cLat + h, Wrap(cLon)), (cLat, Wrap(cLon + h)), (cLat - h, Wrap(cLon)), (cLat, Wrap(cLon - h)) };
                    if (reversed)
                        Array.Reverse(hole);
                }

                if (reversed)
                    ring.Reverse();

                return new PolygonCase("star", ring.ToArray(), hole);
            });

        // Rectangle whose west edge is chosen among the edge longitudes (so that corners fall exactly on +-180 or next to it) and whose
        // north edge may reach the pole.
        private static readonly Gen<PolygonCase> Box = Gen.Select(
            DomainGenerators.Longitude, Gen.Frequency(
                (1, (IGen<double>)Gen.OneOfConst(1e-4, 1e-3, 90.0, 170.0)),
                (3, (IGen<double>)Gen.Double[1e-4, 170.0])),
            DomainGenerators.Latitude, Gen.Frequency(
                (1, (IGen<double>)Gen.OneOfConst(1e-4, 1.0, 180.0)),
                (3, (IGen<double>)Gen.Double[1e-4, 120.0])),
            Gen.Bool,
            (west, width, south, height, reversed) =>
            {
                double s = Math.Min(south, 89.9);
                double north = ClampLat(s + height);
                double east = Wrap(west + width);
                var ring = new List<(double Lat, double Lon)> { (s, west), (s, east), (north, east), (north, west) };
                if (reversed)
                    ring.Reverse();

                return new PolygonCase("box", ring.ToArray(), null);
            });

        // Ring that goes once around the globe (encloses a pole): longitudes advance by a full turn, in either direction.
        private static readonly Gen<PolygonCase> PolarRing = Gen.Select(
            Gen.Int[4, 10], Gen.Bool, Gen.Double[40.0, 89.9], Gen.Double[-180.0, 180.0],
            Gen.Double[-1.0, 1.0].Array[10], Gen.Double[0.0, 8.0].Array[10], Gen.Bool,
            (n, south, baseLat, offset, jitter, dLat, reversed) =>
            {
                var ring = new List<(double Lat, double Lon)>(n);
                for (int i = 0; i < n; i++)
                {
                    double lat = ClampLat(baseLat - (dLat[i] * (baseLat / 90.0)));
                    double lon = Wrap(offset + (360.0 * (i + (0.25 * jitter[i])) / n));
                    ring.Add((south ? -lat : lat, lon));
                }

                if (reversed)
                    ring.Reverse();

                return new PolygonCase("polar", ring.ToArray(), null);
            });

        private static readonly Gen<PolygonCase> Polygons = Gen.Frequency(
            (5, (IGen<PolygonCase>)Star),
            (3, (IGen<PolygonCase>)Box),
            (2, (IGen<PolygonCase>)PolarRing));

        private static readonly LengthUnit[] LengthUnitsUsedForRadius =
            { LengthUnit.Meters, LengthUnit.Feet, LengthUnit.Kilometers, LengthUnit.NauticalMiles, LengthUnit.StatuteMiles };

        // Realistic radii run from 100 m to the 10100 km of the space weather DAYSIDE circle; the upper range (up to 19000 km,
        // the largest angular radius below half the globe) covers circles around one or both poles. Log-uniform, so small circles
        // are as frequent as large ones.
        //
        // Centres ON a pole and next to it (89.99999995 and the polar bands of DomainGenerators.Latitude) are included: the
        // longitude of a vertex is ill-conditioned there unless the formula is chosen with care (see GeodesicCircle).
        private static readonly Gen<CircleCase> Circles = Gen.Select(
            DomainGenerators.Latitude,
            DomainGenerators.Longitude,
            Gen.Double[Math.Log(100.0), Math.Log(19_000_000.0)].Select(Math.Exp),
            Gen.OneOfConst(LengthUnitsUsedForRadius),
            (lat, lon, meters, unit) => new CircleCase(lat, lon, meters, unit));

        // ---- export under test ----

        /// <summary>One exported polygon: rings (exterior first), each ring a list of [longitude, latitude].</summary>
        private sealed class ExportedPolygon
        {
            public ExportedPolygon(List<double[][]> rings) => Rings = rings;

            public List<double[][]> Rings { get; }
        }

        private static List<ExportedPolygon> Export(GeoSurface surface)
        {
            var volume = new EnRoutePhenomenonVolume("v1", surface, null, null);
            Result<Sigmet> report = Sigmet.Create(
                "s1", IwxxmReportType.Sigmet, T0, ReportStatus.Normal, PermissibleUsage.Operational,
                "NZZC", null, null, null, null, "1", T0, T0.AddHours(4), evolvingConditions: new[] { volume });
            Assert.True(report.IsSuccess, string.Join(" | ", report.Errors));

            string geoJson = IwxxmGeoJsonExporter.ToGeoJson(report.Value, GeoJsonExportOptions.Default).Value;
            using (JsonDocument json = JsonDocument.Parse(geoJson))
            {
                JsonElement geometry = json.RootElement.GetProperty("features").EnumerateArray()
                    .Select(f => f.GetProperty("geometry"))
                    .Single(g => g.ValueKind == JsonValueKind.Object && g.GetProperty("type").GetString() is "Polygon" or "MultiPolygon");

                string type = geometry.GetProperty("type").GetString()!;
                JsonElement coordinates = geometry.GetProperty("coordinates");
                IEnumerable<JsonElement> polygons = type == "Polygon" ? new[] { coordinates } : coordinates.EnumerateArray();
                return polygons
                    .Select(p => new ExportedPolygon(p.EnumerateArray()
                        .Select(ring => ring.EnumerateArray().Select(pos => pos.EnumerateArray().Select(n => n.GetDouble()).ToArray()).ToArray())
                        .ToList()))
                    .ToList();
            }
        }

        // ---- assertions on an export ----

        private static double SignedArea(double[][] ring)
        {
            double area = 0.0;
            for (int i = 0, j = ring.Length - 1; i < ring.Length; j = i++)
                area += (ring[j][0] * ring[i][1]) - (ring[i][0] * ring[j][1]);

            return area / 2.0;
        }

        private static IEnumerable<double[][]> AllRings(List<ExportedPolygon> polygons) => polygons.SelectMany(p => p.Rings);

        private static void AssertCoordinatesFiniteAndInRange(List<ExportedPolygon> polygons)
        {
            Assert.NotEmpty(polygons);
            foreach (double[][] ring in AllRings(polygons))
            {
                Assert.True(ring.Length >= 4, "a ring needs at least 4 positions");
                Assert.Equal(ring[0], ring[ring.Length - 1]);
                foreach (double[] position in ring)
                {
                    Assert.Equal(2, position.Length);
                    Assert.True(!double.IsNaN(position[0]) && !double.IsInfinity(position[0]) && !double.IsNaN(position[1]) && !double.IsInfinity(position[1]), "non-finite coordinate");
                    Assert.InRange(position[0], -180.0, 180.0);
                    Assert.InRange(position[1], -90.0, 90.0);
                }
            }
        }

        private static void AssertRightHandRule(List<ExportedPolygon> polygons)
        {
            Assert.NotEmpty(polygons);
            foreach (ExportedPolygon polygon in polygons)
            {
                Assert.True(SignedArea(polygon.Rings[0]) > 0.0, "exterior ring is not counterclockwise");
                foreach (double[][] hole in polygon.Rings.Skip(1))
                    Assert.True(SignedArea(hole) < 0.0, "interior ring is not clockwise");
            }
        }

        private static void AssertNoEdgeJumpsMoreThanHalfTheGlobe(List<ExportedPolygon> polygons)
        {
            foreach (double[][] ring in AllRings(polygons))
            {
                for (int i = 1; i < ring.Length; i++)
                {
                    double[] a = ring[i - 1];
                    double[] b = ring[i];
                    bool bothOnAntimeridian = Math.Abs(a[0]) == 180.0 && Math.Abs(b[0]) == 180.0;
                    bool alongAPole = Math.Abs(a[1]) == 90.0 && Math.Abs(b[1]) == 90.0;
                    if (bothOnAntimeridian || alongAPole)
                        continue;

                    Assert.True(Math.Abs(b[0] - a[0]) <= 180.0, $"edge {i} jumps {Math.Abs(b[0] - a[0]).ToString("R", CultureInfo.InvariantCulture)} degrees of longitude: ({a[0]:R}, {a[1]:R}) -> ({b[0]:R}, {b[1]:R})");
                }
            }
        }

        // ---- polygons ----

        [Fact]
        public void Polygon_Export_NeverThrows_AndKeepsClosedRingsFiniteAndInRange()
        {
            PropertySettings.Run(Polygons, PropertySettings.GeometrySeed, c => AssertCoordinatesFiniteAndInRange(Export(c.ToPolygon())), c => c.ToString());
        }

        [Fact]
        public void Polygon_Export_ExteriorRingsAreCounterclockwise_AndHolesClockwise_WhateverTheSourceOrientation()
        {
            PropertySettings.Run(Polygons, PropertySettings.GeometrySeed, c => AssertRightHandRule(Export(c.ToPolygon())), c => c.ToString());
        }

        [Fact]
        public void Polygon_Export_NoEdgeJumpsMoreThanHalfTheGlobe()
        {
            PropertySettings.Run(Polygons, PropertySettings.GeometrySeed, c => AssertNoEdgeJumpsMoreThanHalfTheGlobe(Export(c.ToPolygon())), c => c.ToString());
        }

        [Fact]
        public void Polygon_Export_KeepsEverySourceVertex()
        {
            // The splitter documents that no vertex is lost (a vertex may be duplicated on a strip border, never dropped), with one
            // exception that no polygon can represent (the spur vertices of a ring along a pole), see AssertEverySourceVertexIsExported.
            // Precondition: no hole, since a hole that cannot be represented is dropped and counted by design.
            Gen<PolygonCase> withoutHoles = Polygons.Select(c => new PolygonCase(c.Family, c.Exterior, null));
            PropertySettings.Run(withoutHoles, PropertySettings.GeometrySeed, c => AssertEverySourceVertexIsExported(c), c => c.ToString());
        }

        /// <summary>
        /// Replays the case that failed in the Windows CI run (CsCheck seed "dV07YuAR1o61"): a star ring clamped to the south pole,
        /// four consecutive vertices on the pole, one of them reached across the antimeridian. The seed replays one case only
        /// (the iterations after the first one of a run are random), so it is sampled once.
        /// </summary>
        [Fact]
        public void Polygon_Export_KeepsEverySourceVertex_ReplayOfTheCiFailure()
        {
            Gen<PolygonCase> withoutHoles = Polygons.Select(c => new PolygonCase(c.Family, c.Exterior, null));
            PropertySettings.Run(withoutHoles, "dV07YuAR1o61", c => AssertEverySourceVertexIsExported(c), c => c.ToString(), iterations: 1);
        }

        /// <summary>The ring of the failed CI run (see <see cref="Polygon_Export_KeepsEverySourceVertex_ReplayOfTheCiFailure"/>), as printed.</summary>
        private static readonly (double Lat, double Lon)[] CiRing =
        {
            (-90, -177.59066436097606), (-88.9815855818126, -177.47988501650121), (-87.872795333046525, -178.19984934376066),
            (-88.928618877638058, -179.77263325133345), (-90, -179.88374930981843), (-90, 179.52510380269143),
            (-90, -178.68536638033652), (-90, -177.7589452527925),
        };

        public static IEnumerable<object[]> PoleSpurCases()
        {
            // A ring that runs along the south pole out across the antimeridian and back: the vertex (-90, 179) is beyond it, between
            // the pole vertices (-90, -179) and (-90, -177), which are inside the strip and must be kept.
            var minimal = new[] { (-88.0, -178.0), (-90.0, -179.0), (-90.0, 179.0), (-90.0, -177.0) };
            yield return new object[] { "the ring of the CI run", CiRing, new[] { 5 } };
            yield return new object[] { "the ring of the CI run, reversed", Enumerable.Reverse(CiRing).ToArray(), new[] { 2 } };
            yield return new object[] { "minimal", minimal, new[] { 2 } };
            yield return new object[] { "minimal, reversed", Enumerable.Reverse(minimal).ToArray(), new[] { 1 } };

            // A strip between two meridians that runs from pole to pole, with a spur at each pole: (-90, 179) and (90, 179).
            var twoSpurs = new[] { (-90.0, -179.0), (-90.0, 179.0), (-90.0, -177.0), (90.0, -177.0), (90.0, -178.0), (90.0, 179.0), (90.0, -179.0) };
            yield return new object[] { "two spurs", twoSpurs, new[] { 1, 5 } };

            // A vertex whose next neighbour is off the pole is not a spur: the lobe (-180, -90), (-181, -90), (-180, -89.75) has an area.
            yield return new object[] { "one neighbour off the pole", new[] { (-88.0, -178.0), (-90.0, -179.0), (-90.0, 179.0), (-89.0, -177.0) }, Array.Empty<int>() };
        }

        /// <summary>
        /// The spurs of rings that run along a pole across the antimeridian: exactly the spur vertices (listed by index) are left out, each
        /// one for the cut of the ring at the antimeridian, and every other vertex, the pole vertices next to a spur included, is exported.
        /// </summary>
        [Theory]
        [MemberData(nameof(PoleSpurCases))]
        public void Polygon_Export_PoleSpur_LeavesOutExactlyTheSpurVertices(string description, (double Lat, double Lon)[] exterior, int[] expectedSpurs)
        {
            var polygon = new PolygonCase(description, exterior, null);
            IReadOnlyList<GeoCoordinate> ring = polygon.ToPolygon().ExteriorRing;
            Assert.Equal(expectedSpurs, SpurVertices(ring, ring.Count - 1).OrderBy(i => i).ToArray());
            AssertEverySourceVertexIsExported(polygon, spursLeftOut: true);
            Assert.All(AllRings(Export(polygon.ToPolygon())), r => Assert.False(RingHasNoArea(r), "a ring without area was written"));
        }

        /// <summary>
        /// The property, run on exports that were made by hand, must see a lost vertex: the vertex next to a spur, a spur whose cut is
        /// missing, a vertex whose neighbour is off the pole, and a spur that was kept although it must be left out.
        /// </summary>
        [Fact]
        public void SourceVertexCheck_SeesWhatIsMissing()
        {
            var ring = new[] { (-88.0, -178.0), (-90.0, -179.0), (-90.0, 179.0), (-90.0, -177.0) };
            var polygon = new PolygonCase("minimal", ring, null);
            var good = new HashSet<(double, double)> { (-178, -88), (-179, -90), (-180, -90), (-177, -90) };
            AssertSourceVerticesExported(polygon, good, spursLeftOut: true);

            Assert.ThrowsAny<Xunit.Sdk.XunitException>(() => AssertSourceVerticesExported(polygon, new HashSet<(double, double)>(good.Where(p => p != (-179, -90)))));
            Assert.ThrowsAny<Xunit.Sdk.XunitException>(() => AssertSourceVerticesExported(polygon, new HashSet<(double, double)>(good.Where(p => p != (-177, -90)))));
            Assert.ThrowsAny<Xunit.Sdk.XunitException>(() => AssertSourceVerticesExported(polygon, new HashSet<(double, double)>(good.Where(p => p != (-180, -90)))));
            Assert.ThrowsAny<Xunit.Sdk.XunitException>(() => AssertSourceVerticesExported(polygon, new HashSet<(double, double)>(good.Where(p => p != (-178, -88)))));

            // The spur vertex written as well as the cut: the splitter is expected to leave it out.
            Assert.ThrowsAny<Xunit.Sdk.XunitException>(() => AssertSourceVerticesExported(polygon, new HashSet<(double, double)>(good) { (179, -90) }, spursLeftOut: true));
            AssertSourceVerticesExported(polygon, new HashSet<(double, double)>(good) { (179, -90) }); // allowed where the spur is a spike of a larger ring

            // The neighbour of the spur vertex is off the pole: the vertex is not a spur and must be exported.
            var offPole = new PolygonCase("off the pole", new[] { (-88.0, -178.0), (-90.0, -179.0), (-90.0, 179.0), (-89.0, -177.0) }, null);
            var kept = new HashSet<(double, double)> { (-178, -88), (-179, -90), (179, -90), (-177, -89), (-180, -90) };
            AssertSourceVerticesExported(offPole, kept);
            Assert.ThrowsAny<Xunit.Sdk.XunitException>(() => AssertSourceVerticesExported(offPole, new HashSet<(double, double)>(kept.Where(p => p != (179, -90)))));
        }

        /// <summary>
        /// A found case: the vertex (-88.18, 179.99998) is 2 m across the antimeridian, a lobe of 4e-10 square degrees. A piece that small
        /// is a real polygon (it has an area on the 1e-7 degree grid it is written on), so it must not be dropped.
        /// </summary>
        [Fact]
        public void Polygon_Export_VertexAFewMetersAcrossTheAntimeridian_IsKept()
        {
            var ring = new[] { (-88.89570063981535, -178.418022261111), (-88.1824533886209, 179.99998214420577), (-85.84587171311654, -178.43099733321367), (-88.25688228025527, -177.04448630712523) };
            AssertEverySourceVertexIsExported(new PolygonCase("thin lobe", ring, null));
        }

        /// <summary>
        /// A ring written without an area (its positions are all on one line on the 1e-7 degree grid) is not a polygon. Next to a piece
        /// that has an area none is written; the source ring itself may have no area (all its vertices on the antimeridian), and is
        /// then left as it is, which is why the check only applies when the export has a piece with an area.
        /// </summary>
        [Fact]
        public void Polygon_Export_WritesNoRingWithoutAnArea_NextToAPieceWithOne()
        {
            PropertySettings.Run(Polygons, PropertySettings.GeometrySeed, c =>
            {
                List<ExportedPolygon> polygons = Export(c.ToPolygon());
                if (polygons.Any(p => !RingHasNoArea(p.Rings[0])))
                {
                    Assert.All(polygons, p => Assert.False(RingHasNoArea(p.Rings[0]), "a piece without an area was written next to one with an area"));
                    Assert.All(polygons, p => Assert.All(p.Rings.Skip(1), r => Assert.False(RingHasNoArea(r), "an interior ring without an area was written")));
                }
            }, c => c.ToString());
        }

        // Twice the area on the 1e-7 degree grid, in exact arithmetic (the positions are multiples of 1e-7).
        private static bool RingHasNoArea(double[][] ring)
        {
            decimal twice = 0m;
            long x0 = (long)Math.Round(ring[0][0] * 1e7);
            long y0 = (long)Math.Round(ring[0][1] * 1e7);
            for (int i = 1; i < ring.Length - 1; i++)
            {
                decimal ax = (long)Math.Round(ring[i][0] * 1e7) - x0;
                decimal ay = (long)Math.Round(ring[i][1] * 1e7) - y0;
                decimal bx = (long)Math.Round(ring[i + 1][0] * 1e7) - x0;
                decimal by = (long)Math.Round(ring[i + 1][1] * 1e7) - y0;
                twice += (ax * by) - (bx * ay);
            }

            return twice == 0m;
        }

        /// <summary>
        /// Every source vertex must be in the export, written at its own longitude (or at the other side of the antimeridian when it is on
        /// it), except the spur vertices (see <see cref="SpurVertices"/>): those may be left out, only where the cut of the ring at the
        /// antimeridian is written at their pole instead. A spur is a piece of no area that the splitter drops when it is a piece of its
        /// own, and keeps as a spike of the ring when the assembly of the pieces joins it to one with an area (it depends on the order of
        /// the arcs of the ring, and both are correct), so the property cannot ask for either; with <paramref name="spursLeftOut"/> (the
        /// rings where a spur is a piece of its own) it asks for the first.
        /// </summary>
        private static void AssertEverySourceVertexIsExported(PolygonCase c, bool spursLeftOut = false)
        {
            var exported = new HashSet<(double, double)>(AllRings(Export(c.ToPolygon())).SelectMany(r => r).Select(p => (Math.Round(p[0], 6), Math.Round(p[1], 6))));
            AssertSourceVerticesExported(c, exported, spursLeftOut);
        }

        private static void AssertSourceVerticesExported(PolygonCase c, HashSet<(double, double)> exported, bool spursLeftOut = false)
        {
            IReadOnlyList<GeoCoordinate> ring = c.ToPolygon().ExteriorRing;
            int count = ring.Count - 1; // the closing vertex repeats the first one
            HashSet<int> spurs = SpurVertices(ring, count);
            for (int i = 0; i < count; i++)
            {
                GeoCoordinate vertex = ring[i];
                bool found = IsExported(vertex, exported);
                if (spurs.Contains(i))
                {
                    // Unless another vertex of the ring is at the very same place.
                    bool shared = Enumerable.Range(0, count).Any(j => j != i && !spurs.Contains(j) && ring[j].Lat == vertex.Lat && ring[j].Lon == vertex.Lon);
                    Assert.True(!spursLeftOut || !found || shared, $"spur vertex ({vertex.Lat:R}, {vertex.Lon:R}) was written");
                    Assert.True(found || exported.Contains((-180.0, Math.Round(vertex.Lat, 6))) || exported.Contains((180.0, Math.Round(vertex.Lat, 6))), $"the cut of the spur at ({vertex.Lat:R}, {vertex.Lon:R}) is missing from the export");
                    continue;
                }

                Assert.True(found, $"source vertex ({vertex.Lat:R}, {vertex.Lon:R}) is missing from the export");
            }
        }

        // A vertex on the antimeridian may be written at +180 or -180.
        private static bool IsExported(GeoCoordinate vertex, HashSet<(double, double)> exported) =>
            exported.Contains((Math.Round(vertex.Lon, 6), Math.Round(vertex.Lat, 6)))
            || (Math.Abs(vertex.Lon) == 180.0 && exported.Contains((-Math.Round(vertex.Lon, 6), Math.Round(vertex.Lat, 6))));

        /// <summary>
        /// The spur vertices of a ring: the vertices the splitter cannot write because they are in a zero-area spike along a pole.
        /// <para>
        /// The ring is unwrapped as the splitter does (an edge of more than 180 degrees of longitude goes the short way) and cut into
        /// the arcs the longitude strips [-180 + 360 k, 180 + 360 k] contain: maximal runs of consecutive vertices in one strip. An arc is a
        /// spur when all its vertices are on one pole, the vertex before it and the vertex after it are on the same pole too (so that the
        /// ring enters and leaves the strip along the pole line, never across a parallel), and both are beyond the same border of the
        /// strip (the ring goes out along the pole line and comes back the way it went). Such an arc, closed through the border, has no
        /// area, whatever the pole line carries on either side. If one neighbour is off the pole the arc starts or ends at a point of the
        /// border above the pole and the lobe has an area. A ring that goes round the globe (a polar cap) and a vertex that is exactly on
        /// a border have no spur.
        /// </para>
        /// </summary>
        private static HashSet<int> SpurVertices(IReadOnlyList<GeoCoordinate> ring, int count)
        {
            var spurs = new HashSet<int>();
            var unwrapped = new double[count + 1];
            unwrapped[0] = ring[0].Lon;
            int turns = 0;
            for (int i = 1; i <= count; i++)
            {
                double delta = ring[i].Lon - ring[i - 1].Lon;
                if (Math.Abs(delta) > 180.0 && !(Math.Abs(ring[i - 1].Lon) == 180.0 && Math.Abs(ring[i].Lon) == 180.0))
                {
                    while (delta > 180.0)
                    {
                        delta -= 360.0;
                        turns--;
                    }

                    while (delta < -180.0)
                    {
                        delta += 360.0;
                        turns++;
                    }
                }

                unwrapped[i] = ring[i].Lon + (360.0 * turns);
            }

            if (Math.Abs(unwrapped[count] - unwrapped[0]) > 180.0)
                return spurs;

            // The strip of each vertex (null on a border).
            var strip = new int?[count];
            for (int i = 0; i < count; i++)
            {
                double position = (unwrapped[i] + 180.0) / 360.0;
                strip[i] = position == Math.Floor(position) ? (int?)null : (int)Math.Floor(position);
            }

            for (int start = 0; start < count; start++)
            {
                int previous = (start + count - 1) % count;
                if (strip[start] == null || strip[previous] == strip[start])
                    continue;

                int last = start;
                while (strip[(last + 1) % count] == strip[start] && (last + 1) % count != start)
                    last++;

                int next = (last + 1) % count;
                int k = strip[start]!.Value;
                double west = -180.0 + (360.0 * k);
                double east = west + 360.0;
                double pole = ring[start].Lat;
                bool onOnePole = Math.Abs(pole) == 90.0 && ring[previous].Lat == pole && ring[next].Lat == pole;
                for (int i = start; onOnePole && i <= last; i++)
                    onOnePole = ring[i % count].Lat == pole;

                if (!onOnePole)
                    continue;

                int entry = Math.Sign(unwrapped[previous] - west) < 0 ? -1 : (unwrapped[previous] > east ? 1 : 0);
                int exit = unwrapped[next] < west ? -1 : (unwrapped[next] > east ? 1 : 0);
                if (entry != 0 && entry == exit)
                {
                    for (int i = start; i <= last; i++)
                        spurs.Add(i % count);
                }
            }

            return spurs;
        }

        // ---- circles ----

        [Fact]
        public void Circle_Export_NeverThrows_AndKeepsClosedRingsFiniteAndInRange()
        {
            PropertySettings.Run(Circles, PropertySettings.GeometrySeed, c => AssertCoordinatesFiniteAndInRange(Export(c.ToCircle())), c => c.ToString());
        }

        /// <summary>
        /// A circle centred on a pole is a polar cap: one polygon whose exterior ring is counterclockwise with a positive area,
        /// whose circle vertices all lie at the latitude 90 - radius (or -90 + radius), and which spans -180..180 closed along the pole.
        /// A radius over a quarter of the globe from the north pole is still a cap around that pole (the far pole is never enclosed).
        /// </summary>
        private static void AssertPolarCap(List<ExportedPolygon> polygons, double centreLat, double radiusMeters)
        {
            AssertCoordinatesFiniteAndInRange(polygons);
            AssertRightHandRule(polygons);
            AssertNoEdgeJumpsMoreThanHalfTheGlobe(polygons);

            double pole = Math.Sign(centreLat) * 90.0;
            double angularRadiusDegrees = radiusMeters / GeodesicCircleMeanRadiusMeters * 180.0 / Math.PI;
            double expectedLat = pole - (Math.Sign(centreLat) * angularRadiusDegrees);

            ExportedPolygon polygon = Assert.Single(polygons);
            double[][] ring = Assert.Single(polygon.Rings);
            Assert.True(ring.Length >= 4, "a ring needs at least 4 positions");
            Assert.True(SignedArea(ring) > 0.0, "exterior ring has no positive area");

            double[][] circleVertices = ring.Where(p => Math.Abs(p[1]) != 90.0).ToArray();
            Assert.True(circleVertices.Length >= 64, "the 64 vertices of the circle are missing");
            Assert.All(circleVertices, p => Assert.InRange(p[1], expectedLat - 1e-6, expectedLat + 1e-6));

            // Closed along the pole, from one side of the antimeridian to the other.
            Assert.Contains(ring, p => p[0] == -180.0 && p[1] == pole);
            Assert.Contains(ring, p => p[0] == 180.0 && p[1] == pole);
            Assert.Contains(circleVertices, p => p[0] <= -180.0 + 5.7);
            Assert.Contains(circleVertices, p => p[0] >= 180.0 - 5.7);
        }

        private const double GeodesicCircleMeanRadiusMeters = 6371008.8;

        [Fact]
        public void Circle_CenteredExactlyOnAPole_IsExportedAsAPolarCap()
        {
            // Up to a radius of 19000 km, the largest angular radius below half the globe.
            var polarCentres = Gen.Select(Gen.OneOfConst(90.0, -90.0), DomainGenerators.Longitude, Gen.Double[Math.Log(100.0), Math.Log(19_000_000.0)].Select(Math.Exp),
                (lat, lon, meters) => new CircleCase(lat, lon, meters, LengthUnit.Meters));
            PropertySettings.Run(polarCentres, PropertySettings.GeometrySeed, c =>
                AssertPolarCap(Export(c.ToCircle()), c.Lat, c.RadiusMeters), c => c.ToString());
        }

        [Theory]
        [InlineData(90.0, 0.0, 1000.0)]
        [InlineData(-90.0, 0.0, 1000.0)]
        [InlineData(90.0, 177.8199240374742, 55883.0)]
        [InlineData(-90.0, -177.8199240374742, 55883.0)]
        [InlineData(90.0, -45.0, 5_000_000.0)]
        [InlineData(-90.0, 120.0, 10_100_000.0)]
        [InlineData(90.0, 10.0, 15_000_000.0)]
        public void Circle_CenteredOnAPole_Example_IsExportedAsAPolarCap(double lat, double lon, double radiusMeters)
        {
            AssertPolarCap(Export(new CircleCase(lat, lon, radiusMeters, LengthUnit.Meters).ToCircle()), lat, radiusMeters);
        }

        [Theory]
        [InlineData(89.9999999, 0.0, 1000.0)]
        [InlineData(89.99999995, 177.8199240374742, 55883.0)]
        [InlineData(-89.9999999, 30.0, 100.0)]
        public void Circle_CenteredNextToAPole_IsExportedAsAPolarCap(double lat, double lon, double radiusMeters)
        {
            AssertPolarCap(Export(new CircleCase(lat, lon, radiusMeters, LengthUnit.Meters).ToCircle()), lat, radiusMeters);
        }

        [Fact]
        public void Circle_ContainingAPole_ButCenteredElsewhere_IsStillAPolarCap()
        {
            // Centre 0.5 degrees from the north pole, radius 100 km (0.9 degrees): encloses the pole; the cap edge is not at constant latitude.
            List<ExportedPolygon> polygons = Export(new CircleCase(89.5, 20.0, 100_000.0, LengthUnit.Meters).ToCircle());
            AssertCoordinatesFiniteAndInRange(polygons);
            AssertRightHandRule(polygons);
            AssertNoEdgeJumpsMoreThanHalfTheGlobe(polygons);
            double[][] ring = Assert.Single(Assert.Single(polygons).Rings);
            Assert.True(SignedArea(ring) > 0.0);
            Assert.Contains(ring, p => p[0] == -180.0 && p[1] == 90.0);
            Assert.Contains(ring, p => p[0] == 180.0 && p[1] == 90.0);
        }

        [Fact]
        public void Circle_Export_ExteriorRingsAreCounterclockwise()
        {
            PropertySettings.Run(Circles, PropertySettings.GeometrySeed, c => AssertRightHandRule(Export(c.ToCircle())), c => c.ToString());
        }

        [Fact]
        public void Circle_Export_NoEdgeJumpsMoreThanHalfTheGlobe()
        {
            PropertySettings.Run(Circles, PropertySettings.GeometrySeed, c => AssertNoEdgeJumpsMoreThanHalfTheGlobe(Export(c.ToCircle())), c => c.ToString());
        }
    }
}
