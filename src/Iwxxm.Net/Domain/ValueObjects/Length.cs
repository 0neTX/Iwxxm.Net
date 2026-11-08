using System;
using System.Globalization;

namespace Iwxxm.Net.Reports
{
    /// <summary>
    /// Immutable Value Object holding a length (height, elevation, distance) exactly as reported
    /// (numeric amount + unit of measure). Negative values are allowed (e.g. elevations below mean sea level).
    /// Equality is structural and exact (amount and unit); use <see cref="ToMeters"/> or <see cref="ToFeet"/>
    /// to compare magnitudes expressed in different units.
    /// </summary>
    public readonly struct Length : IEquatable<Length>
    {
        public Length(double amount, LengthUnit unit)
        {
            if (double.IsNaN(amount) || double.IsInfinity(amount))
                throw new ArgumentOutOfRangeException(nameof(amount), amount, "Length amount must be a finite number.");
            if (unit < LengthUnit.Meters || unit > LengthUnit.StatuteMiles)
                throw new ArgumentOutOfRangeException(nameof(unit), unit, "Unsupported length unit.");

            // Normalize negative zero so that Equals and GetHashCode always agree.
            Amount = amount == 0.0 ? 0.0 : amount;
            Unit = unit;
        }

        /// <summary>Numeric magnitude exactly as reported in <see cref="Unit"/>.</summary>
        public double Amount { get; }

        /// <summary>Unit of measure of <see cref="Amount"/>.</summary>
        public LengthUnit Unit { get; }

        /// <summary>UCUM code of <see cref="Unit"/> as used by IWXXM (e.g. <c>m</c>, <c>[ft_i]</c>).</summary>
        public string UnitOfMeasure => GetUcumCode(Unit);

        public double ToMeters() => Amount * MetersPer(Unit);

        public double ToFeet() => Unit == LengthUnit.Feet ? Amount : ToMeters() / 0.3048;

        public double ToKilometers() => Unit == LengthUnit.Kilometers ? Amount : ToMeters() / 1000.0;

        public double ToNauticalMiles() => Unit == LengthUnit.NauticalMiles ? Amount : ToMeters() / 1852.0;

        /// <summary>Returns the amount converted to <paramref name="unit"/>.</summary>
        public double To(LengthUnit unit) => unit == Unit ? Amount : ToMeters() / MetersPer(unit);

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

        public bool Equals(Length other) => Amount.Equals(other.Amount) && Unit == other.Unit;

        public override bool Equals(object? obj) => obj is Length other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                return (Amount.GetHashCode() * 397) ^ (int)Unit;
            }
        }

        public static bool operator ==(Length left, Length right) => left.Equals(right);
        public static bool operator !=(Length left, Length right) => !left.Equals(right);

        public override string ToString() =>
            string.Format(CultureInfo.InvariantCulture, "{0} {1}", Amount, UnitOfMeasure);
    }
}
