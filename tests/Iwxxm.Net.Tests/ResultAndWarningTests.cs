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

        [Fact]
        public void Map_ProjectsTheValue_KeepsWarnings_AndPropagatesFailures()
        {
            Result<int> mapped = Result<string>.Success("abc", new[] { Warning }).Map(s => s.Length);
            Assert.Equal(3, mapped.Value);
            Assert.Equal(new[] { Warning }, mapped.Warnings);

            int calls = 0;
            Result<int> failed = Result<string>.Failure(Error).Map(s => ++calls);
            Assert.True(failed.IsFailure);
            Assert.Equal(new[] { Error }, failed.Errors);
            Assert.Equal(0, calls);
        }

        [Fact]
        public void Bind_ChainsResults_AndAccumulatesWarningsInOrder()
        {
            var second = new ValidationError("W2", "second", ErrorCategory.DomainMapping, ValidationSeverity.Warning);

            Result<int> chained = Result<string>.Success("abc", new[] { Warning })
                .Bind(s => Result<int>.Success(s.Length, new[] { second }));

            Assert.Equal(3, chained.Value);
            Assert.Equal(new[] { Warning, second }, chained.Warnings);

            Result<int> failedInside = Result<string>.Success("abc", new[] { Warning }).Bind(_ => Result<int>.Failure(Error));
            Assert.Equal(new[] { Error }, failedInside.Errors);

            int calls = 0;
            Result<int> failedBefore = Result<string>.Failure(Error).Bind(_ => Result<int>.Success(++calls));
            Assert.Equal(new[] { Error }, failedBefore.Errors);
            Assert.Equal(0, calls);
        }

        [Fact]
        public void Match_SelectsTheBranch()
        {
            Assert.Equal("ok:1", Result<int>.Success(1).Match(v => "ok:" + v, errors => "ko:" + errors.Count));
            Assert.Equal("ko:1", Result<int>.Failure(Error).Match(v => "ok:" + v, errors => "ko:" + errors.Count));

            var seen = new List<string>();
            Result<int>.Success(2).Match(v => seen.Add("v" + v), errors => seen.Add("e"));
            Result<int>.Failure(Error).Match(v => seen.Add("v" + v), errors => seen.Add("e" + errors[0].Code));
            Assert.Equal(new[] { "v2", "eRULE-E" }, seen);
        }

        [Fact]
        public void Combinators_RejectNullDelegates()
        {
            Result<int> ok = Result<int>.Success(1);
            Assert.Throws<ArgumentNullException>(() => ok.Map<int>(null!));
            Assert.Throws<ArgumentNullException>(() => ok.Bind<int>(null!));
            Assert.Throws<ArgumentNullException>(() => ok.Match<int>(null!, _ => 0));
            Assert.Throws<ArgumentNullException>(() => ok.Match(_ => { }, null!));
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
