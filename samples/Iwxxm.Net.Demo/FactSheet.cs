using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Iwxxm.Net.Reports;

namespace Iwxxm.Net.Demo
{
    /// <summary>One printable line: a heading (no value) or a label with a value, at an indentation level.</summary>
    internal readonly struct FactLine
    {
        public FactLine(int level, string label, string? value)
        {
            Level = level;
            Label = label;
            Value = value;
        }

        public int Level { get; }

        public string Label { get; }

        /// <summary>Null for a heading.</summary>
        public string? Value { get; }
    }

    /// <summary>
    /// A flat, I/O-free description of a document. The visitor fills it; <see cref="ConsoleReport"/> renders it.
    /// Missing values are rendered as <see cref="Fmt.Dash"/>, never as an invented default.
    /// </summary>
    internal sealed class FactSheet
    {
        private readonly List<FactLine> _lines = new List<FactLine>();
        private int _level;

        public IReadOnlyList<FactLine> Lines => _lines;

        public FactSheet Heading(string text)
        {
            _lines.Add(new FactLine(_level, text, null));
            return this;
        }

        public FactSheet Add(string label, string? value)
        {
            _lines.Add(new FactLine(_level, label, string.IsNullOrWhiteSpace(value) ? Fmt.Dash : value));
            return this;
        }

        /// <summary>Runs <paramref name="body"/> one indentation level deeper.</summary>
        public FactSheet Nest(Action body)
        {
            _level++;
            try
            {
                body();
            }
            finally
            {
                _level--;
            }

            return this;
        }

        /// <summary>Appends another sheet, one level deeper than the current position.</summary>
        public FactSheet Append(FactSheet other)
        {
            foreach (FactLine line in other._lines)
            {
                _lines.Add(new FactLine(line.Level + _level + 1, line.Label, line.Value));
            }

            return this;
        }

        /// <summary>Appends another sheet at the current level.</summary>
        public FactSheet AppendFlat(FactSheet other)
        {
            foreach (FactLine line in other._lines)
            {
                _lines.Add(new FactLine(line.Level + _level, line.Label, line.Value));
            }

            return this;
        }
    }

    /// <summary>Text formatting of the library's value objects. Null always becomes <see cref="Dash"/>.</summary>
    internal static class Fmt
    {
        public const string Dash = "-";

        public static string Or(string? text) => string.IsNullOrWhiteSpace(text) ? Dash : text!;

        public static string Time(DateTimeOffset? time) =>
            time.HasValue
                ? time.Value.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture)
                : Dash;

        public static string Period(DateTimeOffset? from, DateTimeOffset? until) => $"{Time(from)} -> {Time(until)}";

        public static string Number(double? value, string unit = "") =>
            value.HasValue ? value.Value.ToString("0.###", CultureInfo.InvariantCulture) + unit : Dash;

        public static string Yes(bool value) => value ? "yes" : "no";

        /// <summary>Value and UCUM unit as reported; a conversion to knots is added when the unit is not knots.</summary>
        public static string SpeedText(Speed? speed)
        {
            if (!(speed is Speed s))
            {
                return Dash;
            }

            string text = $"{s.Amount.ToString("0.###", CultureInfo.InvariantCulture)} {s.UnitOfMeasure}";
            return s.Unit == SpeedUnit.Knots
                ? text
                : text + $" (~{s.ToKnots().ToString("0.#", CultureInfo.InvariantCulture)} kt)";
        }

        /// <summary>Uses the value object's own ToString (Length, VerticalLimit, GeoCoordinate, CodeListValue, ...).</summary>
        public static string Text(object? value) => value?.ToString() ?? Dash;

        public static string Operator(RelationalOperator? op) =>
            op switch
            {
                RelationalOperator.Above => ">",
                RelationalOperator.Below => "<",
                _ => string.Empty,
            };

        public static string WithOperator(RelationalOperator? op, double? value, string unit) =>
            value.HasValue ? Operator(op) + Number(value, unit) : Dash;

        public static string List(IEnumerable<string> items)
        {
            string joined = string.Join(", ", items);
            return joined.Length == 0 ? Dash : joined;
        }

        public static string Levels(VerticalLimit? lower, VerticalLimit? upper) => $"{Text(lower)} -> {Text(upper)}";

        /// <summary>A geometry summary: GeoSurface is a closed union of polygon and circle, consumed with Match.</summary>
        public static string Surface(GeoSurface? surface) =>
            surface == null
                ? Dash
                : surface.Match(
                    polygon => Polygon(polygon),
                    circle => $"circle, radius {circle.Radius} around {circle.Centre}");

        public static string Polygon(GeoPolygon? polygon)
        {
            if (polygon == null)
            {
                return Dash;
            }

            IReadOnlyList<GeoCoordinate> ring = polygon.ExteriorRing;
            string holes = polygon.InteriorRings.Count > 0 ? $", {polygon.InteriorRings.Count} hole(s)" : string.Empty;
            if (ring.Count == 0)
            {
                return "polygon, no vertices";
            }

            double minLat = ring.Min(c => c.Lat);
            double maxLat = ring.Max(c => c.Lat);
            double minLon = ring.Min(c => c.Lon);
            double maxLon = ring.Max(c => c.Lon);
            string wide = maxLon - minLon > 180 ? " [raw min/max: may cross +-180 or enclose a pole]" : string.Empty;
            return $"polygon, {ring.Count} vertices{holes}, lat {Number(minLat)}..{Number(maxLat)}, lon {Number(minLon)}..{Number(maxLon)}{wide}";
        }
    }
}
