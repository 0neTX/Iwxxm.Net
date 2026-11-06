using System;
using System.Collections.Generic;
using System.IO;
using Iwxxm.Net.Application.Ports.Out;
using Iwxxm.Net.Extensibility;
using Iwxxm.Net.Reports;
using Xunit;

namespace Iwxxm.Net.Tests
{
    /// <summary>
    /// Verifies the composition root (<see cref="IwxxmParserBuilder"/>): adapter replacement, disabling Schematron,
    /// options propagation and the instance-based facades.
    /// </summary>
    public sealed class CompositionRootTests
    {
        private const string MetarPath = "Samples/wmo-examples/metar-A3-1.xml";
        private const string WafsPath = "Samples/wmo-examples/WAFS-Example.xml";

        [Fact]
        public void Build_WithDefaults_ParsesOfficialExampleThroughInstanceApi()
        {
            IwxxmParser parser = new IwxxmParserBuilder().Build();

            Result<Metar> result = parser.ProcessFileAs<Metar>(MetarPath);

            Assert.True(result.IsSuccess, string.Join("; ", result.Errors));
        }

        [Fact]
        public void StaticConvenienceMembers_UseTheSharedDefaultInstance()
        {
            Assert.Same(IwxxmParser.Default, IwxxmParser.Default);
            Assert.Same(IwxxmSigwxParser.Default, IwxxmSigwxParser.Default);
            Assert.Same(IwxxmGeoJsonExporter.Default, IwxxmGeoJsonExporter.Default);
            Assert.True(IwxxmParser.ParseFile(MetarPath).IsSuccess);
        }

        [Fact]
        public void UseSchematronValidator_ReplacesTheBusinessRuleAdapterForBothPipelines()
        {
            var rule = new ValidationError("CUSTOM-RULE-1", "Custom rule fired.", ErrorCategory.BusinessRuleViolation);
            var validator = new RecordingSchematronValidator(rule);
            var builder = new IwxxmParserBuilder().UseSchematronValidator(validator);

            var suiteResult = builder.Build().ProcessFile(MetarPath);
            var sigwxResult = builder.BuildSigwxParser().ProcessFile(WafsPath);

            Assert.True(suiteResult.IsFailure);
            Assert.Equal("CUSTOM-RULE-1", Assert.Single(suiteResult.Errors).Code);
            Assert.True(sigwxResult.IsFailure);
            Assert.Equal("CUSTOM-RULE-1", Assert.Single(sigwxResult.Errors).Code);
            Assert.Equal(2, validator.Calls);
        }

        [Fact]
        public void UseSchematronValidator_Factory_ReceivesTheConfiguredOptions()
        {
            var options = new IwxxmParserOptions(maxDocumentBytes: 4L * 1024L * 1024L);
            IwxxmParserOptions? seen = null;

            new IwxxmParserBuilder()
                .WithOptions(options)
                .UseSchematronValidator(o =>
                {
                    seen = o;
                    return new RecordingSchematronValidator();
                })
                .Build();

            Assert.Same(options, seen);
        }

        [Fact]
        public void WithoutSchematron_SkipsBusinessRuleValidation()
        {
            // Violates official rule WAFSSignificantWeatherForecast-1 (enforced by the built-in subset).
            string invalid = File.ReadAllText(WafsPath).Replace(
                "<iwxxm:phenomenonCategory>weatherForecasts</iwxxm:phenomenonCategory>",
                "<iwxxm:phenomenonCategory>weatherObservations</iwxxm:phenomenonCategory>");

            var withRules = new IwxxmParserBuilder().BuildSigwxParser().Process(invalid);
            var withoutRules = new IwxxmParserBuilder().WithoutSchematron().BuildSigwxParser().Process(invalid);

            Assert.True(withRules.IsFailure);
            Assert.True(withoutRules.IsSuccess, string.Join("; ", withoutRules.Errors));
        }

        [Fact]
        public void UseSchemaValidator_ReplacesStructuralValidation()
        {
            var structural = new ValidationError("CUSTOM-XSD", "Rejected by custom schema validator.", ErrorCategory.SchemaViolation);
            var parser = new IwxxmParserBuilder().UseSchemaValidator(new FixedSchemaValidator(structural)).Build();

            var result = parser.ProcessFile(MetarPath);

            Assert.True(result.IsFailure);
            Assert.Equal("CUSTOM-XSD", Assert.Single(result.Errors).Code);
        }

        [Fact]
        public void WithOptions_PropagatesSizeLimitToTheFacade()
        {
            var parser = new IwxxmParserBuilder().WithOptions(new IwxxmParserOptions(maxDocumentBytes: 128)).Build();

            var result = parser.ProcessFile(MetarPath);

            Assert.True(result.IsFailure);
            Assert.Contains(result.Errors, e => e.Code == "XML-SIZE-LIMIT" && e.Category == ErrorCategory.MalformedInput);
        }

        [Fact]
        public void UseGeoJsonExporter_ReplacesTheExporterUsedByTheParserFacade()
        {
            var parser = new IwxxmParserBuilder().UseGeoJsonExporter(new FixedGeoJsonExporter("{\"custom\":true}")).Build();

            var result = parser.ProcessFileToGeoJson(MetarPath);

            Assert.True(result.IsSuccess, string.Join("; ", result.Errors));
            Assert.Equal("{\"custom\":true}", result.Value);
        }

        [Fact]
        public void UseDocumentMapper_ReplacesDomainMapping()
        {
            var mappingError = new ValidationError("CUSTOM-MAP", "Custom mapper.", ErrorCategory.DomainMapping);
            var parser = new IwxxmParserBuilder().UseDocumentMapper(new FailingDocumentMapper(mappingError)).Build();

            var result = parser.ProcessFile(MetarPath);

            Assert.Equal("CUSTOM-MAP", Assert.Single(result.Errors).Code);
        }

        [Fact]
        public void Builder_RejectsNullAdapters()
        {
            var builder = new IwxxmParserBuilder();

            Assert.Throws<ArgumentNullException>(() => builder.WithOptions(null!));
            Assert.Throws<ArgumentNullException>(() => builder.UseSchemaValidator(null!));
            Assert.Throws<ArgumentNullException>(() => builder.UseSchematronValidator((ISchematronValidator)null!));
            Assert.Throws<ArgumentNullException>(() => builder.UseSchematronValidator((Func<IwxxmParserOptions, ISchematronValidator>)null!));
            Assert.Throws<ArgumentNullException>(() => builder.UseDocumentMapper(null!));
            Assert.Throws<ArgumentNullException>(() => builder.UseSigwxDocumentMapper(null!));
            Assert.Throws<ArgumentNullException>(() => builder.UseGeoJsonExporter(null!));
        }

        private sealed class RecordingSchematronValidator : ISchematronValidator
        {
            private readonly ValidationError[] _errors;

            public RecordingSchematronValidator(params ValidationError[] errors)
            {
                _errors = errors;
            }

            public int Calls { get; private set; }

            public IReadOnlyList<ValidationError> Validate(IwxxmDocumentContext document)
            {
                Calls++;
                using (Stream stream = document.OpenRead())
                {
                    Assert.True(stream.CanSeek);
                    Assert.Equal(0, stream.Position);
                    Assert.Equal(document.Length, stream.Length);
                }

                return _errors;
            }
        }

        private sealed class FixedSchemaValidator : ISchemaValidator
        {
            private readonly ValidationError _error;

            public FixedSchemaValidator(ValidationError error)
            {
                _error = error;
            }

            public IReadOnlyList<ValidationError> Validate(IwxxmDocumentContext document) => new[] { _error };
        }

        private sealed class FixedGeoJsonExporter : IGeoJsonExporter
        {
            private readonly string _json;

            public FixedGeoJsonExporter(string json)
            {
                _json = json;
            }

            public string Export(IwxxmDocument document, GeoJsonExportOptions options) => _json;
        }

        private sealed class FailingDocumentMapper : IIwxxmDocumentMapper
        {
            private readonly ValidationError _error;

            public FailingDocumentMapper(ValidationError error)
            {
                _error = error;
            }

            public Result<IwxxmDocument> Map(IwxxmDocumentContext validatedDocument) => Result<IwxxmDocument>.Failure(_error);
        }
    }
}
