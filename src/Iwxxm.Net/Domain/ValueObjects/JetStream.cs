using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Iwxxm.Net.Reports
{
    /// <summary>
    /// Represents a maximum wind symbol or isotach core point along a Jet Stream axis
    /// (e.g., iwxxm:WAFSJetStreamWindSymbol or iwxxm:corePoint).
    /// </summary>
    public sealed class JetStreamCorePoint
    {
        public GeoCoordinate Location { get; }
        /// <summary>Level of the core point as reported (value, unit and vertical reference; e.g. <c>15240 M MSL</c>).</summary>
        public VerticalLimit Level { get; }
        /// <summary>Wind speed exactly as reported (<c>m/s</c> or <c>[kn_i]</c>).</summary>
        public Speed WindSpeed { get; }
        public VerticalLimit? LowerIsotachLimit { get; }
        public VerticalLimit? UpperIsotachLimit { get; }

        public JetStreamCorePoint(
            GeoCoordinate location,
            VerticalLimit level,
            Speed windSpeed,
            VerticalLimit? lowerIsotachLimit = null,
            VerticalLimit? upperIsotachLimit = null)
        {
            Location = location;
            Level = level;
            WindSpeed = windSpeed;
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
        /// <summary>Level of the maximum wind as reported (value, unit and vertical reference); use
        /// <see cref="VerticalLimit.AsFlightLevel"/> for the flight-level view of STD levels.</summary>
        public VerticalLimit CoreLevel { get; }
        /// <summary>Maximum wind speed exactly as reported (<c>m/s</c> or <c>[kn_i]</c>).</summary>
        public Speed MaxWindSpeed { get; }
        public IReadOnlyList<JetStreamCorePoint> CorePoints { get; }

        private JetStream(
            string id,
            IReadOnlyList<GeoCoordinate> axisTrajectory,
            VerticalLimit coreLevel,
            Speed maxWindSpeed,
            IReadOnlyList<JetStreamCorePoint> corePoints)
        {
            Id = id;
            AxisTrajectory = axisTrajectory;
            CoreLevel = coreLevel;
            MaxWindSpeed = maxWindSpeed;
            CorePoints = corePoints;
        }

        public static Result<JetStream> Create(
            string id,
            IEnumerable<GeoCoordinate> axisCoordinates,
            VerticalLimit coreLevel,
            Speed maxWindSpeed,
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

            if (maxWindSpeed.ToKnots() < 80.0)
            {
                return Result<JetStream>.Failure(
                    "DOMAIN-JETSTREAM-SPEED",
                    $"The maximum wind speed of JetStream '{id}' ({maxWindSpeed}, {maxWindSpeed.ToKnots():F1} kt) must be >= 80 kt per ICAO Annex 3.",
                    ErrorCategory.DomainMapping,
                    id);
            }

            var points = corePoints?.ToList() ?? new List<JetStreamCorePoint>(0);

            return Result<JetStream>.Success(new JetStream(
                id,
                new ReadOnlyCollection<GeoCoordinate>(trajectory),
                coreLevel,
                maxWindSpeed,
                new ReadOnlyCollection<JetStreamCorePoint>(points)));
        }

        public bool Equals(JetStream? other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Id == other.Id &&
                   CoreLevel == other.CoreLevel &&
                   MaxWindSpeed == other.MaxWindSpeed &&
                   AxisTrajectory.SequenceEqual(other.AxisTrajectory);
        }

        public override bool Equals(object? obj) => obj is JetStream other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Id.GetHashCode();
                hash = (hash * 397) ^ CoreLevel.GetHashCode();
                hash = (hash * 397) ^ MaxWindSpeed.GetHashCode();
                return hash;
            }
        }
    }
}
