using System;
using System.Globalization;

namespace Iwxxm.Net.Reports
{
    /// <summary>
    /// Units of length/distance used by IWXXM 2025-2 measures (UCUM codes <c>m</c>, <c>[ft_i]</c>, <c>km</c>,
    /// <c>[nmi_i]</c>, <c>[mi_i]</c>).
    /// </summary>
    public enum LengthUnit
    {
        /// <summary>Metres (UCUM <c>m</c>).</summary>
        Meters = 0,
        /// <summary>International feet (UCUM <c>[ft_i]</c>); 1 ft = 0.3048 m exactly.</summary>
        Feet = 1,
        /// <summary>Kilometres (UCUM <c>km</c>).</summary>
        Kilometers = 2,
        /// <summary>International nautical miles (UCUM <c>[nmi_i]</c>); 1 NM = 1852 m exactly.</summary>
        NauticalMiles = 3,
        /// <summary>International statute miles (UCUM <c>[mi_i]</c>); 1 mi = 1609.344 m exactly.</summary>
        StatuteMiles = 4
    }

    /// <summary>
    /// Immutable Value Object holding a length (height, elevation, distance) exactly as reported
    /// (numeric value + unit of measure). Negative values are allowed (e.g. elevations below mean sea level).
    /// Equality is structural and exact (value and unit); use <see cref="ToMeters"/> or <see cref="ToFeet"/>
    /// to compare magnitudes expressed in different units.
    /// </summary>
    public readonly struct Length : IEquatable<Length>
    {
        public Length(double value, LengthUnit unit)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
                throw new ArgumentOutOfRangeException(nameof(value), value, "Length value must be a finite number.");
            if (unit < LengthUnit.Meters || unit > LengthUnit.StatuteMiles)
                throw new ArgumentOutOfRangeException(nameof(unit), unit, "Unsupported length unit.");

            // Normalize negative zero so that Equals and GetHashCode always agree.
            Value = value == 0.0 ? 0.0 : value;
            Unit = unit;
        }

        /// <summary>Numeric value exactly as reported in <see cref="Unit"/>.</summary>
        public double Value { get; }

        /// <summary>Unit of measure of <see cref="Value"/>.</summary>
        public LengthUnit Unit { get; }

        /// <summary>UCUM code of <see cref="Unit"/> as used by IWXXM (e.g. <c>m</c>, <c>[ft_i]</c>).</summary>
        public string UnitOfMeasure => GetUcumCode(Unit);

        public double ToMeters() => Value * MetersPer(Unit);

        public double ToFeet() => Unit == LengthUnit.Feet ? Value : ToMeters() / 0.3048;

        public double ToKilometers() => Unit == LengthUnit.Kilometers ? Value : ToMeters() / 1000.0;

        public double ToNauticalMiles() => Unit == LengthUnit.NauticalMiles ? Value : ToMeters() / 1852.0;

        /// <summary>Returns the value converted to <paramref name="unit"/>.</summary>
        public double To(LengthUnit unit) => unit == Unit ? Value : ToMeters() / MetersPer(unit);

        public static string GetUcumCode(LengthUnit unit) => unit switch
        {
            LengthUnit.Feet => "[ft_i]",
            LengthUnit.Kilometers => "km",
            LengthUnit.NauticalMiles => "[nmi_i]",
            LengthUnit.StatuteMiles => "[mi_i]",
            _ => "m"
        };

        private static double MetersPer(LengthUnit unit) => unit switch
        {
            LengthUnit.Feet => 0.3048,
            LengthUnit.Kilometers => 1000.0,
            LengthUnit.NauticalMiles => 1852.0,
            LengthUnit.StatuteMiles => 1609.344,
            LengthUnit.Meters => 1.0,
            _ => throw new ArgumentOutOfRangeException(nameof(unit), unit, "Unsupported length unit.")
        };

        public bool Equals(Length other) => Value.Equals(other.Value) && Unit == other.Unit;

        public override bool Equals(object? obj) => obj is Length other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                return (Value.GetHashCode() * 397) ^ (int)Unit;
            }
        }

        public static bool operator ==(Length left, Length right) => left.Equals(right);
        public static bool operator !=(Length left, Length right) => !left.Equals(right);

        public override string ToString() =>
            string.Format(CultureInfo.InvariantCulture, "{0} {1}", Value, UnitOfMeasure);
    }
}
