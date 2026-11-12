using System;

namespace Iwxxm.Net.Reports
{
    /// <summary>
    /// Immutable Value Object representing an observed or forecast tropical cyclone position and intensity. The movement
    /// (<c>movement</c>, <c>movementDirection</c>, <c>movementSpeed</c>) and <c>intensityChange</c> are reported for the
    /// observed position only; they are <c>null</c> for forecast positions.
    /// </summary>
    public sealed class TropicalCycloneTrackPoint : IEquatable<TropicalCycloneTrackPoint>
    {
        /// <summary>Gets the observation or forecast time of the position (<c>iwxxm:phenomenonTime</c>), or <c>null</c> when not reported.</summary>
        public DateTimeOffset? PhenomenonTimeUtc { get; }
        /// <summary>Gets the position of the cyclone centre (<c>iwxxm:position</c>).</summary>
        public GeoCoordinate Position { get; }
        /// <summary>Gets the maximum 10-minute mean surface wind speed near the cyclone centre (<c>iwxxm:maximumSurfaceWindSpeed</c>) in the unit reported (<c>m/s</c> or knots), or <c>null</c> when not reported.</summary>
        public Speed? MaxSurfaceWindSpeed { get; }
        /// <summary>Gets the central pressure of the cyclone in hectopascals (<c>iwxxm:centralPressure</c>), or <c>null</c> when not reported or for a forecast position.</summary>
        public double? CentralPressureHpa { get; }
        /// <summary>Gets whether the cyclone is moving or stationary (<c>iwxxm:movement</c>), or <c>null</c> when not reported or for a forecast position.</summary>
        public TropicalCycloneMovement? Movement { get; }
        /// <summary>Gets the observed direction of movement in degrees from true north (<c>iwxxm:movementDirection</c>; only reported when the cyclone moves at least 2 km/h or 1 knot), or <c>null</c> when not reported.</summary>
        public double? MovementDirectionDegrees { get; }
        /// <summary>Gets the observed speed of movement (<c>iwxxm:movementSpeed</c>, <c>km/h</c> or knots; only reported when the cyclone moves at least 2 km/h or 1 knot), or <c>null</c> when not reported.</summary>
        public Speed? MovementSpeed { get; }
        /// <summary>Gets the change of the maximum surface wind speed at the time of observation (<c>iwxxm:intensityChange</c>), or <c>null</c> when not reported or for a forecast position.</summary>
        public TropicalCycloneIntensityChange? IntensityChange { get; }

        /// <summary>Creates a cyclone track point. The advisory factory requires the time to be in UTC and the wind speed to be non-negative.</summary>
        /// <param name="phenomenonTimeUtc">The observation or forecast time; <c>null</c> when not reported.</param>
        /// <param name="position">The position of the cyclone centre.</param>
        /// <param name="maxSurfaceWindSpeed">The maximum surface wind speed; <c>null</c> when not reported.</param>
        /// <param name="centralPressureHpa">The central pressure in hectopascals; <c>null</c> when not reported.</param>
        /// <param name="movement">Moving or stationary; <c>null</c> when not reported.</param>
        /// <param name="movementDirectionDegrees">The direction of movement in degrees from true north; <c>null</c> when not reported.</param>
        /// <param name="movementSpeed">The speed of movement; <c>null</c> when not reported.</param>
        /// <param name="intensityChange">The intensity change; <c>null</c> when not reported.</param>
        public TropicalCycloneTrackPoint(
            DateTimeOffset? phenomenonTimeUtc,
            GeoCoordinate position,
            Speed? maxSurfaceWindSpeed,
            double? centralPressureHpa = null,
            TropicalCycloneMovement? movement = null,
            double? movementDirectionDegrees = null,
            Speed? movementSpeed = null,
            TropicalCycloneIntensityChange? intensityChange = null)
        {
            PhenomenonTimeUtc = phenomenonTimeUtc;
            Position = position;
            MaxSurfaceWindSpeed = maxSurfaceWindSpeed;
            CentralPressureHpa = centralPressureHpa;
            Movement = movement;
            MovementDirectionDegrees = movementDirectionDegrees;
            MovementSpeed = movementSpeed;
            IntensityChange = intensityChange;
        }

        /// <inheritdoc />
        public bool Equals(TropicalCycloneTrackPoint? other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Nullable.Equals(PhenomenonTimeUtc, other.PhenomenonTimeUtc) &&
                   Position == other.Position &&
                   Nullable.Equals(MaxSurfaceWindSpeed, other.MaxSurfaceWindSpeed) &&
                   Nullable.Equals(CentralPressureHpa, other.CentralPressureHpa) &&
                   Movement == other.Movement &&
                   Nullable.Equals(MovementDirectionDegrees, other.MovementDirectionDegrees) &&
                   Nullable.Equals(MovementSpeed, other.MovementSpeed) &&
                   IntensityChange == other.IntensityChange;
        }

        /// <inheritdoc />
        public override bool Equals(object? obj) => obj is TropicalCycloneTrackPoint other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode()
        {
            int hash = ValueEquality.Seed;
            hash = ValueEquality.Add(hash, PhenomenonTimeUtc);
            hash = ValueEquality.Add(hash, Position);
            hash = ValueEquality.Add(hash, MaxSurfaceWindSpeed);
            hash = ValueEquality.AddDouble(hash, CentralPressureHpa);
            hash = ValueEquality.Add(hash, Movement);
            hash = ValueEquality.AddDouble(hash, MovementDirectionDegrees);
            hash = ValueEquality.Add(hash, MovementSpeed);
            return ValueEquality.Add(hash, IntensityChange);
        }

        /// <summary>Returns <c>true</c> when the two <see cref="TropicalCycloneTrackPoint"/> operands are equal by value.</summary>
        public static bool operator ==(TropicalCycloneTrackPoint? left, TropicalCycloneTrackPoint? right) => Equals(left, right);

        /// <summary>Returns <c>true</c> when the two <see cref="TropicalCycloneTrackPoint"/> operands differ by value.</summary>
        public static bool operator !=(TropicalCycloneTrackPoint? left, TropicalCycloneTrackPoint? right) => !Equals(left, right);
    }
}
