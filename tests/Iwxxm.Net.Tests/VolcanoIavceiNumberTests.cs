using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Iwxxm.Net.Reports;
using Xunit;

namespace Iwxxm.Net.Tests
{
    /// <summary>
    /// The volcano identifier is the IAVCEI number (<c>iwxxm:IAVCEINumber</c>): the domain member is
    /// <c>IavceiNumber</c> and the GeoJSON property is <c>iavceiNumber</c>; <c>wmoNumber</c> is never written.
    /// </summary>
    public sealed class VolcanoIavceiNumberTests
    {
        private static void Collect(JsonElement element, string name, List<JsonElement> found)
        {
            if (element.ValueKind == JsonValueKind.Object)
            {
                foreach (JsonProperty p in element.EnumerateObject())
                {
                    if (p.Name == name)
                    {
                        found.Add(p.Value);
                    }

                    Collect(p.Value, name, found);
                }
            }
            else if (element.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement item in element.EnumerateArray())
                {
                    Collect(item, name, found);
                }
            }
        }

        private static List<JsonElement> Find(string json, string name)
        {
            using JsonDocument doc = JsonDocument.Parse(json);
            var found = new List<JsonElement>();
            Collect(doc.RootElement, name, found);
            return found.Select(e => e.Clone()).ToList();
        }

        private static string GeoJson(Result<string> result)
        {
            Assert.True(result.IsSuccess, string.Join(" | ", result.Errors));
            return result.Value;
        }

        [Fact]
        public void SigmetVolcanicAsh_GeoJsonUsesIavceiNumber_AndNeverWmoNumber()
        {
            string json = GeoJson(IwxxmParser.ParseFileToGeoJson("Samples/wmo-examples/sigmet-VA-EGGX.xml"));

            Assert.Contains("ERUPTING_VOLCANO", json);
            Assert.Empty(Find(json, "iavceiNumber")); // SIGMET volcanoes never carry the number; null properties are omitted
            Assert.Empty(Find(json, "wmoNumber"));
        }

        [Fact]
        public void Vona_GeoJsonUsesIavceiNumber_AndNeverWmoNumber()
        {
            string json = GeoJson(IwxxmParser.ParseFileToGeoJson("Samples/wmo-examples/vona-A7-1.xml"));

            Assert.Contains(Find(json, "iavceiNumber"), v => v.ValueKind == JsonValueKind.String && v.GetString() == "300130");
            Assert.Empty(Find(json, "wmoNumber"));
        }

        [Fact]
        public void Qvaci_GeoJsonUsesIavceiNumber_AndNeverWmoNumber()
        {
            string json = GeoJson(IwxxmParser.ParseFileToGeoJson("Samples/wmo-examples/qvaci-Example.xml"));

            Assert.Contains(Find(json, "iavceiNumber"), v => v.ValueKind == JsonValueKind.String && v.GetString() == "262000");
            Assert.Empty(Find(json, "wmoNumber"));
        }

        [Fact]
        public void WafsSigwx_VolcanoWithoutNumber_WritesNeitherProperty()
        {
            string json = GeoJson(IwxxmParser.ParseFileToGeoJson("Samples/wmo-examples/WAFS-Example.xml"));

            Assert.Empty(Find(json, "iavceiNumber"));
            Assert.Empty(Find(json, "wmoNumber"));
        }

        [Fact]
        public void CompactSigwx_GeoJsonCarriesTheLegacyWmoNumberElementAsIavceiNumber()
        {
            string json = GeoJson(IwxxmParser.ParseFileToGeoJson("Samples/sigwx-swh-2025-2.xml"));

            Assert.Contains(Find(json, "iavceiNumber"), v => v.ValueKind == JsonValueKind.String && v.GetString() == "211060");
        }

        [Fact]
        public void CompactSigwx_LegacyWmoNumberElement_MapsIntoIavceiNumber()
        {
            Result<Sigwx> result = IwxxmSigwxParser.ParseFile("Samples/sigwx-swh-2025-2.xml");

            Assert.True(result.IsSuccess, string.Join(" | ", result.Errors));
            Assert.Equal("211060", Assert.Single(result.Value.Volcanoes).IavceiNumber);
        }

        [Fact]
        public void OfficialIavceiElement_MapsIntoIavceiNumber_AndSigmetVolcanoHasNone()
        {
            var vona = IwxxmParser.ParseFileAs<Vona>("Samples/wmo-examples/vona-A7-1.xml");
            Assert.True(vona.IsSuccess, string.Join(" | ", vona.Errors));
            Assert.Equal("300130", vona.Value.Volcanoes[0].IavceiNumber);

            var sigmet = IwxxmParser.ParseFileAs<Sigmet>("Samples/wmo-examples/sigmet-VA-EGGX.xml");
            Assert.True(sigmet.IsSuccess, string.Join(" | ", sigmet.Errors));
            Assert.Null(sigmet.Value.EruptingVolcano!.IavceiNumber);
        }
    }
}
