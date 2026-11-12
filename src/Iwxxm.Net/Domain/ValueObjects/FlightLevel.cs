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

        /// <summary>Creates a flight level from its number.</summary>
        /// <param name="number">The flight level number in hundreds of feet (<c>350</c> for FL350); from 0 to 1000.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="number"/> is below 0 or above 1000.</exception>
        public FlightLevel(int number)
        {
            if (number < 0 || number > 1000)
                throw new ArgumentOutOfRangeException(nameof(number), number, "Flight Level must be between FL000 and FL1000.");

            Number = number;
        }

        /// <summary>
        /// Creates a flight level from a vertical measure, converting metres and feet to hundreds of feet and rounding to the nearest
        /// level (halves away from zero). The value is taken as a pressure altitude: do not use it for heights referenced to SFC or MSL.
        /// </summary>
        /// <param name="rawValue">The measure in <paramref name="uom"/>.</param>
        /// <param name="uom">The unit of measure: <c>FL</c>, <c>M</c> (metres) or <c>[ft_i]</c>/<c>ft</c> (feet), compared case-insensitively; required.</param>
        /// <param name="location">The location (for example a <c>gml:id</c>) attached to the error when the creation fails; <c>null</c> when unknown.</param>
        /// <returns>The flight level, or a failure when the unit is missing or unsupported or the level is outside 0 to 1000.</returns>
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

        /// <inheritdoc />
        public bool Equals(FlightLevel other) => Number == other.Number;
        /// <inheritdoc />
        public override bool Equals(object? obj) => obj is FlightLevel other && Equals(other);
        /// <inheritdoc />
        public override int GetHashCode() => Number;
        /// <inheritdoc />
        public int CompareTo(FlightLevel other) => Number.CompareTo(other.Number);

        /// <summary>Returns <c>true</c> when the left <see cref="FlightLevel"/> is lower than the right one.</summary>
        public static bool operator <(FlightLevel left, FlightLevel right) => left.Number < right.Number;
        /// <summary>Returns <c>true</c> when the left <see cref="FlightLevel"/> is higher than the right one.</summary>
        public static bool operator >(FlightLevel left, FlightLevel right) => left.Number > right.Number;
        /// <summary>Returns <c>true</c> when the left <see cref="FlightLevel"/> is lower than or equal to the right one.</summary>
        public static bool operator <=(FlightLevel left, FlightLevel right) => left.Number <= right.Number;
        /// <summary>Returns <c>true</c> when the left <see cref="FlightLevel"/> is higher than or equal to the right one.</summary>
        public static bool operator >=(FlightLevel left, FlightLevel right) => left.Number >= right.Number;
        /// <summary>Returns <c>true</c> when the two <see cref="FlightLevel"/> operands are equal by value.</summary>
        public static bool operator ==(FlightLevel left, FlightLevel right) => left.Equals(right);
        /// <summary>Returns <c>true</c> when the two <see cref="FlightLevel"/> operands differ by value.</summary>
        public static bool operator !=(FlightLevel left, FlightLevel right) => !left.Equals(right);

        /// <inheritdoc />
        public override string ToString() => string.Format(CultureInfo.InvariantCulture, "FL{0:D3}", Number);
    }
}
