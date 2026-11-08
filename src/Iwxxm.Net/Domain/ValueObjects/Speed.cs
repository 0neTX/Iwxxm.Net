using System;
using System.Globalization;

namespace Iwxxm.Net.Reports
{
    /// <summary>
    /// Immutable Value Object holding a speed exactly as reported (numeric amount + unit of measure).
    /// No conversion is applied on construction; use <see cref="ToMetersPerSecond"/>, <see cref="ToKnots"/> or
    /// <see cref="ToKilometersPerHour"/> to obtain a value in a specific unit.
    /// Equality is structural and exact: two speeds are equal only when both the amount and the unit are equal
    /// (<c>10 kt</c> is not equal to <c>5.144 m/s</c>); use the conversion methods to compare magnitudes.
    /// </summary>
    public readonly struct Speed : IEquatable<Speed>
    {
        private const double MetersPerSecondPerKnot = 1852.0 / 3600.0;
        private const double MetersPerSecondPerKilometerPerHour = 1000.0 / 3600.0;

        public Speed(double amount, SpeedUnit unit)
        {
            if (double.IsNaN(amount) || double.IsInfinity(amount))
                throw new ArgumentOutOfRangeException(nameof(amount), amount, "Speed amount must be a finite number.");
            if (unit != SpeedUnit.MetersPerSecond && unit != SpeedUnit.Knots && unit != SpeedUnit.KilometersPerHour)
                throw new ArgumentOutOfRangeException(nameof(unit), unit, "Unsupported speed unit.");

            // Normalize negative zero so that Equals and GetHashCode always agree.
            Amount = amount == 0.0 ? 0.0 : amount;
            Unit = unit;
        }

        /// <summary>Numeric magnitude exactly as reported in <see cref="Unit"/>.</summary>
        public double Amount { get; }

        /// <summary>Unit of measure of <see cref="Amount"/>.</summary>
        public SpeedUnit Unit { get; }

        /// <summary>UCUM code of <see cref="Unit"/> as used by IWXXM (<c>m/s</c>, <c>[kn_i]</c>, <c>km/h</c>).</summary>
        public string UnitOfMeasure => GetUcumCode(Unit);

        public double ToMetersPerSecond() => Unit switch
        {
            SpeedUnit.Knots => Amount * MetersPerSecondPerKnot,
            SpeedUnit.KilometersPerHour => Amount * MetersPerSecondPerKilometerPerHour,
            _ => Amount
        };

        public double ToKnots() => Unit switch
        {
            SpeedUnit.Knots => Amount,
            SpeedUnit.KilometersPerHour => Amount * 1000.0 / 1852.0,
            _ => Amount * 3600.0 / 1852.0
        };

        public double ToKilometersPerHour() => Unit switch
        {
            SpeedUnit.KilometersPerHour => Amount,
            SpeedUnit.Knots => Amount * 1852.0 / 1000.0,
            _ => Amount * 3.6
        };

        /// <summary>Returns the amount converted to <paramref name="unit"/>.</summary>
        public double To(SpeedUnit unit) => unit switch
        {
            SpeedUnit.Knots => ToKnots(),
            SpeedUnit.KilometersPerHour => ToKilometersPerHour(),
            SpeedUnit.MetersPerSecond => ToMetersPerSecond(),
            _ => throw new ArgumentOutOfRangeException(nameof(unit), unit, "Unsupported speed unit.")
        };

        public static string GetUcumCode(SpeedUnit unit) => unit switch
        {
            SpeedUnit.Knots => "[kn_i]",
            SpeedUnit.KilometersPerHour => "km/h",
            _ => "m/s"
        };

        public bool Equals(Speed other) => Amount.Equals(other.Amount) && Unit == other.Unit;

        public override bool Equals(object? obj) => obj is Speed other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                return (Amount.GetHashCode() * 397) ^ (int)Unit;
            }
        }

        public static bool operator ==(Speed left, Speed right) => left.Equals(right);
        public static bool operator !=(Speed left, Speed right) => !left.Equals(right);

        public override string ToString() =>
            string.Format(CultureInfo.InvariantCulture, "{0} {1}", Amount, UnitOfMeasure);
    }
}
