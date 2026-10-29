using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Iwxxm.Net.Infrastructure.Mapping;
using Iwxxm.Net.Reports;
using Xunit;

namespace Iwxxm.Net.Tests
{
    /// <summary>
    /// Verifies the <see cref="VerticalLimit"/> value object (DDD-08, AUD-M7, AUD-U7): vertical limits keep the reported
    /// value, unit (<c>aixm:UomDistanceVerticalType</c>: FT, M, FL, SM, OTHER; UCUM m, [ft_i], km), vertical reference
    /// (<c>aixm:CodeVerticalReferenceBaseType</c>: SFC, MSL, W84, STD, OTHER), special values
    /// (<c>aixm:ValDistanceVerticalBaseType</c>: GND, UNL, FLOOR, CEILING) and nil reasons. A flight level is only a
    /// derived view when the reference is STD.
    /// </summary>
    public sealed class VerticalLimitTests
    {
        private static readonly XNamespace Aixm = "http://www.aixm.aero/schema/5.1.1";
        private static readonly XNamespace Xsi = "http://www.w3.org/2001/XMLSchema-instance";

        // ---------------------------------------------------------------------
        // Domain value object
        // ---------------------------------------------------------------------

        [Fact]
        public void FifteenKilometresAmsl_IsNotAFlightLevel()
        {
            VerticalLimit limit = VerticalLimit.Create(15, VerticalUnit.Kilometers, VerticalReference.Msl).Value;

            Assert.Equal(15, limit.Value);
            Assert.Equal(VerticalUnit.Kilometers, limit.Unit);
            Assert.Equal(VerticalReference.Msl, limit.Reference);
            Assert.Null(limit.AsFlightLevel()); // previously FL492
        }

        [Fact]
        public void Surface_IsDistinguishableFromFl000()
        {
            VerticalLimit ground = VerticalLimit.FromSpecialValue(VerticalSpecialValue.Ground, VerticalReference.Sfc);
            VerticalLimit fl000 = VerticalLimit.Create(0, VerticalUnit.FlightLevel, VerticalReference.Std).Value;

            Assert.NotEqual(ground, fl000);
            Assert.Null(ground.Value);
            Assert.True(ground.IsGround);
            Assert.Null(ground.AsFlightLevel());
            Assert.Equal(new FlightLevel(0), fl000.AsFlightLevel());
        }

        [Fact]
        public void AsFlightLevel_RequiresStdReference_AndAFlightLevelCompatibleUnit()
        {
            Assert.Equal(new FlightLevel(390), VerticalLimit.Create(390, VerticalUnit.FlightLevel, VerticalReference.Std).Value.AsFlightLevel());
            Assert.Equal(new FlightLevel(350), VerticalLimit.Create(35000, VerticalUnit.Feet, VerticalReference.Std).Value.AsFlightLevel());

            // Official WAFS-Example.xml declares uom="FL" with an MSL reference: not a flight level.
            Assert.Null(VerticalLimit.Create(240, VerticalUnit.FlightLevel, VerticalReference.Msl).Value.AsFlightLevel());
            Assert.Null(VerticalLimit.Create(390, VerticalUnit.FlightLevel, null).Value.AsFlightLevel());
            Assert.Null(VerticalLimit.Create(3000, VerticalUnit.Feet, VerticalReference.Sfc).Value.AsFlightLevel());
            Assert.Null(VerticalLimit.Create(12192, VerticalUnit.Meters, VerticalReference.Std).Value.AsFlightLevel());
        }

        [Fact]
        public void Sfc3000Ft_IsNotFl030()
        {
            VerticalLimit sfc = VerticalLimit.Create(3000, VerticalUnit.Feet, VerticalReference.Sfc).Value;
            VerticalLimit fl030 = VerticalLimit.Create(30, VerticalUnit.FlightLevel, VerticalReference.Std).Value;

            Assert.NotEqual(sfc, fl030);
            Assert.Null(sfc.AsFlightLevel());
        }

        [Fact]
        public void Equality_IsExactAndStructural()
        {
            VerticalLimit a = VerticalLimit.Create(250, VerticalUnit.FlightLevel, VerticalReference.Std).Value;
            VerticalLimit b = VerticalLimit.Create(250, VerticalUnit.FlightLevel, VerticalReference.Std).Value;
            VerticalLimit otherReference = VerticalLimit.Create(250, VerticalUnit.FlightLevel, VerticalReference.Msl).Value;
            VerticalLimit otherUnit = VerticalLimit.Create(250, VerticalUnit.Meters, VerticalReference.Std).Value;
            VerticalLimit nil = VerticalLimit.Nil("unknown");
            VerticalLimit nil2 = VerticalLimit.Nil("unknown");
            VerticalLimit nilOther = VerticalLimit.Nil("missing");

            Assert.Equal(a, b);
            Assert.True(a == b);
            Assert.Equal(a.GetHashCode(), b.GetHashCode());
            Assert.NotEqual(a, otherReference);
            Assert.NotEqual(a, otherUnit);
            Assert.Equal(nil, nil2);
            Assert.Equal(nil.GetHashCode(), nil2.GetHashCode());
            Assert.NotEqual(nil, nilOther);
            Assert.Equal(4, new HashSet<VerticalLimit> { a, b, otherReference, otherUnit, nil, nil2 }.Count);
        }

        [Fact]
        public void Nil_KeepsTheNilReasonAndHasNoValue()
        {
            VerticalLimit nil = VerticalLimit.Nil("unknown", null, null);

            Assert.True(nil.IsNil);
            Assert.Equal("unknown", nil.NilReason);
            Assert.Null(nil.Value);
            Assert.Null(nil.AsFlightLevel());
        }

        [Fact]
        public void Create_RejectsNonFiniteValuesAndNegativeFlightLevels()
        {
            Assert.True(VerticalLimit.Create(double.NaN, VerticalUnit.Meters, VerticalReference.Msl).IsFailure);
            Assert.True(VerticalLimit.Create(double.PositiveInfinity, VerticalUnit.Feet, VerticalReference.Msl).IsFailure);
            Assert.Equal("DOMAIN-VERTICAL-LIMIT-INVALID", VerticalLimit.Create(-10, VerticalUnit.FlightLevel, VerticalReference.Std).Errors[0].Code);
            Assert.True(VerticalLimit.Create(-10, VerticalUnit.Meters, VerticalReference.Msl).IsSuccess); // below MSL is physical
        }

        [Fact]
        public void TryCompareTo_OnlyComparesCommensurableLimits()
        {
            VerticalLimit fl100 = VerticalLimit.Create(100, VerticalUnit.FlightLevel, VerticalReference.Std).Value;
            VerticalLimit fl240 = VerticalLimit.Create(240, VerticalUnit.FlightLevel, VerticalReference.Std).Value;
            VerticalLimit m10363 = VerticalLimit.Create(10363.2, VerticalUnit.Meters, VerticalReference.Msl).Value;
            VerticalLimit ft41000 = VerticalLimit.Create(41000, VerticalUnit.Feet, VerticalReference.Msl).Value;

            Assert.True(fl100.TryCompareTo(fl240, out int c1));
            Assert.True(c1 < 0);
            Assert.True(m10363.TryCompareTo(ft41000, out int c2));
            Assert.True(c2 < 0);
            Assert.False(fl100.TryCompareTo(m10363, out _));
            Assert.False(VerticalLimit.Nil("unknown").TryCompareTo(fl100, out _));
        }

        // ---------------------------------------------------------------------
        // Anti-corruption layer reader (aixm:ValDistanceVerticalType / iwxxm:ValDistanceVerticalType)
        // ---------------------------------------------------------------------

        [Theory]
        [InlineData("FL", "390", "STD", VerticalUnit.FlightLevel, 390.0, VerticalReference.Std)]
        [InlineData("M", "15000", "MSL", VerticalUnit.Meters, 15000.0, VerticalReference.Msl)]
        [InlineData("m", "155", "MSL", VerticalUnit.Meters, 155.0, VerticalReference.Msl)]
        [InlineData("FT", "3000", "SFC", VerticalUnit.Feet, 3000.0, VerticalReference.Sfc)]
        [InlineData("[ft_i]", "12500", "MSL", VerticalUnit.Feet, 12500.0, VerticalReference.Msl)]
        [InlineData("km", "15", "MSL", VerticalUnit.Kilometers, 15.0, VerticalReference.Msl)]
        [InlineData("SM", "11000", "STD", VerticalUnit.StandardMeters, 11000.0, VerticalReference.Std)]
        [InlineData("FL", "300", "W84", VerticalUnit.FlightLevel, 300.0, VerticalReference.W84)]
        [InlineData("FL", "300", "OTHER:QFE", VerticalUnit.FlightLevel, 300.0, VerticalReference.Other)]
        public void Reader_KeepsValueUnitAndReference(string uom, string text, string reference, VerticalUnit unit, double value, VerticalReference expectedReference)
        {
            var errors = new List<ValidationError>();

            VerticalLimit? limit = VerticalLimitReader.Read(
                new XElement(Aixm + "upperLimit", new XAttribute("uom", uom), text),
                new XElement(Aixm + "upperLimitReference", reference),
                "test",
                errors);

            Assert.Empty(errors);
            Assert.NotNull(limit);
            Assert.Equal(value, limit!.Value.Value);
            Assert.Equal(unit, limit.Value.Unit);
            Assert.Equal(expectedReference, limit.Value.Reference);
        }

        [Theory]
        [InlineData("GND", VerticalSpecialValue.Ground)]
        [InlineData("SFC", VerticalSpecialValue.Ground)]
        [InlineData("UNL", VerticalSpecialValue.Unlimited)]
        [InlineData("FLOOR", VerticalSpecialValue.Floor)]
        [InlineData("CEILING", VerticalSpecialValue.Ceiling)]
        public void Reader_MapsSpecialValuesWithoutInventingANumber(string text, VerticalSpecialValue expected)
        {
            var errors = new List<ValidationError>();

            VerticalLimit? limit = VerticalLimitReader.Read(
                new XElement(Aixm + "lowerLimit", new XAttribute("uom", "FT"), text),
                new XElement(Aixm + "lowerLimitReference", "SFC"),
                "test",
                errors);

            Assert.Empty(errors);
            Assert.Equal(expected, limit!.Value.SpecialValue);
            Assert.Null(limit.Value.Value);
            Assert.Null(limit.Value.AsFlightLevel());
        }

        [Fact]
        public void Reader_OtherUomWithNilReason_IsAnExplicitNil()
        {
            var errors = new List<ValidationError>();

            // Official spacewx-A7-5.xml: <aixm:upperLimit uom="OTHER" xsi:nil="true" nilReason="unknown"/>
            VerticalLimit? limit = VerticalLimitReader.Read(
                new XElement(Aixm + "upperLimit", new XAttribute("uom", "OTHER"), new XAttribute(Xsi + "nil", "true"), new XAttribute("nilReason", "unknown")),
                null,
                "test",
                errors);

            Assert.Empty(errors);
            Assert.True(limit!.Value.IsNil);
            Assert.Equal("unknown", limit.Value.NilReason);
            Assert.Null(limit.Value.Unit);
        }

        [Fact]
        public void Reader_AbsentElement_IsNull()
        {
            var errors = new List<ValidationError>();

            Assert.Null(VerticalLimitReader.Read(null, null, "test", errors));
            Assert.Empty(errors);
        }

        [Theory]
        [InlineData("OTHER:XYZ", "100", VerticalLimitReader.UnsupportedUomCode)]
        [InlineData("", "100", UomMeasureReader.UomMissingCode)]
        [InlineData("FL", "high", UomMeasureReader.ValueInvalidCode)]
        public void Reader_UninterpretableValues_AreMappingErrors(string uom, string text, string expectedCode)
        {
            var errors = new List<ValidationError>();
            var element = new XElement(Aixm + "upperLimit", text);
            if (uom.Length > 0)
            {
                element.Add(new XAttribute("uom", uom));
            }

            VerticalLimit? limit = VerticalLimitReader.Read(element, null, "test", errors);

            Assert.Null(limit);
            Assert.Equal(expectedCode, Assert.Single(errors).Code);
        }

        // ---------------------------------------------------------------------
        // End-to-end mapping of official examples
        // ---------------------------------------------------------------------

        [Fact]
        public void Sigmet_OfficialExample_KeepsFlightLevelAndStdReference()
        {
            var sigmet = IwxxmParser.ParseFileAs<SigmetReport>("Samples/wmo-examples/sigmet-multi-location-VA.xml");
            Assert.True(sigmet.IsSuccess, string.Join(" | ", sigmet.Errors));

            EnRoutePhenomenonVolume volume = sigmet.Value.EvolvingConditions[0];
            Assert.Equal(VerticalLimit.Create(370, VerticalUnit.FlightLevel, VerticalReference.Std).Value, volume.UpperLimit);
            Assert.Equal(VerticalLimit.Create(250, VerticalUnit.FlightLevel, VerticalReference.Std).Value, volume.LowerLimit);
            Assert.Equal(new FlightLevel(370), volume.UpperLimit!.Value.AsFlightLevel());
        }

        [Fact]
        public void Sigmet_MetresAmslAndSurfaceLimits_AreNotConvertedToFlightLevels()
        {
            string xml = File.ReadAllText("Samples/wmo-examples/sigmet-A6-1a-TS.xml").Replace(
                "<aixm:upperLimit uom=\"FL\">390</aixm:upperLimit>\n                                    <aixm:upperLimitReference>STD</aixm:upperLimitReference>",
                "<aixm:upperLimit uom=\"M\">15000</aixm:upperLimit>\n                                    <aixm:upperLimitReference>MSL</aixm:upperLimitReference>" +
                "<aixm:lowerLimit uom=\"FT\">GND</aixm:lowerLimit><aixm:lowerLimitReference>SFC</aixm:lowerLimitReference>");
            Assert.Contains("uom=\"M\">15000", xml);

            var result = new IwxxmParserBuilder().WithoutSchematron().Build().ProcessAs<SigmetReport>(xml);

            Assert.True(result.IsSuccess, string.Join(" | ", result.Errors));
            EnRoutePhenomenonVolume volume = result.Value.EvolvingConditions[0];
            Assert.Equal(15000, volume.UpperLimit!.Value.Value);
            Assert.Equal(VerticalUnit.Meters, volume.UpperLimit.Value.Unit);
            Assert.Equal(VerticalReference.Msl, volume.UpperLimit.Value.Reference);
            Assert.Null(volume.UpperLimit.Value.AsFlightLevel());
            Assert.True(volume.LowerLimit!.Value.IsGround);
            Assert.Equal(VerticalReference.Sfc, volume.LowerLimit.Value.Reference);
        }

        [Fact]
        public void Qvaci_OfficialExample_KeepsElevatedVolumeLimits()
        {
            var qvaci = IwxxmParser.ParseFileAs<QvaciReport>("Samples/wmo-examples/qvaci-Example.xml");
            Assert.True(qvaci.IsSuccess, string.Join(" | ", qvaci.Errors));

            VolcanicAshConcentrationCloud first = qvaci.Value.AshConcentrationClouds[0];
            Assert.Equal(VerticalLimit.Create(400, VerticalUnit.FlightLevel, VerticalReference.Std).Value, first.UpperLimit);
            Assert.Equal(VerticalLimit.Create(300, VerticalUnit.FlightLevel, VerticalReference.Std).Value, first.LowerLimit);
        }

        [Fact]
        public void Vona_OfficialExample_AshCloudAt15000MetresAmsl_IsNotFl492()
        {
            // vona-A7-1.xml: <iwxxm:elevation uom="M">15000</iwxxm:elevation><iwxxm:verticalReference>MSL</...>
            var vona = IwxxmParser.ParseFileAs<VonaReport>("Samples/wmo-examples/vona-A7-1.xml");
            Assert.True(vona.IsSuccess, string.Join(" | ", vona.Errors));

            VerticalLimit ash = vona.Value.AshCloudElevation!.Value;
            Assert.Equal(VerticalLimit.Create(15000, VerticalUnit.Meters, VerticalReference.Msl).Value, ash);
            Assert.Equal("M", ash.UnitOfMeasure);
            Assert.Equal("MSL", ash.ReferenceCode);
            Assert.Null(ash.AsFlightLevel());
        }

        [Fact]
        public void Wafs_OfficialExample_KeepsMetresAmslInsteadOfFlightLevels()
        {
            var result = IwxxmSigwxParser.ParseFile("Samples/wmo-examples/WAFS-Example.xml");
            Assert.True(result.IsSuccess, string.Join(" | ", result.Errors));
            SignificantWeatherReport report = result.Value;

            Assert.Equal(new FlightLevel(100), report.LowerLimit.AsFlightLevel());
            Assert.Equal(new FlightLevel(600), report.UpperLimit.AsFlightLevel());

            Turbulence turbulence = report.Turbulences[0];
            Assert.Equal(VerticalLimit.Create(10363.2, VerticalUnit.Meters, VerticalReference.Msl).Value, turbulence.Base);
            Assert.Equal(VerticalLimit.Create(12192.0, VerticalUnit.Meters, VerticalReference.Msl).Value, turbulence.Top);
            Assert.Null(turbulence.Top.AsFlightLevel());

            // uom="FL" with an MSL reference in the official example: kept verbatim, no flight-level view.
            AirframeIcing icing = report.AirframeIcings[0];
            Assert.Equal(VerticalLimit.Create(240, VerticalUnit.FlightLevel, VerticalReference.Msl).Value, icing.Top);
            Assert.Null(icing.Top.AsFlightLevel());

            Cloud cloud = report.Clouds[0];
            Assert.True(cloud.Base!.Value.IsNil);
            Assert.Equal("http://codes.wmo.int/iwxxm/nil/inapplicable", cloud.Base.Value.NilReason);
            Assert.Equal(VerticalLimit.Create(12496.8, VerticalUnit.Meters, VerticalReference.Msl).Value, cloud.Top);

            JetStream jet = report.JetStreams[0];
            Assert.Equal(VerticalLimit.Create(15240, VerticalUnit.Meters, VerticalReference.Msl).Value, jet.CoreLevel);
            Assert.Equal(VerticalLimit.Create(14020, VerticalUnit.Meters, VerticalReference.Msl).Value, report.Tropopauses[0].SpotHeights[0].Level);
        }

        [Fact]
        public void GeoJson_EmitsValueUnitAndReferenceForVerticalLimits()
        {
            var geoJson = IwxxmParser.ParseFileToGeoJson("Samples/wmo-examples/sigmet-multi-location-VA.xml");
            Assert.True(geoJson.IsSuccess, string.Join(" | ", geoJson.Errors));

            using (var doc = System.Text.Json.JsonDocument.Parse(geoJson.Value))
            {
                var volume = doc.RootElement.GetProperty("features").EnumerateArray()
                    .First(f => f.GetProperty("properties").TryGetProperty("upperLimit", out _))
                    .GetProperty("properties");

                Assert.Equal(370, volume.GetProperty("upperLimit").GetDouble());
                Assert.Equal("FL", volume.GetProperty("upperLimitUom").GetString());
                Assert.Equal("STD", volume.GetProperty("upperLimitReference").GetString());
                Assert.Equal("FL370", volume.GetProperty("upperLimitFlightLevel").GetString());
                Assert.Equal(250, volume.GetProperty("lowerLimit").GetDouble());
            }
        }
    }
}
