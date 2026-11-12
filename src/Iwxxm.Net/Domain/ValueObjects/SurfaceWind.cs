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
        /// <summary>Gets a value indicating whether the wind direction is variable (<c>variableWindDirection="true"</c>); a mean direction may still be reported.</summary>
        public bool IsVariableDirection { get; }
        /// <summary>Gets the mean direction the wind blows from, in degrees from true north (<c>iwxxm:meanWindDirection</c>; 360 is a north wind and 0 is calm), or <c>null</c> when not reported (for example when the wind is variable).</summary>
        public double? MeanDirectionDegrees { get; }
        /// <summary>Gets the mean wind speed (<c>iwxxm:meanWindSpeed</c>) in the unit reported (<c>m/s</c> or knots), or <c>null</c> when not reported.</summary>
        public Speed? MeanSpeed { get; }
        /// <summary>Gets the speed of the wind gusts, the variation from the mean speed (<c>iwxxm:windGustSpeed</c>) in the unit reported, or <c>null</c> when no gust is reported.</summary>
        public Speed? GustSpeed { get; }

        /// <summary>Qualifier of <see cref="MeanSpeed"/> (<c>meanWindSpeedOperator</c>).</summary>
        public RelationalOperator? MeanSpeedOperator { get; }

        /// <summary>Qualifier of <see cref="GustSpeed"/> (<c>windGustSpeedOperator</c>).</summary>
        public RelationalOperator? GustSpeedOperator { get; }

        /// <summary>Extreme clockwise direction of a variable wind (<c>extremeClockwiseWindDirection</c>, TAC <c>dxdxdx</c>).</summary>
        public double? ExtremeClockwiseDirectionDegrees { get; }

        /// <summary>Extreme counter-clockwise direction of a variable wind (<c>extremeCounterClockwiseWindDirection</c>, TAC <c>dndndn</c>).</summary>
        public double? ExtremeCounterClockwiseDirectionDegrees { get; }

        /// <summary>Creates a surface wind. The report factories reject directions outside 0-360 degrees and negative speeds.</summary>
        /// <param name="isVariableDirection">Whether the direction is variable.</param>
        /// <param name="meanDirectionDegrees">The mean direction in degrees from true north; <c>null</c> when not reported.</param>
        /// <param name="meanSpeed">The mean speed; <c>null</c> when not reported.</param>
        /// <param name="gustSpeed">The gust speed; <c>null</c> when no gust is reported.</param>
        /// <param name="meanSpeedOperator">The qualifier of the mean speed (for example <see cref="RelationalOperator.Above"/> for <c>P49MPS</c>); <c>null</c> for an exact value.</param>
        /// <param name="gustSpeedOperator">The qualifier of the gust speed; <c>null</c> for an exact value.</param>
        /// <param name="extremeClockwiseDirectionDegrees">The extreme clockwise direction of a varying wind in degrees; <c>null</c> when not reported.</param>
        /// <param name="extremeCounterClockwiseDirectionDegrees">The extreme counter-clockwise direction of a varying wind in degrees; <c>null</c> when not reported.</param>
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

        /// <inheritdoc />
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

        /// <inheritdoc />
        public override bool Equals(object? obj) => obj is SurfaceWind other && Equals(other);

        /// <inheritdoc />
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

        /// <summary>Returns <c>true</c> when the two <see cref="SurfaceWind"/> operands are equal by value.</summary>
        public static bool operator ==(SurfaceWind? left, SurfaceWind? right) => Equals(left, right);

        /// <summary>Returns <c>true</c> when the two <see cref="SurfaceWind"/> operands differ by value.</summary>
        public static bool operator !=(SurfaceWind? left, SurfaceWind? right) => !Equals(left, right);
    }
}
