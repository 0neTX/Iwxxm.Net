using System;

namespace Iwxxm.Net
{
    /// <summary>
    /// Formatting and RFC 4180 options for exporting IWXXM 2025-2 Domain Aggregate Roots to CSV.
    /// </summary>
    public sealed class CsvExportOptions
    {
        /// <summary>
        /// Default RFC 4180 CSV options: comma delimiter, header row included, WKT geometry column included,
        /// formula injection sanitization enabled, and CRLF (<c>\r\n</c>) line endings.
        /// </summary>
        public static readonly CsvExportOptions Default = new CsvExportOptions();

        /// <summary>
        /// Gets the field delimiter (default <c>","</c>).
        /// </summary>
        public string Delimiter { get; }

        /// <summary>
        /// Gets a value indicating whether the header row with column names should be written first.
        /// </summary>
        public bool IncludeHeader { get; }

        /// <summary>
        /// Gets a value indicating whether the <c>GeometryWkt</c> column should be populated with OGC WKT geometries.
        /// </summary>
        public bool IncludeGeometryWkt { get; }

        /// <summary>
        /// Gets a value indicating whether free-text cells starting with formula-trigger characters
        /// (<c>=</c>, <c>+</c>, <c>-</c>, <c>@</c>, tab, or carriage return) should be prefixed with a single quote (<c>'</c>)
        /// to protect spreadsheet consumers against CSV formula injection. Numeric cells (such as negative coordinates) are never altered.
        /// </summary>
        public bool SanitizeFormulas { get; }

        /// <summary>
        /// Gets the line terminator used between CSV records (default <c>"\r\n"</c> per RFC 4180).
        /// </summary>
        public string NewLine { get; }

        /// <summary>Initializes the CSV export options.</summary>
        /// <param name="delimiter">Field delimiter; must not be <c>null</c> or empty (default <c>","</c>).</param>
        /// <param name="includeHeader"><c>true</c> (default) to emit the column header row.</param>
        /// <param name="includeGeometryWkt"><c>true</c> (default) to include WKT geometry strings in the <c>GeometryWkt</c> column.</param>
        /// <param name="sanitizeFormulas"><c>true</c> (default) to neutralize formula-injection prefixes in text cells.</param>
        /// <param name="newLine">Record line terminator; must not be <c>null</c> or empty (default <c>"\r\n"</c>).</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="delimiter"/> or <paramref name="newLine"/> is <c>null</c> or empty.</exception>
        public CsvExportOptions(
            string delimiter = ",",
            bool includeHeader = true,
            bool includeGeometryWkt = true,
            bool sanitizeFormulas = true,
            string newLine = "\r\n")
        {
            if (string.IsNullOrEmpty(delimiter))
                throw new ArgumentException("CSV delimiter cannot be null or empty.", nameof(delimiter));
            if (string.IsNullOrEmpty(newLine))
                throw new ArgumentException("CSV line terminator cannot be null or empty.", nameof(newLine));

            Delimiter = delimiter;
            IncludeHeader = includeHeader;
            IncludeGeometryWkt = includeGeometryWkt;
            SanitizeFormulas = sanitizeFormulas;
            NewLine = newLine;
        }
    }
}
