using System;

namespace Iwxxm.Net.Reports
{
    /// <summary>
    /// Immutable Value Object representing an aerodrome cloud layer: cloud amount (WMO code list
    /// <c>CloudAmountReportedAtAerodrome</c>), base height as reported in <c>m</c> or <c>[ft_i]</c>, and optional
    /// significant convective cloud type (WMO code list <c>SigConvectiveCloudType</c>).
    /// </summary>
    /// <remarks>
    /// <see cref="Amount"/> and <see cref="CloudType"/> are <c>null</c> when the element is absent or nil; a reported
    /// value that is not an official code is kept as <c>Unknown</c> with its raw notation. Equality is structural and exact.
    /// </remarks>
    public sealed class AerodromeCloudLayer : IEquatable<AerodromeCloudLayer>
    {
        public CodeListValue<AerodromeCloudAmount>? Amount { get; }
        public Length? Base { get; }
        public CodeListValue<ConvectiveCloudType>? CloudType { get; }

        public AerodromeCloudLayer(
            CodeListValue<AerodromeCloudAmount>? amount,
            Length? @base,
            CodeListValue<ConvectiveCloudType>? cloudType = null)
        {
            Amount = amount;
            Base = @base;
            CloudType = cloudType;
        }

        public bool Equals(AerodromeCloudLayer? other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Equals(Amount, other.Amount) &&
                   Nullable.Equals(Base, other.Base) &&
                   Equals(CloudType, other.CloudType);
        }

        public override bool Equals(object? obj) => obj is AerodromeCloudLayer other && Equals(other);

        public override int GetHashCode()
        {
            int hash = ValueEquality.Add(ValueEquality.Seed, Amount);
            hash = ValueEquality.Add(hash, Base);
            return ValueEquality.Add(hash, CloudType);
        }

        public static bool operator ==(AerodromeCloudLayer? left, AerodromeCloudLayer? right) => Equals(left, right);

        public static bool operator !=(AerodromeCloudLayer? left, AerodromeCloudLayer? right) => !Equals(left, right);
    }
}
