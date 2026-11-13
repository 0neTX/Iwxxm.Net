using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Iwxxm.Net.Reports
{
    /// <summary>
    /// Immutable Value Object representing one <c>iwxxm:SpaceWeatherRegion</c> of a
    /// <see cref="SpaceWeatherIntensityAndRegion"/>: the location indicators of the region (WMO code list
    /// <c>SpaceWxLocation</c>) paired with the extent they describe, so a consumer always knows which indicator belongs to
    /// which area. Equality is structural and exact; the collections are compared by content, in order.
    /// </summary>
    /// <remarks>
    /// The schema allows up to 6 <c>locationIndicator</c> elements per region (each may be nil; nil ones are not kept
    /// here, so <see cref="LocationIndicators"/> holds 0 to 6 entries). The constructor does not reject other counts.
    /// </remarks>
    public sealed class SpaceWeatherRegion : IEquatable<SpaceWeatherRegion>
    {
        /// <summary>Creates one region of a space weather intensity and region group.</summary>
        /// <param name="id">The <c>gml:id</c> of the region; <c>null</c> is stored as an empty string.</param>
        /// <param name="locationIndicators">The location indicators of this region, in document order; <c>null</c> is treated as empty.</param>
        /// <param name="areas">The horizontal extents of this region; <c>null</c> is treated as empty.</param>
        /// <param name="lowerLimit">The lower vertical limit of the region (radiation events); <c>null</c> when absent.</param>
        /// <param name="upperLimit">The upper vertical limit of the region (radiation events); <c>null</c> when absent.</param>
        public SpaceWeatherRegion(
            string id,
            IEnumerable<CodeListValue<SpaceWeatherLocation>>? locationIndicators = null,
            IEnumerable<GeoSurface>? areas = null,
            VerticalLimit? lowerLimit = null,
            VerticalLimit? upperLimit = null)
        {
            Id = id ?? string.Empty;
            LocationIndicators = new ReadOnlyCollection<CodeListValue<SpaceWeatherLocation>>(
                locationIndicators?.ToList() ?? new List<CodeListValue<SpaceWeatherLocation>>(0));
            Areas = new ReadOnlyCollection<GeoSurface>(areas?.ToList() ?? new List<GeoSurface>(0));
            LowerLimit = lowerLimit;
            UpperLimit = upperLimit;
        }

        /// <summary><c>gml:id</c> of the <c>SpaceWeatherRegion</c>.</summary>
        public string Id { get; }

        /// <summary>Location indicators of this region only (WMO code list <c>SpaceWxLocation</c>, e.g. <c>MNH</c>, <c>DAYSIDE</c>), in document order; nil indicators are not kept.</summary>
        public IReadOnlyList<CodeListValue<SpaceWeatherLocation>> LocationIndicators { get; }

        /// <summary>
        /// Horizontal extents of this region only, in document order: polygons (latitude bands) or circles
        /// (<c>gml:CircleByCenterPoint</c>, e.g. <c>DAYSIDE</c>). A region whose circle centre is the unspecified
        /// placeholder position <c>9999 9999</c> (e.g. <c>NIGHTSIDE</c>) has no extent here and is identified by
        /// <see cref="LocationIndicators"/> only.
        /// </summary>
        public IReadOnlyList<GeoSurface> Areas { get; }

        /// <summary><c>aixm:lowerLimit</c> of the region location (e.g. FL400 of a radiation event); <c>null</c> when absent.</summary>
        public VerticalLimit? LowerLimit { get; }

        /// <summary><c>aixm:upperLimit</c> of the region location; <c>null</c> when absent (a nil limit is a nil <see cref="VerticalLimit"/>).</summary>
        public VerticalLimit? UpperLimit { get; }

        /// <inheritdoc />
        public bool Equals(SpaceWeatherRegion? other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return string.Equals(Id, other.Id, StringComparison.Ordinal) &&
                   ValueEquality.SequenceEqual(LocationIndicators, other.LocationIndicators) &&
                   ValueEquality.SequenceEqual(Areas, other.Areas) &&
                   Nullable.Equals(LowerLimit, other.LowerLimit) &&
                   Nullable.Equals(UpperLimit, other.UpperLimit);
        }

        /// <inheritdoc />
        public override bool Equals(object? obj) => obj is SpaceWeatherRegion other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode()
        {
            int hash = ValueEquality.Add(ValueEquality.Seed, Id);
            hash = ValueEquality.AddSequence(hash, LocationIndicators);
            hash = ValueEquality.AddSequence(hash, Areas);
            hash = ValueEquality.Add(hash, LowerLimit);
            return ValueEquality.Add(hash, UpperLimit);
        }

        /// <summary>Returns <c>true</c> when the two <see cref="SpaceWeatherRegion"/> operands are equal by value.</summary>
        public static bool operator ==(SpaceWeatherRegion? left, SpaceWeatherRegion? right) => Equals(left, right);

        /// <summary>Returns <c>true</c> when the two <see cref="SpaceWeatherRegion"/> operands differ by value.</summary>
        public static bool operator !=(SpaceWeatherRegion? left, SpaceWeatherRegion? right) => !Equals(left, right);
    }
}
