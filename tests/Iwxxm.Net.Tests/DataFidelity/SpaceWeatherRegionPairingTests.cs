using System.Linq;
using System.Text.Json;
using System.Xml.Linq;
using Iwxxm.Net.Infrastructure.Mapping.Xml;
using Iwxxm.Net.Reports;
using Xunit;
using static Iwxxm.Net.Tests.DataFidelity.SampleDocuments;

namespace Iwxxm.Net.Tests.DataFidelity
{
    /// <summary>
    /// Each <c>iwxxm:SpaceWeatherRegion</c> pairs its own location indicators with its own extent and vertical limits;
    /// the GeoJSON output carries only the indicators of the region a polygon belongs to.
    /// </summary>
    public sealed class SpaceWeatherRegionPairingTests
    {
        private static readonly string[] SevereOrder = { "MNH", "EQN", "EQS", "MSH", "DAYSIDE" };

        private static JsonElement[] Features(string xml, out JsonDocument doc)
        {
            var result = IwxxmParser.ParseToGeoJson(xml);
            Assert.True(result.IsSuccess, string.Join(" | ", result.Errors));
            doc = JsonDocument.Parse(result.Value);
            return doc.RootElement.GetProperty("features").EnumerateArray()
                .Where(f => f.GetProperty("properties").GetProperty("featureType").GetString() == "SPACE_WEATHER_REGION")
                .ToArray();
        }

        private static string[] Indicators(JsonElement feature) =>
            feature.GetProperty("properties").GetProperty("locationIndicatorUris").EnumerateArray()
                .Select(u => u.GetString()!.Substring(u.GetString()!.LastIndexOf('/') + 1)).ToArray();

        [Fact]
        public void A73_EachPolygon_CarriesOnlyItsOwnIndicator()
        {
            JsonElement[] features = Features(Text("spacewx-A7-3.xml"), out JsonDocument doc);
            using (doc)
            {
                JsonElement[] severe = features.Where(f => f.GetProperty("properties").GetProperty("intensity").GetString() == "SEV").ToArray();
                Assert.Equal(5, severe.Length);
                for (int i = 0; i < 5; i++)
                {
                    Assert.EndsWith($"#poly-{i + 1}", severe[i].GetProperty("id").GetString());
                    Assert.Equal(new[] { SevereOrder[i] }, Indicators(severe[i]));
                }
            }
        }

        [Fact]
        public void A73_DomainRegions_PairIndicatorAndArea()
        {
            SpaceWx swx = Assert.IsType<SpaceWx>(IwxxmParser.ParseFileAs<SpaceWx>(PathOf("spacewx-A7-3.xml")).Value);
            SpaceWeatherIntensityAndRegion severe = swx.Analyses[0].IntensityAndRegions.Single(g => g.Intensity == "SEV");

            Assert.Equal(SevereOrder, severe.Regions.Select(r => Assert.Single(r.LocationIndicators).Notation).ToArray());
            Assert.All(severe.Regions, r => Assert.Single(r.Areas));
        }

        [Fact]
        public void A73_Nightside_IsAFeatureWithNullGeometryAndItsIndicator()
        {
            JsonElement[] features = Features(Text("spacewx-A7-3.xml"), out JsonDocument doc);
            using (doc)
            {
                JsonElement night = Assert.Single(features, f => f.GetProperty("geometry").ValueKind == JsonValueKind.Null);
                Assert.Contains("#noextent-1", night.GetProperty("id").GetString());
                Assert.Equal(new[] { "NIGHTSIDE" }, Indicators(night));
                Assert.Equal("MOD", night.GetProperty("properties").GetProperty("intensity").GetString());
            }
        }

        [Fact]
        public void A75_ReadsTheLowerLimit_AndTheNilIndicatorIsNotKept()
        {
            SpaceWx swx = Assert.IsType<SpaceWx>(IwxxmParser.ParseFileAs<SpaceWx>(PathOf("spacewx-A7-5.xml")).Value);
            SpaceWeatherRegion region = Assert.Single(Assert.Single(swx.Analyses[0].IntensityAndRegions).Regions);

            Assert.Empty(region.LocationIndicators);
            Assert.Single(region.Areas);
            Assert.Equal(400.0, region.LowerLimit!.Value.Amount);
            Assert.Equal("FL", region.LowerLimit.Value.UnitOfMeasure);
            Assert.True(region.UpperLimit!.Value.IsNil);

            JsonElement[] features = Features(Text("spacewx-A7-5.xml"), out JsonDocument doc);
            using (doc)
            {
                JsonElement props = Assert.Single(features).GetProperty("properties");
                Assert.Equal(400.0, props.GetProperty("lowerLimit").GetDouble());
                Assert.False(props.TryGetProperty("locationIndicatorUris", out _));
            }
        }

        [Fact]
        public void RegionWithTwoIndicators_AndRegionsWithDifferentIndicators_StayPaired()
        {
            XDocument xml = Load("spacewx-A7-3.xml");
            XElement severeGroup = xml.Descendants(IwxxmNs + "SpaceWeatherIntensityAndRegion")
                .First(g => (string?)g.Element(IwxxmNs + "intensity") == "SEV");
            XElement first = severeGroup.Descendants(IwxxmNs + "SpaceWeatherRegion").First();
            XElement indicator = first.Element(IwxxmNs + "locationIndicator")!;
            var second = new XElement(indicator);
            second.SetAttributeValue(XlinkNs + "href", "http://codes.wmo.int/49-2/SpaceWxLocation/HSH");
            indicator.AddAfterSelf(second);

            SpaceWx swx = Assert.IsType<SpaceWx>(MapDirectly(xml).Value);
            SpaceWeatherIntensityAndRegion group = swx.Analyses[0].IntensityAndRegions.Single(g => g.Intensity == "SEV");
            Assert.Equal(new[] { "MNH", "HSH" }, group.Regions[0].LocationIndicators.Select(l => l.Notation).ToArray());
            Assert.Equal(new[] { "EQN" }, group.Regions[1].LocationIndicators.Select(l => l.Notation).ToArray());

            JsonElement[] features = Features(xml.ToString(SaveOptions.DisableFormatting), out JsonDocument doc);
            using (doc)
            {
                JsonElement[] severe = features.Where(f => f.GetProperty("properties").GetProperty("intensity").GetString() == "SEV").ToArray();
                Assert.Equal(new[] { "MNH", "HSH" }, Indicators(severe[0]));
                Assert.Equal(new[] { "EQN" }, Indicators(severe[1]));
            }
        }

        [Fact]
        public void Misalignment_IsImpossibleByConstruction_AreasAndIndicatorsLiveInTheSameRegion()
        {
            var a = new SpaceWeatherRegion("a", new[] { new CodeListValue<SpaceWeatherLocation>(SpaceWeatherLocation.HighLatitudesNorth, "HNH") });
            var b = new SpaceWeatherRegion("b", new[] { new CodeListValue<SpaceWeatherLocation>(SpaceWeatherLocation.Nightside, "NIGHTSIDE") });
            var group = new SpaceWeatherIntensityAndRegion("g", "MOD", new[] { a, b });

            Assert.Equal(2, group.Regions.Count);
            Assert.NotEqual(group.Regions[0], group.Regions[1]);
            Assert.Null(typeof(SpaceWeatherIntensityAndRegion).GetProperty("LocationIndicators"));
            Assert.Null(typeof(SpaceWeatherIntensityAndRegion).GetProperty("Areas"));
        }
    }
}
