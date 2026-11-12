using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Iwxxm.Net.Reports
{
    /// <summary>
    /// Immutable Value Object representing one <c>iwxxm:SpaceWeatherIntensityAndRegion</c> of a
    /// <see cref="SpaceWeatherAnalysis"/>: an intensity and its geographic regions.
    /// <see cref="LocationIndicators"/> holds the location indicators (WMO code list <c>SpaceWxLocation</c>).
    /// Equality is structural and exact; the collections are compared by content, in order.
    /// </summary>
    public sealed class SpaceWeatherAnalysisRegion : IEquatable<SpaceWeatherAnalysisRegion>
    {
        /// <summary><c>gml:id</c> of the <c>SpaceWeatherIntensityAndRegion</c>.</summary>
        public string Id { get; }

        /// <summary><c>iwxxm:intensity</c> (<c>MOD</c> or <c>SEV</c>).</summary>
        public string? Intensity { get; }

        /// <summary>Location indicators of the regions (WMO code list <c>SpaceWxLocation</c>), in document order.</summary>
        public IReadOnlyList<CodeListValue<SpaceWeatherLocation>> LocationIndicators { get; }
        /// <summary>
        /// Horizontal extents of the regions, in document order: polygons (latitude bands) or circles
        /// (<c>gml:CircleByCenterPoint</c>, e.g. <c>DAYSIDE</c>). A region whose circle centre is the unspecified
        /// placeholder position <c>9999 9999</c> (e.g. <c>NIGHTSIDE</c>) has no extent here and is identified by
        /// <see cref="LocationIndicators"/> only.
        /// </summary>
        public IReadOnlyList<GeoSurface> Areas { get; }

        /// <summary>Creates one intensity and region group of a space weather analysis.</summary>
        /// <param name="id">The <c>gml:id</c> of the group; <c>null</c> is stored as an empty string.</param>
        /// <param name="intensity">The intensity as written in the document: <c>MOD</c> (moderate) or <c>SEV</c> (severe); <c>null</c> when not reported.</param>
        /// <param name="locationIndicators">The location indicators of the regions (WMO code list <c>SpaceWxLocation</c>); <c>null</c> is treated as empty.</param>
        /// <param name="areas">The horizontal extents of the regions; <c>null</c> is treated as empty.</param>
        public SpaceWeatherAnalysisRegion(
            string id,
            string? intensity,
            IEnumerable<CodeListValue<SpaceWeatherLocation>>? locationIndicators = null,
            IEnumerable<GeoSurface>? areas = null)
        {
            Id = id ?? string.Empty;
            Intensity = intensity;
            LocationIndicators = new ReadOnlyCollection<CodeListValue<SpaceWeatherLocation>>(
                locationIndicators?.ToList() ?? new List<CodeListValue<SpaceWeatherLocation>>(0));
            Areas = new ReadOnlyCollection<GeoSurface>(areas?.ToList() ?? new List<GeoSurface>(0));
        }

        /// <inheritdoc />
        public bool Equals(SpaceWeatherAnalysisRegion? other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return string.Equals(Id, other.Id, StringComparison.Ordinal) &&
                   string.Equals(Intensity, other.Intensity, StringComparison.Ordinal) &&
                   ValueEquality.SequenceEqual(LocationIndicators, other.LocationIndicators) &&
                   ValueEquality.SequenceEqual(Areas, other.Areas);
        }

        /// <inheritdoc />
        public override bool Equals(object? obj) => obj is SpaceWeatherAnalysisRegion other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode()
        {
            int hash = ValueEquality.Add(ValueEquality.Seed, Id);
            hash = ValueEquality.Add(hash, Intensity);
            hash = ValueEquality.AddSequence(hash, LocationIndicators);
            return ValueEquality.AddSequence(hash, Areas);
        }

        /// <summary>Returns <c>true</c> when the two <see cref="SpaceWeatherAnalysisRegion"/> operands are equal by value.</summary>
        public static bool operator ==(SpaceWeatherAnalysisRegion? left, SpaceWeatherAnalysisRegion? right) => Equals(left, right);

        /// <summary>Returns <c>true</c> when the two <see cref="SpaceWeatherAnalysisRegion"/> operands differ by value.</summary>
        public static bool operator !=(SpaceWeatherAnalysisRegion? left, SpaceWeatherAnalysisRegion? right) => !Equals(left, right);
    }
}
