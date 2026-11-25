using System;
using System.Globalization;

namespace Iwxxm.Net.Export.GeoPackage
{
    /// <summary>
    /// The text form of the <c>DATETIME</c> values of a GeoPackage (<c>gpkg_contents.last_change</c> and the issue and validity
    /// times of the feature and report tables): exactly <c>yyyy-MM-dd'T'HH:mm:ss.fff'Z'</c>, in UTC.
    /// </summary>
    /// <remarks>
    /// The OGC GeoPackage validator of GDAL rejects the text Microsoft.Data.Sqlite writes by default
    /// (<c>2026-11-14 01:11:00</c> or an offset such as <c>+00:00</c>), so values are always formatted here, never bound as
    /// <see cref="DateTime"/> or <see cref="DateTimeOffset"/> parameters. Formatting uses the invariant culture and the
    /// Gregorian calendar whatever the current culture is.
    /// </remarks>
    internal static class GeoPackageDateTime
    {
        private const string Pattern = "yyyy-MM-dd'T'HH:mm:ss.fff'Z'";

        // The 'O' text of a DateTimeOffset has seven fraction digits and a numeric offset; other writers use 'Z' or fewer
        // (or no) fraction digits. An offset is mandatory: a time without one is ambiguous and is rejected. One format per
        // fraction length keeps the parse strict (a dot without digits is not accepted).
        private static readonly string[] IsoFormats = BuildIsoFormats();

        private static string[] BuildIsoFormats()
        {
            var formats = new System.Collections.Generic.List<string>();
            for (int digits = 0; digits <= 7; digits++)
            {
                string fraction = digits == 0 ? string.Empty : "." + new string('f', digits);
                formats.Add("yyyy-MM-dd'T'HH:mm:ss" + fraction + "zzz");
                formats.Add("yyyy-MM-dd'T'HH:mm:ss" + fraction + "'Z'");
            }

            return formats.ToArray();
        }

        /// <summary>
        /// Formats an instant as GeoPackage <c>DATETIME</c> text: converted to UTC first, with milliseconds. Digits below the
        /// millisecond are truncated, never rounded, so a formatted value is never later than the instant.
        /// </summary>
        public static string Format(DateTimeOffset value) =>
            value.UtcDateTime.ToString(Pattern, CultureInfo.InvariantCulture);

        /// <summary>
        /// Formats a <see cref="DateTime"/> like <see cref="Format(DateTimeOffset)"/>: <see cref="DateTimeKind.Utc"/> is taken
        /// as is, <see cref="DateTimeKind.Local"/> and <see cref="DateTimeKind.Unspecified"/> are taken as local time of
        /// this machine (the .NET convention) and converted to UTC.
        /// </summary>
        public static string Format(DateTime value) => Format(new DateTimeOffset(value));

        /// <summary>
        /// Parses the ISO 8601 round-trip text of the GeoJSON properties (<c>2026-11-14T01:11:00.0000000+00:00</c>, the same
        /// with <c>Z</c>, another offset, fewer or no fraction digits).
        /// </summary>
        /// <returns>The instant, keeping the offset of the text, or <c>null</c> when the text is <c>null</c>, blank, has no offset or is not a valid date and time.</returns>
        public static DateTimeOffset? ParseIso(string? text)
        {
            if (string.IsNullOrEmpty(text))
                return null;

            return DateTimeOffset.TryParseExact(text, IsoFormats, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out DateTimeOffset parsed)
                ? parsed
                : (DateTimeOffset?)null;
        }

        /// <summary>
        /// Converts ISO 8601 round-trip text to GeoPackage <c>DATETIME</c> text, or <c>null</c> when <paramref name="text"/> is
        /// not valid (see <see cref="ParseIso"/>).
        /// </summary>
        public static string? Normalize(string? text)
        {
            DateTimeOffset? parsed = ParseIso(text);
            return parsed.HasValue ? Format(parsed.Value) : null;
        }
    }
}
