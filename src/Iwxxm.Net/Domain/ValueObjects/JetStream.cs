using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Iwxxm.Net.Reports
{
    /// <summary>
    /// Value Object representing a Jet Stream phenomenon in a SIGWX report.
    /// Its trajectory is mapped from a gml:Curve (CubicSpline or LineStringSegment).
    /// </summary>
    public sealed class JetStream : IEquatable<JetStream>
    {
        /// <summary>Gets the <c>gml:id</c> of the feature.</summary>
        public string Id { get; }
        /// <summary>Gets the vertices of the jet stream axis, read from a <c>gml:Curve</c> (cubic spline or line string segments), in order along the axis; at least two.</summary>
        public IReadOnlyList<GeoCoordinate> AxisTrajectory { get; }
        /// <summary>Level of the maximum wind as reported (value, unit and vertical reference); use
        /// <see cref="VerticalLimit.AsFlightLevel"/> for the flight-level view of STD levels.</summary>
        public VerticalLimit CoreLevel { get; }
        /// <summary>Maximum wind speed exactly as reported (<c>m/s</c> or <c>[kn_i]</c>).</summary>
        public Speed MaxWindSpeed { get; }
        /// <summary>Gets the wind symbols along the axis (<c>iwxxm:WAFSJetStreamWindSymbol</c>) with their positions and speeds, in document order; empty when none is reported.</summary>
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

        /// <summary>Creates a jet stream of a SIGWX chart, enforcing the identifier, a trajectory of at least two positions and a maximum wind of at least 80 kt (ICAO Annex 3).</summary>
        /// <param name="id">The <c>gml:id</c> of the feature; required (not blank).</param>
        /// <param name="axisCoordinates">The vertices of the jet stream axis; at least two are required.</param>
        /// <param name="coreLevel">The level of the maximum wind as reported, with its unit and vertical reference.</param>
        /// <param name="maxWindSpeed">The maximum wind speed as reported; at least 80 knots once converted.</param>
        /// <param name="corePoints">The wind symbols along the axis; <c>null</c> is treated as empty.</param>
        /// <returns>The jet stream, or a failure with code <c>DOMAIN-JETSTREAM-ID</c>, <c>DOMAIN-JETSTREAM-TRAJECTORY</c> or <c>DOMAIN-JETSTREAM-SPEED</c>.</returns>
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

        /// <inheritdoc />
        public bool Equals(JetStream? other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Id == other.Id &&
                   CoreLevel == other.CoreLevel &&
                   MaxWindSpeed == other.MaxWindSpeed &&
                   AxisTrajectory.SequenceEqual(other.AxisTrajectory) &&
                   ValueEquality.SequenceEqual(CorePoints, other.CorePoints);
        }

        /// <inheritdoc />
        public override bool Equals(object? obj) => obj is JetStream other && Equals(other);

        /// <inheritdoc />
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

        /// <summary>Returns <c>true</c> when the two <see cref="JetStream"/> operands are equal by value.</summary>
        public static bool operator ==(JetStream? left, JetStream? right) => Equals(left, right);

        /// <summary>Returns <c>true</c> when the two <see cref="JetStream"/> operands differ by value.</summary>
        public static bool operator !=(JetStream? left, JetStream? right) => !Equals(left, right);
    }
}
