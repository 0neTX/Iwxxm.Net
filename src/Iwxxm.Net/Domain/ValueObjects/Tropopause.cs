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
        /// <summary>Gets the <c>gml:id</c> of the feature.</summary>
        public string Id { get; }
        /// <summary>Level type reported by the compact format (e.g. <c>SPOT</c>); <c>null</c> when the source does not
        /// report one (WAFSSignificantWeatherForecast). Never invented.</summary>
        public string? LevelType { get; }
        /// <summary>Isolated spot heights (points with their own level); empty for a contour.</summary>
        public IReadOnlyList<TropopauseSpotHeight> SpotHeights { get; }
        /// <summary>Gets the contour of a tropopause height line as a closed polygon, or <c>null</c> when the feature is a set of spot heights.</summary>
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

        /// <summary>Creates a tropopause feature as either spot heights or a contour, enforcing that one of them is present and that a contour reports its level.</summary>
        /// <param name="id">The <c>gml:id</c> of the feature; required (not blank).</param>
        /// <param name="levelType">The level type reported by the compact format (for example <c>SPOT</c>); a blank value is stored as <c>null</c>.</param>
        /// <param name="spotHeights">The isolated spot heights; <c>null</c> is treated as empty. Without a contour at least one is required.</param>
        /// <param name="contourPolygon">The contour polygon; <c>null</c> for a set of spot heights.</param>
        /// <param name="contourLevel">The level of the whole contour; required when <paramref name="contourPolygon"/> is given and ignored otherwise.</param>
        /// <returns>The tropopause, or a failure with code <c>DOMAIN-TROPOPAUSE-ID</c>, <c>DOMAIN-TROPOPAUSE-EMPTY</c> or <c>DOMAIN-TROPOPAUSE-CONTOUR-LEVEL</c>.</returns>
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

        /// <inheritdoc />
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

        /// <inheritdoc />
        public override bool Equals(object? obj) => obj is Tropopause other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                return (Id.GetHashCode() * 397) ^ (LevelType == null ? 0 : LevelType.GetHashCode());
            }
        }

        /// <summary>Returns <c>true</c> when the two <see cref="Tropopause"/> operands are equal by value.</summary>
        public static bool operator ==(Tropopause? left, Tropopause? right) => Equals(left, right);

        /// <summary>Returns <c>true</c> when the two <see cref="Tropopause"/> operands differ by value.</summary>
        public static bool operator !=(Tropopause? left, Tropopause? right) => !Equals(left, right);
    }
}
