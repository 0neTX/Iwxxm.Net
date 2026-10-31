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
        public string Id { get; }
        public string? Name { get; }
        public string? WmoNumber { get; }
        public GeoCoordinate Position { get; }
        public DateTimeOffset? EruptionTimeUtc { get; }

        /// <summary>Activity status (WAFS SIGWX <c>activityStatus</c>); <c>null</c> when not reported.</summary>
        public VolcanoActivityStatus? ActivityStatus { get; }

        /// <summary>Elevation of the ash source (WAFS SIGWX <c>sourceElevation</c>) as reported; <c>null</c> when not reported.</summary>
        public VerticalLimit? SourceElevation { get; }

        private Volcano(
            string id,
            string? name,
            string? wmoNumber,
            GeoCoordinate position,
            DateTimeOffset? eruptionTimeUtc,
            VolcanoActivityStatus? activityStatus,
            VerticalLimit? sourceElevation)
        {
            Id = id;
            Name = name;
            WmoNumber = wmoNumber;
            Position = position;
            EruptionTimeUtc = eruptionTimeUtc;
            ActivityStatus = activityStatus;
            SourceElevation = sourceElevation;
        }

        public static Result<Volcano> Create(
            string id,
            string? name,
            string? wmoNumber,
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
                new Volcano(id, normalizedName, wmoNumber, position, eruptionTimeUtc, activityStatus, sourceElevation));
        }

        public bool Equals(Volcano? other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Id == other.Id &&
                   Name == other.Name &&
                   WmoNumber == other.WmoNumber &&
                   Position == other.Position &&
                   Nullable.Equals(EruptionTimeUtc, other.EruptionTimeUtc) &&
                   ActivityStatus == other.ActivityStatus &&
                   Nullable.Equals(SourceElevation, other.SourceElevation);
        }

        public override bool Equals(object? obj) => obj is Volcano other && Equals(other);

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

        public static bool operator ==(Volcano? left, Volcano? right) => Equals(left, right);

        public static bool operator !=(Volcano? left, Volcano? right) => !Equals(left, right);
    }
}
