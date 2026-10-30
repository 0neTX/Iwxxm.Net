using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Iwxxm.Net.Reports
{
    /// <summary>
    /// Immutable Value Object representing one <c>iwxxm:SpaceWeatherAnalysis</c> of a Space Weather Advisory (the
    /// observation, or the +6/+12/+18/+24 HR forecast): its time indicator, phenomenon time and intensity/region groups.
    /// </summary>
    /// <remarks>
    /// <para>Groups given by a local reference (<c>xlink:href="#id"</c>, e.g. a forecast that repeats the observed
    /// regions) are resolved and appear here with the referenced identifier.</para>
    /// <para>An analysis whose <c>intensityAndRegion</c> is nil (e.g. <c>nothingOfOperationalSignificance</c>, TAC
    /// "NO SWX EXP") has no regions and keeps the reported <see cref="NilReason"/>; it is distinguishable from missing
    /// data. Equality is structural and exact; <see cref="Regions"/> is compared by content, in order.</para>
    /// </remarks>
    public sealed class SpaceWeatherAnalysis : IEquatable<SpaceWeatherAnalysis>
    {
        /// <summary>The nil reason meaning that no space weather of operational significance is expected.</summary>
        public const string NothingOfOperationalSignificance = "http://codes.wmo.int/common/nil/nothingOfOperationalSignificance";

        public SpaceWeatherAnalysis(
            string id,
            TimeIndicator? timeIndicator,
            DateTimeOffset? phenomenonTimeUtc,
            IEnumerable<SpaceWeatherAnalysisRegion>? regions = null,
            string? nilReason = null)
        {
            Id = id ?? string.Empty;
            TimeIndicator = timeIndicator;
            PhenomenonTimeUtc = phenomenonTimeUtc;
            Regions = new ReadOnlyCollection<SpaceWeatherAnalysisRegion>(regions?.ToList() ?? new List<SpaceWeatherAnalysisRegion>(0));
            NilReason = string.IsNullOrWhiteSpace(nilReason) ? null : nilReason;
        }

        /// <summary><c>gml:id</c> of the <c>SpaceWeatherAnalysis</c>.</summary>
        public string Id { get; }

        /// <summary><c>@timeIndicator</c> (observation or forecast); <c>null</c> only when not reported.</summary>
        public TimeIndicator? TimeIndicator { get; }

        /// <summary><c>phenomenonTime</c>, inline or by local reference; <c>null</c> when nil or unresolved.</summary>
        public DateTimeOffset? PhenomenonTimeUtc { get; }

        /// <summary>Intensity and region groups (<c>iwxxm:intensityAndRegion</c>), in document order.</summary>
        public IReadOnlyList<SpaceWeatherAnalysisRegion> Regions { get; }

        /// <summary><c>@nilReason</c> of a nil <c>intensityAndRegion</c>, or <c>null</c>.</summary>
        public string? NilReason { get; }

        /// <summary>Gets a value indicating whether the analysis states that no operationally significant space weather is expected.</summary>
        public bool IsNothingOfOperationalSignificance =>
            string.Equals(NilReason, NothingOfOperationalSignificance, StringComparison.Ordinal);

        public bool Equals(SpaceWeatherAnalysis? other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return string.Equals(Id, other.Id, StringComparison.Ordinal) &&
                   TimeIndicator == other.TimeIndicator &&
                   Nullable.Equals(PhenomenonTimeUtc, other.PhenomenonTimeUtc) &&
                   ValueEquality.SequenceEqual(Regions, other.Regions) &&
                   string.Equals(NilReason, other.NilReason, StringComparison.Ordinal);
        }

        public override bool Equals(object? obj) => obj is SpaceWeatherAnalysis other && Equals(other);

        public override int GetHashCode()
        {
            int hash = ValueEquality.Add(ValueEquality.Seed, Id);
            hash = ValueEquality.Add(hash, TimeIndicator);
            hash = ValueEquality.Add(hash, PhenomenonTimeUtc);
            hash = ValueEquality.AddSequence(hash, Regions);
            return ValueEquality.Add(hash, NilReason);
        }

        public static bool operator ==(SpaceWeatherAnalysis? left, SpaceWeatherAnalysis? right) => Equals(left, right);

        public static bool operator !=(SpaceWeatherAnalysis? left, SpaceWeatherAnalysis? right) => !Equals(left, right);
    }
}
