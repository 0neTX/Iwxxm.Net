using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Iwxxm.Net.Extensibility;
using Iwxxm.Net.Infrastructure.Validation;
using Iwxxm.Net.Reports;
using Iwxxm.Net.Tests.DataFidelity;
using Xunit;

namespace Iwxxm.Net.Tests
{
    /// <summary>
    /// Warnings that do not fail the pipeline (AUD-U5) and the <see cref="Result{T}"/> combinators (AUD-U6).
    /// </summary>
    public sealed class ResultAndWarningTests
    {
        private static readonly ValidationError Warning =
            new ValidationError("RULE-W", "advisory", ErrorCategory.BusinessRuleViolation, ValidationSeverity.Warning, "/x");

        private static readonly ValidationError Error =
            new ValidationError("RULE-E", "violation", ErrorCategory.BusinessRuleViolation);

        [Fact]
        public void SchematronWarnings_DoNotFailTheParse_AndAreReturnedAsWarnings()
        {
            IwxxmParser parser = new IwxxmParserBuilder().UseSchematronValidator(new FixedValidator(Warning)).Build();

            Result<IwxxmDocument> result = parser.Process(SampleDocuments.Text("metar-A3-1.xml"));

            Assert.True(result.IsSuccess, string.Join(" | ", result.Errors));
            Assert.Equal(new[] { Warning }, result.Warnings);
        }

        [Fact]
        public void SchematronErrors_FailTheParse_WithEveryFindingOfTheStage()
        {
            IwxxmParser parser = new IwxxmParserBuilder().UseSchematronValidator(new FixedValidator(Warning, Error)).Build();

            Result<IwxxmDocument> result = parser.Process(SampleDocuments.Text("metar-A3-1.xml"));

            Assert.True(result.IsFailure);
            Assert.Equal(new[] { Warning, Error }, result.Errors);
            Assert.Empty(result.Warnings);
        }

        [Fact]
        public void SchemaWarnings_AreKept_AndPrecedeRuleAndMapperWarnings()
        {
            var schemaWarning = new ValidationError("XSD-W", "schema advisory", ErrorCategory.SchemaViolation, ValidationSeverity.Warning);
            IwxxmSigwxParser parser = new IwxxmParserBuilder()
                .UseSchemaValidator(new FixedValidator(schemaWarning))
                .UseSchematronValidator(new FixedValidator(Warning))
                .BuildSigwxParser();

            Result<Sigwx> result = parser.ProcessFile(SampleDocuments.PathOf("WAFS-Example.xml"));

            Assert.True(result.IsSuccess, string.Join(" | ", result.Errors));
            Assert.Equal(schemaWarning, result.Warnings[0]);
            Assert.Equal(Warning, result.Warnings[1]);
        }

        [Fact]
        public void BulletinParse_KeepsValidatorWarnings()
        {
            IwxxmParser parser = new IwxxmParserBuilder().UseSchematronValidator(new FixedValidator(Warning)).Build();

            Result<MeteorologicalBulletin> result = parser.ProcessBulletin(SampleDocuments.Text("metar-NIL-collect.xml"));

            Assert.True(result.IsSuccess, string.Join(" | ", result.Errors));
            Assert.Contains(Warning, result.Warnings);
        }

        [Theory]
        [InlineData(null, false, ValidationSeverity.Error)]
        [InlineData(null, true, ValidationSeverity.Warning)]
        [InlineData("WARNING", false, ValidationSeverity.Warning)]
        [InlineData("warn", false, ValidationSeverity.Warning)]
        [InlineData("info", false, ValidationSeverity.Warning)]
        [InlineData("ERROR", true, ValidationSeverity.Error)]
        [InlineData("fatal", true, ValidationSeverity.Error)]
        [InlineData("other", false, ValidationSeverity.Error)]
        public void SvrlSeverity_MapsRolesAndSuccessfulReports(string? role, bool isSuccessfulReport, ValidationSeverity expected)
        {
            Assert.Equal(expected, SvrlSeverity.Map(role, isSuccessfulReport));
        }

        [Fact]
        public void TryGetValue_ReturnsTheValueOnlyOnSuccess()
        {
            Assert.True(Result<int>.Success(7).TryGetValue(out int value));
            Assert.Equal(7, value);

            Assert.False(Result<int>.Failure(Error).TryGetValue(out int missing));
            Assert.Equal(0, missing);
        }

        private sealed class FixedValidator : ISchematronValidator, ISchemaValidator
        {
            private readonly ValidationError[] _findings;

            public FixedValidator(params ValidationError[] findings)
            {
                _findings = findings;
            }

            public IReadOnlyList<ValidationError> Validate(IwxxmDocumentContext document) => _findings.ToList();
        }
    }
}
