using System;

namespace Iwxxm.Net.Reports
{
    /// <summary>
    /// Value Object representing a volcano (SIGWX, SIGMET, VONA, QVACI and Volcanic Ash Advisory).
    /// <see cref="Name"/> is <c>null</c> when the document reports it as nil (<c>iwxxm:Volcano/iwxxm:name</c> is
    /// <c>nillable="true"</c> in <c>metFeature.xsd</c>); a name is never invented.
    /// </summary>
    public sealed class Volcano : IEquatable<Volcano>
    {
        /// <summary>Gets the <c>gml:id</c> of the feature.</summary>
        public string Id { get; }
        /// <summary>Gets the volcano name (<c>iwxxm:name</c>), trimmed, or <c>null</c> when nil or blank (unknown).</summary>
        public string? Name { get; }
        /// <summary>Gets the IAVCEI number of the volcano (<c>iwxxm:IAVCEINumber</c>, the International Association of Volcanology and Chemistry of the Earth's Interior identifier), or <c>null</c> when not reported or nil (unknown). The compact SIGWX report reads <c>iwxxm:wmoNumber</c> first and falls back to <c>iwxxm:IAVCEINumber</c>; <c>iwxxm:wmoNumber</c> is a legacy element of the compact SIGWX schema that is not in the official IWXXM 2025-2 schemas. The WAFS SIGWX path reads <c>iwxxm:IAVCEINumber</c> first and falls back to <c>iwxxm:wmoNumber</c>. Volcanoes read from SIGMET and VAA (<c>metce:Volcano</c>) never carry the number, so <c>null</c> does not always mean it was not reported.</summary>
        public string? IavceiNumber { get; }
        /// <summary>Gets the position of the volcano: the <c>iwxxm:phenomenonGeometry</c> point for WAFS SIGWX, VONA and QVACI, the <c>metce:position</c> point for SIGMET and VAA, and the <c>iwxxm:location</c> point for the compact SIGWX report.</summary>
        public GeoCoordinate Position { get; }
        /// <summary>Gets the eruption time in UTC, or <c>null</c> when not reported.</summary>
        public DateTimeOffset? EruptionTimeUtc { get; }

        /// <summary>Activity status (WAFS SIGWX <c>activityStatus</c>); <c>null</c> when not reported.</summary>
        public VolcanoActivityStatus? ActivityStatus { get; }

        /// <summary>Elevation of the ash source (WAFS SIGWX <c>sourceElevation</c>) as reported; <c>null</c> when not reported.</summary>
        public VerticalLimit? SourceElevation { get; }

        private Volcano(
            string id,
            string? name,
            string? iavceiNumber,
            GeoCoordinate position,
            DateTimeOffset? eruptionTimeUtc,
            VolcanoActivityStatus? activityStatus,
            VerticalLimit? sourceElevation)
        {
            Id = id;
            Name = name;
            IavceiNumber = iavceiNumber;
            Position = position;
            EruptionTimeUtc = eruptionTimeUtc;
            ActivityStatus = activityStatus;
            SourceElevation = sourceElevation;
        }

        /// <summary>Creates a volcano, enforcing the identifier and a UTC eruption time.</summary>
        /// <param name="id">The <c>gml:id</c> of the feature; required (not blank).</param>
        /// <param name="name">The volcano name; a blank value is stored as <c>null</c>.</param>
        /// <param name="iavceiNumber">The IAVCEI number; <c>null</c> when not reported.</param>
        /// <param name="position">The position of the volcano.</param>
        /// <param name="eruptionTimeUtc">The eruption time; <c>null</c> when not reported, otherwise expressed in UTC (offset zero).</param>
        /// <param name="activityStatus">The activity status; <c>null</c> when not reported.</param>
        /// <param name="sourceElevation">The elevation of the ash source; <c>null</c> when not reported.</param>
        /// <returns>The volcano, or a failure with code <c>DOMAIN-VOLCANO-ID</c> or the time-not-UTC code.</returns>
        public static Result<Volcano> Create(
            string id,
            string? name,
            string? iavceiNumber,
            GeoCoordinate position,
            DateTimeOffset? eruptionTimeUtc = null,
            VolcanoActivityStatus? activityStatus = null,
            VerticalLimit? sourceElevation = null)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return Result<Volcano>.Failure(
                    "DOMAIN-VOLCANO-ID",
                    "The volcano gml:id identifier is required.",
                    ErrorCategory.DomainMapping);
            }

            if (eruptionTimeUtc.HasValue && eruptionTimeUtc.Value.Offset != TimeSpan.Zero)
            {
                return Result<Volcano>.Failure(
                    DomainErrorCodes.TimeNotUtc,
                    $"The eruption time of volcano '{id}' must be expressed in UTC (offset +00:00).",
                    ErrorCategory.DomainMapping,
                    id);
            }

            string? normalizedName = string.IsNullOrWhiteSpace(name) ? null : name!.Trim();
            return Result<Volcano>.Success(
                new Volcano(id, normalizedName, iavceiNumber, position, eruptionTimeUtc, activityStatus, sourceElevation));
        }

        /// <inheritdoc />
        public bool Equals(Volcano? other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Id == other.Id &&
                   Name == other.Name &&
                   IavceiNumber == other.IavceiNumber &&
                   Position == other.Position &&
                   Nullable.Equals(EruptionTimeUtc, other.EruptionTimeUtc) &&
                   ActivityStatus == other.ActivityStatus &&
                   Nullable.Equals(SourceElevation, other.SourceElevation);
        }

        /// <inheritdoc />
        public override bool Equals(object? obj) => obj is Volcano other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Id.GetHashCode();
                hash = (hash * 397) ^ (Name == null ? 0 : Name.GetHashCode());
                hash = (hash * 397) ^ Position.GetHashCode();
                return hash;
            }
        }

        /// <summary>Returns <c>true</c> when the two <see cref="Volcano"/> operands are equal by value.</summary>
        public static bool operator ==(Volcano? left, Volcano? right) => Equals(left, right);

        /// <summary>Returns <c>true</c> when the two <see cref="Volcano"/> operands differ by value.</summary>
        public static bool operator !=(Volcano? left, Volcano? right) => !Equals(left, right);
    }
}
