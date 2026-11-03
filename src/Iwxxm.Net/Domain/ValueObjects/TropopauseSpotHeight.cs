using System;

namespace Iwxxm.Net.Reports
{
    /// <summary>
    /// Discrete Tropopause height measurement point (Spot Height, High or Low). A contour is not a set of spot heights: its single level is <see cref="Tropopause.ContourLevel"/>.
    /// </summary>
    public sealed class TropopauseSpotHeight : IEquatable<TropopauseSpotHeight>
    {
        public GeoCoordinate Location { get; }
        /// <summary>Tropopause level as reported (value, unit and vertical reference).</summary>
        public VerticalLimit Level { get; }

        public TropopauseSpotHeight(GeoCoordinate location, VerticalLimit level)
        {
            Location = location;
            Level = level;
        }

        public bool Equals(TropopauseSpotHeight? other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Location == other.Location && Level == other.Level;
        }

        public override bool Equals(object? obj) => obj is TropopauseSpotHeight other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                return (Location.GetHashCode() * 397) ^ Level.GetHashCode();
            }
        }

        public static bool operator ==(TropopauseSpotHeight? left, TropopauseSpotHeight? right) => Equals(left, right);

        public static bool operator !=(TropopauseSpotHeight? left, TropopauseSpotHeight? right) => !Equals(left, right);
    }
}
