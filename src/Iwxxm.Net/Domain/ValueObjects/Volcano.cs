using System;

namespace Iwxxm.Net.Reports
{
    /// <summary>
    /// Value Object representing volcanic activity reported in a SIGWX forecast chart.
    /// </summary>
    public sealed class Volcano : IEquatable<Volcano>
    {
        public string Id { get; }
        public string Name { get; }
        public string? WmoNumber { get; }
        public GeoCoordinate Position { get; }
        public DateTimeOffset? EruptionTimeUtc { get; }

        private Volcano(
            string id,
            string name,
            string? wmoNumber,
            GeoCoordinate position,
            DateTimeOffset? eruptionTimeUtc)
        {
            Id = id;
            Name = name;
            WmoNumber = wmoNumber;
            Position = position;
            EruptionTimeUtc = eruptionTimeUtc;
        }

        public static Result<Volcano> Create(
            string id,
            string name,
            string? wmoNumber,
            GeoCoordinate position,
            DateTimeOffset? eruptionTimeUtc = null)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return Result<Volcano>.Failure(
                    "DOMAIN-VOLCANO-ID",
                    "The volcano gml:id identifier is required.",
                    ErrorCategory.DomainMapping);
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                return Result<Volcano>.Failure(
                    "DOMAIN-VOLCANO-NAME",
                    $"The name of volcano '{id}' cannot be empty.",
                    ErrorCategory.DomainMapping,
                    id);
            }

            return Result<Volcano>.Success(
                new Volcano(id, name.Trim(), wmoNumber, position, eruptionTimeUtc));
        }

        public bool Equals(Volcano? other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Id == other.Id &&
                   Name == other.Name &&
                   WmoNumber == other.WmoNumber &&
                   Position == other.Position &&
                   Nullable.Equals(EruptionTimeUtc, other.EruptionTimeUtc);
        }

        public override bool Equals(object? obj) => obj is Volcano other && Equals(other);

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
}
