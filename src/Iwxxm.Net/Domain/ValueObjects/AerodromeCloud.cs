using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Iwxxm.Net.Reports
{
    /// <summary>
    /// Immutable Value Object representing the cloud group of an aerodrome observation or forecast (IWXXM
    /// <c>AerodromeCloud</c> / <c>AerodromeCloudForecast</c>): the cloud layers and the vertical visibility, or the reason
    /// why no cloud is reported.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>No <see cref="AerodromeCloud"/> at all (<c>null</c> on the report) means the cloud group is not reported.</item>
    /// <item><see cref="NilReason"/> set means the group is reported as nil: <see cref="Reports.NilReason.NothingOfOperationalSignificance"/>
    /// is TAC <c>NSC</c>, <see cref="Reports.NilReason.NotDetectedByAutoSystem"/> is <c>NCD</c>.</item>
    /// <item><see cref="VerticalVisibility"/> (<c>VVnnn</c>) is in <c>m</c> or <c>[ft_i]</c> as reported;
    /// <see cref="VerticalVisibilityNilReason"/> holds e.g. <see cref="Reports.NilReason.NotObservable"/> for <c>VV///</c>.</item>
    /// </list>
    /// Equality is structural and exact; layers are compared by content, in order.
    /// </remarks>
    public sealed class AerodromeCloud : IEquatable<AerodromeCloud>
    {
        /// <summary>Creates the cloud group of an aerodrome report or forecast group.</summary>
        /// <param name="layers">The cloud layers in document order; <c>null</c> is treated as empty.</param>
        /// <param name="verticalVisibility">The vertical visibility into an obscuring medium (<c>VVnnn</c>); <c>null</c> when not reported or nil. The schema does not allow it together with cloud layers; a negative value is rejected by the report factories.</param>
        /// <param name="verticalVisibilityNilReason">Why the vertical visibility is nil (for example <c>notObservable</c> for <c>VV///</c>); <c>null</c> otherwise.</param>
        /// <param name="nilReason">Why the whole cloud group is nil (<c>nothingOfOperationalSignificance</c> for <c>NSC</c>, <c>notDetectedByAutoSystem</c> for <c>NCD</c>); <c>null</c> when cloud is reported.</param>
        public AerodromeCloud(
            IEnumerable<AerodromeCloudLayer>? layers = null,
            Length? verticalVisibility = null,
            CodeListValue<NilReason>? verticalVisibilityNilReason = null,
            CodeListValue<NilReason>? nilReason = null)
        {
            Layers = new ReadOnlyCollection<AerodromeCloudLayer>(layers?.ToList() ?? new List<AerodromeCloudLayer>(0));
            VerticalVisibility = verticalVisibility;
            VerticalVisibilityNilReason = verticalVisibilityNilReason;
            NilReason = nilReason;
        }

        /// <summary>Gets the cloud layers, in document order.</summary>
        public IReadOnlyList<AerodromeCloudLayer> Layers { get; }

        /// <summary>Gets the vertical visibility (<c>VVnnn</c>); <c>null</c> when not reported or nil.</summary>
        public Length? VerticalVisibility { get; }

        /// <summary>Gets the reason why the vertical visibility is nil (e.g. <c>notObservable</c> for <c>VV///</c>).</summary>
        public CodeListValue<NilReason>? VerticalVisibilityNilReason { get; }

        /// <summary>Gets the reason why the whole cloud group is nil (<c>NSC</c>, <c>NCD</c>, ...).</summary>
        public CodeListValue<NilReason>? NilReason { get; }

        /// <summary>Gets a value indicating whether no cloud of operational significance is reported (TAC <c>NSC</c>).</summary>
        public bool IsNoSignificantCloud => NilReason?.Code == Reports.NilReason.NothingOfOperationalSignificance;

        /// <summary>Gets a value indicating whether the automatic system detected no cloud (TAC <c>NCD</c>).</summary>
        public bool IsNoCloudDetected => NilReason?.Code == Reports.NilReason.NotDetectedByAutoSystem;

        /// <inheritdoc />
        public bool Equals(AerodromeCloud? other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return ValueEquality.SequenceEqual(Layers, other.Layers) &&
                   Nullable.Equals(VerticalVisibility, other.VerticalVisibility) &&
                   Equals(VerticalVisibilityNilReason, other.VerticalVisibilityNilReason) &&
                   Equals(NilReason, other.NilReason);
        }

        /// <inheritdoc />
        public override bool Equals(object? obj) => obj is AerodromeCloud other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode()
        {
            int hash = ValueEquality.AddSequence(ValueEquality.Seed, Layers);
            hash = ValueEquality.Add(hash, VerticalVisibility);
            hash = ValueEquality.Add(hash, VerticalVisibilityNilReason);
            return ValueEquality.Add(hash, NilReason);
        }

        /// <summary>Returns <c>true</c> when the two <see cref="AerodromeCloud"/> operands are equal by value.</summary>
        public static bool operator ==(AerodromeCloud? left, AerodromeCloud? right) => Equals(left, right);

        /// <summary>Returns <c>true</c> when the two <see cref="AerodromeCloud"/> operands differ by value.</summary>
        public static bool operator !=(AerodromeCloud? left, AerodromeCloud? right) => !Equals(left, right);
    }
}
