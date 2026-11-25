using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Iwxxm.Net.Reports;
using Xunit;

namespace Iwxxm.Net.Tests
{
    /// <summary>
    /// The levels of the 80 kt isotach below and above the maximum wind (<see cref="JetStreamCorePoint.LowerIsotachLimit"/> and
    /// <see cref="JetStreamCorePoint.UpperIsotachLimit"/>) are exported on <c>JETSTREAM_CORE_POINT</c> features as
    /// <c>lowerIsotachLevel</c> / <c>upperIsotachLevel</c>, written like <c>level</c> (value, <c>Uom</c>, <c>Reference</c>), right
    /// after <c>windSpeedUom</c>, and only when the symbol reports them.
    /// </summary>
    public sealed class JetStreamIsotachGeoJsonTests
    {
        private const string WafsPath = "Samples/wmo-examples/WAFS-Example.xml";
        private const string CompactPath = "Samples/sigwx-swh-2025-2.xml";
        private const string WafsJetStreamId = "uuid.8c3d3467-e398-4627-ab23-f9e57c2215b5";

        private static List<JsonProperty> CorePointProperties(string path, string featureId, out JsonDocument keepAlive)
        {
            Result<IwxxmDocument> parsed = IwxxmParser.ParseFile(path);
            Assert.True(parsed.IsSuccess, string.Join(" | ", parsed.Errors));
            Result<string> exported = IwxxmGeoJsonExporter.ToGeoJson(parsed.Value);
            Assert.True(exported.IsSuccess, string.Join(" | ", exported.Errors));

            keepAlive = JsonDocument.Parse(exported.Value);
            JsonElement feature = keepAlive.RootElement.GetProperty("features").EnumerateArray()
                .Single(f => f.TryGetProperty("id", out JsonElement id) && id.GetString() == featureId);
            return feature.GetProperty("properties").EnumerateObject().ToList();
        }

        [Fact]
        public void WafsExample_CorePointWithBothIsotachs_WritesThemRightAfterWindSpeedUom()
        {
            List<JsonProperty> properties = CorePointProperties(WafsPath, WafsJetStreamId + "#core-2", out JsonDocument json);
            using (json)
            {
                string[] names = properties.Select(p => p.Name).ToArray();
                int windSpeedUom = System.Array.IndexOf(names, "windSpeedUom");

                Assert.Equal(
                    new[]
                    {
                        "lowerIsotachLevel", "lowerIsotachLevelUom", "lowerIsotachLevelReference",
                        "upperIsotachLevel", "upperIsotachLevelUom", "upperIsotachLevelReference"
                    },
                    names.Skip(windSpeedUom + 1).ToArray());

                Assert.Equal(13716, properties.Single(p => p.Name == "lowerIsotachLevel").Value.GetDouble());
                Assert.Equal("M", properties.Single(p => p.Name == "lowerIsotachLevelUom").Value.GetString());
                Assert.Equal("MSL", properties.Single(p => p.Name == "lowerIsotachLevelReference").Value.GetString());
                Assert.Equal(15240, properties.Single(p => p.Name == "upperIsotachLevel").Value.GetDouble());
                Assert.Equal("M", properties.Single(p => p.Name == "upperIsotachLevelUom").Value.GetString());
                Assert.Equal("MSL", properties.Single(p => p.Name == "upperIsotachLevelReference").Value.GetString());
            }
        }

        [Fact]
        public void WafsExample_CorePointWithoutIsotachs_EmitsNoIsotachKey()
        {
            List<JsonProperty> properties = CorePointProperties(WafsPath, WafsJetStreamId + "#core-1", out JsonDocument json);
            using (json)
            {
                Assert.DoesNotContain(properties, p => p.Name.Contains("Isotach"));
                Assert.Equal("windSpeedUom", properties.Last().Name);
            }
        }

        [Fact]
        public void WafsExample_OnlyTwoOfFiveCorePoints_CarryIsotachs_AndTheJetStreamFeatureNone()
        {
            Result<IwxxmDocument> parsed = IwxxmParser.ParseFile(WafsPath);
            using (JsonDocument json = JsonDocument.Parse(IwxxmGeoJsonExporter.ToGeoJson(parsed.Value).Value))
            {
                List<JsonElement> features = json.RootElement.GetProperty("features").EnumerateArray().ToList();
                List<JsonElement> corePoints = features.Where(f => FeatureType(f) == "JETSTREAM_CORE_POINT").ToList();
                Assert.Equal(5, corePoints.Count);

                string[] withIsotach = corePoints.Where(HasIsotach).Select(f => f.GetProperty("id").GetString()!).ToArray();
                Assert.Equal(new[] { WafsJetStreamId + "#core-2", WafsJetStreamId + "#core-4" }, withIsotach);

                Assert.DoesNotContain(features.Where(f => FeatureType(f) != "JETSTREAM_CORE_POINT"), HasIsotach);
            }
        }

        [Theory]
        [InlineData(WafsPath)]
        [InlineData(CompactPath)]
        public void EveryCorePoint_HasIsotachKeys_ExactlyWhenTheModelHasTheLimit(string path)
        {
            Result<Sigwx> parsed = IwxxmParser.ParseFileAs<Sigwx>(path);
            Assert.True(parsed.IsSuccess, string.Join(" | ", parsed.Errors));
            using (JsonDocument json = JsonDocument.Parse(IwxxmGeoJsonExporter.ToGeoJson(parsed.Value).Value))
            {
                Dictionary<string, JsonElement> byId = json.RootElement.GetProperty("features").EnumerateArray()
                    .Where(f => FeatureType(f) == "JETSTREAM_CORE_POINT")
                    .ToDictionary(f => f.GetProperty("id").GetString()!);

                int expected = 0;
                foreach (JetStream js in parsed.Value.JetStreams)
                {
                    for (int i = 0; i < js.CorePoints.Count; i++)
                    {
                        JetStreamCorePoint cp = js.CorePoints[i];
                        JsonElement properties = byId[$"{js.Id}#core-{i + 1}"].GetProperty("properties");

                        AssertLimit(properties, "lowerIsotachLevel", cp.LowerIsotachLimit);
                        AssertLimit(properties, "upperIsotachLevel", cp.UpperIsotachLimit);
                        if (cp.LowerIsotachLimit.HasValue || cp.UpperIsotachLimit.HasValue)
                            expected++;
                    }
                }

                Assert.True(expected > 0);
                Assert.Equal(expected, byId.Values.Count(HasIsotach));
            }
        }

        [Fact]
        public void CompactSample_WritesFlightLevelIsotachsWithoutAReference()
        {
            List<JsonProperty> properties = CorePointProperties(CompactPath, "js-north-atlantic-01#core-1", out JsonDocument json);
            using (json)
            {
                string[] names = properties.Select(p => p.Name).ToArray();
                Assert.Equal(
                    new[] { "windSpeedUom", "lowerIsotachLevel", "lowerIsotachLevelUom", "upperIsotachLevel", "upperIsotachLevelUom" },
                    names.Skip(System.Array.IndexOf(names, "windSpeedUom")).ToArray());
                Assert.Equal(310, properties.Single(p => p.Name == "lowerIsotachLevel").Value.GetDouble());
                Assert.Equal("FL", properties.Single(p => p.Name == "lowerIsotachLevelUom").Value.GetString());
                Assert.Equal(400, properties.Single(p => p.Name == "upperIsotachLevel").Value.GetDouble());
            }
        }

        // The two wind symbols with isotachs of the WAFS example, rewritten to the shapes a vertical limit can take.
        private static List<JsonProperty> CorePointPropertiesOfModifiedWafs(string featureSuffix, out JsonDocument keepAlive, params (string From, string To)[] replacements)
        {
            string xml = File.ReadAllText(WafsPath).Replace("\r\n", "\n");
            foreach ((string from, string to) in replacements)
            {
                Assert.Contains(from, xml, System.StringComparison.Ordinal);
                xml = xml.Replace(from, to);
            }

            Result<IwxxmDocument> parsed = IwxxmParser.Parse(xml);
            Assert.True(parsed.IsSuccess, string.Join(" | ", parsed.Errors));
            keepAlive = JsonDocument.Parse(IwxxmGeoJsonExporter.ToGeoJson(parsed.Value).Value);
            JsonElement feature = keepAlive.RootElement.GetProperty("features").EnumerateArray()
                .Single(f => f.TryGetProperty("id", out JsonElement id) && id.GetString() == WafsJetStreamId + featureSuffix);
            return feature.GetProperty("properties").EnumerateObject().ToList();
        }

        [Fact]
        public void AnIsotachInFeetOnTheStandardReference_WritesTheReferenceAndTheFlightLevel()
        {
            List<JsonProperty> properties = CorePointPropertiesOfModifiedWafs(
                "#core-2",
                out JsonDocument json,
                ("<iwxxm:IsotachUpperElevation uom=\"M\">15240</iwxxm:IsotachUpperElevation>\n                    <iwxxm:IsotachUpperVerticalReference>MSL</iwxxm:IsotachUpperVerticalReference>",
                 "<iwxxm:IsotachUpperElevation uom=\"FT\">50000</iwxxm:IsotachUpperElevation>\n                    <iwxxm:IsotachUpperVerticalReference>STD</iwxxm:IsotachUpperVerticalReference>"));
            using (json)
            {
                string[] names = properties.Select(p => p.Name).ToArray();
                Assert.Equal(
                    new[]
                    {
                        "lowerIsotachLevel", "lowerIsotachLevelUom", "lowerIsotachLevelReference",
                        "upperIsotachLevel", "upperIsotachLevelUom", "upperIsotachLevelReference", "upperIsotachLevelFlightLevel"
                    },
                    names.Skip(System.Array.IndexOf(names, "windSpeedUom") + 1).ToArray());
                Assert.Equal(50000, properties.Single(p => p.Name == "upperIsotachLevel").Value.GetDouble());
                Assert.Equal("FT", properties.Single(p => p.Name == "upperIsotachLevelUom").Value.GetString());
                Assert.Equal("STD", properties.Single(p => p.Name == "upperIsotachLevelReference").Value.GetString());
                Assert.Equal("FL500", properties.Single(p => p.Name == "upperIsotachLevelFlightLevel").Value.GetString());
                Assert.Equal(13716, properties.Single(p => p.Name == "lowerIsotachLevel").Value.GetDouble());
            }
        }

        [Fact]
        public void ANilIsotach_WritesTheNilReasonAndNoNumber()
        {
            List<JsonProperty> properties = CorePointPropertiesOfModifiedWafs(
                "#core-4",
                out JsonDocument json,
                ("<iwxxm:IsotachLowerElevation uom=\"M\">14326</iwxxm:IsotachLowerElevation>\n                    <iwxxm:IsotachLowerVerticalReference>MSL</iwxxm:IsotachLowerVerticalReference>",
                 "<iwxxm:IsotachLowerElevation xsi:nil=\"true\" nilReason=\"http://codes.wmo.int/iwxxm/nil/inapplicable\"/>"));
            using (json)
            {
                Assert.DoesNotContain(properties, p => p.Name == "lowerIsotachLevel");
                Assert.Equal("http://codes.wmo.int/iwxxm/nil/inapplicable", properties.Single(p => p.Name == "lowerIsotachLevelNilReason").Value.GetString());
                Assert.Equal(16154, properties.Single(p => p.Name == "upperIsotachLevel").Value.GetDouble());
            }
        }

        internal static string FeatureType(JsonElement feature) =>
            feature.GetProperty("properties").GetProperty("featureType").GetString()!;

        internal static bool HasIsotach(JsonElement feature) =>
            feature.GetProperty("properties").EnumerateObject().Any(p => p.Name.Contains("Isotach"));

        private static void AssertOptionalText(JsonElement properties, string key, string? expected)
        {
            if (expected == null)
                Assert.False(properties.TryGetProperty(key, out _), key + " must be absent");
            else
                Assert.Equal(expected, properties.GetProperty(key).GetString());
        }

        internal static void AssertLimit(JsonElement properties, string key, VerticalLimit? limit)
        {
            if (!limit.HasValue)
            {
                Assert.False(properties.TryGetProperty(key, out _), key + " must be absent");
                Assert.False(properties.TryGetProperty(key + "Uom", out _), key + "Uom must be absent");
                Assert.False(properties.TryGetProperty(key + "Reference", out _), key + "Reference must be absent");
                return;
            }

            Assert.True(limit.Value.Amount.HasValue);
            Assert.Equal(limit.Value.Amount!.Value, properties.GetProperty(key).GetDouble());
            AssertOptionalText(properties, key + "Uom", limit.Value.UnitOfMeasure);
            AssertOptionalText(properties, key + "Reference", limit.Value.ReferenceCode);
        }
    }
}
