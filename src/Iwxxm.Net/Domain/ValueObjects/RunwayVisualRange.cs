using System;

namespace Iwxxm.Net.Reports
{
    /// <summary>
    /// Immutable Value Object representing Runway Visual Range (RVR) reported for a specific runway direction.
    /// <see cref="RunwayDesignator"/> is <c>null</c> when the runway is given only by an <c>xlink:href</c> reference
    /// (not resolved by this version) or is not reported. Equality is structural and exact over every component.
    /// </summary>
    public sealed class RunwayVisualRange : IEquatable<RunwayVisualRange>
    {
        public string? RunwayDesignator { get; }
        public double? MeanRvrMeters { get; }
        public string? PastTendency { get; }

        public RunwayVisualRange(string? runwayDesignator, double? meanRvrMeters, string? pastTendency = null)
        {
            RunwayDesignator = string.IsNullOrWhiteSpace(runwayDesignator) ? null : runwayDesignator;
            MeanRvrMeters = meanRvrMeters;
            PastTendency = pastTendency;
        }

        public bool Equals(RunwayVisualRange? other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return string.Equals(RunwayDesignator, other.RunwayDesignator, StringComparison.Ordinal) &&
                   Nullable.Equals(MeanRvrMeters, other.MeanRvrMeters) &&
                   string.Equals(PastTendency, other.PastTendency, StringComparison.Ordinal);
        }

        public override bool Equals(object? obj) => obj is RunwayVisualRange other && Equals(other);

        public override int GetHashCode()
        {
            int hash = ValueEquality.Add(ValueEquality.Seed, RunwayDesignator);
            hash = ValueEquality.AddDouble(hash, MeanRvrMeters);
            return ValueEquality.Add(hash, PastTendency);
        }

        public static bool operator ==(RunwayVisualRange? left, RunwayVisualRange? right) => Equals(left, right);

        public static bool operator !=(RunwayVisualRange? left, RunwayVisualRange? right) => !Equals(left, right);
    }
}
