using System;

namespace Iwxxm.Net.Reports
{
    /// <summary>
    /// Immutable Value Object representing an observed or forecast tropical cyclone position and intensity.
    /// </summary>
    public sealed class TropicalCycloneTrackPoint : IEquatable<TropicalCycloneTrackPoint>
    {
        public DateTimeOffset? PhenomenonTimeUtc { get; }
        public GeoCoordinate Position { get; }
        public Speed? MaxSurfaceWindSpeed { get; }
        public double? CentralPressureHpa { get; }

        public TropicalCycloneTrackPoint(
            DateTimeOffset? phenomenonTimeUtc,
            GeoCoordinate position,
            Speed? maxSurfaceWindSpeed,
            double? centralPressureHpa = null)
        {
            PhenomenonTimeUtc = phenomenonTimeUtc;
            Position = position;
            MaxSurfaceWindSpeed = maxSurfaceWindSpeed;
            CentralPressureHpa = centralPressureHpa;
        }

        public bool Equals(TropicalCycloneTrackPoint? other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Nullable.Equals(PhenomenonTimeUtc, other.PhenomenonTimeUtc) &&
                   Position == other.Position &&
                   Nullable.Equals(MaxSurfaceWindSpeed, other.MaxSurfaceWindSpeed) &&
                   Nullable.Equals(CentralPressureHpa, other.CentralPressureHpa);
        }

        public override bool Equals(object? obj) => obj is TropicalCycloneTrackPoint other && Equals(other);

        public override int GetHashCode()
        {
            int hash = ValueEquality.Seed;
            hash = ValueEquality.Add(hash, PhenomenonTimeUtc);
            hash = ValueEquality.Add(hash, Position);
            hash = ValueEquality.Add(hash, MaxSurfaceWindSpeed);
            hash = ValueEquality.AddDouble(hash, CentralPressureHpa);
            return hash;
        }

        public static bool operator ==(TropicalCycloneTrackPoint? left, TropicalCycloneTrackPoint? right) => Equals(left, right);

        public static bool operator !=(TropicalCycloneTrackPoint? left, TropicalCycloneTrackPoint? right) => !Equals(left, right);
    }
}
