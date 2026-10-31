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
        public DateTimeOffset? PhenomenonTimeUtc { get; }
        public GeoCoordinate Position { get; }
        public Speed? MaxSurfaceWindSpeed { get; }
        public double? CentralPressureHpa { get; }
        public TropicalCycloneMovement? Movement { get; }
        public double? MovementDirectionDegrees { get; }
        public Speed? MovementSpeed { get; }
        public TropicalCycloneIntensityChange? IntensityChange { get; }

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

        public override bool Equals(object? obj) => obj is TropicalCycloneTrackPoint other && Equals(other);

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

        public static bool operator ==(TropicalCycloneTrackPoint? left, TropicalCycloneTrackPoint? right) => Equals(left, right);

        public static bool operator !=(TropicalCycloneTrackPoint? left, TropicalCycloneTrackPoint? right) => !Equals(left, right);
    }
}
