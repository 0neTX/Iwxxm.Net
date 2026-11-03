using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;
using Iwxxm.Net.Reports;

namespace Iwxxm.Net.Infrastructure.Mapping
{
    /// <summary>
    /// Single Anti-Corruption Layer helper that reads GML measures (numeric value + <c>@uom</c>) and turns them into
    /// Domain values. Units are UCUM codes as constrained by the official IWXXM 2025-2 rules (<c>IWXXM/rule/iwxxm.sch</c>):
    /// <list type="bullet">
    /// <item>Speeds (<c>m/s</c>, <c>[kn_i]</c>, <c>km/h</c>) and lengths (<c>m</c>, <c>[ft_i]</c>, <c>km</c>, <c>[nmi_i]</c>,
    /// <c>[mi_i]</c>) are kept in the reported unit as <see cref="Speed"/> / <see cref="Length"/> value objects.</item>
    /// <item>Single-unit quantities (e.g. temperatures in <c>Cel</c>, pressures in <c>hPa</c>, directions in <c>deg</c>,
    /// visibility/RVR in <c>m</c>) must carry exactly that unit.</item>
    /// </list>
    /// A missing <c>@uom</c> on a non-nil measure, an unknown or mismatching unit, or a non-numeric value produces a
    /// <see cref="ErrorCategory.DomainMapping"/> error; no unit is ever assumed. Nil or empty measures map to <c>null</c>.
    /// </summary>
    internal static class UomMeasureReader
    {
        internal const string UomMissingCode = "ACL-MEASURE-UOM-MISSING";
        internal const string UomUnsupportedCode = "ACL-MEASURE-UOM-UNSUPPORTED";
        internal const string UomMismatchCode = "ACL-MEASURE-UOM-MISMATCH";
        internal const string ValueInvalidCode = "ACL-MEASURE-VALUE-INVALID";
        internal const string UomNotAllowedCode = "ACL-MEASURE-UOM-NOT-ALLOWED";

        /// <summary>Warning: a recognised legacy spelling of a UCUM unit (for example <c>kn_i</c> for <c>[kn_i]</c>) was accepted (AUD-R5).</summary>
        internal const string NonUcumAliasCode = "ACL-MEASURE-UOM-NON-UCUM-ALIAS";

        /// <summary>
        /// Per-field unit subsets of the official rules (<c>IWXXM/rule/iwxxm.sch</c>): a unit valid in general but not
        /// allowed for the field is a mapping error naming the field.
        /// </summary>
        internal static class Subsets
        {
            /// <summary>Surface wind / gust / maximum surface wind speeds: <c>m/s</c> or <c>[kn_i]</c>
            /// (SPECI.AerodromeSurfaceWind-6/7, Common.AerodromeSurfaceWindTrendForecast-2/3, AIRMET.AIRMETEvolvingCondition-6,
            /// TropicalCycloneAdvisory.TropicalCycloneObservedConditions-2, TropicalCycloneForecastConditions-1).</summary>
            public static readonly string[] WindSpeed = { "m/s", "[kn_i]" };

            /// <summary>Speed of motion: <c>km/h</c> or <c>[kn_i]</c> (SIGMET.SIGMETEvolvingCondition-3,
            /// AIRMET.AIRMETEvolvingCondition-4, VolcanicAshAdvisory.VolcanicAshCloud-2).</summary>
            public static readonly string[] SpeedOfMotion = { "km/h", "[kn_i]" };

            /// <summary>Cloud base / top: <c>m</c> or <c>[ft_i]</c> (Common.CloudLayer-1, AIRMET.AIRMETEvolvingCondition-1/2).</summary>
            public static readonly string[] CloudHeight = { "m", "[ft_i]" };

            /// <summary>AIRMET surface visibility: <c>m</c> (AIRMET.AIRMETEvolvingCondition-5).</summary>
            public static readonly string[] Visibility = { "m" };
        }

        private static readonly XNamespace XsiNs = "http://www.w3.org/2001/XMLSchema-instance";

        public static bool TryParseSpeedUnit(string? uom, out SpeedUnit unit)
        {
            switch (uom)
            {
                case "m/s":
                    unit = SpeedUnit.MetersPerSecond;
                    return true;
                case "[kn_i]":
                    unit = SpeedUnit.Knots;
                    return true;
                case "km/h":
                    unit = SpeedUnit.KilometersPerHour;
                    return true;
                default:
                    unit = default;
                    return false;
            }
        }

        public static bool TryParseLengthUnit(string? uom, out LengthUnit unit)
        {
            switch (uom)
            {
                case "m":
                    unit = LengthUnit.Meters;
                    return true;
                case "[ft_i]":
                    unit = LengthUnit.Feet;
                    return true;
                case "km":
                    unit = LengthUnit.Kilometers;
                    return true;
                case "[nmi_i]":
                    unit = LengthUnit.NauticalMiles;
                    return true;
                case "[mi_i]":
                    unit = LengthUnit.StatuteMiles;
                    return true;
                default:
                    unit = default;
                    return false;
            }
        }

        // ---------------------------------------------------------------------
        // XElement (LINQ to XML) overloads used by the report mappers (Mapping/Reports)
        // ---------------------------------------------------------------------

        public static Speed? ReadSpeed(XElement? measureElem, string context, List<ValidationError> errors) =>
            measureElem == null || IsNil(measureElem)
                ? (Speed?)null
                : ReadSpeed(measureElem.Value, measureElem.Attribute("uom")?.Value, Describe(measureElem, context), errors);

        /// <summary>Reads a speed whose unit must belong to the field subset <paramref name="allowedUoms"/>.</summary>
        public static Speed? ReadSpeed(XElement? measureElem, string context, List<ValidationError> errors, IReadOnlyList<string> allowedUoms) =>
            measureElem == null || IsNil(measureElem) || !IsAllowed(measureElem, Describe(measureElem, context), allowedUoms, errors)
                ? (Speed?)null
                : ReadSpeed(measureElem.Value, measureElem.Attribute("uom")?.Value, Describe(measureElem, context), errors);

        public static Length? ReadLength(XElement? measureElem, string context, List<ValidationError> errors) =>
            measureElem == null || IsNil(measureElem)
                ? (Length?)null
                : ReadLength(measureElem.Value, measureElem.Attribute("uom")?.Value, Describe(measureElem, context), errors);

        /// <summary>Reads a length whose unit must belong to the field subset <paramref name="allowedUoms"/>.</summary>
        public static Length? ReadLength(XElement? measureElem, string context, List<ValidationError> errors, IReadOnlyList<string> allowedUoms) =>
            measureElem == null || IsNil(measureElem) || !IsAllowed(measureElem, Describe(measureElem, context), allowedUoms, errors)
                ? (Length?)null
                : ReadLength(measureElem.Value, measureElem.Attribute("uom")?.Value, Describe(measureElem, context), errors);

        /// <summary>
        /// Checks the <c>@uom</c> of a non-nil measure against a per-field subset. A missing unit is left to the regular
        /// reader (which reports it); a unit outside the subset is an <see cref="UomNotAllowedCode"/> error naming the field.
        /// </summary>
        internal static bool IsAllowed(XElement measureElem, string location, IReadOnlyList<string> allowedUoms, List<ValidationError> errors)
        {
            string? uom = measureElem.Attribute("uom")?.Value?.Trim();
            if (string.IsNullOrEmpty(uom))
                return true;

            foreach (string allowed in allowedUoms)
            {
                if (string.Equals(allowed, uom, StringComparison.Ordinal))
                    return true;
            }

            // A recognised legacy spelling of an allowed unit is accepted (and reported by the reader as a warning).
            if (UomAliasScope.IsActiveAlias(uom, out string canonical) && allowedUoms.Contains(canonical))
                return true;

            errors.Add(Error(UomNotAllowedCode,
                $"Unit of measure '{uom}' is not allowed for '{measureElem.Name.LocalName}' at '{location}'; IWXXM 2025-2 (iwxxm.sch) allows {string.Join(" or ", allowedUoms.Select(u => "'" + u + "'"))}.",
                location));
            return false;
        }

        public static double? ReadFixedUnit(XElement? measureElem, string requiredUom, string context, List<ValidationError> errors) =>
            measureElem == null || IsNil(measureElem)
                ? (double?)null
                : ReadFixedUnit(measureElem.Value, measureElem.Attribute("uom")?.Value, requiredUom, Describe(measureElem, context), errors);

        // ---------------------------------------------------------------------
        // Raw string overloads (also used for the SIGWX transport DTOs in Mapping/Wafs)
        // ---------------------------------------------------------------------

        public static Speed? ReadSpeed(string? rawValue, string? uom, string location, List<ValidationError> errors)
        {
            if (string.IsNullOrWhiteSpace(rawValue))
                return null;

            if (!TryReadValueAndUom(rawValue!, uom, location, errors, out double value, out string unitCode))
                return null;

            // A recognised legacy spelling ("kn_i") is accepted while a mapping scope can report it as a warning.
            SpeedUnit unit;
            if (!TryParseSpeedUnit(unitCode, out unit) &&
                UomAliasScope.IsActiveAlias(unitCode, out _) &&
                TryParseSpeedUnit(UomAliasScope.Accept(unitCode, location), out unit))
            {
                return new Speed(value, unit);
            }

            if (!TryParseSpeedUnit(unitCode, out unit))
            {
                errors.Add(Error(UomUnsupportedCode,
                    $"Unsupported speed unit of measure '{unitCode}' at '{location}'. Expected 'm/s', '[kn_i]' or 'km/h'.",
                    location));
                return null;
            }

            return new Speed(value, unit);
        }

        public static Length? ReadLength(string? rawValue, string? uom, string location, List<ValidationError> errors)
        {
            if (string.IsNullOrWhiteSpace(rawValue))
                return null;

            if (!TryReadValueAndUom(rawValue!, uom, location, errors, out double value, out string unitCode))
                return null;

            if (!TryParseLengthUnit(unitCode, out LengthUnit unit))
            {
                errors.Add(Error(UomUnsupportedCode,
                    $"Unsupported length unit of measure '{unitCode}' at '{location}'. Expected 'm', '[ft_i]', 'km', '[nmi_i]' or '[mi_i]'.",
                    location));
                return null;
            }

            return new Length(value, unit);
        }

        public static double? ReadFixedUnit(string? rawValue, string? uom, string requiredUom, string location, List<ValidationError> errors)
        {
            if (string.IsNullOrWhiteSpace(rawValue))
                return null;

            if (!TryReadValueAndUom(rawValue!, uom, location, errors, out double value, out string unitCode))
                return null;

            if (!string.Equals(unitCode, requiredUom, StringComparison.Ordinal))
            {
                errors.Add(Error(UomMismatchCode,
                    $"Unit of measure '{unitCode}' at '{location}' is not supported; IWXXM 2025-2 requires '{requiredUom}'.",
                    location));
                return null;
            }

            return value;
        }

        private static bool TryReadValueAndUom(
            string rawValue,
            string? uom,
            string location,
            List<ValidationError> errors,
            out double value,
            out string unitCode)
        {
            value = 0.0;
            unitCode = string.Empty;

            if (string.IsNullOrWhiteSpace(uom))
            {
                errors.Add(Error(UomMissingCode,
                    $"Measure '{rawValue.Trim()}' at '{location}' has no unit of measure (@uom); the unit cannot be assumed.",
                    location));
                return false;
            }

            if (!double.TryParse(rawValue.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value) ||
                double.IsNaN(value) ||
                double.IsInfinity(value))
            {
                errors.Add(Error(ValueInvalidCode,
                    $"Measure value '{rawValue.Trim()}' at '{location}' is not a finite number.",
                    location));
                return false;
            }

            unitCode = uom!.Trim();
            return true;
        }

        private static bool IsNil(XElement measureElem)
        {
            string? nil = measureElem.Attribute(XsiNs + "nil")?.Value;
            return string.Equals(nil, "true", StringComparison.Ordinal) ||
                   string.Equals(nil, "1", StringComparison.Ordinal) ||
                   string.IsNullOrWhiteSpace(measureElem.Value);
        }

        private static string Describe(XElement measureElem, string context) =>
            string.IsNullOrEmpty(context) ? measureElem.Name.LocalName : context + "/" + measureElem.Name.LocalName;

        private static ValidationError Error(string code, string message, string location) =>
            new ValidationError(code, message, ErrorCategory.DomainMapping, ValidationSeverity.Error, location);
    }
}
