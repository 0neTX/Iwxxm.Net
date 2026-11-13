using System.Linq;
using System.Text.Json;
using System.Xml.Linq;
using Iwxxm.Net.Reports;
using Xunit;
using static Iwxxm.Net.Tests.DataFidelity.SampleDocuments;

namespace Iwxxm.Net.Tests.DataFidelity
{
    /// <summary>
    /// The SIGMET/AIRMET <c>intensityChange</c> attribute and the tropical cyclone advisory <c>intensityChange</c> element
    /// map to the one <see cref="IntensityChange"/> enumeration; an unsupported token is a mapping error, never passed through.
    /// </summary>
    public sealed class IntensityChangeTests
    {
        [Fact]
        public void Sigmet_ValidTokens_Map()
        {
            Sigmet weaken = Assert.IsType<Sigmet>(IwxxmParser.ParseFile(PathOf("sigmet-A6-1a-TS.xml")).Value);
            Assert.Equal(IntensityChange.Weaken, Assert.Single(weaken.EvolvingConditions).IntensityChange);

            Sigmet noChange = Assert.IsType<Sigmet>(IwxxmParser.ParseFile(PathOf("sigmet-VA-EGGX.xml")).Value);
            Assert.Equal(IntensityChange.NoChange, noChange.EvolvingConditions[0].IntensityChange);
        }

        [Fact]
        public void Airmet_ValidToken_Maps()
        {
            Airmet airmet = Assert.IsType<Airmet>(IwxxmParser.ParseFile(PathOf("airmet-A6-1a-TS.xml")).Value);
            Assert.Equal(IntensityChange.Weaken, Assert.Single(airmet.EvolvingConditions).Volume.IntensityChange);
        }

        [Fact]
        public void TropicalCycloneAdvisory_ValidToken_Maps()
        {
            Tca tca = Assert.IsType<Tca>(IwxxmParser.ParseFile(PathOf("tc-advisory-A2-2.xml")).Value);
            Assert.Equal(IntensityChange.Intensify, tca.Observation!.IntensityChange);
        }

        [Theory]
        [InlineData("sigmet-A6-1a-TS.xml", "SIGMETEvolvingCondition", "ACL-ENROUTE-INTENSITY-CHANGE-INVALID")]
        [InlineData("airmet-A6-1a-TS.xml", "AIRMETEvolvingCondition", "ACL-ENROUTE-INTENSITY-CHANGE-INVALID")]
        public void EnRoute_UnsupportedToken_IsAMappingError(string file, string element, string code)
        {
            XDocument doc = Load(file);
            doc.Descendants(IwxxmNs + element).First().SetAttributeValue("intensityChange", "WKN");

            var result = MapDirectly(doc);

            Assert.True(result.IsFailure);
            Assert.Contains(result.Errors, e => e.Code == code && e.Message.Contains("'WKN'"));
        }

        [Fact]
        public void TropicalCycloneAdvisory_UnsupportedToken_IsAMappingError()
        {
            XDocument doc = Load("tc-advisory-A2-2.xml");
            doc.Descendants(IwxxmNs + "intensityChange").First().Value = "WKN";

            var result = MapDirectly(doc);

            Assert.True(result.IsFailure);
            Assert.Contains(result.Errors, e => e.Code == "ACL-TCA-VALUE-INVALID");
        }

        [Theory]
        [InlineData("sigmet-A6-1a-TS.xml", "WEAKEN")]
        [InlineData("sigmet-VA-EGGX.xml", "NO_CHANGE")]
        [InlineData("airmet-A6-1a-TS.xml", "WEAKEN")]
        public void GeoJson_KeepsTheSchemaToken(string file, string expected)
        {
            var result = IwxxmParser.ParseToGeoJson(Text(file));

            Assert.True(result.IsSuccess, string.Join(" | ", result.Errors));
            using JsonDocument json = JsonDocument.Parse(result.Value);
            Assert.Contains(
                json.RootElement.GetProperty("features").EnumerateArray(),
                f => f.GetProperty("properties").TryGetProperty("intensityChange", out JsonElement p) && p.GetString() == expected);
        }
    }
}
