using System;

namespace Iwxxm.Net.Reports
{
    /// <summary>
    /// Discrete Tropopause height measurement point (Spot Height, High or Low). A contour is not a set of spot heights: its single level is <see cref="Tropopause.ContourLevel"/>.
    /// </summary>
    public sealed class TropopauseSpotHeight : IEquatable<TropopauseSpotHeight>
    {
        /// <summary>Gets the position of the spot height.</summary>
        public GeoCoordinate Location { get; }
        /// <summary>Tropopause level as reported (value, unit and vertical reference).</summary>
        public VerticalLimit Level { get; }

        /// <summary>Creates a tropopause spot height.</summary>
        /// <param name="location">The position of the spot height.</param>
        /// <param name="level">The tropopause level at that position as reported, with its unit and vertical reference.</param>
        public TropopauseSpotHeight(GeoCoordinate location, VerticalLimit level)
        {
            Location = location;
            Level = level;
        }

        /// <inheritdoc />
        public bool Equals(TropopauseSpotHeight? other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Location == other.Location && Level == other.Level;
        }

        /// <inheritdoc />
        public override bool Equals(object? obj) => obj is TropopauseSpotHeight other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                return (Location.GetHashCode() * 397) ^ Level.GetHashCode();
            }
        }

        /// <summary>Returns <c>true</c> when the two <see cref="TropopauseSpotHeight"/> operands are equal by value.</summary>
        public static bool operator ==(TropopauseSpotHeight? left, TropopauseSpotHeight? right) => Equals(left, right);

        /// <summary>Returns <c>true</c> when the two <see cref="TropopauseSpotHeight"/> operands differ by value.</summary>
        public static bool operator !=(TropopauseSpotHeight? left, TropopauseSpotHeight? right) => !Equals(left, right);
    }
}
