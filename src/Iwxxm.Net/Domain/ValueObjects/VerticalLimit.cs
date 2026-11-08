using System;
using System.Globalization;

namespace Iwxxm.Net.Reports
{
    /// <summary>
    /// Immutable Value Object holding a vertical limit or level exactly as reported: numeric value, unit, vertical
    /// reference, special value (<c>GND</c>, <c>UNL</c>, ...) and nil reason. No unit or reference conversion is applied:
    /// "15 km AMSL" stays 15 km above MSL (it is not FL492) and "SFC" is distinguishable from FL000.
    /// </summary>
    /// <remarks>
    /// <para>A flight level is a <em>derived view</em> available through <see cref="AsFlightLevel"/> only when the
    /// reference is <see cref="VerticalReference.Std"/> and the unit is <see cref="VerticalUnit.FlightLevel"/> (or
    /// <see cref="VerticalUnit.Feet"/> with an exact multiple of 100 ft).</para>
    /// <para>Equality is structural and exact over every component. <c>default(VerticalLimit)</c> is an "unknown"
    /// limit (no value, unit, reference or nil reason); prefer the factory methods.</para>
    /// </remarks>
    public readonly struct VerticalLimit : IEquatable<VerticalLimit>
    {
        private const double FeetPerMeter = 1.0 / 0.3048;

        private VerticalLimit(
            double? amount,
            VerticalUnit? unit,
            VerticalReference? reference,
            VerticalSpecialValue specialValue,
            bool isNil,
            string? nilReason)
        {
            Amount = amount.HasValue && amount.Value == 0.0 ? 0.0 : amount; // normalize negative zero
            Unit = unit;
            Reference = reference;
            SpecialValue = specialValue;
            IsNil = isNil;
            NilReason = nilReason;
        }

        /// <summary>Numeric magnitude as reported; <c>null</c> for special values (<c>GND</c>, <c>UNL</c>, ...) and nil limits.</summary>
        public double? Amount { get; }

        /// <summary>Unit of <see cref="Amount"/>; <c>null</c> when not reported or not applicable.</summary>
        public VerticalUnit? Unit { get; }

        /// <summary>Vertical reference; <c>null</c> when the document does not report one (never assumed).</summary>
        public VerticalReference? Reference { get; }

        /// <summary>Non-numeric value (<c>GND</c>, <c>UNL</c>, <c>FLOOR</c>, <c>CEILING</c>), or <see cref="VerticalSpecialValue.None"/>.</summary>
        public VerticalSpecialValue SpecialValue { get; }

        /// <summary>Gets a value indicating whether the limit is explicitly nil (<c>xsi:nil</c> / <c>nilReason</c>).</summary>
        public bool IsNil { get; }

        /// <summary>The reported <c>nilReason</c> when <see cref="IsNil"/> is <c>true</c>.</summary>
        public string? NilReason { get; }

        /// <summary>Gets a value indicating whether the limit is the ground / surface (<c>GND</c>).</summary>
        public bool IsGround => SpecialValue == VerticalSpecialValue.Ground;

        /// <summary>Gets a value indicating whether the limit is unlimited (<c>UNL</c>).</summary>
        public bool IsUnlimited => SpecialValue == VerticalSpecialValue.Unlimited;

        /// <summary>Unit code (<c>FL</c>, <c>FT</c>, <c>M</c>, <c>SM</c>, <c>km</c>) or <c>null</c>.</summary>
        public string? UnitOfMeasure => Unit.HasValue ? GetUnitCode(Unit.Value) : null;

        /// <summary>Reference code (<c>SFC</c>, <c>MSL</c>, <c>W84</c>, <c>STD</c>, <c>OTHER</c>) or <c>null</c>.</summary>
        public string? ReferenceCode => Reference.HasValue ? GetReferenceCode(Reference.Value) : null;

        /// <summary>Special value code (<c>GND</c>, <c>UNL</c>, <c>FLOOR</c>, <c>CEILING</c>) or <c>null</c>.</summary>
        public string? SpecialValueCode => SpecialValue switch
        {
            VerticalSpecialValue.Ground => "GND",
            VerticalSpecialValue.Unlimited => "UNL",
            VerticalSpecialValue.Floor => "FLOOR",
            VerticalSpecialValue.Ceiling => "CEILING",
            _ => null
        };

        /// <summary>
        /// Creates a numeric vertical limit. The value must be finite; a flight level must not be negative
        /// (heights and altitudes may be negative, e.g. below mean sea level).
        /// </summary>
        public static Result<VerticalLimit> Create(double amount, VerticalUnit unit, VerticalReference? reference, string? location = null)
        {
            if (double.IsNaN(amount) || double.IsInfinity(amount))
            {
                return Result<VerticalLimit>.Failure(
                    DomainErrorCodes.VerticalLimitInvalid,
                    "A vertical limit value must be a finite number.",
                    ErrorCategory.DomainMapping,
                    location);
            }

            if (!Enum.IsDefined(typeof(VerticalUnit), unit) || (reference.HasValue && !Enum.IsDefined(typeof(VerticalReference), reference.Value)))
            {
                return Result<VerticalLimit>.Failure(
                    DomainErrorCodes.VerticalLimitInvalid,
                    "The vertical unit or reference is not defined.",
                    ErrorCategory.DomainMapping,
                    location);
            }

            if (unit == VerticalUnit.FlightLevel && amount < 0)
            {
                return Result<VerticalLimit>.Failure(
                    DomainErrorCodes.VerticalLimitInvalid,
                    string.Format(CultureInfo.InvariantCulture, "Flight level {0} cannot be negative.", amount),
                    ErrorCategory.DomainMapping,
                    location);
            }

            return Result<VerticalLimit>.Success(new VerticalLimit(amount, unit, reference, VerticalSpecialValue.None, false, null));
        }

        /// <summary>Creates a non-numeric limit (<c>GND</c>, <c>UNL</c>, <c>FLOOR</c>, <c>CEILING</c>).</summary>
        public static VerticalLimit FromSpecialValue(VerticalSpecialValue specialValue, VerticalReference? reference, VerticalUnit? unit = null)
        {
            if (specialValue == VerticalSpecialValue.None || !Enum.IsDefined(typeof(VerticalSpecialValue), specialValue))
                throw new ArgumentOutOfRangeException(nameof(specialValue), specialValue, "A defined special value other than None is required.");

            return new VerticalLimit(null, unit, reference, specialValue, false, null);
        }

        /// <summary>Creates an explicitly nil limit keeping the reported <paramref name="nilReason"/> (which may be absent).</summary>
        public static VerticalLimit Nil(string? nilReason, VerticalReference? reference = null, VerticalUnit? unit = null)
        {
            string? reason = string.IsNullOrWhiteSpace(nilReason) ? null : nilReason!.Trim();
            return new VerticalLimit(null, unit, reference, VerticalSpecialValue.None, true, reason);
        }

        /// <summary>
        /// Returns the flight level when this limit is a pressure altitude referenced to the standard atmosphere
        /// (<see cref="VerticalReference.Std"/>) expressed in <see cref="VerticalUnit.FlightLevel"/>, or in
        /// <see cref="VerticalUnit.Feet"/> with an exact multiple of 100 ft; otherwise <c>null</c>. No rounding or
        /// conversion from heights/altitudes (SFC, MSL, W84) is ever performed.
        /// </summary>
        public FlightLevel? AsFlightLevel()
        {
            if (Reference != VerticalReference.Std || !Amount.HasValue || SpecialValue != VerticalSpecialValue.None || IsNil)
                return null;

            double hundredsOfFeet;
            if (Unit == VerticalUnit.FlightLevel)
            {
                hundredsOfFeet = Amount.Value;
            }
            else if (Unit == VerticalUnit.Feet)
            {
                hundredsOfFeet = Amount.Value / 100.0;
            }
            else
            {
                return null;
            }

            if (hundredsOfFeet != Math.Floor(hundredsOfFeet) || hundredsOfFeet < 0 || hundredsOfFeet > 1000)
                return null;

            return new FlightLevel((int)hundredsOfFeet);
        }

        /// <summary>
        /// Compares two limits only when they are commensurable: same vertical reference (or both unreported) and
        /// units that can be converted without changing the reference (FL with FL; FT/M/km between them; SM with SM;
        /// FL with FT when the reference is STD). <c>GND</c> is below and <c>UNL</c> above any numeric limit of the
        /// same reference. Nil limits are never comparable.
        /// </summary>
        /// <returns><c>true</c> when the comparison is meaningful; <paramref name="comparison"/> follows <see cref="IComparable{T}"/>.</returns>
        public bool TryCompareTo(VerticalLimit other, out int comparison)
        {
            comparison = 0;
            if (IsNil || other.IsNil || Reference != other.Reference)
                return false;

            int? leftRank = SpecialRank(SpecialValue);
            int? rightRank = SpecialRank(other.SpecialValue);
            bool leftNumeric = SpecialValue == VerticalSpecialValue.None && Amount.HasValue;
            bool rightNumeric = other.SpecialValue == VerticalSpecialValue.None && other.Amount.HasValue;

            if (leftRank.HasValue || rightRank.HasValue)
            {
                if ((leftRank.HasValue || leftNumeric) && (rightRank.HasValue || rightNumeric))
                {
                    comparison = (leftRank ?? 0).CompareTo(rightRank ?? 0);
                    return true;
                }

                return false;
            }

            if (!leftNumeric || !rightNumeric || !Unit.HasValue || !other.Unit.HasValue)
                return false;

            if (!TryToCommonScale(this, other.Unit.Value, out double left) || !TryToCommonScale(other, Unit.Value, out double right))
                return false;

            comparison = left.CompareTo(right);
            return true;
        }

        /// <summary>Returns the AIXM / UCUM code of <paramref name="unit"/>.</summary>
        public static string GetUnitCode(VerticalUnit unit) => unit switch
        {
            VerticalUnit.FlightLevel => "FL",
            VerticalUnit.Feet => "FT",
            VerticalUnit.Meters => "M",
            VerticalUnit.Kilometers => "km",
            VerticalUnit.StandardMeters => "SM",
            _ => throw new ArgumentOutOfRangeException(nameof(unit), unit, "Unsupported vertical unit.")
        };

        /// <summary>Returns the AIXM code of <paramref name="reference"/>.</summary>
        public static string GetReferenceCode(VerticalReference reference) => reference switch
        {
            VerticalReference.Sfc => "SFC",
            VerticalReference.Msl => "MSL",
            VerticalReference.W84 => "W84",
            VerticalReference.Std => "STD",
            VerticalReference.Other => "OTHER",
            _ => throw new ArgumentOutOfRangeException(nameof(reference), reference, "Unsupported vertical reference.")
        };

        public bool Equals(VerticalLimit other) =>
            Nullable.Equals(Amount, other.Amount) &&
            Unit == other.Unit &&
            Reference == other.Reference &&
            SpecialValue == other.SpecialValue &&
            IsNil == other.IsNil &&
            string.Equals(NilReason, other.NilReason, StringComparison.Ordinal);

        public override bool Equals(object? obj) => obj is VerticalLimit other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Amount.HasValue ? Amount.Value.GetHashCode() : 0;
                hash = (hash * 397) ^ (Unit.HasValue ? (int)Unit.Value : -1);
                hash = (hash * 397) ^ (Reference.HasValue ? (int)Reference.Value : -1);
                hash = (hash * 397) ^ (int)SpecialValue;
                hash = (hash * 397) ^ (IsNil ? 1 : 0);
                hash = (hash * 397) ^ (NilReason == null ? 0 : StringComparer.Ordinal.GetHashCode(NilReason));
                return hash;
            }
        }

        public static bool operator ==(VerticalLimit left, VerticalLimit right) => left.Equals(right);
        public static bool operator !=(VerticalLimit left, VerticalLimit right) => !left.Equals(right);

        /// <summary>Formats the limit as reported, e.g. <c>FL390 STD</c>, <c>15000 M MSL</c>, <c>GND SFC</c>, <c>NIL(unknown)</c>.</summary>
        public override string ToString()
        {
            string referenceSuffix = Reference.HasValue ? " " + GetReferenceCode(Reference.Value) : string.Empty;

            if (IsNil)
                return "NIL" + (NilReason != null ? "(" + NilReason + ")" : string.Empty);

            if (SpecialValue != VerticalSpecialValue.None)
                return SpecialValueCode + referenceSuffix;

            if (!Amount.HasValue)
                return "UNSPECIFIED" + referenceSuffix;

            if (Unit == VerticalUnit.FlightLevel)
                return string.Format(CultureInfo.InvariantCulture, "FL{0:000}{1}", Amount.Value, referenceSuffix);

            return string.Format(CultureInfo.InvariantCulture, "{0} {1}{2}", Amount.Value, UnitOfMeasure, referenceSuffix);
        }

        private static int? SpecialRank(VerticalSpecialValue value) => value switch
        {
            VerticalSpecialValue.Ground => -1,
            VerticalSpecialValue.Unlimited => 1,
            _ => null
        };

        /// <summary>
        /// Expresses <paramref name="limit"/> on a scale shared with <paramref name="otherUnit"/>: feet for
        /// FL/FT under STD, metres for heights/altitudes, standard metres for SM.
        /// </summary>
        private static bool TryToCommonScale(VerticalLimit limit, VerticalUnit otherUnit, out double scaled)
        {
            scaled = 0;
            double value = limit.Amount!.Value;
            VerticalUnit unit = limit.Unit!.Value;

            bool unitIsLength = unit == VerticalUnit.Feet || unit == VerticalUnit.Meters || unit == VerticalUnit.Kilometers;
            bool otherIsLength = otherUnit == VerticalUnit.Feet || otherUnit == VerticalUnit.Meters || otherUnit == VerticalUnit.Kilometers;

            if (unit == otherUnit && !unitIsLength)
            {
                scaled = value;
                return true;
            }

            if (unitIsLength && otherIsLength)
            {
                scaled = unit switch
                {
                    VerticalUnit.Feet => value / FeetPerMeter,
                    VerticalUnit.Kilometers => value * 1000.0,
                    _ => value
                };
                return true;
            }

            bool pressureFeetPair =
                limit.Reference == VerticalReference.Std &&
                ((unit == VerticalUnit.FlightLevel && otherUnit == VerticalUnit.Feet) ||
                 (unit == VerticalUnit.Feet && otherUnit == VerticalUnit.FlightLevel));

            if (pressureFeetPair)
            {
                scaled = unit == VerticalUnit.FlightLevel ? value * 100.0 : value;
                return true;
            }

            return false;
        }
    }
}
