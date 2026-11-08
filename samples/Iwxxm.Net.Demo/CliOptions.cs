using System;
using System.Collections.Generic;

namespace Iwxxm.Net.Demo
{
    /// <summary>Command-line options of the demo. Parsing never throws; problems are returned as a message.</summary>
    internal sealed class CliOptions
    {
        public const string StdoutMarker = "-";

        public bool ShowHelp { get; private set; }

        public string? InputPath { get; private set; }

        /// <summary>True when GeoJSON output was requested (<c>--geojson</c> or <c>-o</c>).</summary>
        public bool ExportGeoJson { get; private set; }

        /// <summary>Explicit GeoJSON target; <see cref="StdoutMarker"/> means standard output; null means "next to the input".</summary>
        public string? GeoJsonPath { get; private set; }

        public bool PrettyGeoJson { get; private set; }

        public bool NoSchematron { get; private set; }

        public bool NoColor { get; private set; }

        public static bool TryParse(IReadOnlyList<string> args, out CliOptions options, out string? error)
        {
            options = new CliOptions();
            error = null;

            for (int i = 0; i < args.Count; i++)
            {
                string arg = args[i];
                switch (arg)
                {
                    case "-h":
                    case "--help":
                    case "-?":
                        options.ShowHelp = true;
                        return true;

                    case "--geojson":
                        options.ExportGeoJson = true;

                        // An optional value: a path, or "-" for stdout. Anything starting with '-' is the next option.
                        if (i + 1 < args.Count && (args[i + 1] == StdoutMarker || !args[i + 1].StartsWith("-", StringComparison.Ordinal)))
                        {
                            options.GeoJsonPath = args[++i];
                        }

                        break;

                    case "-o":
                    case "--output":
                        if (i + 1 >= args.Count)
                        {
                            error = $"Option '{arg}' needs a path (or '-' for standard output).";
                            return false;
                        }

                        options.ExportGeoJson = true;
                        options.GeoJsonPath = args[++i];
                        break;

                    case "--pretty":
                        options.PrettyGeoJson = true;
                        break;

                    case "--no-schematron":
                        options.NoSchematron = true;
                        break;

                    case "--no-color":
                        options.NoColor = true;
                        break;

                    default:
                        if (arg.Length > 1 && arg[0] == '-')
                        {
                            error = $"Unknown option '{arg}'.";
                            return false;
                        }

                        if (options.InputPath != null)
                        {
                            error = $"Unexpected extra argument '{arg}': only one input file is accepted.";
                            return false;
                        }

                        options.InputPath = arg;
                        break;
                }
            }

            if (string.IsNullOrWhiteSpace(options.InputPath))
            {
                error = "Missing input file. The input comes first: Iwxxm.Net.Demo <file.xml> [--geojson [path|-]] [options].";
                return false;
            }

            return true;
        }

        public string ResolveGeoJsonPath()
        {
            if (!string.IsNullOrEmpty(GeoJsonPath))
            {
                return GeoJsonPath!;
            }

            return System.IO.Path.ChangeExtension(InputPath!, ".geojson");
        }
    }
}
