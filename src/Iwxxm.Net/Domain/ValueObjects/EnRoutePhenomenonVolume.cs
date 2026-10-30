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
        /// <summary>
        /// Horizontal projection of the volume: a <see cref="GeoPolygon"/> or a <see cref="GeoCircle"/>
        /// (<c>gml:CircleByCenterPoint</c>, e.g. "WI 250NM OF TC CENTRE"); <c>null</c> when not reported.
        /// </summary>
        public GeoSurface? Area { get; }
        public VerticalLimit? LowerLimit { get; }
        public VerticalLimit? UpperLimit { get; }
        public string? IntensityChange { get; }
        public double? DirectionOfMotionDegrees { get; }
        public Speed? SpeedOfMotion { get; }
        public GeoCoordinate? TropicalCycloneCentre { get; }

        /// <summary>
        /// <c>@timeIndicator</c> of the enclosing condition collection (SIGMET/AIRMET: observed or forecast);
        /// <c>null</c> when the collection does not report one (e.g. SIGMET forecast positions, VAA clouds).
        /// </summary>
        public TimeIndicator? TimeIndicator { get; }

        /// <summary><c>phenomenonTime</c> of the enclosing collection or conditions; <c>null</c> when nil or unresolved.</summary>
        public DateTimeOffset? PhenomenonTimeUtc { get; }

        public EnRoutePhenomenonVolume(
            string id,
            GeoSurface? area,
            VerticalLimit? lowerLimit,
            VerticalLimit? upperLimit,
            string? intensityChange = null,
            double? directionOfMotionDegrees = null,
            Speed? speedOfMotion = null,
            GeoCoordinate? tropicalCycloneCentre = null,
            TimeIndicator? timeIndicator = null,
            DateTimeOffset? phenomenonTimeUtc = null)
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
            TimeIndicator = timeIndicator;
            PhenomenonTimeUtc = phenomenonTimeUtc;
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
                   Nullable.Equals(TropicalCycloneCentre, other.TropicalCycloneCentre) &&
                   TimeIndicator == other.TimeIndicator &&
                   Nullable.Equals(PhenomenonTimeUtc, other.PhenomenonTimeUtc);
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
            hash = ValueEquality.Add(hash, TimeIndicator);
            return ValueEquality.Add(hash, PhenomenonTimeUtc);
        }

        public static bool operator ==(EnRoutePhenomenonVolume? left, EnRoutePhenomenonVolume? right) => Equals(left, right);

        public static bool operator !=(EnRoutePhenomenonVolume? left, EnRoutePhenomenonVolume? right) => !Equals(left, right);
    }
}
