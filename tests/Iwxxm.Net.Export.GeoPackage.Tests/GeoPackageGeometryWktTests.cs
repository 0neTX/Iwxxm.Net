using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Iwxxm.Net.Export.GeoPackage.Model;
using Xunit;
using static Iwxxm.Net.Export.GeoPackage.Tests.GeoPackageGeometryEncoderTests;
using static Iwxxm.Net.Export.GeoPackage.Tests.GeoPackageGeometryTests;

namespace Iwxxm.Net.Export.GeoPackage.Tests
{
    /// <summary>
    /// The blobs are read back as well-known text, the form in which an independent reader (GDAL, see
    /// <c>tools/validate-geopackage-geometry.sh</c>) reports them. The text is made here from the neutral model and from the
    /// blob, never by the encoder, so these tests need no GDAL.
    /// </summary>
    public sealed class GeoPackageGeometryWktTests
    {
        [Fact]
        public void Wkt_OfTheDecodedBlob_IsTheExpectedText_ForHandWrittenGeometries()
        {
            var cases = new (GeoPackageGeometry Geometry, string Wkt)[]
            {
                (GeoPackageGeometry.ForPoint(new GeoPackagePosition(10.5, 45.25)), "POINT (10.5 45.25)"),
                (
                    GeoPackageGeometry.ForMultiLineString(new[] { Line((0, 0), (1, 1)), Line((2, 3), (4, 5)) }),
                    "MULTILINESTRING ((0 0, 1 1), (2 3, 4 5))"
                ),
                (
                    GeoPackageGeometry.ForMultiPolygon(new[]
                    {
                        Polygon(
                            Ring((0, 0), (10, 0), (10, 10), (0, 10), (0, 0)),
                            Ring((2, 2), (2, 4), (4, 4), (4, 2), (2, 2)))
                    }),
                    "MULTIPOLYGON (((0 0, 10 0, 10 10, 0 10, 0 0), (2 2, 2 4, 4 4, 4 2, 2 2)))"
                ),
                (
                    // An antimeridian split: the two members touch +180 and -180.
                    GeoPackageGeometry.ForMultiPolygon(new[]
                    {
                        Polygon(Ring((170, 10), (180, 10), (180, 20), (170, 20), (170, 10))),
                        Polygon(Ring((-180, 10), (-175, 10), (-175, 20), (-180, 20), (-180, 10)))
                    }),
                    "MULTIPOLYGON (((170 10, 180 10, 180 20, 170 20, 170 10)), ((-180 10, -175 10, -175 20, -180 20, -180 10)))"
                ),
                (
                    // A polar cap: one ring along the parallel, laid out from -180 to 180 and closed at 90.
                    GeoPackageGeometry.ForMultiPolygon(new[]
                    {
                        Polygon(Ring((-180, 60), (-90, 60), (0, 60), (90, 60), (180, 60), (180, 90), (-180, 90), (-180, 60)))
                    }),
                    "MULTIPOLYGON (((-180 60, -90 60, 0 60, 90 60, 180 60, 180 90, -180 90, -180 60)))"
                )
            };

            foreach ((GeoPackageGeometry geometry, string wkt) in cases)
            {
                Assert.Equal(wkt, TestWkt.Format(geometry));
                GeoPackageGeometry decoded = Decode(GeoPackageGeometryEncoder.Encode(geometry, 4326), out _);
                Assert.Equal(wkt, TestWkt.Format(decoded));
            }
        }

        [Fact]
        public void Wkt_OfTheDecodedBlob_EqualsTheWktOfTheGeometry_ForEveryFeatureOfTheSamples()
        {
            foreach (string path in SampleFiles.AllPaths())
            {
                foreach (GeoPackageFeature feature in ReadFeatures(path).Where(f => f.Geometry != null))
                {
                    GeoPackageGeometry decoded = Decode(GeoPackageGeometryEncoder.Encode(feature.Geometry!, 4326), out _);
                    Assert.True(TestWkt.Format(feature.Geometry!) == TestWkt.Format(decoded), path + ": " + feature.FeatureId);
                }
            }
        }

        [Fact]
        public void Samples_IncludeACircleAnAntimeridianSplitAndAPolarCap_ThatSurviveTheBlob()
        {
            int circles = 0, splits = 0, caps = 0;
            foreach (string path in SampleFiles.AllPaths())
            {
                foreach (GeoPackageFeature feature in ReadFeatures(path).Where(f => f.Geometry != null))
                {
                    GeoPackageGeometry geometry = feature.Geometry!;
                    if (geometry.Kind != GeoPackageGeometryKind.MultiPolygon)
                        continue;

                    IReadOnlyList<string> tags = TagsOf(feature);
                    if (tags.Contains("circle"))
                        circles++;
                    if (tags.Contains("antimeridian"))
                        splits++;
                    if (tags.Contains("polar-cap"))
                        caps++;

                    AssertSameGeometry(geometry, Decode(GeoPackageGeometryEncoder.Encode(geometry, 4326), out _), path);
                }
            }

            Assert.True(circles > 0, "no circle polygon in the samples");
            Assert.True(splits > 0, "no antimeridian-split multi polygon in the samples");
            Assert.True(caps > 0, "no polar cap in the samples");
        }

        internal static bool IsAntimeridianSplit(GeoPackageGeometry geometry)
        {
            var xs = geometry.Polygons!.SelectMany(p => p).SelectMany(r => r).Select(p => p.X).ToList();
            return geometry.Polygons!.Count > 1 && xs.Contains(180.0) && xs.Contains(-180.0);
        }

        internal static bool IsPolarCap(GeoPackageGeometry geometry) =>
            geometry.Polygons!.SelectMany(p => p).SelectMany(r => r).Any(p => Math.Abs(p.Y) == 90.0);

        /// <summary>The classification of a feature geometry by what the samples must cover: kind, circle, antimeridian, polar cap, hole.</summary>
        internal static IReadOnlyList<string> TagsOf(GeoPackageFeature feature)
        {
            GeoPackageGeometry geometry = feature.Geometry!;
            var tags = new List<string> { geometry.Kind.ToString().ToLowerInvariant() };
            if (feature.Properties.Any(p => p.Key == "circleSegments")) tags.Add("circle");
            if (geometry.Kind == GeoPackageGeometryKind.MultiPolygon)
            {
                if (IsAntimeridianSplit(geometry)) tags.Add("antimeridian");
                if (IsPolarCap(geometry)) tags.Add("polar-cap");
                if (geometry.Polygons!.Any(p => p.Count > 1)) tags.Add("hole");
            }

            return tags;
        }
    }

    /// <summary>Well-known text with the shortest round-trip number of each coordinate ("R"), made for comparing, not for storing.</summary>
    internal static class TestWkt
    {
        public static string Format(GeoPackageGeometry geometry)
        {
            var sb = new StringBuilder();
            switch (geometry.Kind)
            {
                case GeoPackageGeometryKind.Point:
                    sb.Append("POINT (");
                    AppendPosition(sb, geometry.Point);
                    sb.Append(')');
                    break;

                case GeoPackageGeometryKind.MultiLineString:
                    sb.Append("MULTILINESTRING (");
                    sb.Append(string.Join(", ", geometry.Lines!.Select(Positions)));
                    sb.Append(')');
                    break;

                default:
                    sb.Append("MULTIPOLYGON (");
                    sb.Append(string.Join(", ", geometry.Polygons!.Select(p => "(" + string.Join(", ", p.Select(Positions)) + ")")));
                    sb.Append(')');
                    break;
            }

            return sb.ToString();
        }

        public static string Number(double value) => value.ToString("R", CultureInfo.InvariantCulture);

        private static string Positions(IReadOnlyList<GeoPackagePosition> positions)
        {
            var sb = new StringBuilder("(");
            for (int i = 0; i < positions.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                AppendPosition(sb, positions[i]);
            }

            return sb.Append(')').ToString();
        }

        private static void AppendPosition(StringBuilder sb, GeoPackagePosition position) =>
            sb.Append(Number(position.X)).Append(' ').Append(Number(position.Y));
    }
}
