using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Iwxxm.Net.Reports
{
    /// <summary>
    /// Discrete Tropopause height measurement point (Spot Height, High, Low, or Contour vertex).
    /// </summary>
    public sealed class TropopauseSpotHeight : IEquatable<TropopauseSpotHeight>
    {
        public GeoCoordinate Location { get; }
        public FlightLevel FlightLevel { get; }

        public TropopauseSpotHeight(GeoCoordinate location, FlightLevel flightLevel)
        {
            Location = location;
            FlightLevel = flightLevel;
        }

        public bool Equals(TropopauseSpotHeight? other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Location == other.Location && FlightLevel == other.FlightLevel;
        }

        public override bool Equals(object? obj) => obj is TropopauseSpotHeight other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                return (Location.GetHashCode() * 397) ^ FlightLevel.GetHashCode();
            }
        }
    }

    /// <summary>
    /// Value Object representing Tropopause height observations or contours in a SIGWX report.
    /// </summary>
    public sealed class Tropopause : IEquatable<Tropopause>
    {
        public string Id { get; }
        public string LevelType { get; }
        public IReadOnlyList<TropopauseSpotHeight> SpotHeights { get; }
        public GeoPolygon? ContourPolygon { get; }

        private Tropopause(
            string id,
            string levelType,
            IReadOnlyList<TropopauseSpotHeight> spotHeights,
            GeoPolygon? contourPolygon)
        {
            Id = id;
            LevelType = levelType;
            SpotHeights = spotHeights;
            ContourPolygon = contourPolygon;
        }

        public static Result<Tropopause> Create(
            string id,
            string? levelType,
            IEnumerable<TropopauseSpotHeight> spotHeights,
            GeoPolygon? contourPolygon = null)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return Result<Tropopause>.Failure(
                    "DOMAIN-TROPOPAUSE-ID",
                    "The Tropopause gml:id identifier is required.",
                    ErrorCategory.DomainMapping);
            }

            var list = spotHeights?.ToList() ?? new List<TropopauseSpotHeight>();
            if (list.Count == 0)
            {
                return Result<Tropopause>.Failure(
                    "DOMAIN-TROPOPAUSE-EMPTY",
                    $"Tropopause feature '{id}' must contain at least one height point.",
                    ErrorCategory.DomainMapping,
                    id);
            }

            string normalizedType = string.IsNullOrWhiteSpace(levelType) ? "SPOT" : levelType!;
            return Result<Tropopause>.Success(
                new Tropopause(id, normalizedType, new ReadOnlyCollection<TropopauseSpotHeight>(list), contourPolygon));
        }

        public bool Equals(Tropopause? other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Id == other.Id &&
                   LevelType == other.LevelType &&
                   SpotHeights.SequenceEqual(other.SpotHeights) &&
                   Equals(ContourPolygon, other.ContourPolygon);
        }

        public override bool Equals(object? obj) => obj is Tropopause other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                return (Id.GetHashCode() * 397) ^ LevelType.GetHashCode();
            }
        }
    }
}
