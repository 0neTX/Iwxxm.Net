using System;
using System.Collections.Generic;
using Iwxxm.Net.Reports;

namespace Iwxxm.Net.Infrastructure.GeoJson.Writing
{
    /// <summary>
    /// Approximates a <see cref="GeoCircle"/> by a closed ring for GeoJSON output (RFC 7946 has no circle geometry).
    /// </summary>
    /// <remarks>
    /// <para>The ring has <see cref="SegmentCount"/> segments (<see cref="SegmentCount"/> + 1 positions, the last one closing
    /// the ring). Vertices are the destinations at the circle radius from the centre on a sphere of radius
    /// <see cref="MeanEarthRadiusMeters"/> (IUGG mean Earth radius), at bearings evenly spaced by 360 / <see cref="SegmentCount"/>
    /// degrees starting from true north. Positions are rounded to 1e-7 degrees.</para>
    /// <para>A circle that encloses a pole (e.g. the space weather <c>DAYSIDE</c>, 10100 km) cannot be a simple
    /// longitude/latitude ring: it is written as the polygon bounded by the circle from longitude -180 to +180 and by
    /// the enclosed pole. A circle that encloses both poles covers the whole globe.</para>
    /// <para>Consumers can rebuild the exact circle from the feature properties <c>center</c>, <c>radius</c>,
    /// <c>radiusUom</c>.</para>
    /// </remarks>
    internal static class GeodesicCircle
    {
        /// <summary>Number of segments of the approximating ring.</summary>
        public const int SegmentCount = 64;

        /// <summary>IUGG mean Earth radius (metres).</summary>
        public const double MeanEarthRadiusMeters = 6371008.8;

        private const double DegreesToRadians = Math.PI / 180.0;

        /// <summary>
        /// Returns the exterior ring (closed, not yet oriented) approximating the circle; <paramref name="enclosesPole"/>
        /// tells whether the ring is a polar cap already laid out from longitude -180 to +180.
        /// </summary>
        public static List<GeoJsonPosition> Densify(GeoCircle circle, out bool enclosesPole)
        {
            double angularRadius = circle.Radius.ToMeters() / MeanEarthRadiusMeters;
            double lat0 = circle.Centre.Lat * DegreesToRadians;
            double lon0 = circle.Centre.Lon * DegreesToRadians;

            double distanceToNorthPole = (90.0 - circle.Centre.Lat) * DegreesToRadians;
            double distanceToSouthPole = (90.0 + circle.Centre.Lat) * DegreesToRadians;
            bool enclosesNorthPole = distanceToNorthPole < angularRadius;
            bool enclosesSouthPole = distanceToSouthPole < angularRadius;

            enclosesPole = enclosesNorthPole || enclosesSouthPole;
            if (enclosesNorthPole && enclosesSouthPole)
            {
                return new List<GeoJsonPosition>
                {
                    new GeoJsonPosition(-180.0, -90.0),
                    new GeoJsonPosition(180.0, -90.0),
                    new GeoJsonPosition(180.0, 90.0),
                    new GeoJsonPosition(-180.0, 90.0),
                    new GeoJsonPosition(-180.0, -90.0)
                };
            }

            var lons = new double[SegmentCount];
            var lats = new double[SegmentCount];
            for (int k = 0; k < SegmentCount; k++)
            {
                // Decreasing bearings: north, then west, south and east (counterclockwise on a north-up map).
                double bearing = -k * (2.0 * Math.PI / SegmentCount);
                double lat = Math.Asin((Math.Sin(lat0) * Math.Cos(angularRadius)) +
                                       (Math.Cos(lat0) * Math.Sin(angularRadius) * Math.Cos(bearing)));
                double lon = lon0 + Math.Atan2(
                    Math.Sin(bearing) * Math.Sin(angularRadius) * Math.Cos(lat0),
                    Math.Cos(angularRadius) - (Math.Sin(lat0) * Math.Sin(lat)));
                lats[k] = lat / DegreesToRadians;
                lons[k] = NormalizeLongitude(lon / DegreesToRadians);
            }

            if (enclosesNorthPole || enclosesSouthPole)
            {
                return PolarCap(lons, lats, enclosesNorthPole ? 90.0 : -90.0);
            }

            var ring = new List<GeoJsonPosition>(SegmentCount + 1);
            for (int k = 0; k < SegmentCount; k++)
            {
                ring.Add(GeoJsonPosition.Computed(lons[k], ClampLatitude(lats[k])));
            }

            ring.Add(ring[0]);
            return ring;
        }

        /// <summary>Builds the ring of a circle that encloses one pole (see <see cref="AntimeridianSplitter.PolarCap"/>).</summary>
        private static List<GeoJsonPosition> PolarCap(double[] lons, double[] lats, double poleLat)
        {
            int n = lons.Length;
            var unwrapped = new List<double>(n + 1) { lons[0] };
            var latitudes = new List<double>(n + 1) { lats[0] };
            for (int k = 1; k <= n; k++)
            {
                int index = k % n;
                unwrapped.Add(unwrapped[k - 1] + WrapDelta(lons[index] - lons[k - 1]));
                latitudes.Add(ClampLatitude(lats[index]));
            }

            return AntimeridianSplitter.PolarCap(unwrapped, latitudes, poleLat);
        }

        private static double WrapDelta(double delta)
        {
            while (delta > 180.0) delta -= 360.0;
            while (delta <= -180.0) delta += 360.0;
            return delta;
        }

        private static double NormalizeLongitude(double lon)
        {
            while (lon > 180.0) lon -= 360.0;
            while (lon < -180.0) lon += 360.0;
            return lon;
        }

        private static double ClampLatitude(double lat) => Math.Max(-90.0, Math.Min(90.0, lat));
    }
}
