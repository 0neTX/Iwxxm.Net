using System;
using Iwxxm.Net.Reports;
using Xunit;

namespace Iwxxm.Net.Tests
{
    /// <summary>
    /// Quantization of <see cref="GeoCoordinate"/> uses runtime-independent IEEE 754 arithmetic (not
    /// <c>Math.Round(double, int, MidpointRounding)</c>, whose midpoint behaviour differs between .NET Framework and .NET Core).
    /// </summary>
    public sealed class GeoCoordinateRoundingTests
    {
        [Theory]
        [InlineData(10.00000005, 10.0000001)]
        [InlineData(-10.00000005, -10.0000001)]
        [InlineData(1.00000005, 1.0000001)]
        [InlineData(0.00000005, 0.0000001)]
        [InlineData(-0.00000005, -0.0000001)]
        [InlineData(12.34567895, 12.345679)]
        [InlineData(45.00000015, 45.0000002)]
        [InlineData(10.00000004, 10.0)]
        [InlineData(-10.00000004, -10.0)]
        [InlineData(10.00000006, 10.0000001)]
        [InlineData(89.99999995, 90.0)]
        [InlineData(-89.99999995, -90.0)]
        public void Latitude_IsRoundedHalfAwayFromZero(double input, double expected)
        {
            Assert.Equal(expected, new GeoCoordinate(input, 0.0).Lat);
        }

        [Theory]
        [InlineData(179.99999995, 180.0)]
        [InlineData(-179.99999995, -180.0)]
        [InlineData(180.0, 180.0)]
        [InlineData(-180.0, -180.0)]
        public void Longitude_BoundsAreStable(double input, double expected)
        {
            Assert.Equal(expected, new GeoCoordinate(0.0, input).Lon);
        }

        [Fact]
        public void Bounds_AreAccepted_AndNeverExceeded()
        {
            var max = new GeoCoordinate(90.0, 180.0);
            var min = new GeoCoordinate(-90.0, -180.0);

            Assert.Equal(90.0, max.Lat);
            Assert.Equal(180.0, max.Lon);
            Assert.Equal(-90.0, min.Lat);
            Assert.Equal(-180.0, min.Lon);
            Assert.True(new GeoCoordinate(90.0 - double.Epsilon, 0).Lat <= 90.0);
        }

        [Theory]
        [InlineData(-0.0)]
        [InlineData(-0.00000004)]
        [InlineData(0.00000004)]
        public void NegativeZero_IsNormalized_AndEqualsPositiveZero(double input)
        {
            var coordinate = new GeoCoordinate(input, input);
            var zero = new GeoCoordinate(0.0, 0.0);

            Assert.False(IsNegative(coordinate.Lat));
            Assert.False(IsNegative(coordinate.Lon));
            Assert.Equal(zero, coordinate);
            Assert.Equal(zero.GetHashCode(), coordinate.GetHashCode());
        }

        // double.IsNegative does not exist on .NET Framework; the sign bit also distinguishes -0.0.
        private static bool IsNegative(double value) => BitConverter.DoubleToInt64Bits(value) < 0;

        [Fact]
        public void Quantization_IsIdempotent_Symmetric_AndKeepsEqualsAndHashCodeInAgreement()
        {
            var random = new Random(20250715);
            for (int i = 0; i < 20000; i++)
            {
                double lat = (random.NextDouble() * 180.0) - 90.0;
                double lon = (random.NextDouble() * 360.0) - 180.0;
                if (i % 3 == 0)
                {
                    // exact decimal midpoints of the 1e-7 grid, as typed in a document
                    lat = Math.Round(lat, 7) + 5e-8;
                    lon = Math.Round(lon, 7) + 5e-8;
                    lat = Math.Min(lat, 90.0);
                    lon = Math.Min(lon, 180.0);
                }

                var a = new GeoCoordinate(lat, lon);
                var again = new GeoCoordinate(a.Lat, a.Lon);
                var mirrored = new GeoCoordinate(-lat, -lon);

                Assert.Equal(a, again);
                Assert.Equal(a.GetHashCode(), again.GetHashCode());
                Assert.Equal(-a.Lat, mirrored.Lat);
                Assert.Equal(-a.Lon, mirrored.Lon);
                Assert.InRange(Math.Abs(a.Lat - lat), 0.0, 5e-8 + 1e-12);
                Assert.InRange(Math.Abs(a.Lon - lon), 0.0, 5e-8 + 1e-12);
                Assert.InRange(a.Lat, -90.0, 90.0);
                Assert.InRange(a.Lon, -180.0, 180.0);
            }
        }
    }
}
