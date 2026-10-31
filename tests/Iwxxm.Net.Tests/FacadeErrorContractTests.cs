using System;
using System.Collections.Generic;
using System.IO;
using Iwxxm.Net.Extensibility;
using Iwxxm.Net.Reports;
using Xunit;

namespace Iwxxm.Net.Tests
{
    /// <summary>
    /// Verifies that the <see cref="Result{T}"/>-returning facades never let I/O or adapter exceptions escape
    /// (CLN-04, AUD-M4). Only argument validation (null / whitespace arguments) may throw.
    /// </summary>
    public sealed class FacadeErrorContractTests
    {
        private const string MetarPath = "Samples/wmo-examples/metar-A3-1.xml";
        private const string WafsPath = "Samples/wmo-examples/WAFS-Example.xml";

        [Fact]
        public void ParseFile_MissingFile_ReturnsFileNotFound()
        {
            var result = IwxxmParser.ParseFile(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".xml"));

            Assert.True(result.IsFailure);
            Assert.Equal("FILE-NOT-FOUND", Assert.Single(result.Errors).Code);
        }

        [Fact]
        public void ParseFile_MissingDirectory_ReturnsFileNotFound()
        {
            string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "metar.xml");

            var result = IwxxmParser.ParseFile(path);

            Assert.True(result.IsFailure);
            Assert.Equal("FILE-NOT-FOUND", Assert.Single(result.Errors).Code);
        }

        [Fact]
        public void ParseFile_DirectoryInsteadOfFile_ReturnsAccessDenied()
        {
            string directory = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))).FullName;
            try
            {
                var result = IwxxmParser.ParseFile(directory);

                Assert.True(result.IsFailure);
                Assert.Equal("ACCESS-DENIED", Assert.Single(result.Errors).Code);
                Assert.Equal(ErrorCategory.MalformedInput, result.Errors[0].Category);
            }
            finally
            {
                Directory.Delete(directory);
            }
        }

        [Fact]
        public void ParseFile_FileLockedByAnotherHandle_ReturnsFileReadError()
        {
            string path = CopyToTemp(MetarPath);
            try
            {
                using (new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                {
                    var suite = IwxxmParser.ParseFile(path);
                    var sigwx = IwxxmSigwxParser.ParseFile(path);
                    var geoJson = IwxxmParser.ParseFileToGeoJson(path);

                    Assert.Equal("FILE-READ-ERROR", Assert.Single(suite.Errors).Code);
                    Assert.Equal("FILE-READ-ERROR", Assert.Single(sigwx.Errors).Code);
                    Assert.Equal("FILE-READ-ERROR", Assert.Single(geoJson.Errors).Code);
                }
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public void ParseFile_PathWithInvalidCharacter_ReturnsFileReadError()
        {
            var result = IwxxmParser.ParseFile("metar\0.xml");

            Assert.True(result.IsFailure);
            Assert.Equal("FILE-READ-ERROR", Assert.Single(result.Errors).Code);
        }

        [Fact]
        public void FileVariants_DirectoryInsteadOfFile_NeverThrow()
        {
            string directory = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))).FullName;
            try
            {
                Assert.True(IwxxmParser.ParseFileAs<AerodromeObservation>(directory).IsFailure);
                Assert.True(IwxxmParser.ParseBulletinFile(directory).IsFailure);
                Assert.True(IwxxmParser.ParseFileToGeoJson(directory).IsFailure);
                Assert.True(IwxxmSigwxParser.ParseFile(directory).IsFailure);
            }
            finally
            {
                Directory.Delete(directory);
            }
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void ParseFile_NullOrWhitespacePath_StillThrowsArgumentException(string? path)
        {
            Assert.ThrowsAny<ArgumentException>(() => IwxxmParser.ParseFile(path!));
            Assert.ThrowsAny<ArgumentException>(() => IwxxmSigwxParser.ParseFile(path!));
        }

        [Fact]
        public void ProcessToGeoJson_ExporterThrows_ReturnsGeoJsonExportError()
        {
            var parser = new IwxxmParserBuilder().UseGeoJsonExporter(new ThrowingGeoJsonExporter()).Build();

            Result<string> result = parser.ProcessFileToGeoJson(MetarPath);

            Assert.True(result.IsFailure);
            Assert.Equal("GEOJSON-EXPORT-ERROR", Assert.Single(result.Errors).Code);
        }

        [Fact]
        public void Process_SchemaValidatorThrows_ReturnsSchemaValidatorError()
        {
            var builder = new IwxxmParserBuilder().UseSchemaValidator(new ThrowingValidator());

            var suite = builder.Build().ProcessFile(MetarPath);
            var sigwx = builder.BuildSigwxParser().ProcessFile(WafsPath);

            Assert.Equal("SCHEMA-VALIDATOR-ERROR", Assert.Single(suite.Errors).Code);
            Assert.Equal(ErrorCategory.SchemaViolation, suite.Errors[0].Category);
            Assert.Equal("SCHEMA-VALIDATOR-ERROR", Assert.Single(sigwx.Errors).Code);
        }

        [Fact]
        public void Process_RuleValidatorThrows_ReturnsRuleValidatorError()
        {
            var builder = new IwxxmParserBuilder().UseSchematronValidator(new ThrowingValidator());

            var suite = builder.Build().ProcessFile(MetarPath);
            var sigwx = builder.BuildSigwxParser().ProcessFile(WafsPath);

            Assert.Equal("RULE-VALIDATOR-ERROR", Assert.Single(suite.Errors).Code);
            Assert.Equal(ErrorCategory.BusinessRuleViolation, suite.Errors[0].Category);
            Assert.Equal("RULE-VALIDATOR-ERROR", Assert.Single(sigwx.Errors).Code);
        }

        [Fact]
        public void Process_MapperThrows_ReturnsMapperError()
        {
            var builder = new IwxxmParserBuilder()
                .UseDocumentMapper(new ThrowingMapper())
                .UseSigwxDocumentMapper(new ThrowingMapper());

            var suite = builder.Build().ProcessFile(MetarPath);
            var sigwx = builder.BuildSigwxParser().ProcessFile(WafsPath);

            Assert.Equal("MAPPER-ERROR", Assert.Single(suite.Errors).Code);
            Assert.Equal(ErrorCategory.DomainMapping, suite.Errors[0].Category);
            Assert.Equal("MAPPER-ERROR", Assert.Single(sigwx.Errors).Code);
        }

        [Fact]
        public void Process_AdapterReturnsNull_ReturnsTypedErrorInsteadOfThrowing()
        {
            var parser = new IwxxmParserBuilder().UseSchematronValidator(new NullReturningValidator()).Build();

            var result = parser.ProcessFile(MetarPath);

            Assert.Equal("RULE-VALIDATOR-ERROR", Assert.Single(result.Errors).Code);
        }

        private static string CopyToTemp(string source)
        {
            string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".xml");
            File.Copy(source, path);
            return path;
        }

        private sealed class ThrowingGeoJsonExporter : IGeoJsonExporter
        {
            public string Export(IwxxmDocument document, GeoJsonExportOptions options) =>
                throw new InvalidOperationException("Simulated exporter failure.");
        }

        private sealed class ThrowingValidator : ISchemaValidator, ISchematronValidator
        {
            public IReadOnlyList<ValidationError> Validate(IwxxmDocumentContext document) =>
                throw new IOException("Simulated validator failure.");
        }

        private sealed class NullReturningValidator : ISchematronValidator
        {
            public IReadOnlyList<ValidationError> Validate(IwxxmDocumentContext document) => null!;
        }

        private sealed class ThrowingMapper : IIwxxmDocumentMapper, ISigwxDocumentMapper
        {
            Result<IwxxmDocument> IIwxxmDocumentMapper.Map(IwxxmDocumentContext validatedDocument) =>
                throw new InvalidOperationException("Simulated mapper failure.");

            Result<Sigwx> ISigwxDocumentMapper.Map(IwxxmDocumentContext validatedDocument) =>
                throw new InvalidOperationException("Simulated mapper failure.");
        }
    }
}
