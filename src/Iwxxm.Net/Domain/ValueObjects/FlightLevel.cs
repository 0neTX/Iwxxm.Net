using System;
using System.Globalization;

namespace Iwxxm.Net.Reports
{
    /// <summary>
    /// Value Object representing an aeronautical Flight Level (FL) in hundreds of feet.
    /// Supports direct FL values as well as standard ICAO/WMO conversion from meters ("M", "m") and feet ("[ft_i]", "ft").
    /// </summary>
    public readonly struct FlightLevel : IEquatable<FlightLevel>, IComparable<FlightLevel>
    {
        private const double MetersPerFoot = 0.3048;

        public int Value { get; }
        public int AltitudeInFeet => Value * 100;

        public FlightLevel(int value)
        {
            if (value < 0 || value > 1000)
                throw new ArgumentOutOfRangeException(nameof(value), value, "Flight Level must be between FL000 and FL1000.");

            Value = value;
        }

        public static Result<FlightLevel> Create(double rawValue, string? uom, string? location = null)
        {
            if (string.IsNullOrWhiteSpace(uom))
            {
                return Result<FlightLevel>.Failure(
                    "DOMAIN-FL-MISSING-UOM",
                    "Unit of measure (uom) is required for vertical level measurements.",
                    ErrorCategory.DomainMapping,
                    location);
            }

            double flightLevelValue;
            if (string.Equals(uom, "FL", StringComparison.OrdinalIgnoreCase))
            {
                flightLevelValue = rawValue;
            }
            else if (string.Equals(uom, "M", StringComparison.OrdinalIgnoreCase))
            {
                // Convert meters (MSL/STD in WAFS SIGWX) to hundreds of feet (Flight Level)
                flightLevelValue = (rawValue / MetersPerFoot) / 100.0;
            }
            else if (string.Equals(uom, "[ft_i]", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(uom, "ft", StringComparison.OrdinalIgnoreCase))
            {
                flightLevelValue = rawValue / 100.0;
            }
            else
            {
                return Result<FlightLevel>.Failure(
                    "DOMAIN-FL-INVALID-UOM",
                    $"Unsupported vertical unit of measure '{uom}'. Expected 'FL', 'M', or '[ft_i]'.",
                    ErrorCategory.DomainMapping,
                    location);
            }

            int rounded = (int)Math.Round(flightLevelValue, MidpointRounding.AwayFromZero);
            if (rounded < 0 || rounded > 1000)
            {
                return Result<FlightLevel>.Failure(
                    "DOMAIN-FL-OUT-OF-RANGE",
                    $"Flight level {rounded} is outside the valid operational range [0..1000].",
                    ErrorCategory.DomainMapping,
                    location);
            }

            return Result<FlightLevel>.Success(new FlightLevel(rounded));
        }

        public bool Equals(FlightLevel other) => Value == other.Value;
        public override bool Equals(object? obj) => obj is FlightLevel other && Equals(other);
        public override int GetHashCode() => Value;
        public int CompareTo(FlightLevel other) => Value.CompareTo(other.Value);

        public static bool operator <(FlightLevel left, FlightLevel right) => left.Value < right.Value;
        public static bool operator >(FlightLevel left, FlightLevel right) => left.Value > right.Value;
        public static bool operator <=(FlightLevel left, FlightLevel right) => left.Value <= right.Value;
        public static bool operator >=(FlightLevel left, FlightLevel right) => left.Value >= right.Value;
        public static bool operator ==(FlightLevel left, FlightLevel right) => left.Equals(right);
        public static bool operator !=(FlightLevel left, FlightLevel right) => !left.Equals(right);

        public override string ToString() => string.Format(CultureInfo.InvariantCulture, "FL{0:D3}", Value);
    }
}
