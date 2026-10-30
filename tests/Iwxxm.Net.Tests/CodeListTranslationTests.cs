using System;
using System.IO;
using System.Linq;
using Iwxxm.Net.Infrastructure.Mapping.CodeLists;
using Iwxxm.Net.Reports;
using Xunit;

namespace Iwxxm.Net.Tests
{
    /// <summary>
    /// Verifies the Anti-Corruption Layer translation of WMO code-list references into Domain values (DDD-10):
    /// every translation table equals the codes valid for IWXXM 2025-2 in the embedded official RDF file (the source of
    /// truth), unknown codes are kept as <c>Unknown</c> with their raw value, and Linked-Data outputs can rebuild the exact
    /// original reference.
    /// </summary>
    public sealed class CodeListTranslationTests
    {
        private const string MetarPath = "Samples/wmo-examples/metar-A3-1.xml";

        [Fact]
        public void AerodromeCloudAmountTable_MatchesTheOfficialRdf() => AssertTableMatchesOfficialRdf(CodeListTables.AerodromeCloudAmounts);

        [Fact]
        public void ConvectiveCloudTypeTable_MatchesTheOfficialRdf() => AssertTableMatchesOfficialRdf(CodeListTables.ConvectiveCloudTypes);

        [Fact]
        public void SigmetPhenomenonTable_MatchesTheOfficialRdf() => AssertTableMatchesOfficialRdf(CodeListTables.SigmetPhenomena);

        [Fact]
        public void AirmetPhenomenonTable_MatchesTheOfficialRdf() => AssertTableMatchesOfficialRdf(CodeListTables.AirmetPhenomena);

        [Fact]
        public void AviationColourCodeTable_MatchesTheOfficialRdf() => AssertTableMatchesOfficialRdf(CodeListTables.AviationColourCodes);

        [Fact]
        public void SpaceWeatherLocationTable_MatchesTheOfficialRdf() => AssertTableMatchesOfficialRdf(CodeListTables.SpaceWeatherLocations);

        [Fact]
        public void VisibilityReductionCauseTable_MatchesTheOfficialRdf() => AssertTableMatchesOfficialRdf(CodeListTables.VisibilityReductionCauses);

        [Fact]
        public void AerodromeWeatherCodes_AreReadFromTheOfficialRdf()
        {
            Assert.Equal(AerodromeWeatherCodeList.RegisterUri, OfficialCodeListReader.ReadRegisterUri(AerodromeWeatherCodeList.ResourcePath));
            Assert.Equal(402, AerodromeWeatherCodeList.Codes.Count);
            Assert.Contains("+TSRA", AerodromeWeatherCodeList.Codes);
            Assert.Contains("-DZ", AerodromeWeatherCodeList.Codes);
            Assert.Contains("FG", AerodromeWeatherCodeList.Codes);
        }

        [Fact]
        public void CodesNotValidForIwxxm20252_ResolveToUnknown()
        {
            // DAYLIGHT_SIDE is published in the register but has no IWXXM 2025-2 version.
            CodeListValue<SpaceWeatherLocation>? value =
                CodeListTables.SpaceWeatherLocations.Translate("http://codes.wmo.int/49-2/SpaceWxLocation/DAYLIGHT_SIDE");

            Assert.NotNull(value);
            Assert.Equal(SpaceWeatherLocation.Unknown, value!.Code);
            Assert.False(value.IsKnown);
            Assert.Equal("DAYLIGHT_SIDE", value.Notation);
        }

        [Theory]
        [InlineData("http://codes.wmo.int/49-2/SigWxPhenomena/VA", SigmetPhenomenon.VolcanicAsh, "VA")]
        [InlineData("http://codes.wmo.int/49-2/SigWxPhenomena/XYZ", SigmetPhenomenon.Unknown, "XYZ")]
        [InlineData("http://codes.wmo.int/49-2/AirWxPhenomena/ISOL_TS", SigmetPhenomenon.Unknown, "http://codes.wmo.int/49-2/AirWxPhenomena/ISOL_TS")]
        [InlineData("http://codes.wmo.int/49-2/SigWxPhenomena/VA/extra", SigmetPhenomenon.Unknown, "http://codes.wmo.int/49-2/SigWxPhenomena/VA/extra")]
        [InlineData("http://codes.wmo.int/49-2/SigWxPhenomena/", SigmetPhenomenon.Unknown, "http://codes.wmo.int/49-2/SigWxPhenomena/")]
        public void Translate_KeepsUnknownCodesWithTheirRawValue_AndRebuildsTheExactReference(
            string href, SigmetPhenomenon expectedCode, string expectedNotation)
        {
            CodeListValue<SigmetPhenomenon>? value = CodeListTables.SigmetPhenomena.Translate(href);

            Assert.NotNull(value);
            Assert.Equal(expectedCode, value!.Code);
            Assert.Equal(expectedCode != SigmetPhenomenon.Unknown, value.IsKnown);
            Assert.Equal(expectedNotation, value.Notation);
            Assert.Equal(href, CodeListTables.SigmetPhenomena.ToReference(value));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Translate_AbsentReference_IsNull(string? href)
        {
            Assert.Null(CodeListTables.SigmetPhenomena.Translate(href));
            Assert.Null(CodeListTables.SigmetPhenomena.ToReference(null));
        }

        [Fact]
        public void ColourCodesOfTheRegisterNotValidFor20252_AreKeptVerbatimAsUnknown()
        {
            const string href = "http://codes.wmo.int/49-2/AviationColourCode/RED";

            CodeListValue<AviationColourCode>? value = CodeListTables.AviationColourCodes.Translate(href);

            Assert.Equal(AviationColourCode.Unknown, value!.Code);
            Assert.Equal(href, value.Notation);
            Assert.Equal(href, CodeListTables.AviationColourCodes.ToReference(value));
        }

        [Theory]
        [InlineData("http://codes.wmo.int/306/4678/+TSRA", "+TSRA", true)]
        [InlineData("http://codes.wmo.int/306/4678/ZZZ", "ZZZ", false)]
        [InlineData("http://codes.wmo.int/49-2/AerodromePresentOrForecastWeather/DZ", "http://codes.wmo.int/49-2/AerodromePresentOrForecastWeather/DZ", false)]
        public void AerodromeWeather_TranslatesAndRebuildsTheExactReference(string href, string notation, bool isKnown)
        {
            AerodromeWeather value = AerodromeWeatherCodeList.Translate(href);

            Assert.Equal(new AerodromeWeather(notation, isKnown), value);
            Assert.Equal(href, AerodromeWeatherCodeList.ToReference(value));
        }

        [Fact]
        public void Mapping_TranslatesOfficialAndUnknownCodes_AndGeoJsonKeepsTheOriginalReferences()
        {
            string xml = File.ReadAllText(MetarPath)
                .Replace("xlink:href=\"http://codes.wmo.int/306/4678/DZ\"", "xlink:href=\"http://codes.wmo.int/306/4678/-DZ\"")
                .Replace("xlink:href=\"http://codes.wmo.int/49-2/CloudAmountReportedAtAerodrome/OVC\"", "xlink:href=\"http://codes.wmo.int/bufr4/codeflag/0-20-008/4\"");

            var result = IwxxmParser.ParseAs<AerodromeObservation>(xml);

            Assert.True(result.IsSuccess, string.Join(" | ", result.Errors));
            AerodromeObservation metar = result.Value;
            Assert.Equal(new AerodromeWeather("-DZ", isKnown: true), metar.PresentWeather[0]);
            Assert.Equal(new AerodromeWeather("FG", isKnown: true), metar.PresentWeather[1]);
            Assert.Equal(new CodeListValue<AerodromeCloudAmount>(AerodromeCloudAmount.Scattered, "SCT"), metar.CloudLayers[0].Amount);
            // A reference into another register is not dropped: Unknown, kept verbatim.
            Assert.Equal(
                new CodeListValue<AerodromeCloudAmount>(AerodromeCloudAmount.Unknown, "http://codes.wmo.int/bufr4/codeflag/0-20-008/4"),
                metar.CloudLayers[1].Amount);

            string geoJson = IwxxmGeoJsonExporter.ToGeoJson(metar, new GeoJsonExportOptions(indented: false, includeBoundingBox: false));
            Assert.Contains("\"presentWeatherUris\":[\"http://codes.wmo.int/306/4678/-DZ\",\"http://codes.wmo.int/306/4678/FG\"]", geoJson);
        }

        private static void AssertTableMatchesOfficialRdf<TCode>(CodeListTable<TCode> table)
            where TCode : struct, Enum
        {
            Assert.Equal(table.RegisterUri, OfficialCodeListReader.ReadRegisterUri(table.ResourcePath));
            var official = OfficialCodeListReader.ReadCodes(table.ResourcePath, table.RegisterUri);

            Assert.NotEmpty(official);
            Assert.Equal(official.OrderBy(c => c, StringComparer.Ordinal), table.Codes.Keys.OrderBy(c => c, StringComparer.Ordinal));

            Assert.Equal(0, Convert.ToInt32(default(TCode)));
            Assert.Equal("Unknown", default(TCode).ToString());
            TCode[] members = Enum.GetValues(typeof(TCode)).Cast<TCode>().Where(m => !m.Equals(default(TCode))).ToArray();
            Assert.Equal(members.OrderBy(m => m), table.Codes.Values.OrderBy(m => m));

            foreach (var entry in table.Codes)
            {
                string href = table.RegisterUri + "/" + entry.Key;
                CodeListValue<TCode>? value = table.Translate(href);
                Assert.Equal(new CodeListValue<TCode>(entry.Value, entry.Key), value);
                Assert.True(value!.IsKnown);
                Assert.Equal(href, table.ToReference(value));
            }
        }
    }
}
