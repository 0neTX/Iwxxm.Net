using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Iwxxm.Net.Reports
{
    /// <summary>
    /// Immutable Value Object representing the observed/estimated conditions or one forecast offset (+6/+12/+18 HR) of a
    /// Volcanic Ash Advisory: its <c>phenomenonTime</c>, <c>@status</c> and ash clouds. Each forecast offset is a separate
    /// value, so clouds of different times are never merged. Equality is structural and exact.
    /// </summary>
    public sealed class VolcanicAshConditions : IEquatable<VolcanicAshConditions>
    {
        /// <summary>Creates the ash conditions of one observation or forecast time.</summary>
        /// <param name="id">The <c>gml:id</c> of the conditions element; <c>null</c> is stored as an empty string.</param>
        /// <param name="phenomenonTimeUtc">The time the conditions apply to; <c>null</c> when nil or unresolved.</param>
        /// <param name="status">The <c>status</c> attribute; <c>null</c> when not reported.</param>
        /// <param name="ashClouds">The ash cloud volumes in document order; <c>null</c> is treated as empty.</param>
        public VolcanicAshConditions(
            string id,
            DateTimeOffset? phenomenonTimeUtc,
            VolcanicAshConditionsStatus? status,
            IEnumerable<EnRoutePhenomenonVolume>? ashClouds = null)
        {
            Id = id ?? string.Empty;
            PhenomenonTimeUtc = phenomenonTimeUtc;
            Status = status;
            AshClouds = new ReadOnlyCollection<EnRoutePhenomenonVolume>(ashClouds?.ToList() ?? new List<EnRoutePhenomenonVolume>(0));
        }

        /// <summary><c>gml:id</c> of the conditions element.</summary>
        public string Id { get; }

        /// <summary><c>phenomenonTime</c> (inline or by local reference); <c>null</c> when nil or unresolved.</summary>
        public DateTimeOffset? PhenomenonTimeUtc { get; }

        /// <summary><c>@status</c> (mandatory in the schema); <c>null</c> only when not reported.</summary>
        public VolcanicAshConditionsStatus? Status { get; }

        /// <summary>Ash clouds of this time, in document order (empty unless <see cref="Status"/> is <c>Provided</c>).</summary>
        public IReadOnlyList<EnRoutePhenomenonVolume> AshClouds { get; }

        /// <inheritdoc />
        public bool Equals(VolcanicAshConditions? other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return string.Equals(Id, other.Id, StringComparison.Ordinal) &&
                   Nullable.Equals(PhenomenonTimeUtc, other.PhenomenonTimeUtc) &&
                   Status == other.Status &&
                   ValueEquality.SequenceEqual(AshClouds, other.AshClouds);
        }

        /// <inheritdoc />
        public override bool Equals(object? obj) => obj is VolcanicAshConditions other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode()
        {
            int hash = ValueEquality.Add(ValueEquality.Seed, Id);
            hash = ValueEquality.Add(hash, PhenomenonTimeUtc);
            hash = ValueEquality.Add(hash, Status);
            return ValueEquality.AddSequence(hash, AshClouds);
        }

        /// <summary>Returns <c>true</c> when the two <see cref="VolcanicAshConditions"/> operands are equal by value.</summary>
        public static bool operator ==(VolcanicAshConditions? left, VolcanicAshConditions? right) => Equals(left, right);

        /// <summary>Returns <c>true</c> when the two <see cref="VolcanicAshConditions"/> operands differ by value.</summary>
        public static bool operator !=(VolcanicAshConditions? left, VolcanicAshConditions? right) => !Equals(left, right);
    }
}
