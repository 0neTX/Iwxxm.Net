using System;

namespace Iwxxm.Net.Reports
{
    /// <summary>
    /// Immutable Value Object representing the horizontal visibility observed at an aerodrome (IWXXM
    /// <c>AerodromeHorizontalVisibility</c>): prevailing visibility with its optional operator (e.g. <c>9999</c> = 10000 m
    /// <see cref="RelationalOperator.Above"/>), and the minimum visibility with its direction (e.g. <c>1200NE</c> = 1200 m
    /// towards 45 degrees). Distances are in metres, directions in degrees true. Equality is structural and exact.
    /// </summary>
    public sealed class AerodromeHorizontalVisibility : IEquatable<AerodromeHorizontalVisibility>
    {
        public AerodromeHorizontalVisibility(
            double? prevailingMeters,
            RelationalOperator? prevailingOperator = null,
            double? minimumMeters = null,
            double? minimumDirectionDegrees = null)
        {
            PrevailingMeters = prevailingMeters;
            PrevailingOperator = prevailingOperator;
            MinimumMeters = minimumMeters;
            MinimumDirectionDegrees = minimumDirectionDegrees;
        }

        /// <summary>Prevailing visibility in metres; <c>null</c> when nil or not reported.</summary>
        public double? PrevailingMeters { get; }

        /// <summary>Qualifier of <see cref="PrevailingMeters"/> (<c>ABOVE</c> for 10 km or more).</summary>
        public RelationalOperator? PrevailingOperator { get; }

        /// <summary>Minimum visibility in metres; <c>null</c> when not reported.</summary>
        public double? MinimumMeters { get; }

        /// <summary>Direction of the minimum visibility in degrees; <c>null</c> when not reported.</summary>
        public double? MinimumDirectionDegrees { get; }

        public bool Equals(AerodromeHorizontalVisibility? other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Nullable.Equals(PrevailingMeters, other.PrevailingMeters) &&
                   PrevailingOperator == other.PrevailingOperator &&
                   Nullable.Equals(MinimumMeters, other.MinimumMeters) &&
                   Nullable.Equals(MinimumDirectionDegrees, other.MinimumDirectionDegrees);
        }

        public override bool Equals(object? obj) => obj is AerodromeHorizontalVisibility other && Equals(other);

        public override int GetHashCode()
        {
            int hash = ValueEquality.AddDouble(ValueEquality.Seed, PrevailingMeters);
            hash = ValueEquality.Add(hash, PrevailingOperator);
            hash = ValueEquality.AddDouble(hash, MinimumMeters);
            return ValueEquality.AddDouble(hash, MinimumDirectionDegrees);
        }

        public static bool operator ==(AerodromeHorizontalVisibility? left, AerodromeHorizontalVisibility? right) => Equals(left, right);

        public static bool operator !=(AerodromeHorizontalVisibility? left, AerodromeHorizontalVisibility? right) => !Equals(left, right);
    }
}
