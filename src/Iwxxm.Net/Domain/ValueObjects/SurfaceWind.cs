using System;

namespace Iwxxm.Net.Reports
{
    /// <summary>
    /// Immutable Value Object representing surface wind conditions at an aerodrome.
    /// Wind speeds keep the unit reported in the document (<c>m/s</c> or <c>[kn_i]</c>);
    /// use <see cref="Speed.ToMetersPerSecond"/> / <see cref="Speed.ToKnots"/> to normalize.
    /// The speed operators carry the TAC <c>P</c> qualifier (e.g. <c>P49MPS</c>: above 49 m/s); the extreme directions are
    /// the TAC variation <c>dndndnVdxdxdx</c> (<see cref="ExtremeCounterClockwiseDirectionDegrees"/> = <c>dndndn</c>,
    /// <see cref="ExtremeClockwiseDirectionDegrees"/> = <c>dxdxdx</c>). Equality is structural and exact over every component.
    /// </summary>
    public sealed class SurfaceWind : IEquatable<SurfaceWind>
    {
        public bool IsVariableDirection { get; }
        public double? MeanDirectionDegrees { get; }
        public Speed? MeanSpeed { get; }
        public Speed? GustSpeed { get; }

        /// <summary>Qualifier of <see cref="MeanSpeed"/> (<c>meanWindSpeedOperator</c>).</summary>
        public RelationalOperator? MeanSpeedOperator { get; }

        /// <summary>Qualifier of <see cref="GustSpeed"/> (<c>windGustSpeedOperator</c>).</summary>
        public RelationalOperator? GustSpeedOperator { get; }

        /// <summary>Extreme clockwise direction of a variable wind (<c>extremeClockwiseWindDirection</c>, TAC <c>dxdxdx</c>).</summary>
        public double? ExtremeClockwiseDirectionDegrees { get; }

        /// <summary>Extreme counter-clockwise direction of a variable wind (<c>extremeCounterClockwiseWindDirection</c>, TAC <c>dndndn</c>).</summary>
        public double? ExtremeCounterClockwiseDirectionDegrees { get; }

        public SurfaceWind(
            bool isVariableDirection,
            double? meanDirectionDegrees,
            Speed? meanSpeed,
            Speed? gustSpeed = null,
            RelationalOperator? meanSpeedOperator = null,
            RelationalOperator? gustSpeedOperator = null,
            double? extremeClockwiseDirectionDegrees = null,
            double? extremeCounterClockwiseDirectionDegrees = null)
        {
            IsVariableDirection = isVariableDirection;
            MeanDirectionDegrees = meanDirectionDegrees;
            MeanSpeed = meanSpeed;
            GustSpeed = gustSpeed;
            MeanSpeedOperator = meanSpeedOperator;
            GustSpeedOperator = gustSpeedOperator;
            ExtremeClockwiseDirectionDegrees = extremeClockwiseDirectionDegrees;
            ExtremeCounterClockwiseDirectionDegrees = extremeCounterClockwiseDirectionDegrees;
        }

        public bool Equals(SurfaceWind? other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return IsVariableDirection == other.IsVariableDirection &&
                   Nullable.Equals(MeanDirectionDegrees, other.MeanDirectionDegrees) &&
                   Nullable.Equals(MeanSpeed, other.MeanSpeed) &&
                   Nullable.Equals(GustSpeed, other.GustSpeed) &&
                   MeanSpeedOperator == other.MeanSpeedOperator &&
                   GustSpeedOperator == other.GustSpeedOperator &&
                   Nullable.Equals(ExtremeClockwiseDirectionDegrees, other.ExtremeClockwiseDirectionDegrees) &&
                   Nullable.Equals(ExtremeCounterClockwiseDirectionDegrees, other.ExtremeCounterClockwiseDirectionDegrees);
        }

        public override bool Equals(object? obj) => obj is SurfaceWind other && Equals(other);

        public override int GetHashCode()
        {
            int hash = ValueEquality.Add(ValueEquality.Seed, IsVariableDirection);
            hash = ValueEquality.AddDouble(hash, MeanDirectionDegrees);
            hash = ValueEquality.Add(hash, MeanSpeed);
            hash = ValueEquality.Add(hash, GustSpeed);
            hash = ValueEquality.Add(hash, MeanSpeedOperator);
            hash = ValueEquality.Add(hash, GustSpeedOperator);
            hash = ValueEquality.AddDouble(hash, ExtremeClockwiseDirectionDegrees);
            return ValueEquality.AddDouble(hash, ExtremeCounterClockwiseDirectionDegrees);
        }

        public static bool operator ==(SurfaceWind? left, SurfaceWind? right) => Equals(left, right);

        public static bool operator !=(SurfaceWind? left, SurfaceWind? right) => !Equals(left, right);
    }
}
