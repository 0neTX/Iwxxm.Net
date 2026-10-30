using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Iwxxm.Net.Reports
{
    /// <summary>
    /// Immutable Value Object representing an analyzed or forecast Space Weather intensity and geographic region.
    /// <see cref="LocationIndicators"/> holds the location indicators (WMO code list <c>SpaceWxLocation</c>).
    /// Equality is structural and exact; the collections are compared by content, in order.
    /// </summary>
    public sealed class SpaceWeatherAnalysisRegion : IEquatable<SpaceWeatherAnalysisRegion>
    {
        public string Id { get; }
        public string? TimeIndicator { get; }
        public DateTimeOffset? PhenomenonTimeUtc { get; }
        public string? Intensity { get; }
        public IReadOnlyList<CodeListValue<SpaceWeatherLocation>> LocationIndicators { get; }
        public IReadOnlyList<GeoPolygon> Polygons { get; }

        public SpaceWeatherAnalysisRegion(
            string id,
            string? timeIndicator,
            DateTimeOffset? phenomenonTimeUtc,
            string? intensity,
            IEnumerable<CodeListValue<SpaceWeatherLocation>>? locationIndicators = null,
            IEnumerable<GeoPolygon>? polygons = null)
        {
            Id = id ?? string.Empty;
            TimeIndicator = timeIndicator;
            PhenomenonTimeUtc = phenomenonTimeUtc;
            Intensity = intensity;
            LocationIndicators = new ReadOnlyCollection<CodeListValue<SpaceWeatherLocation>>(
                locationIndicators?.ToList() ?? new List<CodeListValue<SpaceWeatherLocation>>(0));
            Polygons = new ReadOnlyCollection<GeoPolygon>(polygons?.ToList() ?? new List<GeoPolygon>(0));
        }

        public bool Equals(SpaceWeatherAnalysisRegion? other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return string.Equals(Id, other.Id, StringComparison.Ordinal) &&
                   string.Equals(TimeIndicator, other.TimeIndicator, StringComparison.Ordinal) &&
                   Nullable.Equals(PhenomenonTimeUtc, other.PhenomenonTimeUtc) &&
                   string.Equals(Intensity, other.Intensity, StringComparison.Ordinal) &&
                   ValueEquality.SequenceEqual(LocationIndicators, other.LocationIndicators) &&
                   ValueEquality.SequenceEqual(Polygons, other.Polygons);
        }

        public override bool Equals(object? obj) => obj is SpaceWeatherAnalysisRegion other && Equals(other);

        public override int GetHashCode()
        {
            int hash = ValueEquality.Add(ValueEquality.Seed, Id);
            hash = ValueEquality.Add(hash, TimeIndicator);
            hash = ValueEquality.Add(hash, PhenomenonTimeUtc);
            hash = ValueEquality.Add(hash, Intensity);
            hash = ValueEquality.AddSequence(hash, LocationIndicators);
            return ValueEquality.AddSequence(hash, Polygons);
        }

        public static bool operator ==(SpaceWeatherAnalysisRegion? left, SpaceWeatherAnalysisRegion? right) => Equals(left, right);

        public static bool operator !=(SpaceWeatherAnalysisRegion? left, SpaceWeatherAnalysisRegion? right) => !Equals(left, right);
    }
}
