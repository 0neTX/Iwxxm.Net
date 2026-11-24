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
    /// The <c>properties</c> column must hold exactly the text of the <c>properties</c> member of the GeoJSON exporter.
    /// </summary>
    public sealed class GeoPackagePropertiesJsonTests
    {
        public static IEnumerable<object[]> Samples() => SampleFiles.All();

        private static IwxxmDocument Parse(string sample)
        {
            var result = IwxxmParser.ParseFile(SampleFiles.PathOf(sample));
            Assert.True(result.IsSuccess, string.Join(" | ", result.Errors));
            return result.Value;
        }

        [Theory]
        [MemberData(nameof(Samples))]
        public void Properties_AreTheCompactGeoJsonPropertiesText_Verbatim(string sample)
        {
            IwxxmDocument document = Parse(sample);
            GeoPackageDocumentModel model = GeoPackageFeatureSource.Read(document);
            var geoJson = IwxxmGeoJsonExporter.ToGeoJson(document, GeoJsonExportOptions.Default);
            Assert.True(geoJson.IsSuccess);
            using JsonDocument parsed = JsonDocument.Parse(geoJson.Value);
            JsonElement[] expected = parsed.RootElement.GetProperty("features").EnumerateArray().ToArray();

            Assert.Equal(expected.Length, model.Features.Count);
            for (int i = 0; i < expected.Length; i++)
            {
                // The raw text of a parsed element is the text as written, so this also checks key order and number formatting.
                string expectedText = expected[i].GetProperty("properties").GetRawText();
                Assert.Equal(expectedText, GeoPackagePropertiesJson.Write(model.Features[i].Properties));
            }
        }

        [Theory]
        [MemberData(nameof(Samples))]
        public void Properties_ParsedAreJsonEqualToThePrettyGeoJsonProperties_InTheSameOrder(string sample)
        {
            IwxxmDocument document = Parse(sample);
            GeoPackageDocumentModel model = GeoPackageFeatureSource.Read(document);
            var geoJson = IwxxmGeoJsonExporter.ToGeoJson(document, GeoJsonExportOptions.Pretty);
            Assert.True(geoJson.IsSuccess);
            using JsonDocument parsed = JsonDocument.Parse(geoJson.Value);
            JsonElement[] expected = parsed.RootElement.GetProperty("features").EnumerateArray().ToArray();

            for (int i = 0; i < expected.Length; i++)
            {
                using JsonDocument actual = JsonDocument.Parse(GeoPackagePropertiesJson.Write(model.Features[i].Properties));
                AssertJsonEqual(expected[i].GetProperty("properties"), actual.RootElement, sample + " feature " + i);
            }
        }

        [Fact]
        public void Samples_IncludeFeaturesWithArraysAndMeasures()
        {
            var all = SampleFiles.All().Select(a => (string)a[0])
                .SelectMany(n => GeoPackageFeatureSource.Read(Parse(n)).Features)
                .Select(f => GeoPackagePropertiesJson.Write(f.Properties))
                .ToList();

            Assert.Contains(all, text => text.Contains("[") && text.Contains("]"));
            Assert.Contains(all, text => text.Contains("Uom"));
        }

        [Fact]
        public void Write_NoProperties_IsAnEmptyObject() =>
            Assert.Equal("{}", GeoPackagePropertiesJson.Write(new List<(string, object?)>()));

        [Fact]
        public void Write_NullProperties_Throws() =>
            Assert.Throws<ArgumentNullException>(() => GeoPackagePropertiesJson.Write(null!));

        [Fact]
        public void Write_OmitsNullValuesAndKeepsInsertionOrder()
        {
            var properties = new List<(string, object?)> { ("z", "last-alphabetically-first"), ("a", null), ("m", true), ("b", 7) };

            Assert.Equal("{\"z\":\"last-alphabetically-first\",\"m\":true,\"b\":7}", GeoPackagePropertiesJson.Write(properties));
        }

        [Theory]
        [InlineData(0.2, "0.2")]
        [InlineData(1e-7, "1E-07")]
        [InlineData(1e21, "1E+21")]
        [InlineData(-0.5, "-0.5")]
        [InlineData(0.1 + 0.2, "0.30000000000000004")]
        [InlineData(1.5e300, "1.5E+300")]
        [InlineData(9007199254740992.0, "9007199254740992")]
        [InlineData(123456.789, "123456.789")]
        [InlineData(0.0, "0")]
        public void Write_Numbers_UseTheShortestRoundTripText(double value, string expected)
        {
            string text = GeoPackagePropertiesJson.Write(new List<(string, object?)> { ("v", value) });

            Assert.Equal("{\"v\":" + expected + "}", text);
            using JsonDocument parsed = JsonDocument.Parse(text);
            Assert.Equal(value, parsed.RootElement.GetProperty("v").GetDouble());
        }

        [Fact]
        public void Write_NonFiniteNumbers_AreNull()
        {
            var properties = new List<(string, object?)> { ("n", double.NaN), ("i", double.PositiveInfinity), ("l", new double[] { 1, double.NegativeInfinity }) };

            Assert.Equal("{\"n\":null,\"i\":null,\"l\":[1,null]}", GeoPackagePropertiesJson.Write(properties));
        }

        [Fact]
        public void Write_NestedArrays_AreCompact()
        {
            var properties = new List<(string, object?)>
            {
                ("center", new double[] { -179.5, 12.25 }),
                ("flags", new List<string?> { "A", null, "C" }),
                ("empty", new string[0]),
            };

            Assert.Equal("{\"center\":[-179.5,12.25],\"flags\":[\"A\",null,\"C\"],\"empty\":[]}", GeoPackagePropertiesJson.Write(properties));
        }

        [Fact]
        public void Write_NonAsciiAndControlCharacters_FollowTheGeoJsonEscaping()
        {
            string value = "Z\u00fcrich \u6771\u4eac \U0001F30D \"q\" \\ \n\t\u0001 \u2028";
            string text = GeoPackagePropertiesJson.Write(new List<(string, object?)> { ("name", value), ("na\u00efve", "x") });

            Assert.Equal("{\"name\":\"Z\u00fcrich \u6771\u4eac \U0001F30D \\\"q\\\" \\\\ \\n\\t\\u0001 \\u2028\",\"na\u00efve\":\"x\"}", text);
            using JsonDocument parsed = JsonDocument.Parse(text);
            Assert.Equal(value, parsed.RootElement.GetProperty("name").GetString());
        }

        private static void AssertJsonEqual(JsonElement expected, JsonElement actual, string where)
        {
            Assert.True(expected.ValueKind == actual.ValueKind, where + ": kind");
            switch (expected.ValueKind)
            {
                case JsonValueKind.Object:
                    JsonProperty[] e = expected.EnumerateObject().ToArray();
                    JsonProperty[] a = actual.EnumerateObject().ToArray();
                    Assert.Equal(e.Select(p => p.Name), a.Select(p => p.Name));
                    for (int i = 0; i < e.Length; i++)
                        AssertJsonEqual(e[i].Value, a[i].Value, where + "/" + e[i].Name);
                    break;
                case JsonValueKind.Array:
                    JsonElement[] ea = expected.EnumerateArray().ToArray();
                    JsonElement[] aa = actual.EnumerateArray().ToArray();
                    Assert.Equal(ea.Length, aa.Length);
                    for (int i = 0; i < ea.Length; i++)
                        AssertJsonEqual(ea[i], aa[i], where + "[" + i + "]");
                    break;
                case JsonValueKind.String:
                    Assert.True(expected.GetString() == actual.GetString(), where);
                    break;
                case JsonValueKind.Number:
                    Assert.Equal(expected.GetDouble(), actual.GetDouble());
                    break;
            }
        }
    }
}
