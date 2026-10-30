using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Iwxxm.Net.Reports
{
    /// <summary>
    /// Immutable Value Object representing an <c>iwxxm:AIRMETEvolvingCondition</c> (<c>airmet.xsd</c>): the phenomenon
    /// volume shared with SIGMET (<see cref="Volume"/>) plus the AIRMET-specific content: cloud base and top
    /// (<c>BKN CLD</c> / <c>OVC CLD</c>), surface visibility and its causes (<c>SFC VIS</c>) and surface wind
    /// (<c>SFC WIND</c>). Every measure keeps the reported value and unit; absent elements are <c>null</c>.
    /// Equality is structural and exact; <see cref="SurfaceVisibilityCauses"/> is compared by content, in order.
    /// </summary>
    public sealed class AirmetEvolvingCondition : IEquatable<AirmetEvolvingCondition>
    {
        public AirmetEvolvingCondition(
            EnRoutePhenomenonVolume volume,
            VerticalLimit? cloudBase = null,
            VerticalLimit? cloudTop = null,
            bool? isCloudTopAbove = null,
            Length? surfaceVisibility = null,
            IEnumerable<CodeListValue<VisibilityReductionCause>>? surfaceVisibilityCauses = null,
            double? surfaceWindDirectionDegrees = null,
            Speed? surfaceWindSpeed = null)
        {
            Volume = volume ?? throw new ArgumentNullException(nameof(volume));
            CloudBase = cloudBase;
            CloudTop = cloudTop;
            IsCloudTopAbove = isCloudTopAbove;
            SurfaceVisibility = surfaceVisibility;
            SurfaceVisibilityCauses = new ReadOnlyCollection<CodeListValue<VisibilityReductionCause>>(
                surfaceVisibilityCauses?.ToList() ?? new List<CodeListValue<VisibilityReductionCause>>(0));
            SurfaceWindDirectionDegrees = surfaceWindDirectionDegrees;
            SurfaceWindSpeed = surfaceWindSpeed;
        }

        /// <summary>Phenomenon volume (geometry, vertical limits, motion, intensity change).</summary>
        public EnRoutePhenomenonVolume Volume { get; }

        /// <summary>Convenience access to <see cref="EnRoutePhenomenonVolume.Id"/> (<c>gml:id</c> of the condition).</summary>
        public string Id => Volume.Id;

        /// <summary><c>cloudBase</c> with <c>cloudBaseReference</c> (<c>m</c> or <c>[ft_i]</c>), or <c>null</c>.</summary>
        public VerticalLimit? CloudBase { get; }

        /// <summary><c>cloudTop</c> with <c>cloudTopReference</c> (<c>m</c> or <c>[ft_i]</c>), or <c>null</c>.</summary>
        public VerticalLimit? CloudTop { get; }

        /// <summary><c>cloudTopAbove</c>: <c>true</c> when the top is above the reported value; <c>null</c> when absent.</summary>
        public bool? IsCloudTopAbove { get; }

        /// <summary><c>surfaceVisibility</c> (<c>m</c>), or <c>null</c>.</summary>
        public Length? SurfaceVisibility { get; }

        /// <summary><c>surfaceVisibilityCause</c> values (WMO code list), in document order.</summary>
        public IReadOnlyList<CodeListValue<VisibilityReductionCause>> SurfaceVisibilityCauses { get; }

        /// <summary><c>surfaceWindDirection</c> in degrees, or <c>null</c>.</summary>
        public double? SurfaceWindDirectionDegrees { get; }

        /// <summary><c>surfaceWindSpeed</c> as reported (<c>m/s</c> or <c>[kn_i]</c>), or <c>null</c>.</summary>
        public Speed? SurfaceWindSpeed { get; }

        public bool Equals(AirmetEvolvingCondition? other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Volume.Equals(other.Volume) &&
                   Nullable.Equals(CloudBase, other.CloudBase) &&
                   Nullable.Equals(CloudTop, other.CloudTop) &&
                   IsCloudTopAbove == other.IsCloudTopAbove &&
                   Nullable.Equals(SurfaceVisibility, other.SurfaceVisibility) &&
                   ValueEquality.SequenceEqual(SurfaceVisibilityCauses, other.SurfaceVisibilityCauses) &&
                   Nullable.Equals(SurfaceWindDirectionDegrees, other.SurfaceWindDirectionDegrees) &&
                   Nullable.Equals(SurfaceWindSpeed, other.SurfaceWindSpeed);
        }

        public override bool Equals(object? obj) => obj is AirmetEvolvingCondition other && Equals(other);

        public override int GetHashCode()
        {
            int hash = ValueEquality.Add(ValueEquality.Seed, Volume);
            hash = ValueEquality.Add(hash, CloudBase);
            hash = ValueEquality.Add(hash, CloudTop);
            hash = ValueEquality.Add(hash, IsCloudTopAbove);
            hash = ValueEquality.Add(hash, SurfaceVisibility);
            hash = ValueEquality.AddSequence(hash, SurfaceVisibilityCauses);
            hash = ValueEquality.AddDouble(hash, SurfaceWindDirectionDegrees);
            return ValueEquality.Add(hash, SurfaceWindSpeed);
        }

        public static bool operator ==(AirmetEvolvingCondition? left, AirmetEvolvingCondition? right) => Equals(left, right);

        public static bool operator !=(AirmetEvolvingCondition? left, AirmetEvolvingCondition? right) => !Equals(left, right);
    }
}
