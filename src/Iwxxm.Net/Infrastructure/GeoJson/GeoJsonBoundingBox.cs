using System.Collections.Generic;
using System.Linq;
using Iwxxm.Net.Infrastructure.GeoJson.Writing;

namespace Iwxxm.Net.Infrastructure.GeoJson
{
    /// <summary>
    /// Computes the RFC 7946 <c>bbox</c> (<c>[west, south, east, north]</c>) of a set of features. When a geometry crosses
    /// the antimeridian (RFC 7946 §5.2), the longitude extent is the smallest arc that contains every position, so
    /// <c>west</c> may be greater than <c>east</c>; otherwise it is the plain minimum / maximum. A wrapping box is only
    /// written when that arc is at most <see cref="MaxWrappingWidth"/> degrees wide: features that are spread over the
    /// whole longitude range (a global chart, a polar cap) have no meaningful smaller box, and a <c>west &gt; east</c> box
    /// that spans almost 360 degrees would look empty to a client that expects <c>west &lt;= east</c>, so the plain extent
    /// (<c>-180</c> to <c>180</c> for a chart that reaches both sides) is written instead. The latitude extent is always the
    /// plain minimum / maximum of the positions, so a pole is only reported when a position reaches it.
    /// </summary>
    internal sealed class GeoJsonBoundingBox
    {
        /// <summary>Widest longitude arc (degrees) that is still written as a wrapping box (<c>west &gt; east</c>).</summary>
        public const double MaxWrappingWidth = 180.0;

        public bool HasCoordinates { get; private set; }
        public double MinLon { get; private set; } = double.MaxValue;
        public double MinLat { get; private set; } = double.MaxValue;
        public double MaxLon { get; private set; } = double.MinValue;
        public double MaxLat { get; private set; } = double.MinValue;

        public static GeoJsonBoundingBox Of(IReadOnlyList<GeoJsonFeature> features)
        {
            var bbox = new GeoJsonBoundingBox();
            var longitudes = new List<double>();
            bool crossesAntimeridian = false;
            for (int i = 0; i < features.Count; i++)
            {
                GeoJsonGeometry? geometry = features[i].Geometry;
                if (geometry == null)
                    continue;

                crossesAntimeridian |= geometry.CrossesAntimeridian;
                foreach (GeoJsonPosition position in geometry.Positions())
                {
                    bbox.Include(position);
                    longitudes.Add(position.Lon);
                }
            }

            if (crossesAntimeridian)
                bbox.UseSmallestLongitudeArc(longitudes);

            return bbox;
        }

        private void Include(GeoJsonPosition coord)
        {
            HasCoordinates = true;
            if (coord.Lon < MinLon) MinLon = coord.Lon;
            if (coord.Lon > MaxLon) MaxLon = coord.Lon;
            if (coord.Lat < MinLat) MinLat = coord.Lat;
            if (coord.Lat > MaxLat) MaxLat = coord.Lat;
        }

        /// <summary>
        /// Replaces the longitude extent by the complement of the largest empty longitude gap when that is a narrow wrapping box (see the class remarks). -180 and +180 are the same
        /// meridian, so split pieces touching the antimeridian from both sides join across it.
        /// </summary>
        private void UseSmallestLongitudeArc(List<double> longitudes)
        {
            List<double> sorted = longitudes.Select(l => l == -180.0 ? 180.0 : l).Distinct().OrderBy(l => l).ToList();
            if (sorted.Count < 2)
                return;

            double largestGap = sorted[0] + 360.0 - sorted[sorted.Count - 1];
            double west = sorted[0];
            double east = sorted[sorted.Count - 1];
            for (int i = 1; i < sorted.Count; i++)
            {
                double gap = sorted[i] - sorted[i - 1];
                if (gap > largestGap)
                {
                    largestGap = gap;
                    west = sorted[i];
                    east = sorted[i - 1];
                }
            }

            // The arc outside the largest gap is 360 - gap wide; the seam gap (west <= east) is the plain extent.
            if (west > east && 360.0 - largestGap <= MaxWrappingWidth)
            {
                MinLon = west;
                MaxLon = east;
            }
        }
    }
}
