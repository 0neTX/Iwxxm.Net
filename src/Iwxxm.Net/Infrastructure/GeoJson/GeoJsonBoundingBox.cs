using System.Collections.Generic;
using Iwxxm.Net.Reports;

namespace Iwxxm.Net.Infrastructure.GeoJson
{
    /// <summary>Computes the RFC 7946 <c>bbox</c> (<c>[minLon, minLat, maxLon, maxLat]</c>) of a set of features.</summary>
    internal sealed class GeoJsonBoundingBox
    {
        public bool HasCoordinates { get; private set; }
        public double MinLon { get; private set; } = double.MaxValue;
        public double MinLat { get; private set; } = double.MaxValue;
        public double MaxLon { get; private set; } = double.MinValue;
        public double MaxLat { get; private set; } = double.MinValue;

        public static GeoJsonBoundingBox Of(IReadOnlyList<GeoJsonFeature> features)
        {
            var bbox = new GeoJsonBoundingBox();
            for (int i = 0; i < features.Count; i++)
            {
                bbox.Include(features[i]);
            }

            return bbox;
        }

        private void Include(GeoJsonFeature feature)
        {
            if (feature.PointCoordinate.HasValue)
            {
                Include(feature.PointCoordinate.Value);
            }

            if (feature.LineCoordinates != null)
            {
                IncludeRange(feature.LineCoordinates);
            }

            if (feature.Polygon != null)
            {
                IncludeRange(feature.Polygon.ExteriorRing);
                foreach (var hole in feature.Polygon.InteriorRings)
                {
                    IncludeRange(hole);
                }
            }
        }

        private void Include(GeoCoordinate coord)
        {
            HasCoordinates = true;
            if (coord.Lon < MinLon) MinLon = coord.Lon;
            if (coord.Lon > MaxLon) MaxLon = coord.Lon;
            if (coord.Lat < MinLat) MinLat = coord.Lat;
            if (coord.Lat > MaxLat) MaxLat = coord.Lat;
        }

        private void IncludeRange(IEnumerable<GeoCoordinate> coords)
        {
            foreach (var c in coords)
            {
                Include(c);
            }
        }
    }
}
