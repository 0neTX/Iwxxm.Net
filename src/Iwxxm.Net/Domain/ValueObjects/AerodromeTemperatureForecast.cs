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

        public bool Equals(AerodromeTemperatureForecast? other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Nullable.Equals(MaximumCelsius, other.MaximumCelsius) &&
                   Nullable.Equals(MaximumTimeUtc, other.MaximumTimeUtc) &&
                   Nullable.Equals(MinimumCelsius, other.MinimumCelsius) &&
                   Nullable.Equals(MinimumTimeUtc, other.MinimumTimeUtc);
        }

        public override bool Equals(object? obj) => obj is AerodromeTemperatureForecast other && Equals(other);

        public override int GetHashCode()
        {
            int hash = ValueEquality.AddDouble(ValueEquality.Seed, MaximumCelsius);
            hash = ValueEquality.Add(hash, MaximumTimeUtc);
            hash = ValueEquality.AddDouble(hash, MinimumCelsius);
            return ValueEquality.Add(hash, MinimumTimeUtc);
        }

        public static bool operator ==(AerodromeTemperatureForecast? left, AerodromeTemperatureForecast? right) => Equals(left, right);

        public static bool operator !=(AerodromeTemperatureForecast? left, AerodromeTemperatureForecast? right) => !Equals(left, right);
    }
}
