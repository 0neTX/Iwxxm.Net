using System;
using CsCheck;
using Iwxxm.Net.Reports;
using Xunit;

namespace Iwxxm.Net.Tests.Properties
{
    /// <summary>
    /// Properties of the <see cref="GeoCoordinate"/> value object: latitude and longitude are quantized once, at construction,
    /// to 1e-7 degrees (half away from zero, negative zero normalized) and equality compares the stored values exactly.
    /// Precondition of every property except the rejection one: lat in [-90, 90] and lon in [-180, 180] (the domain of the
    /// constructor), drawn by <see cref="DomainGenerators"/> with edge values (0, -0, poles, antimeridian, rounding midpoints).
    /// </summary>
    public sealed class GeoCoordinatePropertyTests
    {
        // Quantization moves a value by at most half a step (5e-8); the factor absorbs the representation error of value / 1e7.
        private const double MaxQuantizationError = (GeoCoordinate.Precision / 2.0) * (1.0 + 1e-6);

        private static readonly Gen<(double Lat, double Lon)> Coordinates = Gen.Select(DomainGenerators.Latitude, DomainGenerators.Longitude);

        private static bool SameBits(double a, double b) => BitConverter.DoubleToInt64Bits(a) == BitConverter.DoubleToInt64Bits(b);

        [Fact]
        public void Quantization_IsIdempotent()
        {
            // quantize(quantize(x)) == quantize(x), bit for bit, both on the raw quantizer and by rebuilding the value object.
            PropertySettings.Run(Coordinates, PropertySettings.GeoCoordinateSeed, c =>
            {
                double lat = GeoCoordinate.Quantize(c.Lat);
                double lon = GeoCoordinate.Quantize(c.Lon);
                Assert.True(SameBits(lat, GeoCoordinate.Quantize(lat)), "latitude quantizer is not idempotent");
                Assert.True(SameBits(lon, GeoCoordinate.Quantize(lon)), "longitude quantizer is not idempotent");

                var once = new GeoCoordinate(c.Lat, c.Lon);
                var twice = new GeoCoordinate(once.Lat, once.Lon);
                Assert.True(SameBits(once.Lat, twice.Lat) && SameBits(once.Lon, twice.Lon), "re-constructing a coordinate changed it");
                Assert.Equal(once, twice);

                Result<GeoCoordinate> created = GeoCoordinate.Create(c.Lat, c.Lon);
                Assert.True(created.IsSuccess);
                Assert.Equal(once, created.Value);
            });
        }

        [Fact]
        public void Quantization_KeepsTheValueInRangeWithinHalfAStep_AndNeverStoresNegativeZero()
        {
            PropertySettings.Run(Coordinates, PropertySettings.GeoCoordinateSeed, c =>
            {
                var coordinate = new GeoCoordinate(c.Lat, c.Lon);

                Assert.True(!double.IsNaN(coordinate.Lat) && !double.IsInfinity(coordinate.Lat));
                Assert.True(!double.IsNaN(coordinate.Lon) && !double.IsInfinity(coordinate.Lon));
                Assert.InRange(coordinate.Lat, -90.0, 90.0);
                Assert.InRange(coordinate.Lon, -180.0, 180.0);
                Assert.True(Math.Abs(coordinate.Lat - c.Lat) <= MaxQuantizationError, "latitude moved by more than half a step");
                Assert.True(Math.Abs(coordinate.Lon - c.Lon) <= MaxQuantizationError, "longitude moved by more than half a step");
                if (coordinate.Lat == 0.0)
                    Assert.True(SameBits(coordinate.Lat, 0.0), "latitude zero must be +0");
                if (coordinate.Lon == 0.0)
                    Assert.True(SameBits(coordinate.Lon, 0.0), "longitude zero must be +0");
            });
        }

        [Fact]
        public void Quantization_IsSignSymmetric_AndMonotonic()
        {
            // Half-away-from-zero rounding: quantize(-x) == -quantize(x); and a larger input never quantizes to a smaller value.
            PropertySettings.Run(Gen.Select(DomainGenerators.Longitude, DomainGenerators.Longitude), PropertySettings.GeoCoordinateSeed, p =>
            {
                double qx = GeoCoordinate.Quantize(p.Item1);
                Assert.Equal(-qx, GeoCoordinate.Quantize(-p.Item1));

                double lo = Math.Min(p.Item1, p.Item2);
                double hi = Math.Max(p.Item1, p.Item2);
                Assert.True(GeoCoordinate.Quantize(lo) <= GeoCoordinate.Quantize(hi), "quantization is not monotonic");
            });
        }

        [Fact]
        public void Equality_IsSymmetric_AndHashCodeAgreesWithEquals()
        {
            // The second coordinate is either an independent one, or the first nudged by less than one grid step, so that both
            // equal and different pairs are exercised (equal pairs are common: nudges below half a step often vanish).
            Gen<(double, double, double, double)> pairs = Gen.Select(
                Coordinates,
                Coordinates,
                Gen.Double[-1.5e-7, 1.5e-7],
                Gen.Bool,
                (a, b, nudge, independent) => independent
                    ? (a.Lat, a.Lon, b.Lat, b.Lon)
                    : (a.Lat, a.Lon, Math.Max(-90.0, Math.Min(90.0, a.Lat + nudge)), a.Lon));

            PropertySettings.Run(pairs, PropertySettings.GeoCoordinateSeed, t =>
            {
                var a = new GeoCoordinate(t.Item1, t.Item2);
                var b = new GeoCoordinate(t.Item3, t.Item4);

                Assert.True(a.Equals(a), "equality is not reflexive");
                Assert.Equal(a.Equals(b), b.Equals(a));
                Assert.Equal(a.Equals(b), a.Equals((object)b));
                Assert.Equal(a.Equals(b), a == b);
                Assert.Equal(!a.Equals(b), a != b);
                if (a.Equals(b))
                    Assert.Equal(a.GetHashCode(), b.GetHashCode());
            });
        }

        [Fact]
        public void NegativeZeroInput_IsEqualToZero_WithTheSameHashCode()
        {
            // -0 (and any value that rounds to zero) is the same coordinate component as +0.
            PropertySettings.Run(Gen.Select(DomainGenerators.Latitude, DomainGenerators.Longitude), PropertySettings.GeoCoordinateSeed, c =>
            {
                var withNegativeZeroLat = new GeoCoordinate(-0.0, c.Item2);
                var withPositiveZeroLat = new GeoCoordinate(0.0, c.Item2);
                var withNegativeZeroLon = new GeoCoordinate(c.Item1, -0.0);
                var withPositiveZeroLon = new GeoCoordinate(c.Item1, 0.0);

                Assert.Equal(withPositiveZeroLat, withNegativeZeroLat);
                Assert.Equal(withPositiveZeroLat.GetHashCode(), withNegativeZeroLat.GetHashCode());
                Assert.Equal(withPositiveZeroLon, withNegativeZeroLon);
                Assert.Equal(withPositiveZeroLon.GetHashCode(), withNegativeZeroLon.GetHashCode());
            });
        }

        [Fact]
        public void OutOfRangeOrNonFiniteInput_IsRejectedConsistently_ByTheConstructorAndByCreate()
        {
            // Precondition is inverted here: exactly one of the two components is invalid (NaN, infinities, beyond the poles or
            // the antimeridian), the other one is valid.
            Gen<(double Invalid, double Valid)> latitudeCases = Gen.Select(DomainGenerators.InvalidLatitude, DomainGenerators.Longitude);
            Gen<(double Invalid, double Valid)> longitudeCases = Gen.Select(DomainGenerators.InvalidLongitude, DomainGenerators.Latitude);

            PropertySettings.Run(latitudeCases, PropertySettings.GeoCoordinateSeed, c =>
            {
                var thrown = Assert.Throws<ArgumentOutOfRangeException>(() => new GeoCoordinate(c.Invalid, c.Valid));
                Assert.Equal("lat", thrown.ParamName);
                Result<GeoCoordinate> result = GeoCoordinate.Create(c.Invalid, c.Valid);
                Assert.True(result.IsFailure);
                Assert.Equal("GEO-INVALID-LAT", result.Errors[0].Code);
            });

            PropertySettings.Run(longitudeCases, PropertySettings.GeoCoordinateSeed, c =>
            {
                var thrown = Assert.Throws<ArgumentOutOfRangeException>(() => new GeoCoordinate(c.Valid, c.Invalid));
                Assert.Equal("lon", thrown.ParamName);
                Result<GeoCoordinate> result = GeoCoordinate.Create(c.Valid, c.Invalid);
                Assert.True(result.IsFailure);
                Assert.Equal("GEO-INVALID-LON", result.Errors[0].Code);
            });
        }
    }
}
