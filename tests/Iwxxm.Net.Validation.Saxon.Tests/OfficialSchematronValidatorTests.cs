using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Iwxxm.Net.Reports;
using Iwxxm.Net.Validation.Saxon.Internal;
using Xunit;

namespace Iwxxm.Net.Validation.Saxon.Tests
{
    /// <summary>
    /// Runs the complete, unmodified official WMO IWXXM 2025-2 Schematron (iwxxm.sch, 165 patterns)
    /// through SaxonCS-HE against the 29 official examples and targeted negative cases.
    /// </summary>
    public sealed class OfficialSchematronValidatorTests
    {
        private const string ExamplesDirectory = "Samples/wmo-examples";

        /// <summary>SHA-256 of wmo-im/iwxxm IWXXM/rule/iwxxm.sch (IWXXM 2025-2).</summary>
        private const string OfficialSchematronSha256 = "e13f5c48fb07b5a45a0d751a944c6b735f04acbb38affdb696c537611f817179";

        private static readonly OfficialSchematronValidator Validator = new OfficialSchematronValidator();

        public static IEnumerable<object[]> OfficialExamples() =>
            Directory.GetFiles(ExamplesDirectory, "*.xml")
                .OrderBy(path => path, StringComparer.Ordinal)
                .Select(path => new object[] { Path.GetFileName(path) });

        [Fact]
        public void OfficialExamples_AreAllPresent()
        {
            Assert.Equal(29, OfficialExamples().Count());
        }

        [Theory]
        [MemberData(nameof(OfficialExamples))]
        public void OfficialExample_PassesTheOfficialSchematron(string fileName)
        {
            IReadOnlyList<ValidationError> findings = ValidateFile(fileName);

            Assert.True(findings.Count == 0, $"{fileName}: " + string.Join(" | ", findings));
        }

        [Theory]
        [MemberData(nameof(OfficialExamples))]
        public void OfficialExample_PassesTheFullPipelineWithUseOfficialSchematron(string fileName)
        {
            IwxxmParser parser = new IwxxmParserBuilder().UseOfficialSchematron().Build();

            Result<IIwxxmReport> result = parser.ProcessFile(Path.Combine(ExamplesDirectory, fileName));

            Assert.True(result.IsSuccess, $"{fileName}: " + string.Join(" | ", result.Errors));
        }

        [Fact]
        public void WafsExample_PassesTheSigwxPipelineWithUseOfficialSchematron()
        {
            IwxxmSigwxParser parser = new IwxxmParserBuilder().UseOfficialSchematron().BuildSigwxParser();

            Result<SignificantWeatherReport> result = parser.ProcessFile(Path.Combine(ExamplesDirectory, "WAFS-Example.xml"));

            Assert.True(result.IsSuccess, string.Join(" | ", result.Errors));
        }

        [Fact]
        public void MetarQnhNotInHectopascal_FiresOfficialRuleThroughThePipeline()
        {
            string xml = ReadExample("metar-A3-1.xml");
            string invalid = ReplaceOnce(xml, "<iwxxm:qnh uom=\"hPa\">", "<iwxxm:qnh uom=\"mbar\">");
            IwxxmParser parser = new IwxxmParserBuilder().UseOfficialSchematron().Build();

            Result<IIwxxmReport> result = parser.Process(invalid);

            Assert.True(result.IsFailure);
            ValidationError error = Assert.Single(result.Errors);
            Assert.Equal("METAR_SPECI.MeteorologicalAerodromeObservation-5", error.Code);
            Assert.Equal(ErrorCategory.BusinessRuleViolation, error.Category);
            Assert.Equal(ValidationSeverity.Error, error.Severity);
            Assert.StartsWith("METAR_SPECI.MeteorologicalAerodromeObservation-5: qnh shall be reported in hectopascals", error.Message, StringComparison.Ordinal);
            Assert.Contains("MeteorologicalAerodromeObservation[1]", error.Location, StringComparison.Ordinal);
        }

        [Fact]
        public void BuiltInSubset_DoesNotEnforceTheQnhUnitRule()
        {
            // Demonstrates the coverage gap the optional package closes (the default XSLT 1.0 subset has 11 of 165 rules).
            string invalid = ReplaceOnce(ReadExample("metar-A3-1.xml"), "<iwxxm:qnh uom=\"hPa\">", "<iwxxm:qnh uom=\"mbar\">");

            Result<IIwxxmReport> result = new IwxxmParserBuilder().Build().Process(invalid);

            Assert.DoesNotContain(result.Errors, e => e.Code == "METAR_SPECI.MeteorologicalAerodromeObservation-5");
        }

        [Fact]
        public void SigmetMarkedAsCancellationButCarryingPhenomenon_FiresSigmet3()
        {
            string invalid = ReplaceOnce(ReadExample("sigmet-A6-1a-TS.xml"), "<iwxxm:SIGMET ", "<iwxxm:SIGMET isCancelReport=\"true\" ");

            IReadOnlyList<ValidationError> findings = Validate(invalid);

            Assert.Contains(findings, e => e.Code == "SIGMET.SIGMET-3" && e.Message.Contains("'CANCELLATION' report", StringComparison.Ordinal));
        }

        [Fact]
        public void TafBaseForecastWithChangeIndicator_FiresTaf6()
        {
            string xml = ReadExample("taf-A5-1.xml");
            string invalid = ReplaceOnce(
                xml,
                "<iwxxm:MeteorologicalAerodromeForecast gml:id=\"uuid.13048235-8f8a-4587-a6be-e8ce99f4525c\" cloudAndVisibilityOK=\"false\">",
                "<iwxxm:MeteorologicalAerodromeForecast gml:id=\"uuid.13048235-8f8a-4587-a6be-e8ce99f4525c\" changeIndicator=\"BECOMING\" cloudAndVisibilityOK=\"false\">");

            IReadOnlyList<ValidationError> findings = Validate(invalid);

            Assert.Contains(findings, e => e.Code == "TAF.TAF-6");
        }

        [Fact]
        public void TafBaseForecastWithoutSurfaceWind_FiresTaf7()
        {
            string xml = ReadExample("taf-A5-1.xml");
            int baseStart = xml.IndexOf("<iwxxm:baseForecast>", StringComparison.Ordinal);
            int windStart = xml.IndexOf("<iwxxm:surfaceWind>", baseStart, StringComparison.Ordinal);
            int windEnd = xml.IndexOf("</iwxxm:surfaceWind>", windStart, StringComparison.Ordinal) + "</iwxxm:surfaceWind>".Length;
            string invalid = xml.Remove(windStart, windEnd - windStart);

            IReadOnlyList<ValidationError> findings = Validate(invalid);

            Assert.Contains(findings, e => e.Code == "TAF.TAF-7");
        }

        [Fact]
        public void TafWithoutAnyBaseForecastElement_IsNotFlaggedByTheOfficialRules()
        {
            // Characterisation of the official rule set (not of this library): TAF-5 tests
            // "iwxxm:baseForecast/@nilReason != '.../missing'", which is false for an empty sequence in XPath 2.0,
            // so an ordinary TAF whose baseForecast element is entirely absent is not reported by any official rule.
            string xml = ReadExample("taf-A5-1.xml");
            string invalid = Regex.Replace(xml, @"<iwxxm:baseForecast>.*?</iwxxm:baseForecast>\s*", string.Empty, RegexOptions.Singleline);
            Assert.NotEqual(xml, invalid);

            IReadOnlyList<ValidationError> findings = Validate(invalid);

            Assert.Empty(findings);
        }

        [Fact]
        public void CloudAmountOutsideTheOfficialCodeList_FiresCodeListRuleOffline()
        {
            string invalid = ReplaceOnce(
                ReadExample("metar-A3-1.xml"),
                "http://codes.wmo.int/49-2/CloudAmountReportedAtAerodrome/SCT",
                "http://codes.wmo.int/49-2/CloudAmountReportedAtAerodrome/XYZ");

            IReadOnlyList<ValidationError> findings = Validate(invalid);

            ValidationError error = Assert.Single(findings);
            Assert.Equal("Common.CloudLayer.amount", error.Code);
            Assert.Contains("http://codes.wmo.int/49-2/CloudAmountReportedAtAerodrome", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void MalformedInput_IsReportedAsMalformedInput()
        {
            IReadOnlyList<ValidationError> findings = Validate("<iwxxm:METAR xmlns:iwxxm=\"http://icao.int/iwxxm/2025-2\">");

            ValidationError error = Assert.Single(findings);
            Assert.Equal(OfficialSchematronValidator.MalformedInputCode, error.Code);
            Assert.Equal(ErrorCategory.MalformedInput, error.Category);
        }

        [Fact]
        public void DocumentTypeDeclaration_IsRejected()
        {
            IReadOnlyList<ValidationError> findings = Validate("<!DOCTYPE x [<!ENTITY e \"boom\">]><x>&e;</x>");

            Assert.Equal(ErrorCategory.MalformedInput, Assert.Single(findings).Category);
        }

        [Fact]
        public void ConcurrentValidation_OfAllExamples_IsThreadSafe()
        {
            string[] files = OfficialExamples().Select(o => (string)o[0]).ToArray();
            var failures = new ConcurrentBag<string>();

            Parallel.For(0, files.Length * 3, new ParallelOptions { MaxDegreeOfParallelism = 8 }, i =>
            {
                string file = files[i % files.Length];
                IReadOnlyList<ValidationError> findings = ValidateFile(file);
                if (findings.Count > 0)
                    failures.Add(file + ": " + string.Join(" | ", findings));
            });

            Assert.Empty(failures);
        }

        [Fact]
        public void Engine_RunsAsHomeEditionWithoutLicenseKey()
        {
            OfficialSchematronValidator.Warmup();

            Assert.StartsWith("HE", OfficialSchematronEngine.Shared.ProductDescription, StringComparison.Ordinal);
            Assert.Equal(TimeSpan.Zero, OfficialSchematronValidator.Warmup());
        }

        [Fact]
        public void EmbeddedSchematron_IsByteIdenticalToTheOfficialRuleSet()
        {
            using (Stream stream = EmbeddedRuleCatalog.OpenRequired(EmbeddedRuleCatalog.SchematronUri))
            using (SHA256 sha = SHA256.Create())
            {
                string hash = Convert.ToHexString(sha.ComputeHash(stream)).ToLowerInvariant();
                Assert.Equal(OfficialSchematronSha256, hash);
            }
        }

        [Fact]
        public void Catalog_CoversEveryCodeListReferencedByTheOfficialRules()
        {
            string sch;
            using (Stream stream = EmbeddedRuleCatalog.OpenRequired(EmbeddedRuleCatalog.SchematronUri))
            using (var reader = new StreamReader(stream, Encoding.UTF8))
            {
                sch = reader.ReadToEnd();
            }

            var referenced = Regex.Matches(sch, @"document\('([^']+)'\)")
                .Select(m => m.Groups[1].Value)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(n => n, StringComparer.Ordinal)
                .ToList();

            // iwxxm.sch dereferences 17 of the 20 official code lists; every one of them must be in the catalog.
            Assert.Equal(17, referenced.Count);
            Assert.Equal(20, EmbeddedRuleCatalog.CodelistFileNames.Count);
            Assert.Subset(new HashSet<string>(EmbeddedRuleCatalog.CodelistFileNames, StringComparer.Ordinal), new HashSet<string>(referenced, StringComparer.Ordinal));
            Assert.All(EmbeddedRuleCatalog.CodelistFileNames, name =>
            {
                using (Stream? stream = EmbeddedRuleCatalog.TryOpen(new Uri(EmbeddedRuleCatalog.RuleBaseUri, name)))
                {
                    Assert.True(stream != null && stream.Length > 0, $"Code list '{name}' is not embedded.");
                }
            });
        }

        [Theory]
        [InlineData("http://codes.wmo.int/49-2/CloudAmountReportedAtAerodrome")]
        [InlineData("https://rules.iwxxm-net.invalid/iwxxm/2025-2/rule/iwxxm.sch")]
        [InlineData("http://rules.iwxxm-net.invalid/iwxxm/2025-2/rule/unknown.rdf")]
        [InlineData("http://schxslt.iwxxm-net.invalid/1.10.1/2.0/../../secret.xsl")]
        [InlineData("http://schxslt.iwxxm-net.invalid/1.10.1/2.0/pipeline.xsl?x=1")]
        [InlineData("file:///etc/passwd")]
        public void Catalog_RefusesUrisOutsideTheEmbeddedResources(string uri)
        {
            Assert.Null(EmbeddedRuleCatalog.MapToManifestName(new Uri(uri), out _));
            Assert.Null(EmbeddedRuleCatalog.TryOpen(new Uri(uri)));
        }

        [Fact]
        public void UseOfficialSchematron_PassesBuilderOptionsToTheValidator()
        {
            // A character quota below the size of the example makes the validator itself reject the document.
            var options = new IwxxmParserOptions(maxCharactersInDocument: 64);
            IwxxmParser parser = new IwxxmParserBuilder()
                .WithOptions(options)
                .UseSchemaValidator(new NoSchemaFindings())
                .UseOfficialSchematron()
                .Build();

            Result<IIwxxmReport> result = parser.ProcessFile(Path.Combine(ExamplesDirectory, "metar-A3-1.xml"));

            Assert.Contains(result.Errors, e => e.Code == OfficialSchematronValidator.MalformedInputCode);
        }

        private static IReadOnlyList<ValidationError> ValidateFile(string fileName)
        {
            using (FileStream stream = File.OpenRead(Path.Combine(ExamplesDirectory, fileName)))
            {
                return Validator.Validate(stream);
            }
        }

        private static IReadOnlyList<ValidationError> Validate(string xml)
        {
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(xml), writable: false))
            {
                return Validator.Validate(stream);
            }
        }

        private static string ReadExample(string fileName) => File.ReadAllText(Path.Combine(ExamplesDirectory, fileName));

        private static string ReplaceOnce(string text, string oldValue, string newValue)
        {
            int index = text.IndexOf(oldValue, StringComparison.Ordinal);
            Assert.True(index >= 0, $"'{oldValue}' not found.");
            return text.Substring(0, index) + newValue + text.Substring(index + oldValue.Length);
        }

        private sealed class NoSchemaFindings : Iwxxm.Net.Extensibility.ISchemaValidator
        {
            public IReadOnlyList<ValidationError> Validate(Stream document) => Array.Empty<ValidationError>();
        }
    }
}
