using System.Collections.Generic;
using System.IO;
using System.Linq;
using Iwxxm.Net.Infrastructure.Mapping;
using Iwxxm.Net.Reports;
using Xunit;

namespace Iwxxm.Net.Tests
{
    /// <summary>
    /// AUD-R5: the legacy unit spelling <c>kn_i</c> (UCUM is <c>[kn_i]</c>) is accepted as knots, but the non-conformance is
    /// always reported: one <c>ACL-MEASURE-UOM-NON-UCUM-ALIAS</c> warning per alias and field for the whole document.
    /// </summary>
    public sealed class UcumAliasTests
    {
        private const string Code = "ACL-MEASURE-UOM-NON-UCUM-ALIAS";

        private static string CompactSample() => File.ReadAllText("Samples/sigwx-swh-2025-2.xml");

        [Fact]
        public void Reader_WithoutACollector_StaysStrict()
        {
            var errors = new List<ValidationError>();

            Speed? speed = UomMeasureReader.ReadSpeed("135", "kn_i", "x/windSpeed", errors, UomAliasCollector.NoAliases);

            Assert.Null(speed);
            Assert.Equal("ACL-MEASURE-UOM-UNSUPPORTED", Assert.Single(errors).Code);
            Assert.Empty(UomAliasCollector.NoAliases.Attach(Result<int>.Success(1)).Warnings);
        }

        [Fact]
        public void Reader_WithACollector_AcceptsTheAlias_AndTheOwnerReportsOneAggregatedWarning()
        {
            var errors = new List<ValidationError>();
            var aliases = new UomAliasCollector();

            Assert.Equal(new Speed(135, SpeedUnit.Knots), UomMeasureReader.ReadSpeed("135", "kn_i", "a/windSpeed", errors, aliases));
            Assert.Equal(new Speed(100, SpeedUnit.Knots), UomMeasureReader.ReadSpeed("100", "kn_i", "b/windSpeed", errors, aliases));
            Assert.Equal(new Speed(5, SpeedUnit.Knots), UomMeasureReader.ReadSpeed("5", "[kn_i]", "c/windSpeed", errors, aliases)); // the UCUM code is not counted
            Result<int> result = aliases.Attach(Result<int>.Success(1));

            Assert.Empty(errors);
            ValidationError warning = Assert.Single(result.Warnings);
            Assert.Equal(Code, warning.Code);
            Assert.Equal(ValidationSeverity.Warning, warning.Severity);
            Assert.Contains("2 'windSpeed' value(s)", warning.Message);
            Assert.Equal("a/windSpeed", warning.Location);
        }

        [Fact]
        public void Reader_WithACollector_StillRejectsOtherUnknownUnits()
        {
            var errors = new List<ValidationError>();

            Assert.Null(UomMeasureReader.ReadSpeed("135", "kt", "x/windSpeed", errors, new UomAliasCollector()));

            Assert.Equal("ACL-MEASURE-UOM-UNSUPPORTED", Assert.Single(errors).Code);
        }

        [Fact]
        public void CompactSigwx_WithKnI_MapsWithOneWarningCountingBothWindSpeeds()
        {
            string xml = CompactSample().Replace("uom=\"[kn_i]\"", "uom=\"kn_i\"");

            Result<Sigwx> result = IwxxmSigwxParser.Parse(xml);

            Assert.True(result.IsSuccess, string.Join(" | ", result.Errors));
            Assert.All(result.Value.JetStreams, j => Assert.Equal(SpeedUnit.Knots, j.MaxWindSpeed.Unit));
            ValidationError warning = Assert.Single(result.Warnings);
            Assert.Equal(Code, warning.Code);
            Assert.Contains("2 'windSpeed' value(s)", warning.Message);
            Assert.Contains("'kn_i'", warning.Message);
            Assert.Contains("'[kn_i]'", warning.Message);
        }

        [Fact]
        public void CompactSigwx_WithTheUcumCode_HasNoAliasWarning()
        {
            Result<Sigwx> result = IwxxmSigwxParser.Parse(CompactSample());

            Assert.True(result.IsSuccess, string.Join(" | ", result.Errors));
            Assert.DoesNotContain(result.Warnings, w => w.Code == Code);
        }

        [Fact]
        public void TheAlias_AlsoWorksThroughTheGenericEntryPoint_WithOneWarning()
        {
            string xml = CompactSample().Replace("uom=\"[kn_i]\"", "uom=\"kn_i\"");

            Result<IwxxmDocument> result = IwxxmParser.Parse(xml);

            Assert.True(result.IsSuccess, string.Join(" | ", result.Errors));
            Assert.Single(result.Warnings, w => w.Code == Code);
        }
    }
}
