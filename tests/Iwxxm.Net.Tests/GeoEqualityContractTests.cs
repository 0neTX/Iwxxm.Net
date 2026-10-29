using System;
using System.Collections.Generic;
using System.Linq;
using Iwxxm.Net.Reports;
using Xunit;

namespace Iwxxm.Net.Tests
{
    /// <summary>
    /// Property-style verification of the <see cref="object.Equals(object)"/> / <see cref="object.GetHashCode"/>
    /// contract of <see cref="GeoCoordinate"/> and <see cref="GeoPolygon"/> (DDD-06, AUD-M6):
    /// equal values must have equal hash codes, equality must be reflexive, symmetric and transitive,
    /// and hash-based collections must deduplicate equal values.
    /// </summary>
    public sealed class GeoEqualityContractTests
    {
        private const int Samples = 5000;

        [Fact]
        public void GeoCoordinate_IsQuantizedOnceAtConstruction_To7Decimals()
        {
            var coordinate = new GeoCoordinate(12.345678949, -98.765432151);

            Assert.Equal(12.3456789, coordinate.Lat);
            Assert.Equal(-98.7654322, coordinate.Lon);
            Assert.Equal(1e-7, GeoCoordinate.Precision);
        }

        [Fact]
        public void GeoCoordinate_10_00000004_And_10_00000006_HaveConsistentEqualsAndHashCode()
        {
            var a = new GeoCoordinate(10.00000004, 20.0);
            var b = new GeoCoordinate(10.00000006, 20.0);

            // Quantized to 10.0000000 and 10.0000001 respectively: they are different values ...
            Assert.Equal(10.0, a.Lat);
            Assert.Equal(10.0000001, b.Lat);
            Assert.False(a.Equals(b));
            Assert.False(a == b);

            // ... and two values that quantize to the same coordinate are equal with the same hash code.
            var c = new GeoCoordinate(10.00000004, 20.0);
            var d = new GeoCoordinate(9.99999996, 20.00000001);
            Assert.True(c.Equals(d));
            Assert.Equal(c.GetHashCode(), d.GetHashCode());
        }

        [Fact]
        public void GeoCoordinate_EqualityIsTransitive_ForValuesWithinTheOldTolerance()
        {
            // With the previous 1e-7 tolerance a ~ b and b ~ c but a !~ c (non-transitive).
            var a = new GeoCoordinate(10.00000000, 0.0);
            var b = new GeoCoordinate(10.00000004, 0.0);
            var c = new GeoCoordinate(10.00000008, 0.0);

            bool ab = a.Equals(b);
            bool bc = b.Equals(c);
            bool ac = a.Equals(c);

            Assert.True(!(ab && bc) || ac, "Equality must be transitive.");
        }

        [Fact]
        public void GeoCoordinate_NegativeZero_EqualsPositiveZero_WithSameHashCode()
        {
            var negative = new GeoCoordinate(-0.0, -0.00000001);
            var positive = new GeoCoordinate(0.0, 0.0);

            Assert.Equal(positive, negative);
            Assert.Equal(positive.GetHashCode(), negative.GetHashCode());
        }

        [Fact]
        public void GeoCoordinate_Property_EqualImpliesSameHash_AndEqualityIsAnEquivalenceRelation()
        {
            var random = new Random(20250202);

            for (int i = 0; i < Samples; i++)
            {
                double baseLat = (random.NextDouble() * 179.0) - 89.5;
                double baseLon = (random.NextDouble() * 359.0) - 179.5;

                // Jitter around the quantization step so that ties, near-ties and distinct values all occur.
                GeoCoordinate a = Jitter(random, baseLat, baseLon);
                GeoCoordinate b = Jitter(random, baseLat, baseLon);
                GeoCoordinate c = Jitter(random, baseLat, baseLon);

                Assert.True(a.Equals(a));
                Assert.Equal(a.Equals(b), b.Equals(a));
                if (a.Equals(b))
                {
                    Assert.Equal(a.GetHashCode(), b.GetHashCode());
                }

                if (a.Equals(b) && b.Equals(c))
                {
                    Assert.True(a.Equals(c), $"Transitivity violated for {a}, {b}, {c}.");
                }
            }
        }

        [Fact]
        public void GeoCoordinate_HashSetAndDistinct_DeduplicateEqualCoordinates()
        {
            var coordinates = new[]
            {
                new GeoCoordinate(45.12345671, 7.00000001),
                new GeoCoordinate(45.12345669, 6.99999999),
                new GeoCoordinate(45.1234567, 7.0),
                new GeoCoordinate(45.1234568, 7.0)
            };

            var set = new HashSet<GeoCoordinate>(coordinates);

            Assert.Equal(2, set.Count);
            Assert.Equal(2, coordinates.Distinct().Count());
        }

        [Fact]
        public void GeoPolygon_InteriorRingContents_TakePartInEqualityAndHashCode()
        {
            GeoPolygon withHoleA = Polygon(Hole(1.0));
            GeoPolygon withHoleA2 = Polygon(Hole(1.0));
            GeoPolygon withHoleB = Polygon(Hole(2.0));
            GeoPolygon withoutHole = Polygon();

            Assert.Equal(withHoleA, withHoleA2);
            Assert.Equal(withHoleA.GetHashCode(), withHoleA2.GetHashCode());

            // Same exterior ring and same number of holes, but different hole contents.
            Assert.NotEqual(withHoleA, withHoleB);
            Assert.NotEqual(withHoleA, withoutHole);

            var set = new HashSet<GeoPolygon> { withHoleA, withHoleA2, withHoleB, withoutHole };
            Assert.Equal(3, set.Count);
        }

        [Fact]
        public void GeoPolygon_Property_EqualImpliesSameHash()
        {
            var random = new Random(7);
            for (int i = 0; i < 500; i++)
            {
                double offset = random.Next(0, 3);
                GeoPolygon a = Polygon(Hole(1.0 + offset));
                GeoPolygon b = Polygon(Hole(1.0 + random.Next(0, 3)));

                Assert.Equal(a.Equals(b), b.Equals(a));
                if (a.Equals(b))
                {
                    Assert.Equal(a.GetHashCode(), b.GetHashCode());
                }
            }
        }

        private static GeoCoordinate Jitter(Random random, double lat, double lon)
        {
            double jitterLat = (random.Next(-3, 4) * 0.5e-7) + ((random.NextDouble() - 0.5) * 1e-9);
            double jitterLon = (random.Next(-3, 4) * 0.5e-7) + ((random.NextDouble() - 0.5) * 1e-9);
            return new GeoCoordinate(lat + jitterLat, lon + jitterLon);
        }

        private static IEnumerable<GeoCoordinate> Hole(double start)
        {
            return new[]
            {
                new GeoCoordinate(start, start),
                new GeoCoordinate(start, start + 0.5),
                new GeoCoordinate(start + 0.5, start + 0.5),
                new GeoCoordinate(start, start)
            };
        }

        private static GeoPolygon Polygon(params IEnumerable<GeoCoordinate>[] holes)
        {
            var exterior = new[]
            {
                new GeoCoordinate(0.0, 0.0),
                new GeoCoordinate(0.0, 5.0),
                new GeoCoordinate(5.0, 5.0),
                new GeoCoordinate(5.0, 0.0),
                new GeoCoordinate(0.0, 0.0)
            };

            Result<GeoPolygon> result = GeoPolygon.Create(exterior, holes);
            Assert.True(result.IsSuccess, string.Join("; ", result.Errors));
            return result.Value;
        }
    }
}
