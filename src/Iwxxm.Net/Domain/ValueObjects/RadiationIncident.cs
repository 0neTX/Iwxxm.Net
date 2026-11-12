using System;

namespace Iwxxm.Net.Reports
{
    /// <summary>
    /// Value Object representing a Radiological / Nuclear Incident symbol in a WAFS SIGWX chart
    /// (<c>http://codes.wmo.int/iwxxm/MeteorologicalFeature/RADIATION</c>).
    /// </summary>
    public sealed class RadiationIncident : IEquatable<RadiationIncident>
    {
        /// <summary>Gets the <c>gml:id</c> of the feature.</summary>
        public string Id { get; }
        /// <summary>Gets the name of the incident site (<c>iwxxm:siteName</c>, the incident location), trimmed, or <c>null</c> when blank or not reported.</summary>
        public string? SiteName { get; }
        /// <summary>Gets the position of the radiation incident symbol.</summary>
        public GeoCoordinate Position { get; }
        /// <summary>Gets the time of the incident (<c>iwxxm:incidentTime</c>), or <c>null</c> when not reported.</summary>
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

        /// <summary>Creates a radiation incident symbol, enforcing the identifier.</summary>
        /// <param name="id">The <c>gml:id</c> of the feature; required (not blank).</param>
        /// <param name="siteName">The incident site name; a blank value is stored as <c>null</c>.</param>
        /// <param name="position">The position of the symbol.</param>
        /// <param name="incidentTimeUtc">The incident time in UTC; <c>null</c> when not reported.</param>
        /// <returns>The incident, or a failure with code <c>DOMAIN-RADIATION-ID</c>.</returns>
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

        /// <inheritdoc />
        public bool Equals(RadiationIncident? other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Id == other.Id &&
                   SiteName == other.SiteName &&
                   Position == other.Position &&
                   Nullable.Equals(IncidentTimeUtc, other.IncidentTimeUtc);
        }

        /// <inheritdoc />
        public override bool Equals(object? obj) => obj is RadiationIncident other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                return (Id.GetHashCode() * 397) ^ Position.GetHashCode();
            }
        }

        /// <summary>Returns <c>true</c> when the two <see cref="RadiationIncident"/> operands are equal by value.</summary>
        public static bool operator ==(RadiationIncident? left, RadiationIncident? right) => Equals(left, right);

        /// <summary>Returns <c>true</c> when the two <see cref="RadiationIncident"/> operands differ by value.</summary>
        public static bool operator !=(RadiationIncident? left, RadiationIncident? right) => !Equals(left, right);
    }
}
