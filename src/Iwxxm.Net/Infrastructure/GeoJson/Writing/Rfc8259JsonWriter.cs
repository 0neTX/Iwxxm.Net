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
            _sb.Append(value.ToString("G15", CultureInfo.InvariantCulture));
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
            _sb.Append(value.ToString("G15", CultureInfo.InvariantCulture));
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
                case IReadOnlyList<string> strList:
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
        public void WriteCoordinatePair(GeoCoordinate coord)
        {
            _sb.Append('[');
            _sb.Append(coord.Lon.ToString("G15", CultureInfo.InvariantCulture));
            _sb.Append(_indented ? ", " : ",");
            _sb.Append(coord.Lat.ToString("G15", CultureInfo.InvariantCulture));
            _sb.Append(']');
            _needsComma = true;
            _afterPropertyName = false;
        }

        public void WriteCoordinateSequence(IReadOnlyList<GeoCoordinate> coords)
        {
            _sb.Append('[');
            for (int i = 0; i < coords.Count; i++)
            {
                if (i > 0) _sb.Append(_indented ? ", " : ",");
                _sb.Append('[');
                _sb.Append(coords[i].Lon.ToString("G15", CultureInfo.InvariantCulture));
                _sb.Append(_indented ? ", " : ",");
                _sb.Append(coords[i].Lat.ToString("G15", CultureInfo.InvariantCulture));
                _sb.Append(']');
            }
            _sb.Append(']');
            _needsComma = true;
            _afterPropertyName = false;
        }

        public void WriteCoordinateRingElement(IReadOnlyList<GeoCoordinate> ring)
        {
            WriteCommaIfNeeded();
            WriteCoordinateSequence(ring);
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

        private void WriteIndent()
        {
            _sb.Append(' ', _indentLevel * 2);
        }

        private void WriteEscapedString(string value)
        {
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
                        if (c < 0x20)
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
