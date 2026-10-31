using System;
using System.Globalization;
using Iwxxm.Net.Reports;

namespace Iwxxm.Net.Infrastructure.Mapping.CodeLists
{
    /// <summary>
    /// Anti-Corruption Layer table of one WMO BUFR code table (<c>http://codes.wmo.int/bufr4/codeflag/...</c>) used by SIGWX:
    /// translates a reported reference into a <see cref="CodeListValue{TCode}"/> and back.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>A code URI of the table (<c>{table}/10</c>) keeps the code (<c>10</c>) as notation; a bare number keeps the
    /// number. Both resolve to the member whose value is that code, or <c>Unknown</c> when the table does not define it.</item>
    /// <item>A TAC abbreviation (compact <c>iwxxm:SIGWXReport</c>, e.g. <c>OCNL_EMBD</c>, <c>CB</c>, <c>MOD</c>) is resolved
    /// through the abbreviation table of the code list and kept verbatim as notation.</item>
    /// <item>Anything else (another table, a non-numeric code) is <c>Unknown</c> with the reference verbatim.</item>
    /// </list>
    /// <see cref="ToReference"/> rebuilds the code URI for a numeric notation and returns any other notation verbatim, so the
    /// Linked-Data output (GeoJSON) is the reported reference; a bare number is written as the code URI of the table.
    /// </remarks>
    internal sealed class BufrCodeTable<TCode>
        where TCode : struct, Enum
    {
        private readonly Func<string?, TCode> _resolve;

        internal BufrCodeTable(string tableUri, Func<string?, TCode> resolve)
        {
            TableUri = tableUri;
            _resolve = resolve;
        }

        /// <summary>Gets the URI of the official table (e.g. <c>http://codes.wmo.int/bufr4/codeflag/0-11-030</c>).</summary>
        internal string TableUri { get; }

        /// <summary>Translates a reference; <c>null</c> for an absent or blank reference.</summary>
        internal CodeListValue<TCode>? Translate(string? rawOrUri)
        {
            if (string.IsNullOrWhiteSpace(rawOrUri))
                return null;

            string value = rawOrUri!.Trim();
            int? numeric = WmoCodeRegistry.TryGetNumericCode(value, TableUri);
            string notation = numeric.HasValue && value.IndexOf('/') >= 0
                ? value.Substring(TableUri.TrimEnd('/').Length + 1).TrimEnd('/')
                : value;
            return new CodeListValue<TCode>(_resolve(value), notation);
        }

        /// <summary>Rebuilds the reference of a translated value (see the type remarks), or <c>null</c>.</summary>
        internal string? ToReference(CodeListValue<TCode>? value)
        {
            if (value == null)
                return null;

            return IsNumeric(value.Notation) ? TableUri + "/" + value.Notation : value.Notation;
        }

        /// <summary>Gets the official numeric code of a translated value, or <c>null</c> for an abbreviation or foreign reference.</summary>
        internal int? NumericCode(CodeListValue<TCode>? value) =>
            value != null && IsNumeric(value.Notation) && int.TryParse(value.Notation, NumberStyles.None, CultureInfo.InvariantCulture, out int code)
                ? code
                : (int?)null;

        private static bool IsNumeric(string notation)
        {
            if (notation.Length == 0)
                return false;

            foreach (char c in notation)
            {
                if (c < '0' || c > '9')
                    return false;
            }

            return true;
        }
    }
}
