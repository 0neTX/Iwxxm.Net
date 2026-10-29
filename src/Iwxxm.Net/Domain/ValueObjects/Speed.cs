using System;
using System.Globalization;

namespace Iwxxm.Net.Reports
{
    /// <summary>
    /// Units of speed allowed by the official IWXXM 2025-2 rules (UCUM codes <c>m/s</c>, <c>[kn_i]</c>, <c>km/h</c>).
    /// </summary>
    public enum SpeedUnit
    {
        /// <summary>Metres per second (UCUM <c>m/s</c>).</summary>
        MetersPerSecond = 0,
        /// <summary>International knots (UCUM <c>[kn_i]</c>); 1 kt = 1852/3600 m/s exactly.</summary>
        Knots = 1,
        /// <summary>Kilometres per hour (UCUM <c>km/h</c>); 1 km/h = 1/3.6 m/s exactly.</summary>
        KilometersPerHour = 2
    }

    /// <summary>
    /// Immutable Value Object holding a speed exactly as reported (numeric value + unit of measure).
    /// No conversion is applied on construction; use <see cref="ToMetersPerSecond"/>, <see cref="ToKnots"/> or
    /// <see cref="ToKilometersPerHour"/> to obtain a value in a specific unit.
    /// Equality is structural and exact: two speeds are equal only when both the value and the unit are equal
    /// (<c>10 kt</c> is not equal to <c>5.144 m/s</c>); use the conversion methods to compare magnitudes.
    /// </summary>
    public readonly struct Speed : IEquatable<Speed>
    {
        private const double MetersPerSecondPerKnot = 1852.0 / 3600.0;
        private const double MetersPerSecondPerKilometerPerHour = 1000.0 / 3600.0;

        public Speed(double value, SpeedUnit unit)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
                throw new ArgumentOutOfRangeException(nameof(value), value, "Speed value must be a finite number.");
            if (unit != SpeedUnit.MetersPerSecond && unit != SpeedUnit.Knots && unit != SpeedUnit.KilometersPerHour)
                throw new ArgumentOutOfRangeException(nameof(unit), unit, "Unsupported speed unit.");

            // Normalize negative zero so that Equals and GetHashCode always agree.
            Value = value == 0.0 ? 0.0 : value;
            Unit = unit;
        }

        /// <summary>Numeric value exactly as reported in <see cref="Unit"/>.</summary>
        public double Value { get; }

        /// <summary>Unit of measure of <see cref="Value"/>.</summary>
        public SpeedUnit Unit { get; }

        /// <summary>UCUM code of <see cref="Unit"/> as used by IWXXM (<c>m/s</c>, <c>[kn_i]</c>, <c>km/h</c>).</summary>
        public string UnitOfMeasure => GetUcumCode(Unit);

        public double ToMetersPerSecond() => Unit switch
        {
            SpeedUnit.Knots => Value * MetersPerSecondPerKnot,
            SpeedUnit.KilometersPerHour => Value * MetersPerSecondPerKilometerPerHour,
            _ => Value
        };

        public double ToKnots() => Unit switch
        {
            SpeedUnit.Knots => Value,
            SpeedUnit.KilometersPerHour => Value * 1000.0 / 1852.0,
            _ => Value * 3600.0 / 1852.0
        };

        public double ToKilometersPerHour() => Unit switch
        {
            SpeedUnit.KilometersPerHour => Value,
            SpeedUnit.Knots => Value * 1852.0 / 1000.0,
            _ => Value * 3.6
        };

        /// <summary>Returns the value converted to <paramref name="unit"/>.</summary>
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

        public bool Equals(Speed other) => Value.Equals(other.Value) && Unit == other.Unit;

        public override bool Equals(object? obj) => obj is Speed other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                return (Value.GetHashCode() * 397) ^ (int)Unit;
            }
        }

        public static bool operator ==(Speed left, Speed right) => left.Equals(right);
        public static bool operator !=(Speed left, Speed right) => !left.Equals(right);

        public override string ToString() =>
            string.Format(CultureInfo.InvariantCulture, "{0} {1}", Value, UnitOfMeasure);
    }
}
