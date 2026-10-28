using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Iwxxm.Net.Domain.Common;

namespace Iwxxm.Net.Domain.ValueObjects
{
    /// <summary>
    /// Represents a maximum wind symbol or isotach core point along a Jet Stream axis
    /// (e.g., iwxxm:WAFSJetStreamWindSymbol or iwxxm:corePoint).
    /// </summary>
    public sealed class JetStreamCorePoint
    {
        public GeoCoordinate Location { get; }
        public FlightLevel FlightLevel { get; }
        public double WindSpeedKnots { get; }
        public FlightLevel? LowerIsotachLimit { get; }
        public FlightLevel? UpperIsotachLimit { get; }

        public JetStreamCorePoint(
            GeoCoordinate location,
            FlightLevel flightLevel,
            double windSpeedKnots,
            FlightLevel? lowerIsotachLimit = null,
            FlightLevel? upperIsotachLimit = null)
        {
            Location = location;
            FlightLevel = flightLevel;
            WindSpeedKnots = windSpeedKnots;
            LowerIsotachLimit = lowerIsotachLimit;
            UpperIsotachLimit = upperIsotachLimit;
        }
    }

    /// <summary>
    /// Value Object representing a Jet Stream phenomenon in a SIGWX report.
    /// Its trajectory is mapped from a gml:Curve (CubicSpline or LineStringSegment).
    /// </summary>
    public sealed class JetStream : IEquatable<JetStream>
    {
        public string Id { get; }
        public IReadOnlyList<GeoCoordinate> AxisTrajectory { get; }
        public FlightLevel CoreFlightLevel { get; }
        public double MaxWindSpeedKnots { get; }
        public IReadOnlyList<JetStreamCorePoint> CorePoints { get; }

        private JetStream(
            string id,
            IReadOnlyList<GeoCoordinate> axisTrajectory,
            FlightLevel coreFlightLevel,
            double maxWindSpeedKnots,
            IReadOnlyList<JetStreamCorePoint> corePoints)
        {
            Id = id;
            AxisTrajectory = axisTrajectory;
            CoreFlightLevel = coreFlightLevel;
            MaxWindSpeedKnots = maxWindSpeedKnots;
            CorePoints = corePoints;
        }

        public static Result<JetStream> Create(
            string id,
            IEnumerable<GeoCoordinate> axisCoordinates,
            FlightLevel coreFlightLevel,
            double maxWindSpeedKnots,
            IEnumerable<JetStreamCorePoint>? corePoints = null)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return Result<JetStream>.Failure(
                    "DOMAIN-JETSTREAM-ID",
                    "The JetStream gml:id identifier is required.",
                    ErrorCategory.DomainMapping);
            }

            var trajectory = axisCoordinates?.ToList() ?? new List<GeoCoordinate>();
            if (trajectory.Count < 2)
            {
                return Result<JetStream>.Failure(
                    "DOMAIN-JETSTREAM-TRAJECTORY",
                    $"The axis (gml:Curve) of JetStream '{id}' must contain at least 2 geographic coordinates.",
                    ErrorCategory.DomainMapping,
                    id);
            }

            if (maxWindSpeedKnots < 80.0)
            {
                return Result<JetStream>.Failure(
                    "DOMAIN-JETSTREAM-SPEED",
                    $"The maximum wind speed of JetStream '{id}' ({maxWindSpeedKnots:F1} kt) must be >= 80 kt per ICAO Annex 3.",
                    ErrorCategory.DomainMapping,
                    id);
            }

            var points = corePoints?.ToList() ?? new List<JetStreamCorePoint>(0);

            return Result<JetStream>.Success(new JetStream(
                id,
                new ReadOnlyCollection<GeoCoordinate>(trajectory),
                coreFlightLevel,
                maxWindSpeedKnots,
                new ReadOnlyCollection<JetStreamCorePoint>(points)));
        }

        public bool Equals(JetStream? other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Id == other.Id &&
                   CoreFlightLevel == other.CoreFlightLevel &&
                   Math.Abs(MaxWindSpeedKnots - other.MaxWindSpeedKnots) < 1e-4 &&
                   AxisTrajectory.SequenceEqual(other.AxisTrajectory);
        }

        public override bool Equals(object? obj) => obj is JetStream other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Id.GetHashCode();
                hash = (hash * 397) ^ CoreFlightLevel.GetHashCode();
                hash = (hash * 397) ^ MaxWindSpeedKnots.GetHashCode();
                return hash;
            }
        }
    }
}
