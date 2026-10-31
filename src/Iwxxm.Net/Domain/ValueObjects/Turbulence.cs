using System;
using System.Linq;
using System.Collections.ObjectModel;
using System.Collections.Generic;

namespace Iwxxm.Net.Reports
{
    /// <summary>
    /// Value Object representing a Turbulence (e.g., Clear Air Turbulence - CAT) forecast area
    /// in a WAFS SIGWX report (<c>http://codes.wmo.int/iwxxm/MeteorologicalFeature/TURBULENCE</c>).
    /// </summary>
    public sealed class Turbulence : IEquatable<Turbulence>
    {
        public string Id { get; }
        /// <summary>
        /// Degree (WMO BUFR table 0 11 030) translated by the Anti-Corruption Layer: the Domain meaning plus the code as reported;
        /// <c>null</c> when absent or nil (never invented).
        /// </summary>
        public CodeListValue<TurbulenceDegreeCode>? Degree { get; }

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

        private Turbulence(
            string id,
            CodeListValue<TurbulenceDegreeCode>? degree,
            VerticalLimit? baseLimit,
            VerticalLimit topLimit,
            GeoSurface area,
            IReadOnlyList<GeoSurface> areas)
        {
            Id = id;
            Degree = degree;
            Base = baseLimit;
            Top = topLimit;
            Area = area;
            Areas = areas;
        }

        public static Result<Turbulence> Create(
            string id,
            CodeListValue<TurbulenceDegreeCode>? degree,
            VerticalLimit? baseLimit,
            VerticalLimit topLimit,
            GeoSurface area,
            IEnumerable<GeoSurface>? additionalAreas = null)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return Result<Turbulence>.Failure(
                    "DOMAIN-TURBULENCE-ID",
                    "The Turbulence feature gml:id identifier is required.",
                    ErrorCategory.DomainMapping);
            }

            if (area == null)
            {
                return Result<Turbulence>.Failure(
                    "DOMAIN-TURBULENCE-AREA",
                    $"Geographic polygon area for turbulence '{id}' is required.",
                    ErrorCategory.DomainMapping,
                    id);
            }

            if (baseLimit.HasValue && topLimit.TryCompareTo(baseLimit.Value, out int order) && order <= 0)
            {
                return Result<Turbulence>.Failure(
                    "DOMAIN-TURBULENCE-VERTICAL-EXTENT",
                    $"Turbulence upper limit for '{id}' ({topLimit}) must be strictly greater than its lower level ({baseLimit.Value}).",
                    ErrorCategory.DomainMapping,
                    id);
            }

            return Result<Turbulence>.Success(new Turbulence(
                id,
                degree,
                baseLimit,
                topLimit,
                area,
                new ReadOnlyCollection<GeoSurface>(new[] { area }.Concat(additionalAreas ?? Enumerable.Empty<GeoSurface>()).ToList())));
        }

        public bool Equals(Turbulence? other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Id == other.Id &&
                   Equals(Degree, other.Degree) &&
                   Nullable.Equals(Base, other.Base) &&
                   Top == other.Top &&
                   Area.Equals(other.Area) &&
                   Areas.SequenceEqual(other.Areas);
        }

        public override bool Equals(object? obj) => obj is Turbulence other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Id.GetHashCode();
                hash = (hash * 397) ^ (Degree == null ? 0 : Degree.GetHashCode());
                hash = (hash * 397) ^ Top.GetHashCode();
                return hash;
            }
        }

        public static bool operator ==(Turbulence? left, Turbulence? right) => Equals(left, right);

        public static bool operator !=(Turbulence? left, Turbulence? right) => !Equals(left, right);
    }
}
