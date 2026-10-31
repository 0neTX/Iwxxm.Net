using System;
using System.Collections.Generic;
using System.Globalization;
using System.Xml.Linq;
using static Iwxxm.Net.Infrastructure.Mapping.Xml.IwxxmXmlNames;

namespace Iwxxm.Net.Infrastructure.Mapping.Xml
{
    /// <summary>
    /// Reads GML 3.2 temporal properties (<c>gml:TimeInstant</c>, <c>gml:TimePeriod</c>) as UTC instants, inline or given
    /// by a local <c>xlink:href="#id"</c> reference (<see cref="GmlReferenceResolver"/>). A mandatory value that is absent
    /// is a mapping error; a value given only by an unresolved (remote or unknown) <c>xlink:href</c> is <c>null</c>.
    /// </summary>
    internal static class GmlTimeReader
    {
        /// <summary>
        /// Reads the mandatory <c>issueTime</c>. An absent element, or one given only by an <c>xlink:href</c> reference
        /// that cannot be resolved locally, is a mapping error: the issue time is never invented.
        /// </summary>
        public static DateTimeOffset ReadIssueTime(XElement root, string id, List<ValidationError> errors)
        {
            XElement? prop = root.Element(IwxxmNs + "issueTime");
            if (prop == null)
            {
                errors.Add(MappingErrors.Create(MappingErrors.IssueTimeMissingCode, "The mandatory element 'issueTime' is missing; no issue time is assumed.", id));
                return default;
            }

            XElement? instant = GmlReferenceResolver.Content(prop, GmlNs + "TimeInstant");
            if (instant == null)
            {
                string? href = prop.Attribute(XlinkNs + "href")?.Value;
                errors.Add(MappingErrors.Create(
                    MappingErrors.IssueTimeMissingCode,
                    href != null
                        ? $"The mandatory element 'issueTime' is given only by the reference xlink:href='{href}', which cannot be resolved in this document; no issue time is assumed."
                        : "The mandatory element 'issueTime' has no gml:TimeInstant; no issue time is assumed.",
                    id));
                return default;
            }

            return ParseTimePosition(instant.Element(GmlNs + "timePosition")?.Value, "issueTime", id, errors) ?? default;
        }

        /// <summary>
        /// Reads a mandatory time-instant property: absent -> mapping error <paramref name="missingCode"/>; given by an
        /// unresolved <c>xlink:href</c> or nil -> <c>null</c>; inline -> the parsed UTC instant.
        /// </summary>
        public static DateTimeOffset? ReadRequiredInstant(XElement? prop, string field, string missingCode, string id, List<ValidationError> errors)
        {
            if (prop == null)
            {
                errors.Add(MappingErrors.Create(missingCode, $"The mandatory element '{field}' is missing; no value is assumed.", id));
                return null;
            }

            return ReadOptionalInstant(prop, field, id, errors);
        }

        /// <summary>Reads an optional time-instant property: absent, nil or unresolved <c>xlink:href</c> -> <c>null</c>.</summary>
        public static DateTimeOffset? ReadOptionalInstant(XElement? prop, string field, string id, List<ValidationError> errors)
        {
            XElement? instant = GmlReferenceResolver.Content(prop, GmlNs + "TimeInstant");
            if (instant == null)
                return null;

            return ParseTimePosition(instant.Element(GmlNs + "timePosition")?.Value, field, id, errors);
        }

        /// <summary>
        /// Reads a time-period property. When <paramref name="missingCode"/> is given the property is mandatory and its
        /// absence is a mapping error; an <c>xlink:href</c>-only or nil property yields <c>null</c> bounds.
        /// </summary>
        public static void ReadPeriod(
            XElement? prop,
            string field,
            string? missingCode,
            string id,
            List<ValidationError> errors,
            out DateTimeOffset? beginUtc,
            out DateTimeOffset? endUtc)
        {
            beginUtc = null;
            endUtc = null;

            if (prop == null)
            {
                if (missingCode != null)
                {
                    errors.Add(MappingErrors.Create(missingCode, $"The mandatory element '{field}' is missing; no period is assumed.", id));
                }

                return;
            }

            XElement? periodElem = GmlReferenceResolver.Content(prop, GmlNs + "TimePeriod");
            if (periodElem == null)
                return;

            beginUtc = ParseTimePosition(periodElem.Element(GmlNs + "beginPosition")?.Value, field + "/beginPosition", id, errors);
            endUtc = ParseTimePosition(periodElem.Element(GmlNs + "endPosition")?.Value, field + "/endPosition", id, errors);
        }

        internal static DateTimeOffset? ParseTimePosition(string? text, string field, string id, List<ValidationError> errors)
        {
            if (string.IsNullOrWhiteSpace(text))
                return null;

            if (DateTimeOffset.TryParse(text!.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out DateTimeOffset dt))
                return dt.ToUniversalTime();

            errors.Add(MappingErrors.Create(MappingErrors.TimeInvalidCode, $"The time position '{text.Trim()}' of '{field}' is not a valid ISO 8601 date-time.", id));
            return null;
        }
    }
}
