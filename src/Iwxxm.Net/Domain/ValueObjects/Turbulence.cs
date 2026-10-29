using System;

namespace Iwxxm.Net.Reports
{
    /// <summary>
    /// Value Object representing a Turbulence (e.g., Clear Air Turbulence - CAT) forecast area
    /// in a WAFS SIGWX report (<c>http://codes.wmo.int/iwxxm/MeteorologicalFeature/TURBULENCE</c>).
    /// </summary>
    public sealed class Turbulence : IEquatable<Turbulence>
    {
        public string Id { get; }
        public string RawDegreeReference { get; }
        public TurbulenceDegreeCode Degree { get; }

        /// <summary>
        /// Official WMO BUFR 0 11 030 code parsed from <see cref="RawDegreeReference"/> (bare number or code URI),
        /// or <c>null</c> when the raw value is a TAC abbreviation or does not belong to that table.
        /// </summary>
        public int? DegreeNumericCode => WmoCodeRegistry.TryGetNumericCode(RawDegreeReference, WmoCodeRegistry.TurbulenceDegreeTableUri);
        public FlightLevel? BaseFlightLevel { get; }
        public FlightLevel TopFlightLevel { get; }
        public GeoPolygon Area { get; }

        private Turbulence(
            string id,
            string rawDegreeReference,
            TurbulenceDegreeCode degree,
            FlightLevel? baseFlightLevel,
            FlightLevel topFlightLevel,
            GeoPolygon area)
        {
            Id = id;
            RawDegreeReference = rawDegreeReference;
            Degree = degree;
            BaseFlightLevel = baseFlightLevel;
            TopFlightLevel = topFlightLevel;
            Area = area;
        }

        public static Result<Turbulence> Create(
            string id,
            string rawDegreeReference,
            FlightLevel? baseFlightLevel,
            FlightLevel topFlightLevel,
            GeoPolygon area)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return Result<Turbulence>.Failure(
                    "DOMAIN-TURBULENCE-ID",
                    "The Turbulence feature gml:id identifier is required.",
                    ErrorCategory.DomainMapping);
            }

            if (string.IsNullOrWhiteSpace(rawDegreeReference))
            {
                return Result<Turbulence>.Failure(
                    "DOMAIN-TURBULENCE-DEGREE",
                    $"DegreeOfTurbulence in '{id}' is required.",
                    ErrorCategory.DomainMapping,
                    id);
            }

            if (area == null)
            {
                return Result<Turbulence>.Failure(
                    "DOMAIN-TURBULENCE-AREA",
                    $"Geographic polygon area for turbulence '{id}' is required.",
                    ErrorCategory.DomainMapping,
                    id);
            }

            if (baseFlightLevel.HasValue && topFlightLevel <= baseFlightLevel.Value)
            {
                return Result<Turbulence>.Failure(
                    "DOMAIN-TURBULENCE-VERTICAL-EXTENT",
                    $"Turbulence upper flight level for '{id}' ({topFlightLevel}) must be strictly greater than its lower level ({baseFlightLevel.Value}).",
                    ErrorCategory.DomainMapping,
                    id);
            }

            TurbulenceDegreeCode degree = WmoCodeRegistry.ResolveTurbulenceDegree(rawDegreeReference);

            return Result<Turbulence>.Success(new Turbulence(
                id,
                rawDegreeReference.Trim(),
                degree,
                baseFlightLevel,
                topFlightLevel,
                area));
        }

        public bool Equals(Turbulence? other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Id == other.Id &&
                   RawDegreeReference == other.RawDegreeReference &&
                   Degree == other.Degree &&
                   Nullable.Equals(BaseFlightLevel, other.BaseFlightLevel) &&
                   TopFlightLevel == other.TopFlightLevel &&
                   Area.Equals(other.Area);
        }

        public override bool Equals(object? obj) => obj is Turbulence other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Id.GetHashCode();
                hash = (hash * 397) ^ RawDegreeReference.GetHashCode();
                hash = (hash * 397) ^ TopFlightLevel.GetHashCode();
                return hash;
            }
        }
    }
}
