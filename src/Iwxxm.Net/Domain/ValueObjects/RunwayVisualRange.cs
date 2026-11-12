using System;

namespace Iwxxm.Net.Reports
{
    /// <summary>
    /// Immutable Value Object representing Runway Visual Range (RVR) reported for a specific runway direction.
    /// <see cref="RunwayDesignator"/> is <c>null</c> when the runway is given only by an <c>xlink:href</c> reference
    /// (not resolved by this version) or is not reported. <see cref="MeanRvrOperator"/> carries the TAC <c>P</c>/<c>M</c>
    /// qualifier (e.g. <c>R27/P2000</c>) and <see cref="PastTendency"/> the <c>U</c>/<c>D</c>/<c>N</c> tendency.
    /// Equality is structural and exact over every component.
    /// </summary>
    public sealed class RunwayVisualRange : IEquatable<RunwayVisualRange>
    {
        /// <summary>Gets the designator of the runway direction the range applies to (for example <c>27</c> or <c>09L</c>, <c>aixm:designator</c> of the referenced <c>RunwayDirection</c>), or <c>null</c> when it is blank or the runway is referenced remotely and not resolved.</summary>
        public string? RunwayDesignator { get; }
        /// <summary>Gets the mean runway visual range of the last 10 minutes in metres (<c>iwxxm:meanRVR</c>), or <c>null</c> when not reported. A reported value at the limit of the scale is qualified by <see cref="MeanRvrOperator"/> (2000 m with <c>Above</c>, 50 m with <c>Below</c>).</summary>
        public double? MeanRvrMeters { get; }
        /// <summary>Gets the tendency of the runway visual range over the 10-minute period (<c>pastTendency</c>), or <c>null</c> when no tendency is reported.</summary>
        public RunwayVisualRangeTendency? PastTendency { get; }
        /// <summary>Gets the qualifier of <see cref="MeanRvrMeters"/> (<c>iwxxm:meanRVROperator</c>), or <c>null</c> when the mean is an exact value.</summary>
        public RelationalOperator? MeanRvrOperator { get; }

        /// <summary>Creates the runway visual range of one runway.</summary>
        /// <param name="runwayDesignator">The runway direction designator; a blank value is stored as <c>null</c>.</param>
        /// <param name="meanRvrMeters">The mean RVR in metres; <c>null</c> when not reported. The report factories reject negative values.</param>
        /// <param name="pastTendency">The RVR tendency; <c>null</c> when none is reported.</param>
        /// <param name="meanRvrOperator">The qualifier of the mean RVR; <c>null</c> for an exact value.</param>
        public RunwayVisualRange(
            string? runwayDesignator,
            double? meanRvrMeters,
            RunwayVisualRangeTendency? pastTendency = null,
            RelationalOperator? meanRvrOperator = null)
        {
            RunwayDesignator = string.IsNullOrWhiteSpace(runwayDesignator) ? null : runwayDesignator;
            MeanRvrMeters = meanRvrMeters;
            PastTendency = pastTendency;
            MeanRvrOperator = meanRvrOperator;
        }

        /// <inheritdoc />
        public bool Equals(RunwayVisualRange? other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return string.Equals(RunwayDesignator, other.RunwayDesignator, StringComparison.Ordinal) &&
                   Nullable.Equals(MeanRvrMeters, other.MeanRvrMeters) &&
                   PastTendency == other.PastTendency &&
                   MeanRvrOperator == other.MeanRvrOperator;
        }

        /// <inheritdoc />
        public override bool Equals(object? obj) => obj is RunwayVisualRange other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode()
        {
            int hash = ValueEquality.Add(ValueEquality.Seed, RunwayDesignator);
            hash = ValueEquality.AddDouble(hash, MeanRvrMeters);
            hash = ValueEquality.Add(hash, PastTendency);
            return ValueEquality.Add(hash, MeanRvrOperator);
        }

        /// <summary>Returns <c>true</c> when the two <see cref="RunwayVisualRange"/> operands are equal by value.</summary>
        public static bool operator ==(RunwayVisualRange? left, RunwayVisualRange? right) => Equals(left, right);

        /// <summary>Returns <c>true</c> when the two <see cref="RunwayVisualRange"/> operands differ by value.</summary>
        public static bool operator !=(RunwayVisualRange? left, RunwayVisualRange? right) => !Equals(left, right);
    }
}
