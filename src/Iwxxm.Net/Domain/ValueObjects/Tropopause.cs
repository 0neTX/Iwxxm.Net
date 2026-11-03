using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Iwxxm.Net.Reports
{
    /// <summary>
    /// Value Object representing Tropopause height observations or contours in a SIGWX report. A tropopause is either a
    /// set of isolated spot heights (each a point with its own level) or a contour (a polygon that carries one level for
    /// the whole contour); the level of a contour is never repeated per vertex.
    /// </summary>
    public sealed class Tropopause : IEquatable<Tropopause>
    {
        public string Id { get; }
        /// <summary>Level type reported by the compact format (e.g. <c>SPOT</c>); <c>null</c> when the source does not
        /// report one (WAFSSignificantWeatherForecast). Never invented.</summary>
        public string? LevelType { get; }
        /// <summary>Isolated spot heights (points with their own level); empty for a contour.</summary>
        public IReadOnlyList<TropopauseSpotHeight> SpotHeights { get; }
        public GeoPolygon? ContourPolygon { get; }
        /// <summary>Level of the whole contour as reported (value, unit and vertical reference); set exactly when <see cref="ContourPolygon"/> is.</summary>
        public VerticalLimit? ContourLevel { get; }

        private Tropopause(
            string id,
            string? levelType,
            IReadOnlyList<TropopauseSpotHeight> spotHeights,
            GeoPolygon? contourPolygon,
            VerticalLimit? contourLevel)
        {
            Id = id;
            LevelType = levelType;
            SpotHeights = spotHeights;
            ContourPolygon = contourPolygon;
            ContourLevel = contourLevel;
        }

        public static Result<Tropopause> Create(
            string id,
            string? levelType,
            IEnumerable<TropopauseSpotHeight> spotHeights,
            GeoPolygon? contourPolygon = null,
            VerticalLimit? contourLevel = null)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return Result<Tropopause>.Failure(
                    "DOMAIN-TROPOPAUSE-ID",
                    "The Tropopause gml:id identifier is required.",
                    ErrorCategory.DomainMapping);
            }

            var list = spotHeights?.ToList() ?? new List<TropopauseSpotHeight>();
            if (list.Count == 0 && contourPolygon == null)
            {
                return Result<Tropopause>.Failure(
                    "DOMAIN-TROPOPAUSE-EMPTY",
                    $"Tropopause feature '{id}' must contain at least one spot height or a contour.",
                    ErrorCategory.DomainMapping,
                    id);
            }

            if (contourPolygon != null && !contourLevel.HasValue)
            {
                return Result<Tropopause>.Failure(
                    "DOMAIN-TROPOPAUSE-CONTOUR-LEVEL",
                    $"Tropopause contour '{id}' must report its level.",
                    ErrorCategory.DomainMapping,
                    id);
            }

            string? normalizedType = string.IsNullOrWhiteSpace(levelType) ? null : levelType!.Trim();
            return Result<Tropopause>.Success(
                new Tropopause(id, normalizedType, new ReadOnlyCollection<TropopauseSpotHeight>(list), contourPolygon, contourPolygon == null ? null : contourLevel));
        }

        public bool Equals(Tropopause? other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Id == other.Id &&
                   LevelType == other.LevelType &&
                   SpotHeights.SequenceEqual(other.SpotHeights) &&
                   Equals(ContourPolygon, other.ContourPolygon) &&
                   Nullable.Equals(ContourLevel, other.ContourLevel);
        }

        public override bool Equals(object? obj) => obj is Tropopause other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                return (Id.GetHashCode() * 397) ^ (LevelType == null ? 0 : LevelType.GetHashCode());
            }
        }

        public static bool operator ==(Tropopause? left, Tropopause? right) => Equals(left, right);

        public static bool operator !=(Tropopause? left, Tropopause? right) => !Equals(left, right);
    }
}
