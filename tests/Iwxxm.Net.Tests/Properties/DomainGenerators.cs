using System;
using CsCheck;

namespace Iwxxm.Net.Tests.Properties
{
    /// <summary>
    /// Generators of REAL inputs: coordinates inside the geodetic domain, with the edge values that matter (zero and negative
    /// zero, the poles, the antimeridian, the rounding midpoints of the 1e-7 degree grid) and realistic magnitudes. Unconstrained
    /// doubles are only used where a property is about rejection (see <see cref="InvalidLatitude"/>, <see cref="InvalidLongitude"/>).
    /// </summary>
    internal static class DomainGenerators
    {
        /// <summary>Latitude in [-90, 90]: edge values, the polar bands, grid midpoints and the whole range.</summary>
        public static readonly Gen<double> Latitude = Gen.Frequency(
            (2, (IGen<double>)Gen.OneOfConst(0.0, -0.0, 90.0, -90.0, 89.99999995, -89.99999995, 5e-8, -5e-8, 4.9e-8, -4.9e-8, 1e-7, 45.0, -45.0)),
            (2, (IGen<double>)Gen.Double[0.0, 10.0].Select(d => 90.0 - d)),
            (2, (IGen<double>)Gen.Double[0.0, 10.0].Select(d => -90.0 + d)),
            (1, (IGen<double>)Gen.Int[-900_000_000, 899_999_999].Select(k => (k + 0.5) * 1e-7)),
            (4, (IGen<double>)Gen.Double[-90.0, 90.0]));

        /// <summary>Longitude in [-180, 180]: edge values, both sides of the antimeridian, grid midpoints and the whole range.</summary>
        public static readonly Gen<double> Longitude = Gen.Frequency(
            (2, (IGen<double>)Gen.OneOfConst(0.0, -0.0, 180.0, -180.0, 179.99999995, -179.99999995, 5e-8, -5e-8, 4.9e-8, -4.9e-8, 1e-7, 90.0, -90.0)),
            (2, (IGen<double>)Gen.Double[0.0, 5.0].Select(d => 180.0 - d)),
            (2, (IGen<double>)Gen.Double[0.0, 5.0].Select(d => -180.0 + d)),
            (1, (IGen<double>)Gen.Int[-1_800_000_000, 1_799_999_999].Select(k => (k + 0.5) * 1e-7)),
            (4, (IGen<double>)Gen.Double[-180.0, 180.0]));

        /// <summary>Latitudes outside [-90, 90], or not finite (for the rejection property only).</summary>
        public static readonly Gen<double> InvalidLatitude = Invalid(90.0);

        /// <summary>Longitudes outside [-180, 180], or not finite (for the rejection property only).</summary>
        public static readonly Gen<double> InvalidLongitude = Invalid(180.0);

        private static Gen<double> Invalid(double limit) => Gen.Frequency(
            (3, (IGen<double>)Gen.OneOfConst(double.NaN, double.PositiveInfinity, double.NegativeInfinity, double.MaxValue, double.MinValue, limit * (1.0 + 1e-15), -limit * (1.0 + 1e-15))),
            (2, (IGen<double>)Gen.Double[limit * (1.0 + 1e-9), 1e6]),
            (2, (IGen<double>)Gen.Double[limit * (1.0 + 1e-9), 1e6].Select(d => -d)));
    }
}
