using System;
using System.Text;

namespace Iwxxm.Net.Infrastructure.Csv
{
    /// <summary>
    /// Low-level RFC 4180 CSV writer with optional formula-injection sanitization for non-numeric text cells.
    /// </summary>
    internal sealed class Rfc4180CsvWriter
    {
        private readonly StringBuilder _sb;
        private readonly CsvExportOptions _options;

        public Rfc4180CsvWriter(StringBuilder sb, CsvExportOptions options)
        {
            _sb = sb ?? throw new ArgumentNullException(nameof(sb));
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }

        public void WriteHeader(string[] headers)
        {
            for (int i = 0; i < headers.Length; i++)
            {
                if (i > 0)
                    _sb.Append(_options.Delimiter);
                WriteField(headers[i], isNumeric: false);
            }
            _sb.Append(_options.NewLine);
        }

        public void WriteRow(CsvRow row)
        {
            for (int i = 0; i < CsvColumns.Count; i++)
            {
                if (i > 0)
                    _sb.Append(_options.Delimiter);
                CsvCell cell = row[i];
                WriteField(cell.Value, cell.IsNumeric);
            }
            _sb.Append(_options.NewLine);
        }

        private void WriteField(string? value, bool isNumeric)
        {
            if (string.IsNullOrEmpty(value))
                return;

            string text = value!;
            if (!isNumeric && _options.SanitizeFormulas && IsFormulaTrigger(text[0]))
            {
                text = "'" + text;
            }

            bool needsQuotes =
                text.IndexOf(_options.Delimiter, StringComparison.Ordinal) >= 0 ||
                text.IndexOf('"') >= 0 ||
                text.IndexOf('\r') >= 0 ||
                text.IndexOf('\n') >= 0;

            if (!needsQuotes)
            {
                _sb.Append(text);
                return;
            }

            _sb.Append('"');
            for (int i = 0; i < text.Length; i++)
            {
                char ch = text[i];
                if (ch == '"')
                    _sb.Append("\"\"");
                else
                    _sb.Append(ch);
            }
            _sb.Append('"');
        }

        private static bool IsFormulaTrigger(char first) =>
            first == '=' || first == '+' || first == '-' || first == '@' || first == '\t' || first == '\r';
    }
}
