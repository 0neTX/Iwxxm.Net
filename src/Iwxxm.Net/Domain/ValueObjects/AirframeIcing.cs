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
        public string Id { get; }
        /// <summary>Raw degree reference (code URI or value) as reported; <c>null</c> when absent or nil (never invented).</summary>
        public string? RawDegreeReference { get; }
        public IcingDegreeCode Degree { get; }

        /// <summary>
        /// Official WMO BUFR 0 20 041 code parsed from <see cref="RawDegreeReference"/> (bare number or code URI),
        /// or <c>null</c> when the raw value is a TAC abbreviation or does not belong to that table.
        /// </summary>
        public int? DegreeNumericCode => WmoCodeRegistry.TryGetNumericCode(RawDegreeReference, WmoCodeRegistry.IcingDegreeTableUri);
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
            string? rawDegreeReference,
            IcingDegreeCode degree,
            VerticalLimit? baseLimit,
            VerticalLimit topLimit,
            GeoSurface area,
            IReadOnlyList<GeoSurface> areas)
        {
            Id = id;
            RawDegreeReference = rawDegreeReference;
            Degree = degree;
            Base = baseLimit;
            Top = topLimit;
            Area = area;
            Areas = areas;
        }

        public static Result<AirframeIcing> Create(
            string id,
            string? rawDegreeReference,
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

            IcingDegreeCode degree = WmoCodeRegistry.ResolveIcingDegree(rawDegreeReference);

            return Result<AirframeIcing>.Success(new AirframeIcing(
                id,
                string.IsNullOrWhiteSpace(rawDegreeReference) ? null : rawDegreeReference!.Trim(),
                degree,
                baseLimit,
                topLimit,
                area,
                new ReadOnlyCollection<GeoSurface>(new[] { area }.Concat(additionalAreas ?? Enumerable.Empty<GeoSurface>()).ToList())));
        }

        public bool Equals(AirframeIcing? other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Id == other.Id &&
                   RawDegreeReference == other.RawDegreeReference &&
                   Degree == other.Degree &&
                   Nullable.Equals(Base, other.Base) &&
                   Top == other.Top &&
                   Area.Equals(other.Area) &&
                   Areas.SequenceEqual(other.Areas);
        }

        public override bool Equals(object? obj) => obj is AirframeIcing other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Id.GetHashCode();
                hash = (hash * 397) ^ (RawDegreeReference == null ? 0 : RawDegreeReference.GetHashCode());
                hash = (hash * 397) ^ Top.GetHashCode();
                return hash;
            }
        }

        public static bool operator ==(AirframeIcing? left, AirframeIcing? right) => Equals(left, right);

        public static bool operator !=(AirframeIcing? left, AirframeIcing? right) => !Equals(left, right);
    }
}
