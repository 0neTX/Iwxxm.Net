using System;

namespace Iwxxm.Net.Reports
{
    /// <summary>
    /// Represents a maximum wind symbol or isotach core point along a Jet Stream axis
    /// (e.g., iwxxm:WAFSJetStreamWindSymbol or iwxxm:corePoint).
    /// </summary>
    public sealed class JetStreamCorePoint : IEquatable<JetStreamCorePoint>
    {
        public GeoCoordinate Location { get; }
        /// <summary>Level of the core point as reported (value, unit and vertical reference; e.g. <c>15240 M MSL</c>).</summary>
        public VerticalLimit Level { get; }
        /// <summary>Wind speed exactly as reported (<c>m/s</c> or <c>[kn_i]</c>).</summary>
        public Speed WindSpeed { get; }
        public VerticalLimit? LowerIsotachLimit { get; }
        public VerticalLimit? UpperIsotachLimit { get; }

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

        public override bool Equals(object? obj) => obj is JetStreamCorePoint other && Equals(other);

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

        public static bool operator ==(JetStreamCorePoint? left, JetStreamCorePoint? right) => Equals(left, right);

        public static bool operator !=(JetStreamCorePoint? left, JetStreamCorePoint? right) => !Equals(left, right);
    }
}
