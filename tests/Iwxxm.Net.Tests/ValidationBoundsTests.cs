using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Xsl;
using Iwxxm.Net.Infrastructure.Caching;
using Iwxxm.Net.Infrastructure.Validation;
using Iwxxm.Net.Tests.DataFidelity;
using Xunit;

namespace Iwxxm.Net.Tests
{
    /// <summary>
    /// Bounded validation output (AUD-M14 / AUD-N3) and retry-on-failure compilation caches (AUD-L6).
    /// </summary>
    public sealed class ValidationBoundsTests
    {
        [Fact]
        public void RetryOnFailureLazy_DoesNotCacheFailures_AndCachesSuccess()
        {
            int calls = 0;
            var lazy = new RetryOnFailureLazy<string>(() =>
            {
                calls++;
                if (calls == 1)
                    throw new InvalidOperationException("transient");
                return "ok";
            });

            Assert.Throws<InvalidOperationException>(() => lazy.Value);
            Assert.False(lazy.IsValueCreated);
            Assert.Equal("ok", lazy.Value);
            Assert.Equal("ok", lazy.Value);
            Assert.True(lazy.IsValueCreated);
            Assert.Equal(2, calls);
        }

        [Fact]
        public void XsltSchematron_CompileFailure_IsAResultError_AndIsRetried()
        {
            int calls = 0;
            var lazy = new RetryOnFailureLazy<XslCompiledTransform>(() =>
            {
                calls++;
                if (calls == 1)
                    throw new XsltException("simulated compile failure");
                return XsltSchematronValidator.LoadAndCompileXslt();
            });
            var validator = new XsltSchematronValidator(null, lazy);

            IReadOnlyList<ValidationError> first = Validate(validator, SampleDocuments.Text("metar-A3-1.xml"));
            ValidationError unavailable = Assert.Single(first);
            Assert.Equal(XsltSchematronValidator.UnavailableCode, unavailable.Code);
            Assert.Equal(ErrorCategory.BusinessRuleViolation, unavailable.Category);

            IReadOnlyList<ValidationError> second = Validate(validator, SampleDocuments.Text("metar-A3-1.xml"));
            Assert.Empty(second);
            Assert.Equal(2, calls);
        }

        [Fact]
        public void XsltSchematron_ReportLargerThanLimit_IsABusinessRuleError_NotMalformedInput()
        {
            var options = new IwxxmParserOptions(maxRuleReportBytes: 64);
            var validator = new XsltSchematronValidator(options);

            ValidationError error = Assert.Single(Validate(validator, SampleDocuments.Text("metar-A3-1.xml")));

            Assert.Equal(XsltSchematronValidator.ReportSizeLimitCode, error.Code);
            Assert.Equal(ErrorCategory.BusinessRuleViolation, error.Category);
        }

        [Fact]
        public void XsltSchematron_ReportIsNotReadWithTheInputQuota()
        {
            // The input quota is barely above the input size; the SVRL report (one finding per invalid code) is read with
            // its own limits, so its findings are returned instead of a misreported XML-MALFORMED (AUD-M14).
            string xml = InvalidWeatherCodes();
            var options = new IwxxmParserOptions(maxCharactersInDocument: xml.Length + 16);

            IReadOnlyList<ValidationError> errors = Validate(new XsltSchematronValidator(options), xml);

            Assert.True(errors.Count >= 3, string.Join("; ", errors));
            Assert.All(errors, e => Assert.Equal(ErrorCategory.BusinessRuleViolation, e.Category));
        }

        [Fact]
        public void XsltSchematron_FindingsAreCapped_WithATooManyErrorsSentinel()
        {
            var options = new IwxxmParserOptions(maxValidationErrors: 2);

            IReadOnlyList<ValidationError> errors = Validate(new XsltSchematronValidator(options), InvalidWeatherCodes());

            Assert.Equal(3, errors.Count);
            Assert.Equal(ValidationFindingLimit.SchematronTooManyErrorsCode, errors[2].Code);
            Assert.Equal(ValidationSeverity.Error, errors[2].Severity);
            Assert.Equal(ErrorCategory.BusinessRuleViolation, errors[2].Category);
        }

        [Fact]
        public void Xsd_FindingsAreCapped_WithATooManyErrorsSentinel()
        {
            string xml = SampleDocuments.Text("metar-A3-1.xml")
                .Replace("<iwxxm:airTemperature uom=\"Cel\">", "<iwxxm:airTemperature uom=\"Cel\">x")
                .Replace("<iwxxm:dewpointTemperature uom=\"Cel\">", "<iwxxm:dewpointTemperature uom=\"Cel\">x")
                .Replace("<iwxxm:qnh uom=\"hPa\">", "<iwxxm:qnh uom=\"hPa\">x");
            var unlimited = XsdValidator.CreateFullSuiteValidator();
            Assert.True(Validate(unlimited, xml).Count >= 3, "The fixture must produce at least three schema errors.");

            var capped = XsdValidator.CreateFullSuiteValidator(new IwxxmParserOptions(maxValidationErrors: 1));
            IReadOnlyList<ValidationError> errors = Validate(capped, xml);

            Assert.Equal(2, errors.Count);
            Assert.Equal("XSD-SCHEMA-VIOLATION", errors[0].Code);
            Assert.Equal(ValidationFindingLimit.XsdTooManyErrorsCode, errors[1].Code);
            Assert.Equal(ErrorCategory.SchemaViolation, errors[1].Category);
        }

        [Fact]
        public void FindingLimit_SentinelIsAWarning_WhenOnlyWarningsWereDiscarded()
        {
            var findings = new List<ValidationError>();
            var limit = new ValidationFindingLimit(findings, 1, "X-TOO-MANY", ErrorCategory.BusinessRuleViolation);

            Assert.True(limit.TryAdd(Warning("A")));
            Assert.False(limit.TryAdd(Warning("B")));
            Assert.Equal(ValidationSeverity.Warning, findings.Last().Severity);

            Assert.False(limit.TryAdd(new ValidationError("C", "c", ErrorCategory.BusinessRuleViolation)));
            Assert.Equal(2, findings.Count);
            Assert.Equal("X-TOO-MANY", findings.Last().Code);
            Assert.Equal(ValidationSeverity.Error, findings.Last().Severity);
        }

        [Fact]
        public void RuleDocumentResolver_ParsesEachCodeListOnce_AndSharesTheTree()
        {
            var resolver = new CachedRuleDocumentResolver();
            Uri uri = resolver.ResolveUri(
                new Uri("embedded://iwxxm-resources/Schematron/iwxxm-2025-2-sigwx.xslt"),
                "../Codelists/codes.wmo.int-49-2-AerodromePresentOrForecastWeather.rdf");

            object first = resolver.GetEntity(uri, null, null);
            object second = resolver.GetEntity(uri, null, typeof(object));

            Assert.IsType<System.Xml.XPath.XPathDocument>(first);
            Assert.Same(first, second);
            Assert.Equal(1, resolver.ParseCount);
            using (var stream = (Stream)resolver.GetEntity(uri, null, typeof(Stream)))
            {
                Assert.True(stream.CanRead);
            }
        }

        [Fact]
        public void BoundedOutputStream_RefusesWritesBeyondTheLimit()
        {
            using (var stream = new BoundedOutputStream(4))
            {
                stream.Write(new byte[] { 1, 2, 3 }, 0, 3);
                Assert.False(stream.LimitExceeded);
                Assert.Throws<OutputLimitExceededException>(() => stream.Write(new byte[] { 4, 5 }, 0, 2));
                Assert.True(stream.LimitExceeded);
                using (MemoryStream view = stream.OpenRead())
                {
                    Assert.Equal(new byte[] { 1, 2, 3 }, view.ToArray());
                }
            }
        }

        [Fact]
        public void Options_RejectNonPositiveValidationLimits()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new IwxxmParserOptions(maxValidationErrors: 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new IwxxmParserOptions(maxRuleReportBytes: 0));
            Assert.Equal(100, IwxxmParserOptions.Default.MaxValidationErrors);
            Assert.Equal(4L * 1024 * 1024, IwxxmParserOptions.Default.MaxRuleReportBytes);
        }

        internal static string InvalidWeatherCodes() =>
            SampleDocuments.Text("metar-A3-1.xml").Replace("http://codes.wmo.int/306/4678/", "http://codes.wmo.int/306/4678/ZZ");

        private static ValidationError Warning(string code) =>
            new ValidationError(code, code, ErrorCategory.BusinessRuleViolation, ValidationSeverity.Warning);

        private static IReadOnlyList<ValidationError> Validate(Extensibility.ISchematronValidator validator, string xml)
        {
            return validator.Validate(new Extensibility.IwxxmDocumentContext(Encoding.UTF8.GetBytes(xml)));
        }

        private static IReadOnlyList<ValidationError> Validate(Extensibility.ISchemaValidator validator, string xml)
        {
            return validator.Validate(new Extensibility.IwxxmDocumentContext(Encoding.UTF8.GetBytes(xml)));
        }
    }
}
