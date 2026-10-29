using System;
using System.Collections.Generic;
using System.Globalization;
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

        public static Length? ReadLength(XElement? measureElem, string context, List<ValidationError> errors) =>
            measureElem == null || IsNil(measureElem)
                ? (Length?)null
                : ReadLength(measureElem.Value, measureElem.Attribute("uom")?.Value, Describe(measureElem, context), errors);

        public static double? ReadFixedUnit(XElement? measureElem, string requiredUom, string context, List<ValidationError> errors) =>
            measureElem == null || IsNil(measureElem)
                ? (double?)null
                : ReadFixedUnit(measureElem.Value, measureElem.Attribute("uom")?.Value, requiredUom, Describe(measureElem, context), errors);

        // ---------------------------------------------------------------------
        // Raw string overloads (also used for the SIGWX transport DTOs in Mapping/Sigwx)
        // ---------------------------------------------------------------------

        public static Speed? ReadSpeed(string? rawValue, string? uom, string location, List<ValidationError> errors)
        {
            if (string.IsNullOrWhiteSpace(rawValue))
                return null;

            if (!TryReadValueAndUom(rawValue!, uom, location, errors, out double value, out string unitCode))
                return null;

            if (!TryParseSpeedUnit(unitCode, out SpeedUnit unit))
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
