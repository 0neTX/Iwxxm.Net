using System;

namespace Iwxxm.Net.Reports
{
    /// <summary>
    /// Value Object representing a significant cloud distribution (e.g., ISOL/OCNL/FRQ CB)
    /// bounded by a geographic polygon in a SIGWX report.
    /// Exposes the strongly-typed WMO BUFR code enums together with the original raw/URI code strings
    /// (<see cref="CloudAmount"/>, <see cref="CloudType"/>) and the parsed official numeric codes.
    /// </summary>
    public sealed class Cloud : IEquatable<Cloud>
    {
        public string Id { get; }
        public string CloudAmount { get; }
        public CloudDistributionCode DistributionCode { get; }
        public string CloudType { get; }
        public CloudTypeCode TypeCode { get; }

        /// <summary>
        /// Official WMO BUFR 0 20 008 code parsed from <see cref="CloudAmount"/> (bare number or code URI),
        /// or <c>null</c> when the raw value is a TAC abbreviation or does not belong to that table.
        /// </summary>
        public int? DistributionNumericCode => WmoCodeRegistry.TryGetNumericCode(CloudAmount, WmoCodeRegistry.CloudDistributionTableUri);

        /// <summary>
        /// Official WMO BUFR 0 20 012 code parsed from <see cref="CloudType"/> (bare number or code URI),
        /// or <c>null</c> when the raw value is a TAC abbreviation or does not belong to that table.
        /// </summary>
        public int? TypeNumericCode => WmoCodeRegistry.TryGetNumericCode(CloudType, WmoCodeRegistry.CloudTypeTableUri);
        public FlightLevel? BaseFlightLevel { get; }
        public FlightLevel TopFlightLevel { get; }
        public GeoPolygon Area { get; }

        private Cloud(
            string id,
            string cloudAmount,
            CloudDistributionCode distributionCode,
            string cloudType,
            CloudTypeCode typeCode,
            FlightLevel? baseFlightLevel,
            FlightLevel topFlightLevel,
            GeoPolygon area)
        {
            Id = id;
            CloudAmount = cloudAmount;
            DistributionCode = distributionCode;
            CloudType = cloudType;
            TypeCode = typeCode;
            BaseFlightLevel = baseFlightLevel;
            TopFlightLevel = topFlightLevel;
            Area = area;
        }

        public static Result<Cloud> Create(
            string id,
            string cloudAmount,
            string cloudType,
            FlightLevel? baseFlightLevel,
            FlightLevel topFlightLevel,
            GeoPolygon area)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return Result<Cloud>.Failure(
                    "DOMAIN-CLOUD-ID",
                    "The cloud phenomenon gml:id identifier is required.",
                    ErrorCategory.DomainMapping);
            }

            if (string.IsNullOrWhiteSpace(cloudAmount))
            {
                return Result<Cloud>.Failure(
                    "DOMAIN-CLOUD-AMOUNT",
                    $"Cloud amount/distribution in '{id}' is required.",
                    ErrorCategory.DomainMapping,
                    id);
            }

            if (string.IsNullOrWhiteSpace(cloudType))
            {
                return Result<Cloud>.Failure(
                    "DOMAIN-CLOUD-TYPE",
                    $"Cloud type in '{id}' is required.",
                    ErrorCategory.DomainMapping,
                    id);
            }

            if (area == null)
            {
                return Result<Cloud>.Failure(
                    "DOMAIN-CLOUD-AREA",
                    $"Geographic polygon area for cloud '{id}' is required.",
                    ErrorCategory.DomainMapping,
                    id);
            }

            if (baseFlightLevel.HasValue && topFlightLevel <= baseFlightLevel.Value)
            {
                return Result<Cloud>.Failure(
                    "DOMAIN-CLOUD-VERTICAL-EXTENT",
                    $"Cloud top flight level for '{id}' ({topFlightLevel}) must be strictly greater than its base ({baseFlightLevel.Value}).",
                    ErrorCategory.DomainMapping,
                    id);
            }

            CloudDistributionCode distCode = WmoCodeRegistry.ResolveCloudDistribution(cloudAmount);
            CloudTypeCode typeCode = WmoCodeRegistry.ResolveCloudType(cloudType);

            return Result<Cloud>.Success(new Cloud(
                id,
                cloudAmount.Trim(),
                distCode,
                cloudType.Trim(),
                typeCode,
                baseFlightLevel,
                topFlightLevel,
                area));
        }

        public bool Equals(Cloud? other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Id == other.Id &&
                   CloudAmount == other.CloudAmount &&
                   CloudType == other.CloudType &&
                   Nullable.Equals(BaseFlightLevel, other.BaseFlightLevel) &&
                   TopFlightLevel == other.TopFlightLevel &&
                   Area.Equals(other.Area);
        }

        public override bool Equals(object? obj) => obj is Cloud other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Id.GetHashCode();
                hash = (hash * 397) ^ CloudAmount.GetHashCode();
                hash = (hash * 397) ^ CloudType.GetHashCode();
                hash = (hash * 397) ^ TopFlightLevel.GetHashCode();
                return hash;
            }
        }
    }
}
