using System;

namespace Iwxxm.Net.Reports
{
    /// <summary>
    /// Value Object representing a Tropical Cyclone center reported in a WAFS SIGWX chart
    /// (<c>http://codes.wmo.int/iwxxm/MeteorologicalFeature/TROPICAL_CYCLONE</c>).
    /// </summary>
    public sealed class TropicalCyclone : IEquatable<TropicalCyclone>
    {
        /// <summary>Gets the <c>gml:id</c> of the feature.</summary>
        public string Id { get; }
        /// <summary>Gets the name of the tropical cyclone, trimmed and never blank.</summary>
        public string Name { get; }
        /// <summary>Gets the position of the cyclone centre.</summary>
        public GeoCoordinate Position { get; }

        private TropicalCyclone(string id, string name, GeoCoordinate position)
        {
            Id = id;
            Name = name;
            Position = position;
        }

        /// <summary>Creates a tropical cyclone symbol, enforcing the identifier and the name.</summary>
        /// <param name="id">The <c>gml:id</c> of the feature; required (not blank).</param>
        /// <param name="name">The cyclone name; required (not blank), stored trimmed.</param>
        /// <param name="position">The position of the cyclone centre.</param>
        /// <returns>The cyclone, or a failure with code <c>DOMAIN-TC-ID</c> or <c>DOMAIN-TC-NAME</c>.</returns>
        public static Result<TropicalCyclone> Create(string id, string name, GeoCoordinate position)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return Result<TropicalCyclone>.Failure(
                    "DOMAIN-TC-ID",
                    "The TropicalCyclone feature gml:id identifier is required.",
                    ErrorCategory.DomainMapping);
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                return Result<TropicalCyclone>.Failure(
                    "DOMAIN-TC-NAME",
                    $"The name of TropicalCyclone '{id}' cannot be empty.",
                    ErrorCategory.DomainMapping,
                    id);
            }

            return Result<TropicalCyclone>.Success(new TropicalCyclone(id, name.Trim(), position));
        }

        /// <inheritdoc />
        public bool Equals(TropicalCyclone? other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Id == other.Id && Name == other.Name && Position == other.Position;
        }

        /// <inheritdoc />
        public override bool Equals(object? obj) => obj is TropicalCyclone other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Id.GetHashCode();
                hash = (hash * 397) ^ Name.GetHashCode();
                hash = (hash * 397) ^ Position.GetHashCode();
                return hash;
            }
        }

        /// <summary>Returns <c>true</c> when the two <see cref="TropicalCyclone"/> operands are equal by value.</summary>
        public static bool operator ==(TropicalCyclone? left, TropicalCyclone? right) => Equals(left, right);

        /// <summary>Returns <c>true</c> when the two <see cref="TropicalCyclone"/> operands differ by value.</summary>
        public static bool operator !=(TropicalCyclone? left, TropicalCyclone? right) => !Equals(left, right);
    }
}
