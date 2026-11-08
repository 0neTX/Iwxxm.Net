using System;
using System.Globalization;

namespace Iwxxm.Net.Reports
{
    /// <summary>
    /// Value Object representing an aeronautical Flight Level (FL): a pressure altitude, referenced to the standard
    /// atmosphere (STD), in hundreds of feet.
    /// </summary>
    /// <remarks>
    /// Report vertical limits are modelled by <see cref="VerticalLimit"/>, which keeps the reported unit and vertical
    /// reference; <see cref="VerticalLimit.AsFlightLevel"/> derives a <see cref="FlightLevel"/> only for STD limits.
    /// <see cref="Create"/> converts metres and feet assuming the value already is a pressure altitude: never use it for
    /// heights or altitudes referenced to SFC or MSL.
    /// </remarks>
    public readonly struct FlightLevel : IEquatable<FlightLevel>, IComparable<FlightLevel>
    {
        private const double MetersPerFoot = 0.3048;

        /// <summary>The flight level number: hundreds of feet (<c>350</c> for FL350).</summary>
        public int Number { get; }

        /// <summary>The pressure altitude in feet (<c>35000</c> for FL350).</summary>
        public int AltitudeInFeet => Number * 100;

        public FlightLevel(int number)
        {
            if (number < 0 || number > 1000)
                throw new ArgumentOutOfRangeException(nameof(number), number, "Flight Level must be between FL000 and FL1000.");

            Number = number;
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

        public bool Equals(FlightLevel other) => Number == other.Number;
        public override bool Equals(object? obj) => obj is FlightLevel other && Equals(other);
        public override int GetHashCode() => Number;
        public int CompareTo(FlightLevel other) => Number.CompareTo(other.Number);

        public static bool operator <(FlightLevel left, FlightLevel right) => left.Number < right.Number;
        public static bool operator >(FlightLevel left, FlightLevel right) => left.Number > right.Number;
        public static bool operator <=(FlightLevel left, FlightLevel right) => left.Number <= right.Number;
        public static bool operator >=(FlightLevel left, FlightLevel right) => left.Number >= right.Number;
        public static bool operator ==(FlightLevel left, FlightLevel right) => left.Equals(right);
        public static bool operator !=(FlightLevel left, FlightLevel right) => !left.Equals(right);

        public override string ToString() => string.Format(CultureInfo.InvariantCulture, "FL{0:D3}", Number);
    }
}
