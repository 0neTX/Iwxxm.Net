using System;
using System.Collections.Generic;
using System.IO;
using Iwxxm.Net;

namespace Iwxxm.Net.Demo
{
    /// <summary>Renders fact sheets and diagnostics to a text writer, with optional ANSI colours.</summary>
    internal sealed class ConsoleReport
    {
        private const int LabelWidth = 26;
        private const string Reset = "\u001b[0m";
        private const string Bold = "\u001b[1;36m";
        private const string Dim = "\u001b[90m";
        private const string Red = "\u001b[31m";
        private const string Yellow = "\u001b[33m";
        private const string Green = "\u001b[32m";

        private readonly TextWriter _out;
        private readonly bool _color;

        public ConsoleReport(TextWriter output, bool color)
        {
            _out = output;
            _color = color;
        }

        /// <summary>Colours only when the target is a terminal, NO_COLOR is unset and the user did not opt out.</summary>
        public static bool ShouldUseColor(bool streamIsRedirected, bool optedOut) =>
            !optedOut
            && !streamIsRedirected
            && string.IsNullOrEmpty(Environment.GetEnvironmentVariable("NO_COLOR"))
            && !string.Equals(Environment.GetEnvironmentVariable("TERM"), "dumb", StringComparison.Ordinal);

        public void Banner(string text)
        {
            _out.WriteLine(Paint(Bold, "== " + text + " " + new string('=', Math.Max(3, 70 - text.Length))));
        }

        public void Info(string label, string value)
        {
            _out.WriteLine($"{Paint(Dim, (label + ":").PadRight(LabelWidth))}{value}");
        }

        public void Sheet(FactSheet sheet)
        {
            foreach (FactLine line in sheet.Lines)
            {
                string indent = new string(' ', 2 * line.Level);
                if (line.Value == null)
                {
                    if (line.Level == 0)
                    {
                        _out.WriteLine();
                    }

                    _out.WriteLine(indent + Paint(Bold, line.Label));
                }
                else
                {
                    string label = line.Label + ":";
                    int width = Math.Max(label.Length + 1, LabelWidth - 2 * line.Level);
                    _out.WriteLine(indent + Paint(Dim, label.PadRight(width)) + line.Value);
                }
            }
        }

        /// <summary>Prints every error and warning with code, severity, category and location.</summary>
        public void Diagnostics(string heading, IReadOnlyList<ValidationError> errors, IReadOnlyList<ValidationError> warnings)
        {
            _out.WriteLine();
            _out.WriteLine(Paint(Bold, heading));
            Group("Errors", errors, Red);
            Group("Warnings", warnings, Yellow);
        }

        public void Verdict(bool ok, string text)
        {
            _out.WriteLine();
            _out.WriteLine(Paint(ok ? Green : Red, text));
        }

        private void Group(string title, IReadOnlyList<ValidationError> items, string color)
        {
            _out.WriteLine($"  {title}: {items.Count}");
            foreach (ValidationError e in items)
            {
                string position = e.LineNumber.HasValue ? $"line {e.LineNumber}, pos {e.LinePosition?.ToString() ?? Fmt.Dash}" : Fmt.Dash;
                _out.WriteLine($"    {Paint(color, e.Severity.ToString().ToUpperInvariant().PadRight(7))} {Paint(color, e.Code)}  ({e.Category})");
                _out.WriteLine($"      location: {Fmt.Or(e.Location)}  position: {position}");
                _out.WriteLine($"      {e.Message}");
            }
        }

        private string Paint(string color, string text) => _color ? color + text + Reset : text;
    }
}
