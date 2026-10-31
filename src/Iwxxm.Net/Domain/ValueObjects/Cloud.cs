using System;
using System.Linq;
using System.Collections.ObjectModel;
using System.Collections.Generic;

namespace Iwxxm.Net.Reports
{
    /// <summary>
    /// Value Object representing a significant cloud distribution (e.g., ISOL/OCNL/FRQ CB)
    /// bounded by a geographic polygon in a SIGWX report.
    /// <see cref="Distribution"/> (WMO BUFR table 0 20 008) and <see cref="CloudType"/> (0 20 012) are translated by the
    /// Anti-Corruption Layer into <see cref="CodeListValue{TCode}"/> values: the Domain meaning plus the code as reported
    /// (the numeric code of a code URI, or the TAC abbreviation of the compact report); <c>null</c> when not reported.
    /// </summary>
    public sealed class Cloud : IEquatable<Cloud>
    {
        public string Id { get; }
        /// <summary>Cloud distribution (BUFR 0 20 008); <c>null</c> when absent or nil (never invented).</summary>
        public CodeListValue<CloudDistributionCode>? Distribution { get; }

        /// <summary>Cloud type (BUFR 0 20 012); <c>null</c> when absent or nil (never invented).</summary>
        public CodeListValue<CloudTypeCode>? CloudType { get; }
        /// <summary>Lower limit as reported (value, unit, vertical reference or nil reason); <c>null</c> when not reported.</summary>
        public VerticalLimit? Base { get; }
        /// <summary>Upper limit as reported (value, unit, vertical reference or nil reason).</summary>
        public VerticalLimit Top { get; }
        /// <summary>Horizontal extent: a <see cref="GeoPolygon"/> or a <see cref="GeoCircle"/> (<c>gml:CircleByCenterPoint</c>).</summary>
        public GeoSurface Area { get; }

        /// <summary>
        /// Every horizontal extent of the feature, one per <c>gml:PolygonPatch</c> in document order; <see cref="Area"/> is
        /// the first one. A single-patch feature has exactly one entry.
        /// </summary>
        public IReadOnlyList<GeoSurface> Areas { get; }

        private Cloud(
            string id,
            CodeListValue<CloudDistributionCode>? distribution,
            CodeListValue<CloudTypeCode>? cloudType,
            VerticalLimit? baseLimit,
            VerticalLimit topLimit,
            GeoSurface area,
            IReadOnlyList<GeoSurface> areas)
        {
            Id = id;
            Distribution = distribution;
            CloudType = cloudType;
            Base = baseLimit;
            Top = topLimit;
            Area = area;
            Areas = areas;
        }

        public static Result<Cloud> Create(
            string id,
            CodeListValue<CloudDistributionCode>? distribution,
            CodeListValue<CloudTypeCode>? cloudType,
            VerticalLimit? baseLimit,
            VerticalLimit topLimit,
            GeoSurface area,
            IEnumerable<GeoSurface>? additionalAreas = null)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return Result<Cloud>.Failure(
                    "DOMAIN-CLOUD-ID",
                    "The cloud phenomenon gml:id identifier is required.",
                    ErrorCategory.DomainMapping);
            }

            if (area == null)
            {
                return Result<Cloud>.Failure(
                    "DOMAIN-CLOUD-AREA",
                    $"Geographic polygon area for cloud '{id}' is required.",
                    ErrorCategory.DomainMapping,
                    id);
            }

            if (baseLimit.HasValue && topLimit.TryCompareTo(baseLimit.Value, out int order) && order <= 0)
            {
                return Result<Cloud>.Failure(
                    "DOMAIN-CLOUD-VERTICAL-EXTENT",
                    $"Cloud top limit for '{id}' ({topLimit}) must be strictly greater than its base ({baseLimit.Value}).",
                    ErrorCategory.DomainMapping,
                    id);
            }

            return Result<Cloud>.Success(new Cloud(
                id,
                distribution,
                cloudType,
                baseLimit,
                topLimit,
                area,
                new ReadOnlyCollection<GeoSurface>(new[] { area }.Concat(additionalAreas ?? Enumerable.Empty<GeoSurface>()).ToList())));
        }

        public bool Equals(Cloud? other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Id == other.Id &&
                   Equals(Distribution, other.Distribution) &&
                   Equals(CloudType, other.CloudType) &&
                   Nullable.Equals(Base, other.Base) &&
                   Top == other.Top &&
                   Area.Equals(other.Area) &&
                   Areas.SequenceEqual(other.Areas);
        }

        public override bool Equals(object? obj) => obj is Cloud other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Id.GetHashCode();
                hash = (hash * 397) ^ (Distribution == null ? 0 : Distribution.GetHashCode());
                hash = (hash * 397) ^ (CloudType == null ? 0 : CloudType.GetHashCode());
                hash = (hash * 397) ^ Top.GetHashCode();
                return hash;
            }
        }

        public static bool operator ==(Cloud? left, Cloud? right) => Equals(left, right);

        public static bool operator !=(Cloud? left, Cloud? right) => !Equals(left, right);
    }
}
