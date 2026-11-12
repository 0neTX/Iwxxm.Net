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
        /// <summary>Gets the <c>gml:id</c> of the condition or position the volume was read from; an empty string when the source has none.</summary>
        public string Id { get; }
        /// <summary>
        /// Horizontal projection of the volume: a <see cref="GeoPolygon"/> or a <see cref="GeoCircle"/>
        /// (<c>gml:CircleByCenterPoint</c>, e.g. "WI 250NM OF TC CENTRE"); <c>null</c> when not reported.
        /// </summary>
        public GeoSurface? Area { get; }
        /// <summary>Gets the lower vertical limit of the volume (<c>aixm:lowerLimit</c> with its reference) as reported, or <c>null</c> when missing. A report of a cloud top only (<c>CB TOP FL500</c>) has no lower limit.</summary>
        public VerticalLimit? LowerLimit { get; }
        /// <summary>Gets the upper vertical limit of the volume (<c>aixm:upperLimit</c> with its reference) as reported, or <c>null</c> when missing.</summary>
        public VerticalLimit? UpperLimit { get; }
        /// <summary>Gets the expected change in intensity (<c>intensityChange</c> attribute, WMO BUFR code table 0 20 028) as written in the document: <c>NO_CHANGE</c>, <c>WEAKEN</c> or <c>INTENSIFY</c>; <c>null</c> when not reported.</summary>
        public string? IntensityChange { get; }
        /// <summary>Gets the direction the phenomenon is moving towards, in degrees from true north (<c>iwxxm:directionOfMotion</c>), or <c>null</c> when not reported or nil (a stationary phenomenon has a nil direction and a speed of zero).</summary>
        public double? DirectionOfMotionDegrees { get; }
        /// <summary>Gets the speed of movement (<c>iwxxm:speedOfMotion</c>) in the unit reported (<c>km/h</c> or knots), or <c>null</c> when not reported. Zero denotes a stationary phenomenon.</summary>
        public Speed? SpeedOfMotion { get; }
        /// <summary>Gets the position of the tropical cyclone centre of a tropical cyclone SIGMET condition, or <c>null</c> for other phenomena.</summary>
        public GeoCoordinate? TropicalCycloneCentre { get; }

        /// <summary>
        /// <c>@timeIndicator</c> of the enclosing condition collection (SIGMET/AIRMET: observed or forecast);
        /// <c>null</c> when the collection does not report one (e.g. SIGMET forecast positions, VAA clouds).
        /// </summary>
        public TimeIndicator? TimeIndicator { get; }

        /// <summary><c>phenomenonTime</c> of the enclosing collection or conditions; <c>null</c> when nil or unresolved.</summary>
        public DateTimeOffset? PhenomenonTimeUtc { get; }

        /// <summary>Creates a phenomenon volume. The report factories reject a direction of motion outside 0-360 degrees and a negative speed.</summary>
        /// <param name="id">The <c>gml:id</c> of the source condition; <c>null</c> is stored as an empty string.</param>
        /// <param name="area">The horizontal projection (polygon or circle); <c>null</c> when not reported.</param>
        /// <param name="lowerLimit">The lower vertical limit; <c>null</c> when missing.</param>
        /// <param name="upperLimit">The upper vertical limit; <c>null</c> when missing.</param>
        /// <param name="intensityChange">The expected intensity change as written in the document; a blank value is stored as <c>null</c>.</param>
        /// <param name="directionOfMotionDegrees">The direction of motion in degrees from true north; <c>null</c> when not reported.</param>
        /// <param name="speedOfMotion">The speed of motion; <c>null</c> when not reported.</param>
        /// <param name="tropicalCycloneCentre">The tropical cyclone centre; <c>null</c> for other phenomena.</param>
        /// <param name="timeIndicator">Whether the enclosing collection is observed or forecast; <c>null</c> when it does not say.</param>
        /// <param name="phenomenonTimeUtc">The time the conditions apply to; <c>null</c> when nil or unresolved.</param>
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

        /// <inheritdoc />
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

        /// <inheritdoc />
        public override bool Equals(object? obj) => obj is EnRoutePhenomenonVolume other && Equals(other);

        /// <inheritdoc />
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

        /// <summary>Returns <c>true</c> when the two <see cref="EnRoutePhenomenonVolume"/> operands are equal by value.</summary>
        public static bool operator ==(EnRoutePhenomenonVolume? left, EnRoutePhenomenonVolume? right) => Equals(left, right);

        /// <summary>Returns <c>true</c> when the two <see cref="EnRoutePhenomenonVolume"/> operands differ by value.</summary>
        public static bool operator !=(EnRoutePhenomenonVolume? left, EnRoutePhenomenonVolume? right) => !Equals(left, right);
    }
}
