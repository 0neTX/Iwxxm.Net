using System;

namespace Iwxxm.Net.Reports
{
    /// <summary>
    /// Immutable Value Object representing one TAF maximum/minimum temperature pair (IWXXM
    /// <c>AerodromeAirTemperatureForecast</c>, TAC <c>TXnn/nnnnZ TNnn/nnnnZ</c>) in degrees Celsius with the forecast
    /// time of each extreme. Equality is structural and exact.
    /// </summary>
    public sealed class AerodromeTemperatureForecast : IEquatable<AerodromeTemperatureForecast>
    {
        /// <summary>Creates a TAF temperature forecast (the <c>TXTX/TNTN</c> groups of a forecast).</summary>
        /// <param name="maximumCelsius">The forecast maximum air temperature in degrees Celsius; <c>null</c> when not forecast.</param>
        /// <param name="maximumTimeUtc">The time of the maximum, which the schema requires within the forecast record period; <c>null</c> when given only by an unresolved reference. Must be in UTC when given.</param>
        /// <param name="minimumCelsius">The forecast minimum air temperature in degrees Celsius; <c>null</c> when not forecast.</param>
        /// <param name="minimumTimeUtc">The time of the minimum; <c>null</c> when given only by an unresolved reference. Must be in UTC when given.</param>
        public AerodromeTemperatureForecast(
            double? maximumCelsius,
            DateTimeOffset? maximumTimeUtc,
            double? minimumCelsius,
            DateTimeOffset? minimumTimeUtc)
        {
            MaximumCelsius = maximumCelsius;
            MaximumTimeUtc = maximumTimeUtc;
            MinimumCelsius = minimumCelsius;
            MinimumTimeUtc = minimumTimeUtc;
        }

        /// <summary>Forecast maximum temperature (<c>TX</c>) in degrees Celsius.</summary>
        public double? MaximumCelsius { get; }

        /// <summary>Time of the forecast maximum temperature; <c>null</c> when given only by an unresolved reference.</summary>
        public DateTimeOffset? MaximumTimeUtc { get; }

        /// <summary>Forecast minimum temperature (<c>TN</c>) in degrees Celsius.</summary>
        public double? MinimumCelsius { get; }

        /// <summary>Time of the forecast minimum temperature; <c>null</c> when given only by an unresolved reference.</summary>
        public DateTimeOffset? MinimumTimeUtc { get; }

        /// <inheritdoc />
        public bool Equals(AerodromeTemperatureForecast? other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Nullable.Equals(MaximumCelsius, other.MaximumCelsius) &&
                   Nullable.Equals(MaximumTimeUtc, other.MaximumTimeUtc) &&
                   Nullable.Equals(MinimumCelsius, other.MinimumCelsius) &&
                   Nullable.Equals(MinimumTimeUtc, other.MinimumTimeUtc);
        }

        /// <inheritdoc />
        public override bool Equals(object? obj) => obj is AerodromeTemperatureForecast other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode()
        {
            int hash = ValueEquality.AddDouble(ValueEquality.Seed, MaximumCelsius);
            hash = ValueEquality.Add(hash, MaximumTimeUtc);
            hash = ValueEquality.AddDouble(hash, MinimumCelsius);
            return ValueEquality.Add(hash, MinimumTimeUtc);
        }

        /// <summary>Returns <c>true</c> when the two <see cref="AerodromeTemperatureForecast"/> operands are equal by value.</summary>
        public static bool operator ==(AerodromeTemperatureForecast? left, AerodromeTemperatureForecast? right) => Equals(left, right);

        /// <summary>Returns <c>true</c> when the two <see cref="AerodromeTemperatureForecast"/> operands differ by value.</summary>
        public static bool operator !=(AerodromeTemperatureForecast? left, AerodromeTemperatureForecast? right) => !Equals(left, right);
    }
}
