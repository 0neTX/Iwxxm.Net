using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Iwxxm.Net.Reports;
using Iwxxm.Net.Infrastructure.Resources;
using Iwxxm.Net.Infrastructure.Validation;
using Xunit;
using Xunit.Abstractions;

namespace Iwxxm.Net.Tests
{
    /// <summary>
    /// Tests for XSD validation against the embedded, unmodified official WMO IWXXM 2025-2 schema tree,
    /// the air-gapped resolver, and the official RDF code-list checks of the Schematron XSLT subset.
    /// </summary>
    public sealed class OfficialSchemaValidationTests
    {
        private const string ExamplesDir = "Samples/wmo-examples";
        private const string MetarPath = ExamplesDir + "/metar-A3-1.xml";
        private const string TafPath = ExamplesDir + "/taf-A5-1.xml";
        private static readonly XNamespace Iwxxm = "http://icao.int/iwxxm/2025-2";

        private readonly ITestOutputHelper _output;

        public OfficialSchemaValidationTests(ITestOutputHelper output)
        {
            _output = output;
        }

        public static IEnumerable<object[]> GetAllOfficialWmoExamples() =>
            Directory.GetFiles(ExamplesDir, "*.xml").OrderBy(f => f, StringComparer.Ordinal).Select(f => new object[] { f });

        [Theory]
        [MemberData(nameof(GetAllOfficialWmoExamples))]
        public void Validate_EveryOfficialWmoExample_PassesOfficialXsdValidation(string path)
        {
            IReadOnlyList<ValidationError> errors = ValidateXsd(File.ReadAllText(path));
            Assert.True(errors.Count == 0, $"'{path}': {string.Join(" | ", errors)}");
        }

        [Fact]
        public void OfficialSchemaSet_CompilesFromEmbeddedResourcesAndDeclaresAllReportRoots()
        {
            var schemaSet = XsdValidator.OfficialSchemaSet;

            Assert.True(schemaSet.IsCompiled);
            foreach (string root in new[] { "METAR", "SPECI", "TAF", "SIGMET", "AIRMET", "WAFSSignificantWeatherForecast", "SpaceWeatherAdvisory" })
            {
                Assert.True(schemaSet.GlobalElements.Contains(new XmlQualifiedName(root, Iwxxm.NamespaceName)), root);
            }
            Assert.True(schemaSet.GlobalElements.Contains(new XmlQualifiedName("MeteorologicalBulletin", "http://def.wmo.int/collect/2014")));

            TimeSpan? compileTime = XsdValidator.OfficialSchemaSetCompileTime;
            Assert.NotNull(compileTime);
            _output.WriteLine($"Official IWXXM 2025-2 schema set: {schemaSet.Count} XmlSchema objects (one per target namespace) compiled in {compileTime!.Value.TotalMilliseconds:F0} ms.");
        }

        [Fact]
        public void Validate_NonNumericAirTemperature_IsRejectedByOfficialXsd()
        {
            string xml = File.ReadAllText(MetarPath).Replace(
                "<iwxxm:airTemperature uom=\"Cel\">17.0</iwxxm:airTemperature>",
                "<iwxxm:airTemperature uom=\"Cel\">abc</iwxxm:airTemperature>");

            IReadOnlyList<ValidationError> errors = ValidateXsd(xml);

            Assert.Contains(errors, e => e.Code == "XSD-SCHEMA-VIOLATION" && e.Category == ErrorCategory.SchemaViolation);
        }

        [Fact]
        public void Validate_MissingRequiredReportStatus_IsRejectedByOfficialXsd()
        {
            string xml = File.ReadAllText(MetarPath).Replace("reportStatus=\"NORMAL\"", string.Empty);

            IReadOnlyList<ValidationError> errors = ValidateXsd(xml);

            Assert.Contains(errors, e => e.Category == ErrorCategory.SchemaViolation && e.Message.Contains("reportStatus"));
        }

        [Fact]
        public void Validate_MissingRequiredPermissibleUsage_IsRejectedByOfficialXsd()
        {
            string xml = File.ReadAllText(MetarPath).Replace("permissibleUsage=\"OPERATIONAL\"", string.Empty);

            Assert.NotEmpty(ValidateXsd(xml));
        }

        [Fact]
        public void Validate_UnknownRootElement_IsRejectedWithoutRelyingOnMessageText()
        {
            IReadOnlyList<ValidationError> errors = ValidateXsd(
                "<iwxxm:NOT_A_REPORT xmlns:iwxxm=\"http://icao.int/iwxxm/2025-2\"/>");

            var error = Assert.Single(errors);
            Assert.Equal("XSD-UNKNOWN-ROOT-ELEMENT", error.Code);
        }

        [Fact]
        public void Parse_OfficialMetarWithLightShowerOfRainCode_IsAccepted()
        {
            string xml = File.ReadAllText(MetarPath).Replace(
                "<iwxxm:presentWeather xlink:href=\"http://codes.wmo.int/306/4678/DZ\"/>",
                "<iwxxm:presentWeather xlink:href=\"http://codes.wmo.int/306/4678/-SHRA\"/>");

            var result = IwxxmParser.ParseAs<AerodromeObservation>(xml);

            Assert.True(result.IsSuccess, string.Join(" | ", result.Errors));
            Assert.Contains(new AerodromeWeather("-SHRA", isKnown: true), result.Value.PresentWeather);
        }

        [Fact]
        public void Parse_PresentWeatherCodeOutsideOfficialList_FailsRdfCodeListRule()
        {
            string xml = File.ReadAllText(MetarPath).Replace(
                "<iwxxm:presentWeather xlink:href=\"http://codes.wmo.int/306/4678/DZ\"/>",
                "<iwxxm:presentWeather xlink:href=\"http://codes.wmo.int/306/4678/XXSHRA\"/>");

            var result = IwxxmParser.Parse(xml);

            Assert.True(result.IsFailure);
            Assert.Contains(result.Errors, e => e.Code == "IWXXM.RdfCodeListRegistry-1" && e.Message.Contains("AerodromePresentOrForecastWeather"));
        }

        [Fact]
        public void Parse_CodeFromUnknownRegister_FailsRdfCodeListRule()
        {
            string xml = File.ReadAllText(MetarPath).Replace(
                "<iwxxm:presentWeather xlink:href=\"http://codes.wmo.int/306/4678/DZ\"/>",
                "<iwxxm:presentWeather xlink:href=\"http://codes.wmo.int/49-2/../../evil/DZ\"/>");

            var result = IwxxmParser.Parse(xml);

            Assert.True(result.IsFailure);
            Assert.Contains(result.Errors, e => e.Code == "IWXXM.RdfCodeListRegistry-1");
        }

        [Fact]
        public void Parse_NilAirTemperature_IsAcceptedAndMappedToNull()
        {
            string xml = File.ReadAllText(MetarPath).Replace(
                "<iwxxm:airTemperature uom=\"Cel\">17.0</iwxxm:airTemperature>",
                "<iwxxm:airTemperature uom=\"Cel\" xsi:nil=\"true\" nilReason=\"http://codes.wmo.int/common/nil/missing\"/>");

            var result = IwxxmParser.ParseAs<AerodromeObservation>(xml);

            Assert.True(result.IsSuccess, string.Join(" | ", result.Errors));
            Assert.Null(result.Value.AirTemperatureCelsius);
            Assert.Equal(16.0, result.Value.DewpointTemperatureCelsius);
        }

        [Fact]
        public void Parse_TafWithoutBaseForecastAndWithoutIsCancelReport_FailsTafRule()
        {
            // isCancelReport is absent: in XPath 1.0 "@isCancelReport != 'true'" is false for a missing attribute,
            // which previously disabled this rule. baseForecast is optional in the official XSD.
            XDocument taf = XDocument.Load(TafPath);
            Assert.Null(taf.Root!.Attribute("isCancelReport"));
            taf.Root.Element(Iwxxm + "baseForecast")!.Remove();
            taf.Root.Elements(Iwxxm + "changeForecast").Remove();

            var result = IwxxmParser.Parse(taf.ToString(SaveOptions.DisableFormatting));

            Assert.True(result.IsFailure);
            Assert.Contains(result.Errors, e => e.Code == "TAF.BaseForecastMandatory-1");
        }

        [Fact]
        public void Parse_CompactSigwxReportExtension_IsValidatedAgainstLegacySchemaSet()
        {
            var full = IwxxmParser.ParseFile("Samples/sigwx-swh-2025-2.xml");
            Assert.True(full.IsSuccess, string.Join(" | ", full.Errors));

            string invalid = File.ReadAllText("Samples/sigwx-swh-2025-2.xml").Replace(
                "<iwxxm:issuingCentre>EGRR</iwxxm:issuingCentre>", string.Empty);
            var result = IwxxmSigwxParser.Parse(invalid);

            Assert.True(result.IsFailure);
            Assert.Contains(result.Errors, e => e.Category == ErrorCategory.SchemaViolation);
        }

        [Theory]
        [InlineData("http://schemas.opengis.net/gml/3.2.1/gml.xsd", "Xsd/Official/externalSchema/schemas.opengis.net/gml/3.2.1/gml.xsd")]
        [InlineData("https://schemas.wmo.int/iwxxm/2025-2/iwxxm.xsd", "Xsd/Official/IWXXM/iwxxm.xsd")]
        [InlineData("http://www.aixm.aero/schema/5.1.1/AIXM_Features.xsd", "Xsd/Official/externalSchema/aero/aixm/5.1.1/AIXM_Features.xsd")]
        [InlineData("http://schemas.wmo.int/metce/1.2/metce.xsd", "Xsd/Official/externalSchema/schemas.wmo.int/metce/1.2/metce.xsd")]
        [InlineData("http://www.w3.org/1999/xlink.xsd", "Xsd/Official/externalSchema/org/w3c/1999/xlink.xsd")]
        [InlineData("embedded://iwxxm-resources/Codelists/codes.wmo.int-common-nil.rdf", "Codelists/codes.wmo.int-common-nil.rdf")]
        public void Resolver_MapsOfficialUrlsToEmbeddedResources(string url, string expectedPath)
        {
            Assert.Equal(expectedPath, EmbeddedXmlUrlResolver.MapToResourcePath(new Uri(url)));

            var resolver = new EmbeddedXmlUrlResolver();
            using (var stream = (Stream)resolver.GetEntity(new Uri(url), null, typeof(Stream)))
            {
                Assert.True(stream.Length > 0);
            }
        }

        [Theory]
        [InlineData("file:///etc/passwd")]
        [InlineData("http://example.com/gml.xsd")]
        [InlineData("http://schemas.opengis.net/gml/3.2.1/not-embedded.xsd")]
        [InlineData("http://schemas.opengis.net/sampling/2.0/samplingFeature.xsd")]
        [InlineData("embedded://other-host/Codelists/codes.wmo.int-common-nil.rdf")]
        public void Resolver_UnknownOrNonEmbeddedUris_FailWithoutAnyIo(string url)
        {
            var resolver = new EmbeddedXmlUrlResolver();
            Assert.Throws<XmlException>(() => resolver.GetEntity(new Uri(url), null, typeof(Stream)));
        }

        [Fact]
        public void Resolver_RelativeIncludesResolveAgainstCanonicalBaseUri()
        {
            var resolver = new EmbeddedXmlUrlResolver();

            Uri aixm = resolver.ResolveUri(new Uri("http://www.aixm.aero/schema/5.1.1/AIXM_Features.xsd"), "./AIXM_DataTypes.xsd");
            Uri codeList = resolver.ResolveUri(new Uri("embedded://iwxxm-resources/Schematron/iwxxm-2025-2-sigwx.xslt"), "../Codelists/codes.wmo.int-iwxxm-nil.rdf");

            Assert.Equal("http://www.aixm.aero/schema/5.1.1/AIXM_DataTypes.xsd", aixm.AbsoluteUri);
            Assert.Equal("Codelists/codes.wmo.int-iwxxm-nil.rdf", EmbeddedXmlUrlResolver.MapToResourcePath(codeList));
        }

        [Fact]
        public void EmbeddedResources_ContainTheTwentyOfficialCodeListsAndTheOfficialSchemas()
        {
            string[] names = typeof(XsdValidator).Assembly.GetManifestResourceNames();

            Assert.Equal(20, names.Count(n => n.StartsWith(EmbeddedResourceLoader.LogicalNamePrefix + "Codelists/", StringComparison.Ordinal)));
            Assert.Equal(80, names.Count(n => n.StartsWith(EmbeddedResourceLoader.LogicalNamePrefix + "Xsd/Official/", StringComparison.Ordinal)));
            Assert.Equal(4, names.Count(n => n.StartsWith(EmbeddedResourceLoader.LogicalNamePrefix + "Xsd/Legacy/", StringComparison.Ordinal)));
        }

        private static IReadOnlyList<ValidationError> ValidateXsd(string xml)
        {
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(xml), writable: false))
            {
                return XsdValidator.CreateFullSuiteValidator().Validate(stream);
            }
        }
    }
}
