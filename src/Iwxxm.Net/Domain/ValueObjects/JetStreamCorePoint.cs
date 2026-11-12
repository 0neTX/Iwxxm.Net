using System;

namespace Iwxxm.Net.Reports
{
    /// <summary>
    /// Represents a maximum wind symbol or isotach core point along a Jet Stream axis
    /// (e.g., iwxxm:WAFSJetStreamWindSymbol or iwxxm:corePoint).
    /// </summary>
    public sealed class JetStreamCorePoint : IEquatable<JetStreamCorePoint>
    {
        /// <summary>Gets the position of the wind symbol (<c>iwxxm:location</c>).</summary>
        public GeoCoordinate Location { get; }
        /// <summary>Level of the core point as reported (value, unit and vertical reference; e.g. <c>15240 M MSL</c>).</summary>
        public VerticalLimit Level { get; }
        /// <summary>Wind speed exactly as reported (<c>m/s</c> or <c>[kn_i]</c>).</summary>
        public Speed WindSpeed { get; }
        /// <summary>Gets the level of the 80 kt isotach below the maximum wind level (<c>IsotachLowerVerticalReference</c> and its elevation), or <c>null</c> when not reported.</summary>
        public VerticalLimit? LowerIsotachLimit { get; }
        /// <summary>Gets the level of the 80 kt isotach above the maximum wind level (<c>IsotachUpperVerticalReference</c> and its elevation), or <c>null</c> when not reported.</summary>
        public VerticalLimit? UpperIsotachLimit { get; }

        /// <summary>Creates a jet stream wind symbol.</summary>
        /// <param name="location">The position of the symbol.</param>
        /// <param name="level">The level of the maximum wind as reported, with its unit and vertical reference.</param>
        /// <param name="windSpeed">The wind speed as reported.</param>
        /// <param name="lowerIsotachLimit">The level of the lower isotach; <c>null</c> when not reported.</param>
        /// <param name="upperIsotachLimit">The level of the upper isotach; <c>null</c> when not reported.</param>
        public JetStreamCorePoint(
            GeoCoordinate location,
            VerticalLimit level,
            Speed windSpeed,
            VerticalLimit? lowerIsotachLimit = null,
            VerticalLimit? upperIsotachLimit = null)
        {
            Location = location;
            Level = level;
            WindSpeed = windSpeed;
            LowerIsotachLimit = lowerIsotachLimit;
            UpperIsotachLimit = upperIsotachLimit;
        }

        /// <inheritdoc />
        public bool Equals(JetStreamCorePoint? other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Location == other.Location &&
                   Level == other.Level &&
                   WindSpeed == other.WindSpeed &&
                   Nullable.Equals(LowerIsotachLimit, other.LowerIsotachLimit) &&
                   Nullable.Equals(UpperIsotachLimit, other.UpperIsotachLimit);
        }

        /// <inheritdoc />
        public override bool Equals(object? obj) => obj is JetStreamCorePoint other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode()
        {
            int hash = ValueEquality.Seed;
            hash = ValueEquality.Add(hash, Location);
            hash = ValueEquality.Add(hash, Level);
            hash = ValueEquality.Add(hash, WindSpeed);
            hash = ValueEquality.Add(hash, LowerIsotachLimit);
            hash = ValueEquality.Add(hash, UpperIsotachLimit);
            return hash;
        }

        /// <summary>Returns <c>true</c> when the two <see cref="JetStreamCorePoint"/> operands are equal by value.</summary>
        public static bool operator ==(JetStreamCorePoint? left, JetStreamCorePoint? right) => Equals(left, right);

        /// <summary>Returns <c>true</c> when the two <see cref="JetStreamCorePoint"/> operands differ by value.</summary>
        public static bool operator !=(JetStreamCorePoint? left, JetStreamCorePoint? right) => !Equals(left, right);
    }
}
