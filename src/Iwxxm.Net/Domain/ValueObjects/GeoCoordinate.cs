using System;
using System.Globalization;

namespace Iwxxm.Net.Reports
{
    /// <summary>
    /// Native geospatial Value Object representing a WGS84 (EPSG:4326) geographic coordinate.
    /// Built without external GIS library dependencies.
    /// </summary>
    /// <remarks>
    /// Latitude and longitude are quantized <b>once</b>, at construction, to <see cref="Precision"/> (7 decimal
    /// degrees, about 1.1 cm at the equator; rounding is half away from zero and negative zero is normalized).
    /// Equality then compares the stored values exactly, so <see cref="Equals(GeoCoordinate)"/> is reflexive,
    /// symmetric and transitive and always agrees with <see cref="GetHashCode"/>.
    /// </remarks>
    public readonly struct GeoCoordinate : IEquatable<GeoCoordinate>
    {
        /// <summary>Number of decimal places kept for latitude and longitude.</summary>
        public const int DecimalPlaces = 7;

        /// <summary>Quantization step, in decimal degrees, applied at construction (<c>1e-7</c>).</summary>
        public const double Precision = 1e-7;

        /// <summary>
        /// Latitude in decimal degrees [-90.0, +90.0].
        /// </summary>
        public double Lat { get; }

        /// <summary>
        /// Longitude in decimal degrees [-180.0, +180.0].
        /// </summary>
        public double Lon { get; }

        public GeoCoordinate(double lat, double lon)
        {
            if (double.IsNaN(lat) || double.IsInfinity(lat) || lat < -90.0 || lat > 90.0)
                throw new ArgumentOutOfRangeException(nameof(lat), lat, "Latitude must be between -90.0 and +90.0 degrees.");

            if (double.IsNaN(lon) || double.IsInfinity(lon) || lon < -180.0 || lon > 180.0)
                throw new ArgumentOutOfRangeException(nameof(lon), lon, "Longitude must be between -180.0 and +180.0 degrees.");

            Lat = Quantize(lat);
            Lon = Quantize(lon);
        }

        public static Result<GeoCoordinate> Create(double lat, double lon, string? location = null)
        {
            if (double.IsNaN(lat) || double.IsInfinity(lat) || lat < -90.0 || lat > 90.0)
            {
                return Result<GeoCoordinate>.Failure(
                    "GEO-INVALID-LAT",
                    $"Latitude '{lat.ToString(CultureInfo.InvariantCulture)}' is outside the valid geodetic range [-90, 90].",
                    ErrorCategory.DomainMapping,
                    location);
            }

            if (double.IsNaN(lon) || double.IsInfinity(lon) || lon < -180.0 || lon > 180.0)
            {
                return Result<GeoCoordinate>.Failure(
                    "GEO-INVALID-LON",
                    $"Longitude '{lon.ToString(CultureInfo.InvariantCulture)}' is outside the valid geodetic range [-180, 180].",
                    ErrorCategory.DomainMapping,
                    location);
            }

            return Result<GeoCoordinate>.Success(new GeoCoordinate(lat, lon));
        }

        public bool Equals(GeoCoordinate other) => Lat.Equals(other.Lat) && Lon.Equals(other.Lon);

        public override bool Equals(object? obj) => obj is GeoCoordinate other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                return (Lat.GetHashCode() * 397) ^ Lon.GetHashCode();
            }
        }

        private static double Quantize(double degrees)
        {
            double rounded = Math.Round(degrees, DecimalPlaces, MidpointRounding.AwayFromZero);
            return rounded == 0.0 ? 0.0 : rounded; // normalize negative zero so that Equals and GetHashCode agree
        }

        public static bool operator ==(GeoCoordinate left, GeoCoordinate right) => left.Equals(right);
        public static bool operator !=(GeoCoordinate left, GeoCoordinate right) => !left.Equals(right);

        public override string ToString() =>
            string.Format(CultureInfo.InvariantCulture, "({0:F4}, {1:F4})", Lat, Lon);
    }
}
