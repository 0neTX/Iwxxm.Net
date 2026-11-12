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
    /// value that is not an official code is kept as <c>Unknown</c> with its raw notation. The nil reasons keep the TAC
    /// semantics of missing parts: <see cref="BaseNilReason"/> <c>inapplicable</c> with amount <c>SKC</c> is a clear sky
    /// (TAC <c>SKC</c>/<c>CLR</c>); <c>notObservable</c> / <c>notDetectedByAutoSystem</c> are the automatic-station
    /// <c>///</c> groups; <see cref="NilReason"/> is set when the whole layer is nil (<c>//////</c>). Equality is structural and exact.
    /// </remarks>
    public sealed class AerodromeCloudLayer : IEquatable<AerodromeCloudLayer>
    {
        /// <summary>Gets the cloud amount of the layer (WMO code list <c>CloudAmountReportedAtAerodrome</c>: <c>FEW</c>, <c>SCT</c>, <c>BKN</c> or <c>OVC</c>), or <c>null</c> when nil, see <see cref="AmountNilReason"/>.</summary>
        public CodeListValue<AerodromeCloudAmount>? Amount { get; }
        /// <summary>Gets the height of the lowest level of the layer where the air contains a perceptible quantity of cloud particles, in the unit reported (metres or feet), or <c>null</c> when nil, see <see cref="BaseNilReason"/>.</summary>
        public Length? Base { get; }
        /// <summary>Gets the significant convective cloud type of the layer (WMO code list <c>SigConvectiveCloudType</c>: cumulonimbus or towering cumulus), or <c>null</c> when none is reported.</summary>
        public CodeListValue<ConvectiveCloudType>? CloudType { get; }

        /// <summary>Gets the reason why the amount is nil (e.g. <c>notObservable</c> for <c>///015</c>).</summary>
        public CodeListValue<NilReason>? AmountNilReason { get; }

        /// <summary>Gets the reason why the base is nil (<c>inapplicable</c> for a clear sky, <c>notObservable</c> for <c>BKN///</c>).</summary>
        public CodeListValue<NilReason>? BaseNilReason { get; }

        /// <summary>Gets the reason why the whole layer is nil (<c>//////</c>); amount and base are then <c>null</c>.</summary>
        public CodeListValue<NilReason>? NilReason { get; }

        /// <summary>Creates one cloud layer.</summary>
        /// <param name="amount">The cloud amount; <c>null</c> when nil.</param>
        /// <param name="base">The cloud base height with its unit; <c>null</c> when nil.</param>
        /// <param name="cloudType">The significant convective cloud type; <c>null</c> when none is reported.</param>
        /// <param name="amountNilReason">Why the amount is nil; <c>null</c> otherwise.</param>
        /// <param name="baseNilReason">Why the base is nil; <c>null</c> otherwise.</param>
        /// <param name="nilReason">Why the whole layer is nil; <c>null</c> otherwise.</param>
        public AerodromeCloudLayer(
            CodeListValue<AerodromeCloudAmount>? amount,
            Length? @base,
            CodeListValue<ConvectiveCloudType>? cloudType = null,
            CodeListValue<NilReason>? amountNilReason = null,
            CodeListValue<NilReason>? baseNilReason = null,
            CodeListValue<NilReason>? nilReason = null)
        {
            Amount = amount;
            Base = @base;
            CloudType = cloudType;
            AmountNilReason = amountNilReason;
            BaseNilReason = baseNilReason;
            NilReason = nilReason;
        }

        /// <inheritdoc />
        public bool Equals(AerodromeCloudLayer? other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Equals(Amount, other.Amount) &&
                   Nullable.Equals(Base, other.Base) &&
                   Equals(CloudType, other.CloudType) &&
                   Equals(AmountNilReason, other.AmountNilReason) &&
                   Equals(BaseNilReason, other.BaseNilReason) &&
                   Equals(NilReason, other.NilReason);
        }

        /// <inheritdoc />
        public override bool Equals(object? obj) => obj is AerodromeCloudLayer other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode()
        {
            int hash = ValueEquality.Add(ValueEquality.Seed, Amount);
            hash = ValueEquality.Add(hash, Base);
            hash = ValueEquality.Add(hash, CloudType);
            hash = ValueEquality.Add(hash, AmountNilReason);
            hash = ValueEquality.Add(hash, BaseNilReason);
            return ValueEquality.Add(hash, NilReason);
        }

        /// <summary>Returns <c>true</c> when the two <see cref="AerodromeCloudLayer"/> operands are equal by value.</summary>
        public static bool operator ==(AerodromeCloudLayer? left, AerodromeCloudLayer? right) => Equals(left, right);

        /// <summary>Returns <c>true</c> when the two <see cref="AerodromeCloudLayer"/> operands differ by value.</summary>
        public static bool operator !=(AerodromeCloudLayer? left, AerodromeCloudLayer? right) => !Equals(left, right);
    }
}
