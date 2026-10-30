using System;

namespace Iwxxm.Net.Reports
{
    /// <summary>
    /// Value Object representing a Tropical Cyclone center reported in a WAFS SIGWX chart
    /// (<c>http://codes.wmo.int/iwxxm/MeteorologicalFeature/TROPICAL_CYCLONE</c>).
    /// </summary>
    public sealed class TropicalCyclone : IEquatable<TropicalCyclone>
    {
        public string Id { get; }
        public string Name { get; }
        public GeoCoordinate Position { get; }

        private TropicalCyclone(string id, string name, GeoCoordinate position)
        {
            Id = id;
            Name = name;
            Position = position;
        }

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

        public bool Equals(TropicalCyclone? other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Id == other.Id && Name == other.Name && Position == other.Position;
        }

        public override bool Equals(object? obj) => obj is TropicalCyclone other && Equals(other);

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

        public static bool operator ==(TropicalCyclone? left, TropicalCyclone? right) => Equals(left, right);

        public static bool operator !=(TropicalCyclone? left, TropicalCyclone? right) => !Equals(left, right);
    }
}
