using System;
using System.Linq;
using System.Collections.ObjectModel;
using System.Collections.Generic;

namespace Iwxxm.Net.Reports
{
    /// <summary>
    /// Value Object representing an Airframe Icing forecast volume in a WAFS SIGWX report
    /// (<c>http://codes.wmo.int/iwxxm/MeteorologicalFeature/AIRFRAME_ICING</c>).
    /// </summary>
    public sealed class AirframeIcing : IEquatable<AirframeIcing>
    {
        /// <summary>Gets the <c>gml:id</c> of the feature.</summary>
        public string Id { get; }
        /// <summary>
        /// Degree (WMO BUFR table 0 20 041) translated by the Anti-Corruption Layer: the Domain meaning plus the code as reported;
        /// <c>null</c> when absent or nil (never invented).
        /// </summary>
        public CodeListValue<IcingDegreeCode>? Degree { get; }

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

        private AirframeIcing(
            string id,
            CodeListValue<IcingDegreeCode>? degree,
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

        /// <summary>Creates an airframe icing area of a SIGWX chart, enforcing the identifier, the area and the order of the vertical limits.</summary>
        /// <param name="id">The <c>gml:id</c> of the feature; required (not blank).</param>
        /// <param name="degree">The degree from the WMO code list as reported; <c>null</c> when absent or nil.</param>
        /// <param name="baseLimit">The lower limit as reported; <c>null</c> when not reported.</param>
        /// <param name="topLimit">The upper limit as reported; when it is comparable with <paramref name="baseLimit"/> it must be strictly higher.</param>
        /// <param name="area">The first horizontal extent (polygon or circle); required.</param>
        /// <param name="additionalAreas">The further horizontal extents of a multi-patch feature, in document order; <c>null</c> for none.</param>
        /// <returns>The icing area, or a failure with code <c>DOMAIN-ICING-ID</c>, <c>DOMAIN-ICING-AREA</c> or <c>DOMAIN-ICING-VERTICAL-EXTENT</c>.</returns>
        public static Result<AirframeIcing> Create(
            string id,
            CodeListValue<IcingDegreeCode>? degree,
            VerticalLimit? baseLimit,
            VerticalLimit topLimit,
            GeoSurface area,
            IEnumerable<GeoSurface>? additionalAreas = null)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return Result<AirframeIcing>.Failure(
                    "DOMAIN-ICING-ID",
                    "The AirframeIcing feature gml:id identifier is required.",
                    ErrorCategory.DomainMapping);
            }

            if (area == null)
            {
                return Result<AirframeIcing>.Failure(
                    "DOMAIN-ICING-AREA",
                    $"Geographic polygon area for airframe icing '{id}' is required.",
                    ErrorCategory.DomainMapping,
                    id);
            }

            if (baseLimit.HasValue && topLimit.TryCompareTo(baseLimit.Value, out int order) && order <= 0)
            {
                return Result<AirframeIcing>.Failure(
                    "DOMAIN-ICING-VERTICAL-EXTENT",
                    $"Airframe icing upper limit for '{id}' ({topLimit}) must be strictly greater than its lower level ({baseLimit.Value}).",
                    ErrorCategory.DomainMapping,
                    id);
            }

            return Result<AirframeIcing>.Success(new AirframeIcing(
                id,
                degree,
                baseLimit,
                topLimit,
                area,
                new ReadOnlyCollection<GeoSurface>(new[] { area }.Concat(additionalAreas ?? Enumerable.Empty<GeoSurface>()).ToList())));
        }

        /// <inheritdoc />
        public bool Equals(AirframeIcing? other)
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

        /// <inheritdoc />
        public override bool Equals(object? obj) => obj is AirframeIcing other && Equals(other);

        /// <inheritdoc />
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

        /// <summary>Returns <c>true</c> when the two <see cref="AirframeIcing"/> operands are equal by value.</summary>
        public static bool operator ==(AirframeIcing? left, AirframeIcing? right) => Equals(left, right);

        /// <summary>Returns <c>true</c> when the two <see cref="AirframeIcing"/> operands differ by value.</summary>
        public static bool operator !=(AirframeIcing? left, AirframeIcing? right) => !Equals(left, right);
    }
}
