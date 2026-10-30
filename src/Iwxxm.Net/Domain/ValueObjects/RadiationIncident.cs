using System;

namespace Iwxxm.Net.Reports
{
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

        public static bool operator ==(RadiationIncident? left, RadiationIncident? right) => Equals(left, right);

        public static bool operator !=(RadiationIncident? left, RadiationIncident? right) => !Equals(left, right);
    }
}
