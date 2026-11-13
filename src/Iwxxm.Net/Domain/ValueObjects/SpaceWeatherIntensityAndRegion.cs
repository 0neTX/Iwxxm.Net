using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Iwxxm.Net.Reports
{
    /// <summary>
    /// Immutable Value Object representing one <c>iwxxm:SpaceWeatherIntensityAndRegion</c> of a
    /// <see cref="SpaceWeatherAnalysis"/>: an intensity and the <see cref="SpaceWeatherRegion"/>s it applies to, each
    /// with its own location indicators, extent and vertical limits.
    /// Equality is structural and exact; <see cref="Regions"/> is compared by content, in order.
    /// </summary>
    public sealed class SpaceWeatherIntensityAndRegion : IEquatable<SpaceWeatherIntensityAndRegion>
    {
        /// <summary><c>gml:id</c> of the <c>SpaceWeatherIntensityAndRegion</c>.</summary>
        public string Id { get; }

        /// <summary><c>iwxxm:intensity</c> (<c>MOD</c> or <c>SEV</c>).</summary>
        public string? Intensity { get; }

        /// <summary>The regions (<c>iwxxm:region</c>) the intensity applies to, in document order.</summary>
        public IReadOnlyList<SpaceWeatherRegion> Regions { get; }

        /// <summary>Creates one intensity and region group of a space weather analysis.</summary>
        /// <param name="id">The <c>gml:id</c> of the group; <c>null</c> is stored as an empty string.</param>
        /// <param name="intensity">The intensity as written in the document: <c>MOD</c> (moderate) or <c>SEV</c> (severe); <c>null</c> when not reported.</param>
        /// <param name="regions">The regions of the group, in document order; <c>null</c> is treated as empty.</param>
        public SpaceWeatherIntensityAndRegion(
            string id,
            string? intensity,
            IEnumerable<SpaceWeatherRegion>? regions = null)
        {
            Id = id ?? string.Empty;
            Intensity = intensity;
            Regions = new ReadOnlyCollection<SpaceWeatherRegion>(regions?.ToList() ?? new List<SpaceWeatherRegion>(0));
        }

        /// <inheritdoc />
        public bool Equals(SpaceWeatherIntensityAndRegion? other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return string.Equals(Id, other.Id, StringComparison.Ordinal) &&
                   string.Equals(Intensity, other.Intensity, StringComparison.Ordinal) &&
                   ValueEquality.SequenceEqual(Regions, other.Regions);
        }

        /// <inheritdoc />
        public override bool Equals(object? obj) => obj is SpaceWeatherIntensityAndRegion other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode()
        {
            int hash = ValueEquality.Add(ValueEquality.Seed, Id);
            hash = ValueEquality.Add(hash, Intensity);
            return ValueEquality.AddSequence(hash, Regions);
        }

        /// <summary>Returns <c>true</c> when the two <see cref="SpaceWeatherIntensityAndRegion"/> operands are equal by value.</summary>
        public static bool operator ==(SpaceWeatherIntensityAndRegion? left, SpaceWeatherIntensityAndRegion? right) => Equals(left, right);

        /// <summary>Returns <c>true</c> when the two <see cref="SpaceWeatherIntensityAndRegion"/> operands differ by value.</summary>
        public static bool operator !=(SpaceWeatherIntensityAndRegion? left, SpaceWeatherIntensityAndRegion? right) => !Equals(left, right);
    }
}
