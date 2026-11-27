using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Iwxxm.Net.Reports;

namespace Iwxxm.Net.Infrastructure.GeoJson.Writing
{
    /// <summary>
    /// Lightweight, zero-dependency RFC 8259 JSON / RFC 7946 GeoJSON writer backed by <see cref="StringBuilder"/>.
    /// Enforces RFC 7946 Section 3.1.1 <c>[longitude, latitude]</c> coordinate tuple ordering.
    /// </summary>
    internal sealed class Rfc8259JsonWriter
    {
        private readonly StringBuilder _sb;
        private readonly bool _indented;
        private int _indentLevel;
        private bool _needsComma;
        private bool _afterPropertyName;
        private bool _inlineNeedsComma;

        public Rfc8259JsonWriter(StringBuilder sb, bool indented)
        {
            _sb = sb;
            _indented = indented;
        }

        public void BeginObject()
        {
            WriteCommaIfNeeded();
            _sb.Append('{');
            _indentLevel++;
            _needsComma = false;
            _afterPropertyName = false;
        }

        public void EndObject()
        {
            _indentLevel--;
            if (_indented)
            {
                _sb.Append('\n');
                WriteIndent();
            }
            _sb.Append('}');
            _needsComma = true;
            _afterPropertyName = false;
        }

        public void BeginArray()
        {
            _sb.Append('[');
            _indentLevel++;
            _needsComma = false;
            _afterPropertyName = false;
        }

        public void EndArray()
        {
            _indentLevel--;
            if (_indented)
            {
                _sb.Append('\n');
                WriteIndent();
            }
            _sb.Append(']');
            _needsComma = true;
            _afterPropertyName = false;
        }

        public void BeginInlineArray()
        {
            _sb.Append('[');
            _inlineNeedsComma = false;
            _afterPropertyName = false;
        }

        public void WriteInlineNumber(double value)
        {
            if (_inlineNeedsComma)
            {
                _sb.Append(_indented ? ", " : ",");
            }
            AppendNumber(value);
            _inlineNeedsComma = true;
        }

        public void EndInlineArray()
        {
            _sb.Append(']');
            _needsComma = true;
            _afterPropertyName = false;
        }

        public void WritePropertyName(string name)
        {
            if (_needsComma)
            {
                _sb.Append(',');
            }
            if (_indented)
            {
                _sb.Append('\n');
                WriteIndent();
            }
            WriteEscapedString(name);
            _sb.Append(_indented ? ": " : ":");
            _needsComma = false;
            _afterPropertyName = true;
        }

        public void WriteProperty(string name, string? value)
        {
            if (value == null) return;
            WritePropertyName(name);
            WriteEscapedString(value);
            _needsComma = true;
            _afterPropertyName = false;
        }

        public void WriteProperty(string name, int value)
        {
            WritePropertyName(name);
            _sb.Append(value.ToString(CultureInfo.InvariantCulture));
            _needsComma = true;
            _afterPropertyName = false;
        }

        public void WriteProperty(string name, double value)
        {
            WritePropertyName(name);
            AppendPropertyNumber(value);
            _needsComma = true;
            _afterPropertyName = false;
        }

        public void WriteProperty(string name, bool value)
        {
            WritePropertyName(name);
            _sb.Append(value ? "true" : "false");
            _needsComma = true;
            _afterPropertyName = false;
        }

        public void WriteNullValue()
        {
            _sb.Append("null");
            _needsComma = true;
            _afterPropertyName = false;
        }

        public void WriteDynamicProperty(string name, object value)
        {
            switch (value)
            {
                case string s:
                    WriteProperty(name, s);
                    break;
                case int i:
                    WriteProperty(name, i);
                    break;
                case double d:
                    WriteProperty(name, d);
                    break;
                case bool b:
                    WriteProperty(name, b);
                    break;
                case IReadOnlyList<double> numbers:
                    WritePropertyName(name);
                    _sb.Append('[');
                    for (int idx = 0; idx < numbers.Count; idx++)
                    {
                        if (idx > 0) _sb.Append(_indented ? ", " : ",");
                        AppendPropertyNumber(numbers[idx]);
                    }
                    _sb.Append(']');
                    _needsComma = true;
                    _afterPropertyName = false;
                    break;
                case IReadOnlyList<string?> strList:
                    WritePropertyName(name);
                    _sb.Append('[');
                    for (int idx = 0; idx < strList.Count; idx++)
                    {
                        if (idx > 0) _sb.Append(_indented ? ", " : ",");
                        WriteEscapedString(strList[idx]);
                    }
                    _sb.Append(']');
                    _needsComma = true;
                    _afterPropertyName = false;
                    break;
                default:
                    WriteProperty(name, value.ToString());
                    break;
            }
        }

        /// <summary>
        /// Writes an RFC 7946 Section 3.1.1 <c>[longitude, latitude]</c> coordinate position.
        /// </summary>
        public void WritePosition(GeoJsonPosition position)
        {
            _sb.Append('[');
            AppendNumber(position.Lon);
            _sb.Append(_indented ? ", " : ",");
            AppendNumber(position.Lat);
            _sb.Append(']');
            _needsComma = true;
            _afterPropertyName = false;
        }

        /// <summary>Writes an inline array of positions (a line string or a linear ring).</summary>
        public void WritePositionSequence(IReadOnlyList<GeoJsonPosition> positions)
        {
            _sb.Append('[');
            for (int i = 0; i < positions.Count; i++)
            {
                if (i > 0) _sb.Append(_indented ? ", " : ",");
                _sb.Append('[');
                AppendNumber(positions[i].Lon);
                _sb.Append(_indented ? ", " : ",");
                AppendNumber(positions[i].Lat);
                _sb.Append(']');
            }
            _sb.Append(']');
            _needsComma = true;
            _afterPropertyName = false;
        }

        /// <summary>Writes a position sequence as an element of the current array.</summary>
        public void WritePositionSequenceElement(IReadOnlyList<GeoJsonPosition> positions)
        {
            WriteCommaIfNeeded();
            WritePositionSequence(positions);
        }

        /// <summary>Begins an array that is an element of the current array.</summary>
        public void BeginArrayElement()
        {
            WriteCommaIfNeeded();
            BeginArray();
        }

        private void WriteCommaIfNeeded()
        {
            if (_needsComma)
            {
                _sb.Append(',');
                _needsComma = false;
            }
            if (_indented && _indentLevel > 0 && !_afterPropertyName)
            {
                _sb.Append('\n');
                WriteIndent();
            }
            _afterPropertyName = false;
        }

        /// <summary>
        /// Appends a coordinate or bbox number. RFC 8259 has no NaN/Infinity tokens and a position cannot be null, so a
        /// non-finite value here is an internal invariant violation (the mappers reject non-finite input): it is never
        /// written as a bare token.
        /// </summary>
        private void AppendNumber(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
                throw new InvalidOperationException("RFC 8259 cannot represent the non-finite number " + value.ToString(CultureInfo.InvariantCulture) + " in a GeoJSON position.");

            AppendRoundTrip(value);
        }

        /// <summary>Appends a property number; a non-finite value is written as <c>null</c> (RFC 8259 has no NaN/Infinity).</summary>
        private void AppendPropertyNumber(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                _sb.Append("null");
                return;
            }

            AppendRoundTrip(value);
        }

        /// <summary>
        /// Appends the shortest of <c>G15</c>, <c>G16</c> and <c>G17</c> that parses back to exactly <paramref name="value"/>
        /// (AUD-L9; <c>G17</c> always round-trips). Unlike <c>"R"</c>, whose output differs between .NET Framework and .NET,
        /// the digits are the same on every target framework (<c>G15</c> alone lost up to two digits). The one known difference
        /// is negative zero, which <c>ToString</c> writes as <c>-0</c> on .NET and as <c>0</c> on .NET Framework and Mono.
        /// </summary>
        private void AppendRoundTrip(double value)
        {
            string text = value.ToString("G15", CultureInfo.InvariantCulture);
            if (!ParsesBackTo(text, value))
            {
                text = value.ToString("G16", CultureInfo.InvariantCulture);
                if (!ParsesBackTo(text, value))
                {
                    text = value.ToString("G17", CultureInfo.InvariantCulture);
                }
            }

            _sb.Append(text);
        }

        /// <summary>
        /// Whether <paramref name="text"/> parses back to exactly <paramref name="value"/>. <c>TryParse</c> and not <c>Parse</c>: near
        /// <c>double.MaxValue</c> the <c>G15</c> text rounds above the maximum, which .NET Framework rejects with an
        /// <see cref="OverflowException"/> (newer runtimes return infinity); either way the text does not round-trip.
        /// </summary>
        private static bool ParsesBackTo(string text, double value) =>
            double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed) && parsed == value;

        private void WriteIndent()
        {
            _sb.Append(' ', _indentLevel * 2);
        }

        /// <summary>Writes a JSON string; <c>null</c> is written as the JSON <c>null</c> literal (AUD-L9).</summary>
        private void WriteEscapedString(string? value)
        {
            if (value == null)
            {
                _sb.Append("null");
                return;
            }

            _sb.Append('"');
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                switch (c)
                {
                    case '"':
                        _sb.Append("\\\"");
                        break;
                    case '\\':
                        _sb.Append("\\\\");
                        break;
                    case '\b':
                        _sb.Append("\\b");
                        break;
                    case '\f':
                        _sb.Append("\\f");
                        break;
                    case '\n':
                        _sb.Append("\\n");
                        break;
                    case '\r':
                        _sb.Append("\\r");
                        break;
                    case '\t':
                        _sb.Append("\\t");
                        break;
                    default:
                        // Control characters, and U+2028/U+2029, which are valid in JSON but terminate lines in JavaScript.
                        if (c < 0x20 || c == '\u2028' || c == '\u2029')
                        {
                            _sb.Append("\\u");
                            _sb.Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            _sb.Append(c);
                        }
                        break;
                }
            }
            _sb.Append('"');
        }
    }
}
