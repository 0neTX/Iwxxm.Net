using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Iwxxm.Net.Domain.Common;

namespace Iwxxm.Net.Domain.ValueObjects
{
    /// <summary>
    /// Native geospatial Value Object representing a closed geographic polygon
    /// (mapped from gml:Polygon, gml:LinearRing, or gml:Ring / gml:PolygonPatch).
    /// Enforces a minimum of 4 vertices with a closed exterior ring.
    /// </summary>
    public sealed class GeoPolygon : IEquatable<GeoPolygon>
    {
        public IReadOnlyList<GeoCoordinate> ExteriorRing { get; }

        private GeoPolygon(IReadOnlyList<GeoCoordinate> exteriorRing)
        {
            ExteriorRing = exteriorRing;
        }

        public static Result<GeoPolygon> Create(IEnumerable<GeoCoordinate> coordinates, string? location = null)
        {
            if (coordinates == null)
                throw new ArgumentNullException(nameof(coordinates));

            var vertices = coordinates.ToList();
            if (vertices.Count < 3)
            {
                return Result<GeoPolygon>.Failure(
                    "GEO-POLYGON-MIN-VERTICES",
                    $"A GML polygon requires at least 3 distinct vertices (received {vertices.Count}).",
                    ErrorCategory.DomainMapping,
                    location);
            }

            // Ensure the exterior ring is closed according to OGC GML 3.2 LinearRing/Ring semantics
            if (vertices[0] != vertices[vertices.Count - 1])
            {
                vertices.Add(vertices[0]);
            }

            if (vertices.Count < 4)
            {
                return Result<GeoPolygon>.Failure(
                    "GEO-POLYGON-DEGENERATE",
                    "The polygon exterior ring must contain at least 4 positions (including the closing vertex).",
                    ErrorCategory.DomainMapping,
                    location);
            }

            return Result<GeoPolygon>.Success(new GeoPolygon(new ReadOnlyCollection<GeoCoordinate>(vertices)));
        }

        public bool Equals(GeoPolygon? other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return ExteriorRing.SequenceEqual(other.ExteriorRing);
        }

        public override bool Equals(object? obj) => obj is GeoPolygon other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 19;
                foreach (var pt in ExteriorRing)
                {
                    hash = (hash * 31) + pt.GetHashCode();
                }
                return hash;
            }
        }
    }
}
