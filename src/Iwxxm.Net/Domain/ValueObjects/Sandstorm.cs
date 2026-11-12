using System;

namespace Iwxxm.Net.Reports
{
    /// <summary>
    /// Value Object representing a widespread Sandstorm or Duststorm symbol in a WAFS SIGWX chart
    /// (<c>http://codes.wmo.int/iwxxm/MeteorologicalFeature/SANDSTORM</c>).
    /// </summary>
    public sealed class Sandstorm : IEquatable<Sandstorm>
    {
        /// <summary>Gets the <c>gml:id</c> of the feature.</summary>
        public string Id { get; }
        /// <summary>Gets the position of the sandstorm or duststorm symbol.</summary>
        public GeoCoordinate Position { get; }

        private Sandstorm(string id, GeoCoordinate position)
        {
            Id = id;
            Position = position;
        }

        /// <summary>Creates a sandstorm symbol, enforcing the identifier.</summary>
        /// <param name="id">The <c>gml:id</c> of the feature; required (not blank).</param>
        /// <param name="position">The position of the symbol.</param>
        /// <returns>The sandstorm, or a failure with code <c>DOMAIN-SANDSTORM-ID</c>.</returns>
        public static Result<Sandstorm> Create(string id, GeoCoordinate position)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return Result<Sandstorm>.Failure(
                    "DOMAIN-SANDSTORM-ID",
                    "The Sandstorm feature gml:id identifier is required.",
                    ErrorCategory.DomainMapping);
            }

            return Result<Sandstorm>.Success(new Sandstorm(id, position));
        }

        /// <inheritdoc />
        public bool Equals(Sandstorm? other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Id == other.Id && Position == other.Position;
        }

        /// <inheritdoc />
        public override bool Equals(object? obj) => obj is Sandstorm other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                return (Id.GetHashCode() * 397) ^ Position.GetHashCode();
            }
        }

        /// <summary>Returns <c>true</c> when the two <see cref="Sandstorm"/> operands are equal by value.</summary>
        public static bool operator ==(Sandstorm? left, Sandstorm? right) => Equals(left, right);

        /// <summary>Returns <c>true</c> when the two <see cref="Sandstorm"/> operands differ by value.</summary>
        public static bool operator !=(Sandstorm? left, Sandstorm? right) => !Equals(left, right);
    }
}
