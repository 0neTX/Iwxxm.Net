using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Iwxxm.Net.Infrastructure.Mapping.CodeLists;
using Iwxxm.Net.Reports;
using Xunit;

namespace Iwxxm.Net.Tests
{
    /// <summary>
    /// Data-driven tests that iterate every concept of the official WMO BUFR code tables used by IWXXM 2025-2
    /// and assert that <see cref="WmoCodeRegistry"/> resolves each official code to the enum member that represents
    /// its official meaning. The RDF files under <c>TestData/OfficialCodelists</c> are byte-identical copies of
    /// <c>IWXXM/rule/*.rdf</c> from https://github.com/wmo-im/iwxxm.
    /// </summary>
    public sealed class WmoCodeRegistryTests
    {
        private const string CodelistDirectory = "TestData/OfficialCodelists";
        private static readonly XNamespace Skos = "http://www.w3.org/2004/02/skos/core#";
        private static readonly XNamespace Rdf = "http://www.w3.org/1999/02/22-rdf-syntax-ns#";
        private static readonly XNamespace DcTerms = "http://purl.org/dc/terms/";

        /// <summary>
        /// Official WMO FM 94 BUFR code table 0 20 008 (http://codes.wmo.int/bufr4/codeflag/0-20-008),
        /// transcribed from wmo-im/iwxxm IWXXM/rule/codes.wmo.int-bufr4-codeflag-0-20-008.rdf:
        /// code (skos:notation), meaning (dcterms:description [+ skos:note]) and the expected enum member.
        /// </summary>
        public static readonly TheoryData<int, string, CloudDistributionCode> OfficialCloudDistributionTable = new TheoryData<int, string, CloudDistributionCode>
        {
            { 0, "Sky clear", CloudDistributionCode.SkyClear },
            { 1, "Few", CloudDistributionCode.Few },
            { 2, "Scattered", CloudDistributionCode.Scattered },
            { 3, "Broken", CloudDistributionCode.Broken },
            { 4, "Overcast", CloudDistributionCode.Overcast },
            { 6, "Scattered/broken", CloudDistributionCode.ScatteredBroken },
            { 7, "Broken/overcast", CloudDistributionCode.BrokenOvercast },
            { 8, "Isolated", CloudDistributionCode.Isolated },
            { 9, "Isolated embedded", CloudDistributionCode.IsolatedEmbedded },
            { 10, "Occasional", CloudDistributionCode.Occasional },
            { 11, "Occasional embedded", CloudDistributionCode.OccasionalEmbedded },
            { 12, "Frequent", CloudDistributionCode.Frequent },
            { 13, "Dense", CloudDistributionCode.Dense },
            { 14, "Layers", CloudDistributionCode.Layers },
            { 15, "Obscured (OBSC)", CloudDistributionCode.Obscured },
            { 16, "Embedded (EMBD)", CloudDistributionCode.Embedded },
            { 17, "Frequent embedded", CloudDistributionCode.FrequentEmbedded },
            { 31, "Missing value", CloudDistributionCode.MissingValue },
        };
        /// <summary>
        /// Official WMO FM 94 BUFR code table 0 20 012 (http://codes.wmo.int/bufr4/codeflag/0-20-012),
        /// transcribed from wmo-im/iwxxm IWXXM/rule/codes.wmo.int-bufr4-codeflag-0-20-012.rdf:
        /// code (skos:notation), meaning (dcterms:description [+ skos:note]) and the expected enum member.
        /// </summary>
        public static readonly TheoryData<int, string, CloudTypeCode> OfficialCloudTypeTable = new TheoryData<int, string, CloudTypeCode>
        {
            { 0, "Cirrus (Ci)", CloudTypeCode.Cirrus },
            { 1, "Cirrocumulus (Cc)", CloudTypeCode.Cirrocumulus },
            { 2, "Cirrostratus (Cs)", CloudTypeCode.Cirrostratus },
            { 3, "Altocumulus (Ac)", CloudTypeCode.Altocumulus },
            { 4, "Altostratus (As)", CloudTypeCode.Altostratus },
            { 5, "Nimbostratus (Ns)", CloudTypeCode.Nimbostratus },
            { 6, "Stratocumulus (Sc)", CloudTypeCode.Stratocumulus },
            { 7, "Stratus (St)", CloudTypeCode.Stratus },
            { 8, "Cumulus (Cu)", CloudTypeCode.Cumulus },
            { 9, "Cumulonimbus (Cb)", CloudTypeCode.Cumulonimbus },
            { 10, "No CH clouds", CloudTypeCode.NoHighClouds },
            { 11, "Cirrus fibratus, sometimes uncinus, not progressively invading the sky", CloudTypeCode.CirrusFibratusNotInvading },
            { 12, "Cirrus spissatus, in patches or entangled sheaves, which usually do not increase and sometimes seem to be the remains of the upper part of a Cumulonimbus; or Cirrus castellanus or floccus", CloudTypeCode.CirrusSpissatusCastellanusOrFloccus },
            { 13, "Cirrus spissatus cumulonimbogenitus", CloudTypeCode.CirrusSpissatusCumulonimbogenitus },
            { 14, "Cirrus uncinus or fibratus, or both, progressively invading the sky; they generally thicken as a whole", CloudTypeCode.CirrusUncinusOrFibratusInvading },
            { 15, "Cirrus (often in bands) and Cirrostratus, or Cirrostratus alone, progressively invading the sky; they generally thicken as a whole, but the continuous veil does not reach 45 degrees above the horizon", CloudTypeCode.CirrusAndCirrostratusInvadingBelow45Degrees },
            { 16, "Cirrus (often in bands) and Cirrostratus, or Cirrostratus alone, progressively Invading the sky; they generally thicken as a whole; the continuous veil extends more than 45 degrees above the horizon, without the sky being totally covered", CloudTypeCode.CirrusAndCirrostratusInvadingAbove45Degrees },
            { 17, "Cirrostratus covering the whole sky", CloudTypeCode.CirrostratusCoveringWholeSky },
            { 18, "Cirrostratus not progressively invading the sky and not entirely covering it", CloudTypeCode.CirrostratusNotInvadingNotCovering },
            { 19, "Cirrocumulus alone, or Cirrocumulus predominant among the CH clouds", CloudTypeCode.CirrocumulusAloneOrPredominant },
            { 20, "No CM clouds", CloudTypeCode.NoMiddleClouds },
            { 21, "Altostratus translucidus", CloudTypeCode.AltostratusTranslucidus },
            { 22, "Altostratus opacus or Nimbostratus", CloudTypeCode.AltostratusOpacusOrNimbostratus },
            { 23, "Altocumulus translucidus at a single level", CloudTypeCode.AltocumulusTranslucidusSingleLevel },
            { 24, "Patches (often lenticular) of Altocumulus translucidus, continually changing and occurring at one or more levels", CloudTypeCode.AltocumulusTranslucidusPatches },
            { 25, "Altocumulus translucidus in bands, or one or more layers of Altocumulus translucidus or opacus, progressively invading the sky; these Altocumulus clouds generally thicken as a whole", CloudTypeCode.AltocumulusInvading },
            { 26, "Altocumulus cumulogenitus (or cumulonimbogenitus)", CloudTypeCode.AltocumulusCumulogenitus },
            { 27, "Altocumulus translucidus or opacus in two or more layers, or Altocumulus opacus in a single layer, not progressively invading the sky, or Altocumulus with Altostratus or Nimbostratus", CloudTypeCode.AltocumulusMultipleLayersOrWithAltostratusOrNimbostratus },
            { 28, "Altocumulus castellanus or floccus", CloudTypeCode.AltocumulusCastellanusOrFloccus },
            { 29, "Altocumulus of a chaotic sky, generally at several levels", CloudTypeCode.AltocumulusChaoticSky },
            { 30, "No CL clouds", CloudTypeCode.NoLowClouds },
            { 31, "Cumulus humilis or Cumulus fractus other than of bad weather,* or both", CloudTypeCode.CumulusHumilisOrFractus },
            { 32, "Cumulus mediocris or congestus, Towering cumulus (TCU), with or without Cumulus of species fractus or humilis or Stratocumulus, all having their bases at the same level", CloudTypeCode.CumulusMediocrisOrCongestus },
            { 33, "Cumulonimbus calvus, with or without Cumulus, Stratocumulus or Stratus", CloudTypeCode.CumulonimbusCalvus },
            { 34, "Stratocumulus cumulogenitus", CloudTypeCode.StratocumulusCumulogenitus },
            { 35, "Stratocumulus other than Stratocumulus cumulogenitus", CloudTypeCode.StratocumulusNotCumulogenitus },
            { 36, "Stratus nebulosus or Stratus fractus other than of bad weather,* or both", CloudTypeCode.StratusNebulosusOrFractus },
            { 37, "Stratus fractus or Cumulus fractus of bad weather,* or both (pannus), usually below Altostratus or Nimbostratus", CloudTypeCode.FractusOfBadWeather },
            { 38, "Cumulus and Stratocumulus other than Stratocumulus cumulogenitus, with bases at different levels", CloudTypeCode.CumulusAndStratocumulusAtDifferentLevels },
            { 39, "Cumulonimbus capillatus (often with an anvil), with or without Cumulonimbus calvus, Cumulus, Stratocumulus, Stratus or pannus", CloudTypeCode.CumulonimbusCapillatus },
            { 40, "CH", CloudTypeCode.HighCloudClass },
            { 41, "CM", CloudTypeCode.MiddleCloudClass },
            { 42, "CL", CloudTypeCode.LowCloudClass },
            { 59, "Cloud not visible owing to darkness, fog, duststorm, sandstorm, or other analogous phenomena", CloudTypeCode.CloudNotVisible },
            { 60, "CH clouds invisible owing to darkness, fog, blowing dust or sand, or other similar phenomena, or because of a continuous layer of lower clouds", CloudTypeCode.HighCloudsInvisible },
            { 61, "CM clouds invisible owing to darkness, fog, blowing dust or sand, or other similar phenomena, or because of continuous layer of lower clouds", CloudTypeCode.MiddleCloudsInvisible },
            { 62, "CL clouds invisible owing to darkness, fog, blowing dust or sand, or other similar phenomena", CloudTypeCode.LowCloudsInvisible },
            { 63, "Missing value", CloudTypeCode.MissingValue },
        };
        /// <summary>
        /// Official WMO FM 94 BUFR code table 0 11 030 (http://codes.wmo.int/bufr4/codeflag/0-11-030),
        /// transcribed from wmo-im/iwxxm IWXXM/rule/codes.wmo.int-bufr4-codeflag-0-11-030.rdf:
        /// code (skos:notation), meaning (dcterms:description [+ skos:note]) and the expected enum member.
        /// </summary>
        public static readonly TheoryData<int, string, TurbulenceDegreeCode> OfficialTurbulenceDegreeTable = new TheoryData<int, string, TurbulenceDegreeCode>
        {
            { 0, "Nil (in cloud)", TurbulenceDegreeCode.NilInCloud },
            { 1, "Light (in cloud)", TurbulenceDegreeCode.LightInCloud },
            { 2, "Moderate (in cloud)", TurbulenceDegreeCode.ModerateInCloud },
            { 3, "Severe (in cloud)", TurbulenceDegreeCode.SevereInCloud },
            { 4, "Nil (in clear air)", TurbulenceDegreeCode.NilClearAir },
            { 5, "Light (in clear air)", TurbulenceDegreeCode.LightClearAir },
            { 6, "Moderate (in clear air)", TurbulenceDegreeCode.ModerateClearAir },
            { 7, "Severe (in clear air)", TurbulenceDegreeCode.SevereClearAir },
            { 8, "Nil (cloud/clear air not specified)", TurbulenceDegreeCode.Nil },
            { 9, "Light (cloud/clear air not specified)", TurbulenceDegreeCode.Light },
            { 10, "Moderate (cloud/clear air not specified)", TurbulenceDegreeCode.Moderate },
            { 11, "Severe (cloud/clear air not specified)", TurbulenceDegreeCode.Severe },
            { 12, "Extreme, in clear air", TurbulenceDegreeCode.ExtremeClearAir },
            { 13, "Extreme, in cloud", TurbulenceDegreeCode.ExtremeInCloud },
            { 14, "Extreme, cloud/clear air not specified", TurbulenceDegreeCode.Extreme },
            { 15, "Light, isolated moderate", TurbulenceDegreeCode.LightIsolatedModerate },
            { 16, "Light, occasional moderate", TurbulenceDegreeCode.LightOccasionalModerate },
            { 17, "Light, frequently moderate", TurbulenceDegreeCode.LightFrequentlyModerate },
            { 18, "Moderate, isolated severe", TurbulenceDegreeCode.ModerateIsolatedSevere },
            { 19, "Moderate, occasional severe", TurbulenceDegreeCode.ModerateOccasionalSevere },
            { 20, "Moderate, frequently severe", TurbulenceDegreeCode.ModerateFrequentlySevere },
            { 21, "Severe, isolated extreme", TurbulenceDegreeCode.SevereIsolatedExtreme },
            { 22, "Severe, occasional extreme", TurbulenceDegreeCode.SevereOccasionalExtreme },
            { 23, "Severe, frequently extreme", TurbulenceDegreeCode.SevereFrequentlyExtreme },
            { 63, "Missing value", TurbulenceDegreeCode.MissingValue },
        };
        /// <summary>
        /// Official WMO FM 94 BUFR code table 0 20 041 (http://codes.wmo.int/bufr4/codeflag/0-20-041),
        /// transcribed from wmo-im/iwxxm IWXXM/rule/codes.wmo.int-bufr4-codeflag-0-20-041.rdf:
        /// code (skos:notation), meaning (dcterms:description [+ skos:note]) and the expected enum member.
        /// </summary>
        public static readonly TheoryData<int, string, IcingDegreeCode> OfficialIcingDegreeTable = new TheoryData<int, string, IcingDegreeCode>
        {
            { 0, "No icing", IcingDegreeCode.None },
            { 1, "Light icing", IcingDegreeCode.Light },
            { 2, "Light icing in cloud", IcingDegreeCode.LightInCloud },
            { 3, "Light icing in precipitation", IcingDegreeCode.LightInPrecipitation },
            { 4, "Moderate icing", IcingDegreeCode.Moderate },
            { 5, "Moderate icing in cloud", IcingDegreeCode.ModerateInCloud },
            { 6, "Moderate icing in precipitation", IcingDegreeCode.ModerateInPrecipitation },
            { 7, "Severe icing", IcingDegreeCode.Severe },
            { 8, "Severe icing in cloud", IcingDegreeCode.SevereInCloud },
            { 9, "Severe icing in precipitation", IcingDegreeCode.SevereInPrecipitation },
            { 10, "Trace of icing", IcingDegreeCode.Trace },
            { 11, "Trace of icing in cloud", IcingDegreeCode.TraceInCloud },
            { 12, "Trace of icing in precipitation", IcingDegreeCode.TraceInPrecipitation },
            { 15, "Missing value", IcingDegreeCode.MissingValue },
        };

        [Theory]
        [MemberData(nameof(OfficialCloudDistributionTable))]
        public void ResolveCloudDistribution_EveryOfficialCode_MapsToFaithfulMember(int code, string meaning, CloudDistributionCode expected)
        {
            AssertOfficialConcept("0-20-008", code, meaning);
            string uri = $"{WmoCodeRegistry.CloudDistributionTableUri}/{code}";
            Assert.Equal(expected, WmoCodeRegistry.ResolveCloudDistribution(uri));
            Assert.Equal(expected, WmoCodeRegistry.ResolveCloudDistribution(code.ToString(System.Globalization.CultureInfo.InvariantCulture)));
            Assert.Equal(code, (int)expected);
            Assert.Equal(code, WmoCodeRegistry.TryGetNumericCode(uri, WmoCodeRegistry.CloudDistributionTableUri));
        }

        [Theory]
        [MemberData(nameof(OfficialCloudTypeTable))]
        public void ResolveCloudType_EveryOfficialCode_MapsToFaithfulMember(int code, string meaning, CloudTypeCode expected)
        {
            AssertOfficialConcept("0-20-012", code, meaning);
            string uri = $"{WmoCodeRegistry.CloudTypeTableUri}/{code}";
            Assert.Equal(expected, WmoCodeRegistry.ResolveCloudType(uri));
            Assert.Equal(expected, WmoCodeRegistry.ResolveCloudType(code.ToString(System.Globalization.CultureInfo.InvariantCulture)));
            Assert.Equal(code, (int)expected);
        }

        [Theory]
        [MemberData(nameof(OfficialTurbulenceDegreeTable))]
        public void ResolveTurbulenceDegree_EveryOfficialCode_MapsToFaithfulMember(int code, string meaning, TurbulenceDegreeCode expected)
        {
            AssertOfficialConcept("0-11-030", code, meaning);
            string uri = $"{WmoCodeRegistry.TurbulenceDegreeTableUri}/{code}";
            Assert.Equal(expected, WmoCodeRegistry.ResolveTurbulenceDegree(uri));
            Assert.Equal(expected, WmoCodeRegistry.ResolveTurbulenceDegree(code.ToString(System.Globalization.CultureInfo.InvariantCulture)));
            Assert.Equal(code, (int)expected);
        }

        [Theory]
        [MemberData(nameof(OfficialIcingDegreeTable))]
        public void ResolveIcingDegree_EveryOfficialCode_MapsToFaithfulMember(int code, string meaning, IcingDegreeCode expected)
        {
            AssertOfficialConcept("0-20-041", code, meaning);
            string uri = $"{WmoCodeRegistry.IcingDegreeTableUri}/{code}";
            Assert.Equal(expected, WmoCodeRegistry.ResolveIcingDegree(uri));
            Assert.Equal(expected, WmoCodeRegistry.ResolveIcingDegree(code.ToString(System.Globalization.CultureInfo.InvariantCulture)));
            Assert.Equal(code, (int)expected);
        }

        [Theory]
        [InlineData("0-20-008", 18)]
        [InlineData("0-20-012", 48)]
        [InlineData("0-11-030", 25)]
        [InlineData("0-20-041", 14)]
        public void OfficialTables_AreFullyCoveredByTheExpectedMappings(string table, int expectedConceptCount)
        {
            var officialCodes = LoadOfficialTable(table).Keys.OrderBy(c => c).ToList();
            IEnumerable<object[]> expectedRows = table switch
            {
                "0-20-008" => OfficialCloudDistributionTable,
                "0-20-012" => OfficialCloudTypeTable,
                "0-11-030" => OfficialTurbulenceDegreeTable,
                _ => OfficialIcingDegreeTable
            };

            Assert.Equal(expectedConceptCount, officialCodes.Count);
            Assert.Equal(officialCodes, expectedRows.Select(r => (int)r[0]).OrderBy(c => c).ToList());
        }

        [Theory]
        [InlineData("http://codes.wmo.int/bufr4/codeflag/0-20-008/5")]
        [InlineData("http://codes.wmo.int/bufr4/codeflag/0-20-008/18")]
        [InlineData("http://codes.wmo.int/bufr4/codeflag/0-11-030/10")]
        [InlineData("DNS_EMBD")]
        [InlineData("")]
        public void ResolveCloudDistribution_NonOfficialOrForeignTableCode_ReturnsUnknown(string value)
        {
            Assert.Equal(CloudDistributionCode.Unknown, WmoCodeRegistry.ResolveCloudDistribution(value));
        }

        [Theory]
        [InlineData("http://codes.wmo.int/bufr4/codeflag/0-11-030/10", TurbulenceDegreeCode.Moderate, "10", "http://codes.wmo.int/bufr4/codeflag/0-11-030/10", 10)]
        [InlineData("10", TurbulenceDegreeCode.Moderate, "10", "http://codes.wmo.int/bufr4/codeflag/0-11-030/10", 10)]
        [InlineData("MOD", TurbulenceDegreeCode.Moderate, "MOD", "MOD", null)]
        [InlineData("http://codes.wmo.int/bufr4/codeflag/0-11-030/99", TurbulenceDegreeCode.Unknown, "99", "http://codes.wmo.int/bufr4/codeflag/0-11-030/99", 99)]
        [InlineData("http://codes.wmo.int/bufr4/codeflag/0-20-041/3", TurbulenceDegreeCode.Unknown, "http://codes.wmo.int/bufr4/codeflag/0-20-041/3", "http://codes.wmo.int/bufr4/codeflag/0-20-041/3", null)]
        [InlineData("http://codes.wmo.int/bufr4/codeflag/0-11-030/0", TurbulenceDegreeCode.NilInCloud, "0", "http://codes.wmo.int/bufr4/codeflag/0-11-030/0", 0)]
        public void BufrTable_TranslatesIntoCodeListValues_AndRebuildsTheReference(
            string reported, TurbulenceDegreeCode expected, string notation, string reference, int? numeric)
        {
            CodeListValue<TurbulenceDegreeCode>? value = WmoCodeRegistry.TurbulenceDegrees.Translate(reported);

            Assert.NotNull(value);
            Assert.Equal(expected, value!.Code);
            Assert.Equal(expected != TurbulenceDegreeCode.Unknown, value.IsKnown);
            Assert.Equal(notation, value.Notation);
            Assert.Equal(reference, WmoCodeRegistry.TurbulenceDegrees.ToReference(value));
            Assert.Equal(numeric, WmoCodeRegistry.TurbulenceDegrees.NumericCode(value));
        }

        [Fact]
        public void BufrTable_AbsentReference_IsNull()
        {
            Assert.Null(WmoCodeRegistry.CloudDistributions.Translate(null));
            Assert.Null(WmoCodeRegistry.CloudDistributions.Translate("  "));
            Assert.Null(WmoCodeRegistry.CloudDistributions.ToReference(null));
        }

        [Fact]
        public void ResolveTurbulenceAndIcing_UndefinedCodes_ReturnUnknown()
        {
            Assert.Equal(TurbulenceDegreeCode.Unknown, WmoCodeRegistry.ResolveTurbulenceDegree("http://codes.wmo.int/bufr4/codeflag/0-11-030/24"));
            Assert.Equal(IcingDegreeCode.Unknown, WmoCodeRegistry.ResolveIcingDegree("http://codes.wmo.int/bufr4/codeflag/0-20-041/13"));
            Assert.Equal(IcingDegreeCode.Unknown, WmoCodeRegistry.ResolveIcingDegree("http://codes.wmo.int/bufr4/codeflag/0-11-030/3"));
            Assert.Null(WmoCodeRegistry.TryGetNumericCode("OCNL", WmoCodeRegistry.CloudDistributionTableUri));
        }

        [Theory]
        [InlineData("OCNL_EMBD", CloudDistributionCode.OccasionalEmbedded)]
        [InlineData("FRQ_EMBD", CloudDistributionCode.FrequentEmbedded)]
        [InlineData("DNS", CloudDistributionCode.Dense)]
        [InlineData("LYR", CloudDistributionCode.Layers)]
        [InlineData("obsc", CloudDistributionCode.Obscured)]
        [InlineData("EMBD", CloudDistributionCode.Embedded)]
        public void ResolveCloudDistribution_TacAbbreviations_MapToOfficialMeaning(string tac, CloudDistributionCode expected)
        {
            Assert.Equal(expected, WmoCodeRegistry.ResolveCloudDistribution(tac));
        }

        private static void AssertOfficialConcept(string table, int code, string meaning)
        {
            var concepts = LoadOfficialTable(table);
            Assert.True(concepts.ContainsKey(code), $"Code {code} is not defined in official table {table}.");
            Assert.Equal(meaning, concepts[code]);
        }

        private static Dictionary<int, string> LoadOfficialTable(string table)
        {
            string path = Path.Combine(CodelistDirectory, $"codes.wmo.int-bufr4-codeflag-{table}.rdf");
            XDocument rdf = XDocument.Load(path);
            var result = new Dictionary<int, string>();
            foreach (XElement concept in rdf.Descendants(Skos + "Concept"))
            {
                int code = int.Parse(concept.Element(Skos + "notation")!.Value, System.Globalization.CultureInfo.InvariantCulture);
                string description = concept.Element(DcTerms + "description")!.Value;
                string? note = concept.Element(Skos + "note")?.Value;
                Assert.Equal($"http://codes.wmo.int/bufr4/codeflag/{table}/{code}", concept.Attribute(Rdf + "about")!.Value);
                result[code] = table == "0-11-030" && !string.IsNullOrEmpty(note) ? $"{description} ({note})" : description;
            }

            return result;
        }
    }
}
