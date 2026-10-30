using System;

namespace Iwxxm.Net.Reports
{
    /// <summary>
    /// Immutable Value Object representing an airspace volume (horizontal polygon + vertical limits + motion)
    /// used in <c>SIGMET</c>, <c>AIRMET</c>, and <c>VolcanicAshAdvisory</c> conditions.
    /// The speed of motion keeps the reported unit (<c>km/h</c> or <c>[kn_i]</c>). Vertical limits keep the reported
    /// value, unit and reference (<c>aixm:lowerLimit</c> / <c>aixm:lowerLimitReference</c>, ...): use
    /// <see cref="VerticalLimit.AsFlightLevel"/> for the flight-level view of STD limits.
    /// </summary>
    public sealed class EnRoutePhenomenonVolume : IEquatable<EnRoutePhenomenonVolume>
    {
        public string Id { get; }
        public GeoPolygon? Area { get; }
        public VerticalLimit? LowerLimit { get; }
        public VerticalLimit? UpperLimit { get; }
        public string? IntensityChange { get; }
        public double? DirectionOfMotionDegrees { get; }
        public Speed? SpeedOfMotion { get; }
        public GeoCoordinate? TropicalCycloneCentre { get; }

        public EnRoutePhenomenonVolume(
            string id,
            GeoPolygon? area,
            VerticalLimit? lowerLimit,
            VerticalLimit? upperLimit,
            string? intensityChange = null,
            double? directionOfMotionDegrees = null,
            Speed? speedOfMotion = null,
            GeoCoordinate? tropicalCycloneCentre = null)
        {
            Id = id ?? string.Empty;
            Area = area;
            LowerLimit = lowerLimit;
            UpperLimit = upperLimit;
            intensityChange = string.IsNullOrWhiteSpace(intensityChange) ? null : intensityChange;
            IntensityChange = intensityChange;
            DirectionOfMotionDegrees = directionOfMotionDegrees;
            SpeedOfMotion = speedOfMotion;
            TropicalCycloneCentre = tropicalCycloneCentre;
        }

        public bool Equals(EnRoutePhenomenonVolume? other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return string.Equals(Id, other.Id, StringComparison.Ordinal) &&
                   Equals(Area, other.Area) &&
                   Nullable.Equals(LowerLimit, other.LowerLimit) &&
                   Nullable.Equals(UpperLimit, other.UpperLimit) &&
                   string.Equals(IntensityChange, other.IntensityChange, StringComparison.Ordinal) &&
                   Nullable.Equals(DirectionOfMotionDegrees, other.DirectionOfMotionDegrees) &&
                   Nullable.Equals(SpeedOfMotion, other.SpeedOfMotion) &&
                   Nullable.Equals(TropicalCycloneCentre, other.TropicalCycloneCentre);
        }

        public override bool Equals(object? obj) => obj is EnRoutePhenomenonVolume other && Equals(other);

        public override int GetHashCode()
        {
            int hash = ValueEquality.Seed;
            hash = ValueEquality.Add(hash, Id);
            hash = ValueEquality.Add(hash, Area);
            hash = ValueEquality.Add(hash, LowerLimit);
            hash = ValueEquality.Add(hash, UpperLimit);
            hash = ValueEquality.Add(hash, IntensityChange);
            hash = ValueEquality.AddDouble(hash, DirectionOfMotionDegrees);
            hash = ValueEquality.Add(hash, SpeedOfMotion);
            hash = ValueEquality.Add(hash, TropicalCycloneCentre);
            return hash;
        }

        public static bool operator ==(EnRoutePhenomenonVolume? left, EnRoutePhenomenonVolume? right) => Equals(left, right);

        public static bool operator !=(EnRoutePhenomenonVolume? left, EnRoutePhenomenonVolume? right) => !Equals(left, right);
    }
}
