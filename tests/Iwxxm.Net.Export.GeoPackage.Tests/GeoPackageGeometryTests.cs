using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Iwxxm.Net.Export.GeoPackage.Model;
using Iwxxm.Net.Reports;
using Xunit;

namespace Iwxxm.Net.Export.GeoPackage.Tests
{
    /// <summary>
    /// The neutral geometry of a feature must carry exactly the coordinates of the GeoJSON feature of the same document,
    /// with the kinds promoted to the ones a feature table stores.
    /// </summary>
    public sealed class GeoPackageGeometryTests
    {
        public static IEnumerable<object[]> Samples() => SampleFiles.All();

        public static IEnumerable<object[]> ExtraSamples() => SampleFiles.Extra();

        [Theory]
        [MemberData(nameof(Samples))]
        public void Geometry_EqualsTheGeoJsonGeometry_ForTheWmoExamples(string sample) =>
            AssertGeometries(SampleFiles.PathOf(sample), sample);

        [Theory]
        [MemberData(nameof(ExtraSamples))]
        public void Geometry_EqualsTheGeoJsonGeometry_ForTheSigwxSamples(string sample) =>
            AssertGeometries(SampleFiles.ExtraPathOf(sample), sample);

        [Fact]
        public void Samples_CoverEveryKindIncludingPromotionAndSplitGeometries()
        {
            var all = new List<GeoPackageGeometry>();
            foreach (string path in SampleFiles.AllPaths())
            {
                all.AddRange(Read(path).Features.Where(f => f.Geometry != null).Select(f => f.Geometry!));
            }

            Assert.Contains(all, g => g.Kind == GeoPackageGeometryKind.Point);
            Assert.Contains(all, g => g.Kind == GeoPackageGeometryKind.MultiLineString && g.Lines!.Count == 1);
            Assert.Contains(all, g => g.Kind == GeoPackageGeometryKind.MultiPolygon && g.Polygons!.Count == 1);
            Assert.Contains(all, g => g.Kind == GeoPackageGeometryKind.MultiPolygon && g.Polygons!.Count > 1);
            Assert.Contains(all, g => g.Kind == GeoPackageGeometryKind.MultiPolygon && g.Polygons!.Any(p => p.Count > 1));
        }

        [Fact]
        public void Envelope_IsThePlainMinimumAndMaximumOfEveryCoordinate()
        {
            GeoPackageGeometry geometry = GeoPackageGeometry.ForMultiPolygon(new[]
            {
                // Two pieces of an antimeridian split: one east of 170, one west of -170.
                Polygon(Ring((170, 10), (180, 10), (180, 20), (170, 20), (170, 10))),
                Polygon(Ring((-180, 10), (-175, 10), (-175, 20), (-180, 20), (-180, 10)))
            });

            GeoPackageEnvelope envelope = geometry.Envelope()!.Value;

            Assert.Equal(-180.0, envelope.MinX);
            Assert.Equal(180.0, envelope.MaxX);
            Assert.Equal(10.0, envelope.MinY);
            Assert.Equal(20.0, envelope.MaxY);
        }

        [Fact]
        public void Envelope_OfAPoint_IsThePointItself()
        {
            GeoPackageEnvelope envelope = GeoPackageGeometry.ForPoint(new GeoPackagePosition(10.5, -45.25)).Envelope()!.Value;

            Assert.Equal(10.5, envelope.MinX);
            Assert.Equal(10.5, envelope.MaxX);
            Assert.Equal(-45.25, envelope.MinY);
            Assert.Equal(-45.25, envelope.MaxY);
        }

        [Fact]
        public void Envelope_OfALine_UsesAllPositions()
        {
            GeoPackageEnvelope envelope = GeoPackageGeometry.ForMultiLineString(new[]
            {
                Line((1, 2), (-3, 4), (5, -6))
            }).Envelope()!.Value;

            Assert.Equal(-3.0, envelope.MinX);
            Assert.Equal(5.0, envelope.MaxX);
            Assert.Equal(-6.0, envelope.MinY);
            Assert.Equal(4.0, envelope.MaxY);
        }

        [Fact]
        public void Envelope_OfAnEmptyGeometry_IsNull()
        {
            Assert.Null(GeoPackageGeometry.ForMultiLineString(Array.Empty<IReadOnlyList<GeoPackagePosition>>()).Envelope());
            Assert.Null(GeoPackageGeometry.ForMultiLineString(new[] { Line() }).Envelope());
            Assert.Null(GeoPackageGeometry.ForMultiPolygon(Array.Empty<IReadOnlyList<IReadOnlyList<GeoPackagePosition>>>()).Envelope());
            Assert.Null(GeoPackageGeometry.ForMultiPolygon(new[] { Polygon() }).Envelope());
        }

        [Fact]
        public void Factories_RejectNull()
        {
            Assert.Throws<ArgumentNullException>(() => GeoPackageGeometry.ForMultiLineString(null!));
            Assert.Throws<ArgumentNullException>(() => GeoPackageGeometry.ForMultiPolygon(null!));
        }

        internal static IReadOnlyList<GeoPackagePosition> Line(params (double X, double Y)[] positions) =>
            positions.Select(p => new GeoPackagePosition(p.X, p.Y)).ToArray();

        internal static IReadOnlyList<GeoPackagePosition> Ring(params (double X, double Y)[] positions) => Line(positions);

        internal static IReadOnlyList<IReadOnlyList<GeoPackagePosition>> Polygon(params IReadOnlyList<GeoPackagePosition>[] rings) => rings;

        private static GeoPackageDocumentModel Read(string path)
        {
            var result = IwxxmParser.ParseFile(path);
            Assert.True(result.IsSuccess, string.Join(" | ", result.Errors));
            return GeoPackageFeatureSource.Read(result.Value);
        }

        private static void AssertGeometries(string path, string sample)
        {
            var parsed = IwxxmParser.ParseFile(path);
            Assert.True(parsed.IsSuccess, string.Join(" | ", parsed.Errors));
            GeoPackageDocumentModel model = GeoPackageFeatureSource.Read(parsed.Value);
            var geoJsonResult = IwxxmGeoJsonExporter.ToGeoJson(parsed.Value);
            Assert.True(geoJsonResult.IsSuccess, string.Join(" | ", geoJsonResult.Errors));

            using JsonDocument geoJson = JsonDocument.Parse(geoJsonResult.Value);
            List<JsonElement> expected = geoJson.RootElement.GetProperty("features").EnumerateArray().ToList();
            Assert.Equal(expected.Count, model.Features.Count);

            for (int i = 0; i < expected.Count; i++)
            {
                string where = sample + " feature " + i;
                JsonElement geometry = expected[i].GetProperty("geometry");
                GeoPackageGeometry? actual = model.Features[i].Geometry;

                Assert.True((geometry.ValueKind == JsonValueKind.Null) == (actual == null), where + ": null geometry iff null geometry");
                if (actual != null)
                    AssertSame(geometry, actual, where);
            }
        }

        private static void AssertSame(JsonElement geometry, GeoPackageGeometry actual, string where)
        {
            string type = geometry.GetProperty("type").GetString()!;
            JsonElement coordinates = geometry.GetProperty("coordinates");

            switch (type)
            {
                case "Point":
                    Assert.Equal(GeoPackageGeometryKind.Point, actual.Kind);
                    AssertPosition(coordinates, actual.Point, where);
                    break;

                case "LineString":
                    Assert.Equal(GeoPackageGeometryKind.MultiLineString, actual.Kind);
                    Assert.Single(actual.Lines!);
                    AssertPositions(coordinates, actual.Lines![0], where);
                    break;

                case "MultiLineString":
                    Assert.Equal(GeoPackageGeometryKind.MultiLineString, actual.Kind);
                    AssertLines(coordinates, actual.Lines!, where);
                    break;

                case "Polygon":
                    Assert.Equal(GeoPackageGeometryKind.MultiPolygon, actual.Kind);
                    Assert.Single(actual.Polygons!);
                    AssertRings(coordinates, actual.Polygons![0], where);
                    break;

                case "MultiPolygon":
                    Assert.Equal(GeoPackageGeometryKind.MultiPolygon, actual.Kind);
                    Assert.Equal(coordinates.GetArrayLength(), actual.Polygons!.Count);
                    for (int p = 0; p < actual.Polygons.Count; p++)
                        AssertRings(coordinates[p], actual.Polygons[p], where + " polygon " + p);
                    break;

                default:
                    Assert.Fail(where + ": unexpected GeoJSON geometry type " + type);
                    break;
            }
        }

        private static void AssertLines(JsonElement lines, IReadOnlyList<IReadOnlyList<GeoPackagePosition>> actual, string where)
        {
            Assert.Equal(lines.GetArrayLength(), actual.Count);
            for (int l = 0; l < actual.Count; l++)
                AssertPositions(lines[l], actual[l], where + " line " + l);
        }

        private static void AssertRings(JsonElement rings, IReadOnlyList<IReadOnlyList<GeoPackagePosition>> actual, string where)
        {
            Assert.Equal(rings.GetArrayLength(), actual.Count);
            for (int r = 0; r < actual.Count; r++)
                AssertPositions(rings[r], actual[r], where + " ring " + r);
        }

        private static void AssertPositions(JsonElement positions, IReadOnlyList<GeoPackagePosition> actual, string where)
        {
            Assert.Equal(positions.GetArrayLength(), actual.Count);
            for (int n = 0; n < actual.Count; n++)
                AssertPosition(positions[n], actual[n], where + " position " + n);
        }

        private static void AssertPosition(JsonElement position, GeoPackagePosition actual, string where)
        {
            Assert.Equal(2, position.GetArrayLength());
            Assert.True(BitConverter.DoubleToInt64Bits(position[0].GetDouble()) == BitConverter.DoubleToInt64Bits(actual.X), where + ": x");
            Assert.True(BitConverter.DoubleToInt64Bits(position[1].GetDouble()) == BitConverter.DoubleToInt64Bits(actual.Y), where + ": y");
        }
    }
}
