using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Iwxxm.Net.Reports
{
    /// <summary>
    /// Native geospatial Value Object representing a closed geographic polygon
    /// (mapped from gml:Polygon, gml:LinearRing, or gml:Ring / gml:PolygonPatch).
    /// Enforces a minimum of 4 vertices with a closed exterior ring.
    /// Equality is structural: the exterior ring and every interior ring are compared vertex by vertex, in order
    /// (vertices use the exact, quantized equality of <see cref="GeoCoordinate"/>), and all of them take part in the hash code.
    /// </summary>
    public sealed class GeoPolygon : GeoSurface, IEquatable<GeoPolygon>
    {
        public IReadOnlyList<GeoCoordinate> ExteriorRing { get; }

        /// <summary>
        /// Optional interior rings (holes) mapped from <c>&lt;gml:interior&gt;</c> elements (e.g., in QVACI ash cloud contours).
        /// </summary>
        public IReadOnlyList<IReadOnlyList<GeoCoordinate>> InteriorRings { get; }

        private GeoPolygon(
            IReadOnlyList<GeoCoordinate> exteriorRing,
            IReadOnlyList<IReadOnlyList<GeoCoordinate>> interiorRings)
        {
            ExteriorRing = exteriorRing;
            InteriorRings = interiorRings;
        }

        public static Result<GeoPolygon> Create(IEnumerable<GeoCoordinate> coordinates, string? location = null)
        {
            return Create(coordinates, null, location);
        }

        public static Result<GeoPolygon> Create(
            IEnumerable<GeoCoordinate> coordinates,
            IEnumerable<IEnumerable<GeoCoordinate>>? interiorRings,
            string? location = null)
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

            var holes = new List<IReadOnlyList<GeoCoordinate>>();
            if (interiorRings != null)
            {
                // A hole is a closed ring of at least 4 positions (RFC 7946 §3.1.6, GML LinearRing); a degenerate hole is an
                // error instead of being dropped or written as an invalid ring (AUD-L9).
                foreach (var hole in interiorRings)
                {
                    if (hole == null) continue;
                    var holeVertices = hole.ToList();
                    if (holeVertices.Count > 0 && holeVertices[0] != holeVertices[holeVertices.Count - 1])
                    {
                        holeVertices.Add(holeVertices[0]);
                    }

                    if (holeVertices.Count < 4)
                    {
                        return Result<GeoPolygon>.Failure(
                            "GEO-POLYGON-HOLE-DEGENERATE",
                            $"A polygon interior ring must contain at least 4 positions including the closing vertex (received {holeVertices.Count}).",
                            ErrorCategory.DomainMapping,
                            location);
                    }

                    holes.Add(new ReadOnlyCollection<GeoCoordinate>(holeVertices));
                }
            }

            return Result<GeoPolygon>.Success(new GeoPolygon(
                new ReadOnlyCollection<GeoCoordinate>(vertices),
                new ReadOnlyCollection<IReadOnlyList<GeoCoordinate>>(holes)));
        }

        /// <inheritdoc />
        public override TResult Match<TResult>(Func<GeoPolygon, TResult> polygon, Func<GeoCircle, TResult> circle) =>
            (polygon ?? throw new ArgumentNullException(nameof(polygon)))(this);

        public bool Equals(GeoPolygon? other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            if (!ExteriorRing.SequenceEqual(other.ExteriorRing) || InteriorRings.Count != other.InteriorRings.Count)
                return false;

            for (int i = 0; i < InteriorRings.Count; i++)
            {
                if (!InteriorRings[i].SequenceEqual(other.InteriorRings[i]))
                    return false;
            }

            return true;
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

                foreach (var ring in InteriorRings)
                {
                    hash = (hash * 31) + ring.Count;
                    foreach (var pt in ring)
                    {
                        hash = (hash * 31) + pt.GetHashCode();
                    }
                }

                return hash;
            }
        }

        public static bool operator ==(GeoPolygon? left, GeoPolygon? right) => Equals(left, right);

        public static bool operator !=(GeoPolygon? left, GeoPolygon? right) => !Equals(left, right);
    }
}
