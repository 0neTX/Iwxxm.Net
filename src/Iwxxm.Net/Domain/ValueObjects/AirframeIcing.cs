using System;

namespace Iwxxm.Net.Reports
{
    /// <summary>
    /// Value Object representing an Airframe Icing forecast volume in a WAFS SIGWX report
    /// (<c>http://codes.wmo.int/iwxxm/MeteorologicalFeature/AIRFRAME_ICING</c>).
    /// </summary>
    public sealed class AirframeIcing : IEquatable<AirframeIcing>
    {
        public string Id { get; }
        public string RawDegreeReference { get; }
        public IcingDegreeCode Degree { get; }

        /// <summary>
        /// Official WMO BUFR 0 20 041 code parsed from <see cref="RawDegreeReference"/> (bare number or code URI),
        /// or <c>null</c> when the raw value is a TAC abbreviation or does not belong to that table.
        /// </summary>
        public int? DegreeNumericCode => WmoCodeRegistry.TryGetNumericCode(RawDegreeReference, WmoCodeRegistry.IcingDegreeTableUri);
        public FlightLevel? BaseFlightLevel { get; }
        public FlightLevel TopFlightLevel { get; }
        public GeoPolygon Area { get; }

        private AirframeIcing(
            string id,
            string rawDegreeReference,
            IcingDegreeCode degree,
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

        public static Result<AirframeIcing> Create(
            string id,
            string rawDegreeReference,
            FlightLevel? baseFlightLevel,
            FlightLevel topFlightLevel,
            GeoPolygon area)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return Result<AirframeIcing>.Failure(
                    "DOMAIN-ICING-ID",
                    "The AirframeIcing feature gml:id identifier is required.",
                    ErrorCategory.DomainMapping);
            }

            if (string.IsNullOrWhiteSpace(rawDegreeReference))
            {
                return Result<AirframeIcing>.Failure(
                    "DOMAIN-ICING-DEGREE",
                    $"DegreeOfIcing in '{id}' is required.",
                    ErrorCategory.DomainMapping,
                    id);
            }

            if (area == null)
            {
                return Result<AirframeIcing>.Failure(
                    "DOMAIN-ICING-AREA",
                    $"Geographic polygon area for airframe icing '{id}' is required.",
                    ErrorCategory.DomainMapping,
                    id);
            }

            if (baseFlightLevel.HasValue && topFlightLevel <= baseFlightLevel.Value)
            {
                return Result<AirframeIcing>.Failure(
                    "DOMAIN-ICING-VERTICAL-EXTENT",
                    $"Airframe icing upper flight level for '{id}' ({topFlightLevel}) must be strictly greater than its lower level ({baseFlightLevel.Value}).",
                    ErrorCategory.DomainMapping,
                    id);
            }

            IcingDegreeCode degree = WmoCodeRegistry.ResolveIcingDegree(rawDegreeReference);

            return Result<AirframeIcing>.Success(new AirframeIcing(
                id,
                rawDegreeReference.Trim(),
                degree,
                baseFlightLevel,
                topFlightLevel,
                area));
        }

        public bool Equals(AirframeIcing? other)
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

        public override bool Equals(object? obj) => obj is AirframeIcing other && Equals(other);

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
