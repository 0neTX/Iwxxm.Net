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

        /// <summary>Creates a speed exactly as reported, without conversion.</summary>
        /// <param name="amount">The magnitude in <paramref name="unit"/>; must be finite. The report factories reject negative speeds.</param>
        /// <param name="unit">The unit of measure; must be a defined <see cref="SpeedUnit"/>.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="amount"/> is not finite or <paramref name="unit"/> is not supported.</exception>
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

        /// <summary>Returns the speed in metres per second (1 kt = 1852/3600 m/s).</summary>
        public double ToMetersPerSecond() => Unit switch
        {
            SpeedUnit.Knots => Amount * MetersPerSecondPerKnot,
            SpeedUnit.KilometersPerHour => Amount * MetersPerSecondPerKilometerPerHour,
            _ => Amount
        };

        /// <summary>Returns the speed in international knots.</summary>
        public double ToKnots() => Unit switch
        {
            SpeedUnit.Knots => Amount,
            SpeedUnit.KilometersPerHour => Amount * 1000.0 / 1852.0,
            _ => Amount * 3600.0 / 1852.0
        };

        /// <summary>Returns the speed in kilometres per hour.</summary>
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

        /// <summary>Returns the UCUM code IWXXM uses for <paramref name="unit"/> (<c>m/s</c>, <c>[kn_i]</c> or <c>km/h</c>).</summary>
        /// <param name="unit">The unit of measure.</param>
        public static string GetUcumCode(SpeedUnit unit) => unit switch
        {
            SpeedUnit.Knots => "[kn_i]",
            SpeedUnit.KilometersPerHour => "km/h",
            _ => "m/s"
        };

        /// <inheritdoc />
        public bool Equals(Speed other) => Amount.Equals(other.Amount) && Unit == other.Unit;

        /// <inheritdoc />
        public override bool Equals(object? obj) => obj is Speed other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                return (Amount.GetHashCode() * 397) ^ (int)Unit;
            }
        }

        /// <summary>Returns <c>true</c> when the two <see cref="Speed"/> operands are equal by value.</summary>
        public static bool operator ==(Speed left, Speed right) => left.Equals(right);
        /// <summary>Returns <c>true</c> when the two <see cref="Speed"/> operands differ by value.</summary>
        public static bool operator !=(Speed left, Speed right) => !left.Equals(right);

        /// <inheritdoc />
        public override string ToString() =>
            string.Format(CultureInfo.InvariantCulture, "{0} {1}", Amount, UnitOfMeasure);
    }
}
