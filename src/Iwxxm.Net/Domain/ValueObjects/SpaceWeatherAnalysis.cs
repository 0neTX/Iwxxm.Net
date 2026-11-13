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
        /// <summary>Creates one space weather analysis.</summary>
        /// <param name="id">The <c>gml:id</c> of the analysis; <c>null</c> is stored as an empty string.</param>
        /// <param name="timeIndicator">Whether the analysis is an observation or a forecast; <c>null</c> when not reported.</param>
        /// <param name="phenomenonTimeUtc">The time the analysis applies to; <c>null</c> when nil or unresolved.</param>
        /// <param name="regions">The intensity and region groups in document order; <c>null</c> is treated as empty.</param>
        /// <param name="nilReason">The <c>nilReason</c> of a nil <c>intensityAndRegion</c> (<see cref="Reports.NilReason.NothingOfOperationalSignificance"/> for "NO SWX EXP"); <c>null</c> when the group is not nil.</param>
        public SpaceWeatherAnalysis(
            string id,
            TimeIndicator? timeIndicator,
            DateTimeOffset? phenomenonTimeUtc,
            IEnumerable<SpaceWeatherAnalysisRegion>? regions = null,
            CodeListValue<NilReason>? nilReason = null)
        {
            Id = id ?? string.Empty;
            TimeIndicator = timeIndicator;
            PhenomenonTimeUtc = phenomenonTimeUtc;
            Regions = new ReadOnlyCollection<SpaceWeatherAnalysisRegion>(regions?.ToList() ?? new List<SpaceWeatherAnalysisRegion>(0));
            NilReason = nilReason;
        }

        /// <summary><c>gml:id</c> of the <c>SpaceWeatherAnalysis</c>.</summary>
        public string Id { get; }

        /// <summary><c>@timeIndicator</c> (observation or forecast); <c>null</c> only when not reported.</summary>
        public TimeIndicator? TimeIndicator { get; }

        /// <summary><c>phenomenonTime</c>, inline or by local reference; <c>null</c> when nil or unresolved.</summary>
        public DateTimeOffset? PhenomenonTimeUtc { get; }

        /// <summary>Intensity and region groups (<c>iwxxm:intensityAndRegion</c>), in document order.</summary>
        public IReadOnlyList<SpaceWeatherAnalysisRegion> Regions { get; }

        /// <summary><c>@nilReason</c> of a nil <c>intensityAndRegion</c> as a code of the WMO register <c>http://codes.wmo.int/common/nil</c> (e.g. <c>nothingOfOperationalSignificance</c>), or <c>null</c>. A reason outside the register is kept as <c>Unknown</c> with its raw value in <see cref="CodeListValue{TCode}.Notation"/>.</summary>
        public CodeListValue<NilReason>? NilReason { get; }

        /// <summary>Gets a value indicating whether the analysis states that no operationally significant space weather is expected.</summary>
        public bool IsNothingOfOperationalSignificance =>
            NilReason?.Code == Reports.NilReason.NothingOfOperationalSignificance;

        /// <inheritdoc />
        public bool Equals(SpaceWeatherAnalysis? other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return string.Equals(Id, other.Id, StringComparison.Ordinal) &&
                   TimeIndicator == other.TimeIndicator &&
                   Nullable.Equals(PhenomenonTimeUtc, other.PhenomenonTimeUtc) &&
                   ValueEquality.SequenceEqual(Regions, other.Regions) &&
                   Equals(NilReason, other.NilReason);
        }

        /// <inheritdoc />
        public override bool Equals(object? obj) => obj is SpaceWeatherAnalysis other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode()
        {
            int hash = ValueEquality.Add(ValueEquality.Seed, Id);
            hash = ValueEquality.Add(hash, TimeIndicator);
            hash = ValueEquality.Add(hash, PhenomenonTimeUtc);
            hash = ValueEquality.AddSequence(hash, Regions);
            return ValueEquality.Add(hash, NilReason);
        }

        /// <summary>Returns <c>true</c> when the two <see cref="SpaceWeatherAnalysis"/> operands are equal by value.</summary>
        public static bool operator ==(SpaceWeatherAnalysis? left, SpaceWeatherAnalysis? right) => Equals(left, right);

        /// <summary>Returns <c>true</c> when the two <see cref="SpaceWeatherAnalysis"/> operands differ by value.</summary>
        public static bool operator !=(SpaceWeatherAnalysis? left, SpaceWeatherAnalysis? right) => !Equals(left, right);
    }
}
