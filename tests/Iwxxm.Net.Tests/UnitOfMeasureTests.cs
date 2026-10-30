using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Iwxxm.Net.Reports;
using Iwxxm.Net.Infrastructure.Mapping;
using Xunit;

namespace Iwxxm.Net.Tests
{
    /// <summary>
    /// Tests for unit-of-measure (<c>@uom</c>) handling: <see cref="Speed"/> / <see cref="Length"/> value objects,
    /// the <see cref="UomMeasureReader"/> anti-corruption helper, and end-to-end mapping of official and synthetic documents.
    /// </summary>
    public sealed class UnitOfMeasureTests
    {
        private const string MetarPath = "Samples/wmo-examples/metar-A3-1.xml";
        private const string SpeciPath = "Samples/wmo-examples/speci-A3-2.xml";
        private const string SigmetPath = "Samples/wmo-examples/sigmet-A6-1a-TS.xml";
        private const string VaaPath = "Samples/wmo-examples/va-advisory-A7-2.xml";
        private const double Tolerance = 1e-4;

        // ------------------------------------------------------------------
        // Value objects
        // ------------------------------------------------------------------

        [Fact]
        public void Speed_ConvertsWithExactFactors()
        {
            var knots = new Speed(25.0, SpeedUnit.Knots);
            Assert.Equal(25.0 * 1852.0 / 3600.0, knots.ToMetersPerSecond(), 12);
            Assert.Equal(12.8611, knots.ToMetersPerSecond(), 4);
            Assert.Equal(46.3, knots.ToKilometersPerHour(), 10);
            Assert.Equal(25.0, knots.ToKnots());

            var kmh = new Speed(36.0, SpeedUnit.KilometersPerHour);
            Assert.Equal(10.0, kmh.ToMetersPerSecond(), 12);
            Assert.Equal(36.0 * 1000.0 / 1852.0, kmh.ToKnots(), 12);

            var ms = new Speed(10.0, SpeedUnit.MetersPerSecond);
            Assert.Equal(36.0, ms.ToKilometersPerHour(), 12);
            Assert.Equal(10.0 * 3600.0 / 1852.0, ms.ToKnots(), 12);
            Assert.Equal("[kn_i]", knots.UnitOfMeasure);
            Assert.Equal("km/h", kmh.UnitOfMeasure);
            Assert.Equal("m/s", ms.UnitOfMeasure);
        }

        [Fact]
        public void Speed_EqualityIsStructuralAndExactAndConsistentWithHashCode()
        {
            var a = new Speed(25.0, SpeedUnit.Knots);
            var b = new Speed(25.0, SpeedUnit.Knots);
            var sameMagnitudeOtherUnit = new Speed(a.ToMetersPerSecond(), SpeedUnit.MetersPerSecond);

            Assert.Equal(a, b);
            Assert.True(a == b);
            Assert.Equal(a.GetHashCode(), b.GetHashCode());
            Assert.NotEqual(a, sameMagnitudeOtherUnit);
            Assert.NotEqual(a, new Speed(25.000001, SpeedUnit.Knots));

            var zero = new Speed(0.0, SpeedUnit.MetersPerSecond);
            var negativeZero = new Speed(-0.0, SpeedUnit.MetersPerSecond);
            Assert.Equal(zero, negativeZero);
            Assert.Equal(zero.GetHashCode(), negativeZero.GetHashCode());

            Assert.Throws<ArgumentOutOfRangeException>(() => new Speed(double.NaN, SpeedUnit.Knots));
            Assert.Throws<ArgumentOutOfRangeException>(() => new Speed(1.0, (SpeedUnit)42));
        }

        [Fact]
        public void Length_ConvertsWithExactFactorsAndKeepsReportedUnit()
        {
            var feet = new Length(1000.0, LengthUnit.Feet);
            Assert.Equal(304.8, feet.ToMeters(), 10);
            Assert.Equal(1000.0, feet.ToFeet());
            Assert.Equal("[ft_i]", feet.UnitOfMeasure);

            var meters = new Length(304.8, LengthUnit.Meters);
            Assert.Equal(1000.0, meters.ToFeet(), 10);
            Assert.NotEqual(feet, meters);
            Assert.Equal(1852.0, new Length(1.0, LengthUnit.NauticalMiles).ToMeters());
            Assert.Equal(1609.344, new Length(1.0, LengthUnit.StatuteMiles).ToMeters());
            Assert.Equal(2.5, new Length(2500.0, LengthUnit.Meters).ToKilometers(), 12);
            Assert.Equal(-10.0, new Length(-10.0, LengthUnit.Meters).ToMeters());
            Assert.Equal(new Length(-0.0, LengthUnit.Feet).GetHashCode(), new Length(0.0, LengthUnit.Feet).GetHashCode());
        }

        // ------------------------------------------------------------------
        // UomMeasureReader (mapper helper)
        // ------------------------------------------------------------------

        [Theory]
        [InlineData("m/s", SpeedUnit.MetersPerSecond)]
        [InlineData("[kn_i]", SpeedUnit.Knots)]
        [InlineData("km/h", SpeedUnit.KilometersPerHour)]
        public void ReadSpeed_SupportedUcumUnits_KeepValueAndUnit(string uom, SpeedUnit expectedUnit)
        {
            var errors = new List<ValidationError>();
            Speed? speed = UomMeasureReader.ReadSpeed("12.5", uom, "test", errors);

            Assert.Empty(errors);
            Assert.Equal(new Speed(12.5, expectedUnit), speed);
        }

        [Theory]
        [InlineData(null, UomMeasureReader.UomMissingCode)]
        [InlineData("", UomMeasureReader.UomMissingCode)]
        [InlineData("kt", UomMeasureReader.UomUnsupportedCode)]
        [InlineData("M/S", UomMeasureReader.UomUnsupportedCode)]
        public void ReadSpeed_MissingOrUnknownUom_ReturnsMappingError(string? uom, string expectedCode)
        {
            var errors = new List<ValidationError>();
            Speed? speed = UomMeasureReader.ReadSpeed("12.5", uom, "test", errors);

            Assert.Null(speed);
            var error = Assert.Single(errors);
            Assert.Equal(expectedCode, error.Code);
            Assert.Equal(ErrorCategory.DomainMapping, error.Category);
        }

        [Theory]
        [InlineData("m", LengthUnit.Meters)]
        [InlineData("[ft_i]", LengthUnit.Feet)]
        [InlineData("km", LengthUnit.Kilometers)]
        [InlineData("[nmi_i]", LengthUnit.NauticalMiles)]
        [InlineData("[mi_i]", LengthUnit.StatuteMiles)]
        public void ReadLength_SupportedUcumUnits_KeepValueAndUnit(string uom, LengthUnit expectedUnit)
        {
            var errors = new List<ValidationError>();
            Length? length = UomMeasureReader.ReadLength("300", uom, "test", errors);

            Assert.Empty(errors);
            Assert.Equal(new Length(300.0, expectedUnit), length);
        }

        [Fact]
        public void ReadLength_UnknownUomOrInvalidValue_ReturnsMappingError()
        {
            var errors = new List<ValidationError>();
            Assert.Null(UomMeasureReader.ReadLength("300", "FT", "test", errors));
            Assert.Null(UomMeasureReader.ReadLength("abc", "m", "test", errors));
            Assert.Equal(
                new[] { UomMeasureReader.UomUnsupportedCode, UomMeasureReader.ValueInvalidCode },
                errors.Select(e => e.Code).ToArray());
        }

        [Fact]
        public void ReadFixedUnit_MismatchingOrMissingUom_ReturnsMappingError()
        {
            var errors = new List<ValidationError>();
            Assert.Equal(17.0, UomMeasureReader.ReadFixedUnit("17.0", "Cel", "Cel", "test", errors));
            Assert.Empty(errors);

            Assert.Null(UomMeasureReader.ReadFixedUnit("290.15", "K", "Cel", "test", errors));
            Assert.Null(UomMeasureReader.ReadFixedUnit("1018", null, "hPa", "test", errors));
            Assert.Equal(
                new[] { UomMeasureReader.UomMismatchCode, UomMeasureReader.UomMissingCode },
                errors.Select(e => e.Code).ToArray());
        }

        // ------------------------------------------------------------------
        // End-to-end with official WMO examples and synthetic variants
        // ------------------------------------------------------------------

        [Fact]
        public void ParseFileAs_OfficialSpeciA32_KeepsKnotsAndConvertsExactly()
        {
            var result = IwxxmParser.ParseFileAs<AerodromeObservation>(SpeciPath);

            Assert.True(result.IsSuccess, string.Join(" | ", result.Errors));
            SurfaceWind wind = result.Value.SurfaceWind!;
            Assert.Equal(new Speed(25.0, SpeedUnit.Knots), wind.MeanSpeed);
            Assert.Equal(12.8611, wind.MeanSpeed!.Value.ToMetersPerSecond(), 4);
            Assert.Equal(new Speed(37.0, SpeedUnit.Knots), wind.GustSpeed);
            Assert.Equal(19.0344, wind.GustSpeed!.Value.ToMetersPerSecond(), 4);
            Assert.Equal(new Length(500.0, LengthUnit.Feet), result.Value.CloudLayers[0].Base);
        }

        [Fact]
        public void ParseFileAs_OfficialMetarA31_KeepsMetersPerSecondAndFeet()
        {
            var result = IwxxmParser.ParseFileAs<AerodromeObservation>(MetarPath);

            Assert.True(result.IsSuccess, string.Join(" | ", result.Errors));
            Assert.Equal(new Speed(4.0, SpeedUnit.MetersPerSecond), result.Value.SurfaceWind!.MeanSpeed);
            Assert.Equal(new Length(1000.0, LengthUnit.Feet), result.Value.CloudLayers[0].Base);
            Assert.Equal(304.8, result.Value.CloudLayers[0].Base!.Value.ToMeters(), 10);
        }

        [Theory]
        [InlineData("[kn_i]", SpeedUnit.Knots)]
        public void Parse_MetarWithAlternativeWindUnit_KeepsReportedUnit(string uom, SpeedUnit expected)
        {
            string xml = File.ReadAllText(MetarPath).Replace(
                "<iwxxm:meanWindSpeed uom=\"m/s\">4.0</iwxxm:meanWindSpeed>",
                $"<iwxxm:meanWindSpeed uom=\"{uom}\">4.0</iwxxm:meanWindSpeed>");

            var result = IwxxmParser.ParseAs<AerodromeObservation>(xml);

            Assert.True(result.IsSuccess, string.Join(" | ", result.Errors));
            Assert.Equal(new Speed(4.0, expected), result.Value.SurfaceWind!.MeanSpeed);
        }

        [Fact]
        public void Parse_MetarWithCloudBaseInMeters_KeepsMeters()
        {
            string xml = File.ReadAllText(MetarPath).Replace(
                "<iwxxm:base uom=\"[ft_i]\">1000</iwxxm:base>",
                "<iwxxm:base uom=\"m\">300</iwxxm:base>");

            var result = IwxxmParser.ParseAs<AerodromeObservation>(xml);

            Assert.True(result.IsSuccess, string.Join(" | ", result.Errors));
            Assert.Equal(new Length(300.0, LengthUnit.Meters), result.Value.CloudLayers[0].Base);
            Assert.Equal(984.2520, result.Value.CloudLayers[0].Base!.Value.ToFeet(), 4);
        }

        [Fact]
        public void Parse_MetarWithUnknownWindUnit_FailsWithMappingError()
        {
            string xml = File.ReadAllText(MetarPath).Replace(
                "<iwxxm:meanWindSpeed uom=\"m/s\">4.0</iwxxm:meanWindSpeed>",
                "<iwxxm:meanWindSpeed uom=\"furlong/fortnight\">4.0</iwxxm:meanWindSpeed>");

            var result = IwxxmParser.Parse(xml);

            Assert.True(result.IsFailure);
            Assert.Contains(result.Errors, e =>
                e.Category == ErrorCategory.DomainMapping && e.Code == UomMeasureReader.UomNotAllowedCode);
        }

        [Fact]
        public void Parse_MetarWithAirTemperatureInKelvin_FailsWithUomMismatch()
        {
            string xml = File.ReadAllText(MetarPath).Replace(
                "<iwxxm:airTemperature uom=\"Cel\">17.0</iwxxm:airTemperature>",
                "<iwxxm:airTemperature uom=\"K\">290.15</iwxxm:airTemperature>");

            var result = IwxxmParser.Parse(xml);

            Assert.True(result.IsFailure);
            Assert.Contains(result.Errors, e =>
                e.Category == ErrorCategory.DomainMapping && e.Code == UomMeasureReader.UomMismatchCode);
        }

        [Theory]
        [InlineData("[kn_i]", SpeedUnit.Knots)]
        [InlineData("km/h", SpeedUnit.KilometersPerHour)]
        public void Parse_SigmetSpeedOfMotion_KeepsReportedUnit(string uom, SpeedUnit expected)
        {
            string xml = File.ReadAllText(SigmetPath).Replace(
                "<iwxxm:speedOfMotion uom=\"[kn_i]\">20</iwxxm:speedOfMotion>",
                $"<iwxxm:speedOfMotion uom=\"{uom}\">20</iwxxm:speedOfMotion>");

            var result = IwxxmParser.ParseAs<Sigmet>(xml);

            Assert.True(result.IsSuccess, string.Join(" | ", result.Errors));
            Assert.Contains(result.Value.EvolvingConditions, v => v.SpeedOfMotion == new Speed(20.0, expected));
        }

        [Fact]
        public void ParseFileAs_OfficialVaaA72_KeepsSpeedOfMotionInKnotsAndSourceElevation()
        {
            var result = IwxxmParser.ParseFileAs<Vaa>(VaaPath);

            Assert.True(result.IsSuccess, string.Join(" | ", result.Errors));
            Assert.Contains(result.Value.ObservedAshClouds, c => c.SpeedOfMotion == new Speed(20.0, SpeedUnit.Knots));
            Assert.Equal(LengthUnit.Meters, result.Value.SourceElevation!.Value.Unit);
        }

        [Fact]
        public void ParseFileToGeoJson_SpeciA32_EmitsValueWithUnit()
        {
            var result = IwxxmParser.ParseFileToGeoJson(SpeciPath);

            Assert.True(result.IsSuccess, string.Join(" | ", result.Errors));
            Assert.Contains("\"windSpeed\":25,", result.Value);
            Assert.Contains("\"windSpeedUom\":\"[kn_i]\"", result.Value);
        }
    }
}
