using System;
using System.Linq;
using CsCheck;
using Iwxxm.Net.Reports;
using Xunit;

namespace Iwxxm.Net.Tests.Properties
{
    /// <summary>
    /// Properties of the <see cref="Length"/> and <see cref="Speed"/> value objects (value + unit, structural exact equality,
    /// conversion through SI units, non-finite input rejected at construction).
    /// <para>
    /// Conversion contract (read from the code): every unit converts through metres (or metres per second) with one
    /// multiplication and one division, so one conversion is off by at most about 1.1e-16 relative per rounding. A round trip
    /// performs four roundings; <see cref="RoundTripTolerance"/> (1e-12 relative) leaves about three orders of magnitude of
    /// headroom while still being far tighter than any wrong conversion factor, which the anchor property catches separately
    /// (a round trip alone cancels a wrong factor).
    /// </para>
    /// </summary>
    public sealed class MeasurePropertyTests
    {
        private const double RoundTripTolerance = 1e-12;
        private const double AnchorTolerance = 1e-14;

        private static readonly LengthUnit[] LengthUnits = { LengthUnit.Meters, LengthUnit.Feet, LengthUnit.Kilometers, LengthUnit.NauticalMiles, LengthUnit.StatuteMiles };
        private static readonly SpeedUnit[] SpeedUnits = { SpeedUnit.MetersPerSecond, SpeedUnit.Knots, SpeedUnit.KilometersPerHour };

        private static readonly Gen<LengthUnit> AnyLengthUnit = Gen.OneOfConst(LengthUnits);
        private static readonly Gen<SpeedUnit> AnySpeedUnit = Gen.OneOfConst(SpeedUnits);

        // Reported magnitudes: edge values, the whole numbers real reports carry, and a log-uniform sweep from 1e-3 to 1e7.
        private static Gen<double> Magnitudes(bool allowNegative) => Gen.Frequency(
            (2, (IGen<double>)Gen.OneOfConst(0.0, 1.0, 0.5, 9999.0, 1852.0, 0.3048, 100000.0, 3.6)),
            (3, (IGen<double>)Gen.Int[0, 20000].Select(i => (double)i)),
            (4, (IGen<double>)Gen.Double[Math.Log(1e-3), Math.Log(1e7)].Select(Math.Exp))).Select(Gen.Bool, (m, negative) => allowNegative && negative ? -m : m);

        private static readonly Gen<double> LengthValues = Magnitudes(allowNegative: true).Select(Gen.OneOfConst(0.0, -0.0), (v, z) => v == 0.0 ? z : v);
        private static readonly Gen<double> SpeedValues = Magnitudes(allowNegative: false).Select(Gen.OneOfConst(0.0, -0.0), (v, z) => v == 0.0 ? z : v);

        private static readonly Gen<double> NonFinite = Gen.OneOfConst(double.NaN, double.PositiveInfinity, double.NegativeInfinity);

        // Values that are not members of the enum (negative, just above the last member, far away).
        private static readonly Gen<int> UnknownEnumValue = Gen.OneOfConst(-1, 5, 6, 99, int.MinValue, int.MaxValue);
        private static readonly Gen<int> UnknownSpeedEnumValue = Gen.OneOfConst(-1, 3, 4, 99, int.MinValue, int.MaxValue);

        private static bool SameBits(double a, double b) => BitConverter.DoubleToInt64Bits(a) == BitConverter.DoubleToInt64Bits(b);

        private static void AssertClose(double expected, double actual, double relativeTolerance, string what)
        {
            double allowed = relativeTolerance * Math.Abs(expected);
            Assert.True(Math.Abs(actual - expected) <= allowed, $"{what}: expected {expected:R}, got {actual:R} (allowed error {allowed:R})");
        }

        // ---- Length ----

        [Fact]
        public void Length_ConversionRoundTrip_ReturnsTheOriginalWithinTolerance()
        {
            PropertySettings.Run(Gen.Select(LengthValues, AnyLengthUnit, AnyLengthUnit), PropertySettings.MeasureSeed, c =>
            {
                var original = new Length(c.Item1, c.Item2);
                double converted = original.To(c.Item3);
                double back = new Length(converted, c.Item3).To(c.Item2);

                AssertClose(original.Value, back, RoundTripTolerance, $"{original} via {c.Item3}");
            });
        }

        [Fact]
        public void Length_Conversions_AgreeWithTheDocumentedFactorsAndWithEachOther()
        {
            // Anchors: 1 ft = 0.3048 m, 1 NM = 1852 m, 1 mi = 1609.344 m, 1 km = 1000 m (LengthUnit docs). Written here as
            // independent constants so that a wrong factor in the implementation is caught (a round trip would cancel it).
            PropertySettings.Run(Gen.Select(LengthValues, AnyLengthUnit, AnyLengthUnit), PropertySettings.MeasureSeed, c =>
            {
                var length = new Length(c.Item1, c.Item2);
                double metres = length.ToMeters();
                AssertClose(c.Item1 * MetresPerUnit(c.Item2), metres, AnchorTolerance, "ToMeters");

                // The named converters agree with To(unit), and converting in two steps agrees with converting directly.
                Assert.Equal(length.To(LengthUnit.Feet), length.ToFeet());
                Assert.Equal(length.To(LengthUnit.Kilometers), length.ToKilometers());
                Assert.Equal(length.To(LengthUnit.NauticalMiles), length.ToNauticalMiles());
                Assert.Equal(length.To(LengthUnit.Meters), metres);
                AssertClose(length.To(c.Item3), new Length(length.To(LengthUnit.Feet), LengthUnit.Feet).To(c.Item3), RoundTripTolerance, "two-step conversion");
            });
        }

        [Fact]
        public void Length_Equality_IsReflexive_Symmetric_AndHashCoherent()
        {
            PropertySettings.Run(PairsOf(LengthValues, AnyLengthUnit), PropertySettings.MeasureSeed, p =>
            {
                var a = new Length(p.Item1, p.Item2);
                var b = new Length(p.Item3, p.Item4);

                Assert.True(a.Equals(a));
                var copy = a;
                Assert.True(a == copy);
                Assert.Equal(a.Equals(b), b.Equals(a));
                Assert.Equal(a.Equals(b), a.Equals((object)b));
                Assert.Equal(a.Equals(b), a == b);
                Assert.Equal(!a.Equals(b), a != b);

                // Oracle: structural equality, i.e. numerically equal values (so -0 == 0) AND the same unit.
                Assert.Equal(p.Item1 == p.Item3 && p.Item2 == p.Item4, a.Equals(b));
                if (a.Equals(b))
                    Assert.Equal(a.GetHashCode(), b.GetHashCode());
            });
        }

        [Fact]
        public void Length_NegativeZero_NormalizesToZero()
        {
            PropertySettings.Run(AnyLengthUnit, PropertySettings.MeasureSeed, unit =>
            {
                var negative = new Length(-0.0, unit);
                var positive = new Length(0.0, unit);

                Assert.True(SameBits(0.0, negative.Value), "stored value must be +0");
                Assert.Equal(positive, negative);
                Assert.Equal(positive.GetHashCode(), negative.GetHashCode());
                Assert.DoesNotContain("-", negative.ToString());
                foreach (LengthUnit target in LengthUnits)
                    Assert.True(SameBits(0.0, negative.To(target)), "zero converts to +0");
            });
        }

        [Fact]
        public void Length_NonFiniteValueOrUnknownUnit_IsRejected()
        {
            PropertySettings.Run(Gen.Select(NonFinite, AnyLengthUnit), PropertySettings.MeasureSeed, c =>
            {
                var thrown = Assert.Throws<ArgumentOutOfRangeException>(() => new Length(c.Item1, c.Item2));
                Assert.Equal("value", thrown.ParamName);
            });

            PropertySettings.Run(Gen.Select(LengthValues, UnknownEnumValue), PropertySettings.MeasureSeed, c =>
            {
                var thrown = Assert.Throws<ArgumentOutOfRangeException>(() => new Length(c.Item1, (LengthUnit)c.Item2));
                Assert.Equal("unit", thrown.ParamName);
                Assert.Throws<ArgumentOutOfRangeException>(() => new Length(1.0, LengthUnit.Meters).To((LengthUnit)c.Item2));
            });
        }

        // ---- Speed ----

        [Fact]
        public void Speed_ConversionRoundTrip_ReturnsTheOriginalWithinTolerance()
        {
            PropertySettings.Run(Gen.Select(SpeedValues, AnySpeedUnit, AnySpeedUnit), PropertySettings.MeasureSeed, c =>
            {
                var original = new Speed(c.Item1, c.Item2);
                double converted = original.To(c.Item3);
                double back = new Speed(converted, c.Item3).To(c.Item2);

                AssertClose(original.Value, back, RoundTripTolerance, $"{original} via {c.Item3}");
            });
        }

        [Fact]
        public void Speed_Conversions_AgreeWithTheDocumentedFactorsAndWithEachOther()
        {
            // Anchors: 1 kt = 1852/3600 m/s, 1 km/h = 1/3.6 m/s (SpeedUnit docs).
            PropertySettings.Run(Gen.Select(SpeedValues, AnySpeedUnit, AnySpeedUnit), PropertySettings.MeasureSeed, c =>
            {
                var speed = new Speed(c.Item1, c.Item2);
                AssertClose(c.Item1 * MetresPerSecondPerUnit(c.Item2), speed.ToMetersPerSecond(), AnchorTolerance, "ToMetersPerSecond");

                Assert.Equal(speed.To(SpeedUnit.Knots), speed.ToKnots());
                Assert.Equal(speed.To(SpeedUnit.KilometersPerHour), speed.ToKilometersPerHour());
                Assert.Equal(speed.To(SpeedUnit.MetersPerSecond), speed.ToMetersPerSecond());
                AssertClose(speed.To(c.Item3), new Speed(speed.ToKnots(), SpeedUnit.Knots).To(c.Item3), RoundTripTolerance, "two-step conversion");
            });
        }

        [Fact]
        public void Speed_Equality_IsReflexive_Symmetric_AndHashCoherent()
        {
            PropertySettings.Run(PairsOf(SpeedValues, AnySpeedUnit), PropertySettings.MeasureSeed, p =>
            {
                var a = new Speed(p.Item1, p.Item2);
                var b = new Speed(p.Item3, p.Item4);

                Assert.True(a.Equals(a));
                var copy = a;
                Assert.True(a == copy);
                Assert.Equal(a.Equals(b), b.Equals(a));
                Assert.Equal(a.Equals(b), a.Equals((object)b));
                Assert.Equal(a.Equals(b), a == b);
                Assert.Equal(!a.Equals(b), a != b);
                Assert.Equal(p.Item1 == p.Item3 && p.Item2 == p.Item4, a.Equals(b));
                if (a.Equals(b))
                    Assert.Equal(a.GetHashCode(), b.GetHashCode());
            });
        }

        [Fact]
        public void Speed_NegativeZero_NormalizesToZero()
        {
            PropertySettings.Run(AnySpeedUnit, PropertySettings.MeasureSeed, unit =>
            {
                var negative = new Speed(-0.0, unit);
                var positive = new Speed(0.0, unit);

                Assert.True(SameBits(0.0, negative.Value), "stored value must be +0");
                Assert.Equal(positive, negative);
                Assert.Equal(positive.GetHashCode(), negative.GetHashCode());
                Assert.DoesNotContain("-", negative.ToString());
                foreach (SpeedUnit target in SpeedUnits)
                    Assert.True(SameBits(0.0, negative.To(target)), "zero converts to +0");
            });
        }

        [Fact]
        public void Speed_NonFiniteValueOrUnknownUnit_IsRejected()
        {
            PropertySettings.Run(Gen.Select(NonFinite, AnySpeedUnit), PropertySettings.MeasureSeed, c =>
            {
                var thrown = Assert.Throws<ArgumentOutOfRangeException>(() => new Speed(c.Item1, c.Item2));
                Assert.Equal("value", thrown.ParamName);
            });

            PropertySettings.Run(Gen.Select(SpeedValues, UnknownSpeedEnumValue), PropertySettings.MeasureSeed, c =>
            {
                var thrown = Assert.Throws<ArgumentOutOfRangeException>(() => new Speed(c.Item1, (SpeedUnit)c.Item2));
                Assert.Equal("unit", thrown.ParamName);
                Assert.Throws<ArgumentOutOfRangeException>(() => new Speed(1.0, SpeedUnit.Knots).To((SpeedUnit)c.Item2));
            });
        }

        // (value, unit, value, unit): the second pair is the first, the first with another unit, or an independent pair, so that
        // equal and different pairs are both frequent.
        private static Gen<(double, T, double, T)> PairsOf<T>(Gen<double> values, Gen<T> units) =>
            Gen.Select(values, units, values, units, Gen.Int[0, 2], (v1, u1, v2, u2, mode) =>
                mode == 0 ? (v1, u1, v1, u1) : mode == 1 ? (v1, u1, v1, u2) : (v1, u1, v2, u2));

        private static double MetresPerUnit(LengthUnit unit) => unit switch
        {
            LengthUnit.Feet => 0.3048,
            LengthUnit.Kilometers => 1000.0,
            LengthUnit.NauticalMiles => 1852.0,
            LengthUnit.StatuteMiles => 1609.344,
            _ => 1.0
        };

        private static double MetresPerSecondPerUnit(SpeedUnit unit) => unit switch
        {
            SpeedUnit.Knots => 1852.0 / 3600.0,
            SpeedUnit.KilometersPerHour => 1.0 / 3.6,
            _ => 1.0
        };
    }
}
