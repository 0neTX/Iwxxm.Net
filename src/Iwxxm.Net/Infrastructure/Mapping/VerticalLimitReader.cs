using System;
using System.Collections.Generic;
using System.Globalization;
using System.Xml.Linq;
using Iwxxm.Net.Reports;

namespace Iwxxm.Net.Infrastructure.Mapping
{
    /// <summary>
    /// Anti-corruption reader for vertical distances (<c>aixm:ValDistanceVerticalType</c> and
    /// <c>iwxxm:ValDistanceVerticalType</c>) and their vertical reference (<c>aixm:CodeVerticalReferenceBaseType</c>).
    /// Values are kept exactly as reported (<see cref="VerticalLimit"/>); nothing is converted to a flight level and no
    /// reference is assumed.
    /// </summary>
    internal static class VerticalLimitReader
    {
        internal const string UnsupportedUomCode = "ACL-VERTICAL-UOM-UNSUPPORTED";
        internal const string UnsupportedReferenceCode = "ACL-VERTICAL-REFERENCE-UNSUPPORTED";

        private static readonly XNamespace XsiNs = "http://www.w3.org/2001/XMLSchema-instance";

        /// <summary>
        /// Reads a vertical distance element and its optional reference element. Returns <c>null</c> when the element is
        /// absent; returns a nil <see cref="VerticalLimit"/> for <c>xsi:nil</c> / <c>nilReason</c> / empty content; records a
        /// mapping error (and returns <c>null</c>) when the value or unit cannot be interpreted.
        /// </summary>
        public static VerticalLimit? Read(XElement? valueElement, XElement? referenceElement, string context, List<ValidationError> errors) =>
            Read(valueElement, referenceElement, context, errors, null);

        /// <summary>Reads a vertical distance whose unit must belong to <paramref name="allowedUoms"/> when given.</summary>
        public static VerticalLimit? Read(
            XElement? valueElement,
            XElement? referenceElement,
            string context,
            List<ValidationError> errors,
            IReadOnlyList<string>? allowedUoms)
        {
            if (valueElement == null)
                return null;

            if (allowedUoms != null && !UomMeasureReader.IsAllowed(valueElement, context + "/" + valueElement.Name.LocalName, allowedUoms, errors))
                return null;

            string? nil = valueElement.Attribute(XsiNs + "nil")?.Value;
            bool isNil = string.Equals(nil, "true", StringComparison.Ordinal) || string.Equals(nil, "1", StringComparison.Ordinal);

            return Read(
                valueElement.Value,
                valueElement.Attribute("uom")?.Value,
                valueElement.Attribute("nilReason")?.Value,
                isNil,
                referenceElement?.Value,
                context + "/" + valueElement.Name.LocalName,
                errors);
        }

        /// <summary>
        /// Raw overload used for XmlSerializer transport DTOs. See <see cref="Read(XElement, XElement, string, List{ValidationError})"/>.
        /// </summary>
        public static VerticalLimit? Read(
            string? rawValue,
            string? uom,
            string? nilReason,
            bool isNil,
            string? referenceCode,
            string location,
            List<ValidationError> errors)
        {
            if (!TryParseReference(referenceCode, location, errors, out VerticalReference? reference))
                return null;

            string text = rawValue?.Trim() ?? string.Empty;
            VerticalUnit? unit = TryParseUnit(uom, out VerticalUnit parsedUnit) ? parsedUnit : (VerticalUnit?)null;

            if (isNil || !string.IsNullOrWhiteSpace(nilReason) || text.Length == 0)
            {
                return VerticalLimit.Nil(nilReason, reference, unit);
            }

            VerticalSpecialValue special = ParseSpecialValue(text);
            if (special != VerticalSpecialValue.None)
            {
                return VerticalLimit.FromSpecialValue(special, reference, unit);
            }

            if (string.IsNullOrWhiteSpace(uom))
            {
                errors.Add(Error(UomMeasureReader.UomMissingCode,
                    $"Vertical distance '{text}' at '{location}' has no unit of measure (@uom); the unit cannot be assumed.",
                    location));
                return null;
            }

            if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) ||
                double.IsNaN(value) || double.IsInfinity(value))
            {
                errors.Add(Error(UomMeasureReader.ValueInvalidCode,
                    $"Vertical distance '{text}' at '{location}' is neither a finite number nor GND/UNL/FLOOR/CEILING.",
                    location));
                return null;
            }

            if (!unit.HasValue)
            {
                errors.Add(Error(UnsupportedUomCode,
                    $"Unsupported vertical unit of measure '{uom!.Trim()}' at '{location}'. Expected FL, FT, M, SM, [ft_i], m or km.",
                    location));
                return null;
            }

            Result<VerticalLimit> created = VerticalLimit.Create(value, unit.Value, reference, location);
            if (created.IsFailure)
            {
                errors.AddRange(created.Errors);
                return null;
            }

            return created.Value;
        }

        internal static bool TryParseUnit(string? uom, out VerticalUnit unit)
        {
            switch (uom?.Trim())
            {
                case "FL":
                    unit = VerticalUnit.FlightLevel;
                    return true;
                case "FT":
                case "[ft_i]":
                    unit = VerticalUnit.Feet;
                    return true;
                case "M":
                case "m":
                    unit = VerticalUnit.Meters;
                    return true;
                case "km":
                    unit = VerticalUnit.Kilometers;
                    return true;
                case "SM":
                    unit = VerticalUnit.StandardMeters;
                    return true;
                default:
                    unit = default;
                    return false;
            }
        }

        private static bool TryParseReference(string? code, string location, List<ValidationError> errors, out VerticalReference? reference)
        {
            reference = null;
            string trimmed = code?.Trim() ?? string.Empty;
            if (trimmed.Length == 0)
                return true;

            switch (trimmed)
            {
                case "SFC":
                    reference = VerticalReference.Sfc;
                    return true;
                case "MSL":
                    reference = VerticalReference.Msl;
                    return true;
                case "W84":
                    reference = VerticalReference.W84;
                    return true;
                case "STD":
                    reference = VerticalReference.Std;
                    return true;
            }

            if (trimmed == "OTHER" || trimmed.StartsWith("OTHER:", StringComparison.Ordinal))
            {
                reference = VerticalReference.Other;
                return true;
            }

            errors.Add(Error(UnsupportedReferenceCode,
                $"Unsupported vertical reference '{trimmed}' at '{location}'. Expected SFC, MSL, W84, STD or OTHER.",
                location));
            return false;
        }

        private static VerticalSpecialValue ParseSpecialValue(string text)
        {
            switch (text)
            {
                case "GND":
                case "SFC":
                    return VerticalSpecialValue.Ground;
                case "UNL":
                    return VerticalSpecialValue.Unlimited;
                case "FLOOR":
                    return VerticalSpecialValue.Floor;
                case "CEILING":
                    return VerticalSpecialValue.Ceiling;
                default:
                    return VerticalSpecialValue.None;
            }
        }

        private static ValidationError Error(string code, string message, string location) =>
            new ValidationError(code, message, ErrorCategory.DomainMapping, ValidationSeverity.Error, location);
    }
}
