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

            string geoJson = IwxxmGeoJsonExporter.ToGeoJson(report.Value, GeoJsonExportOptions.Default);
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
            // The splitter documents that no vertex is lost (a vertex may be duplicated on a strip border, never dropped).
            // Precondition: no hole, since a hole that cannot be represented is dropped and counted by design.
            Gen<PolygonCase> withoutHoles = Polygons.Select(c => new PolygonCase(c.Family, c.Exterior, null));
            PropertySettings.Run(withoutHoles, PropertySettings.GeometrySeed, c =>
            {
                var exported = new HashSet<(double, double)>(AllRings(Export(c.ToPolygon())).SelectMany(r => r).Select(p => (Math.Round(p[0], 6), Math.Round(p[1], 6))));
                foreach (GeoCoordinate vertex in c.ToPolygon().ExteriorRing)
                {
                    // A vertex on the antimeridian may be written at +180 or -180.
                    bool found = exported.Contains((Math.Round(vertex.Lon, 6), Math.Round(vertex.Lat, 6)))
                        || (Math.Abs(vertex.Lon) == 180.0 && exported.Contains((-Math.Round(vertex.Lon, 6), Math.Round(vertex.Lat, 6))));
                    Assert.True(found, $"source vertex ({vertex.Lat:R}, {vertex.Lon:R}) is missing from the export");
                }
            }, c => c.ToString());
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
