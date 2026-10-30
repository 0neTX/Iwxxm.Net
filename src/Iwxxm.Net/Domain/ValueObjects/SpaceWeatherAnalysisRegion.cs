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

        public bool Equals(SpaceWeatherAnalysisRegion? other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return string.Equals(Id, other.Id, StringComparison.Ordinal) &&
                   string.Equals(Intensity, other.Intensity, StringComparison.Ordinal) &&
                   ValueEquality.SequenceEqual(LocationIndicators, other.LocationIndicators) &&
                   ValueEquality.SequenceEqual(Areas, other.Areas);
        }

        public override bool Equals(object? obj) => obj is SpaceWeatherAnalysisRegion other && Equals(other);

        public override int GetHashCode()
        {
            int hash = ValueEquality.Add(ValueEquality.Seed, Id);
            hash = ValueEquality.Add(hash, Intensity);
            hash = ValueEquality.AddSequence(hash, LocationIndicators);
            return ValueEquality.AddSequence(hash, Areas);
        }

        public static bool operator ==(SpaceWeatherAnalysisRegion? left, SpaceWeatherAnalysisRegion? right) => Equals(left, right);

        public static bool operator !=(SpaceWeatherAnalysisRegion? left, SpaceWeatherAnalysisRegion? right) => !Equals(left, right);
    }
}
