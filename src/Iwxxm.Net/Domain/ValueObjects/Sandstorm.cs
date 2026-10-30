using System;

namespace Iwxxm.Net.Reports
{
    /// <summary>
    /// Value Object representing a widespread Sandstorm or Duststorm symbol in a WAFS SIGWX chart
    /// (<c>http://codes.wmo.int/iwxxm/MeteorologicalFeature/SANDSTORM</c>).
    /// </summary>
    public sealed class Sandstorm : IEquatable<Sandstorm>
    {
        public string Id { get; }
        public GeoCoordinate Position { get; }

        private Sandstorm(string id, GeoCoordinate position)
        {
            Id = id;
            Position = position;
        }

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

        public bool Equals(Sandstorm? other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Id == other.Id && Position == other.Position;
        }

        public override bool Equals(object? obj) => obj is Sandstorm other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                return (Id.GetHashCode() * 397) ^ Position.GetHashCode();
            }
        }

        public static bool operator ==(Sandstorm? left, Sandstorm? right) => Equals(left, right);

        public static bool operator !=(Sandstorm? left, Sandstorm? right) => !Equals(left, right);
    }
}
