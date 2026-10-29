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
        /// <summary>Raw cloud distribution reference as reported; <c>null</c> when absent or nil (never invented).</summary>
        public string? CloudAmount { get; }
        public CloudDistributionCode DistributionCode { get; }
        /// <summary>Raw cloud type reference as reported; <c>null</c> when absent or nil (never invented).</summary>
        public string? CloudType { get; }
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
        /// <summary>Lower limit as reported (value, unit, vertical reference or nil reason); <c>null</c> when not reported.</summary>
        public VerticalLimit? Base { get; }
        /// <summary>Upper limit as reported (value, unit, vertical reference or nil reason).</summary>
        public VerticalLimit Top { get; }
        public GeoPolygon Area { get; }

        private Cloud(
            string id,
            string? cloudAmount,
            CloudDistributionCode distributionCode,
            string? cloudType,
            CloudTypeCode typeCode,
            VerticalLimit? baseLimit,
            VerticalLimit topLimit,
            GeoPolygon area)
        {
            Id = id;
            CloudAmount = cloudAmount;
            DistributionCode = distributionCode;
            CloudType = cloudType;
            TypeCode = typeCode;
            Base = baseLimit;
            Top = topLimit;
            Area = area;
        }

        public static Result<Cloud> Create(
            string id,
            string? cloudAmount,
            string? cloudType,
            VerticalLimit? baseLimit,
            VerticalLimit topLimit,
            GeoPolygon area)
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

            CloudDistributionCode distCode = WmoCodeRegistry.ResolveCloudDistribution(cloudAmount);
            CloudTypeCode typeCode = WmoCodeRegistry.ResolveCloudType(cloudType);

            return Result<Cloud>.Success(new Cloud(
                id,
                string.IsNullOrWhiteSpace(cloudAmount) ? null : cloudAmount!.Trim(),
                distCode,
                string.IsNullOrWhiteSpace(cloudType) ? null : cloudType!.Trim(),
                typeCode,
                baseLimit,
                topLimit,
                area));
        }

        public bool Equals(Cloud? other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Id == other.Id &&
                   CloudAmount == other.CloudAmount &&
                   CloudType == other.CloudType &&
                   Nullable.Equals(Base, other.Base) &&
                   Top == other.Top &&
                   Area.Equals(other.Area);
        }

        public override bool Equals(object? obj) => obj is Cloud other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Id.GetHashCode();
                hash = (hash * 397) ^ (CloudAmount == null ? 0 : CloudAmount.GetHashCode());
                hash = (hash * 397) ^ (CloudType == null ? 0 : CloudType.GetHashCode());
                hash = (hash * 397) ^ Top.GetHashCode();
                return hash;
            }
        }
    }
}
