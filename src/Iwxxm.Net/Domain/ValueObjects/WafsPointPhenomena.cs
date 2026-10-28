using System;
using Iwxxm.Net.Domain.Common;

namespace Iwxxm.Net.Domain.ValueObjects
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
    }

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
    }

    /// <summary>
    /// Value Object representing a Radiological / Nuclear Incident symbol in a WAFS SIGWX chart
    /// (<c>http://codes.wmo.int/iwxxm/MeteorologicalFeature/RADIATION</c>).
    /// </summary>
    public sealed class RadiationIncident : IEquatable<RadiationIncident>
    {
        public string Id { get; }
        public string? SiteName { get; }
        public GeoCoordinate Position { get; }
        public DateTimeOffset? IncidentTimeUtc { get; }

        private RadiationIncident(
            string id,
            string? siteName,
            GeoCoordinate position,
            DateTimeOffset? incidentTimeUtc)
        {
            Id = id;
            SiteName = siteName;
            Position = position;
            IncidentTimeUtc = incidentTimeUtc;
        }

        public static Result<RadiationIncident> Create(
            string id,
            string? siteName,
            GeoCoordinate position,
            DateTimeOffset? incidentTimeUtc = null)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return Result<RadiationIncident>.Failure(
                    "DOMAIN-RADIATION-ID",
                    "The RadiationIncident feature gml:id identifier is required.",
                    ErrorCategory.DomainMapping);
            }

            string? normalizedSite = string.IsNullOrWhiteSpace(siteName) ? null : siteName!.Trim();
            return Result<RadiationIncident>.Success(
                new RadiationIncident(id, normalizedSite, position, incidentTimeUtc));
        }

        public bool Equals(RadiationIncident? other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Id == other.Id &&
                   SiteName == other.SiteName &&
                   Position == other.Position &&
                   Nullable.Equals(IncidentTimeUtc, other.IncidentTimeUtc);
        }

        public override bool Equals(object? obj) => obj is RadiationIncident other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                return (Id.GetHashCode() * 397) ^ Position.GetHashCode();
            }
        }
    }
}
