using System;

namespace Iwxxm.Net.Reports
{
    /// <summary>
    /// Immutable Value Object representing surface wind conditions at an aerodrome.
    /// Wind speeds keep the unit reported in the document (<c>m/s</c> or <c>[kn_i]</c>);
    /// use <see cref="Speed.ToMetersPerSecond"/> / <see cref="Speed.ToKnots"/> to normalize.
    /// Equality is structural and exact over every component.
    /// </summary>
    public sealed class SurfaceWind : IEquatable<SurfaceWind>
    {
        public bool IsVariableDirection { get; }
        public double? MeanDirectionDegrees { get; }
        public Speed? MeanSpeed { get; }
        public Speed? GustSpeed { get; }

        public SurfaceWind(
            bool isVariableDirection,
            double? meanDirectionDegrees,
            Speed? meanSpeed,
            Speed? gustSpeed = null)
        {
            IsVariableDirection = isVariableDirection;
            MeanDirectionDegrees = meanDirectionDegrees;
            MeanSpeed = meanSpeed;
            GustSpeed = gustSpeed;
        }

        public bool Equals(SurfaceWind? other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return IsVariableDirection == other.IsVariableDirection &&
                   Nullable.Equals(MeanDirectionDegrees, other.MeanDirectionDegrees) &&
                   Nullable.Equals(MeanSpeed, other.MeanSpeed) &&
                   Nullable.Equals(GustSpeed, other.GustSpeed);
        }

        public override bool Equals(object? obj) => obj is SurfaceWind other && Equals(other);

        public override int GetHashCode()
        {
            int hash = ValueEquality.Add(ValueEquality.Seed, IsVariableDirection);
            hash = ValueEquality.AddDouble(hash, MeanDirectionDegrees);
            hash = ValueEquality.Add(hash, MeanSpeed);
            return ValueEquality.Add(hash, GustSpeed);
        }

        public static bool operator ==(SurfaceWind? left, SurfaceWind? right) => Equals(left, right);

        public static bool operator !=(SurfaceWind? left, SurfaceWind? right) => !Equals(left, right);
    }
}
