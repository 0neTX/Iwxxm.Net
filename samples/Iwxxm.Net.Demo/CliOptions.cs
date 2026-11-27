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

        /// <summary>True when CSV output was requested (<c>--csv</c>).</summary>
        public bool ExportCsv { get; private set; }

        /// <summary>Explicit CSV target; <see cref="StdoutMarker"/> means standard output; null means "next to the input".</summary>
        public string? CsvPath { get; private set; }

        /// <summary>True when GeoPackage output was requested (<c>--geopackage</c>).</summary>
        public bool ExportGeoPackage { get; private set; }

        /// <summary>Explicit GeoPackage target; null means "next to the input". Standard output is not supported (a GeoPackage is a SQLite file).</summary>
        public string? GeoPackagePath { get; private set; }

        /// <summary>True when the GeoPackage is written without the <c>gpkg_schema</c> extension (<c>--geopackage-no-schema</c>).</summary>
        public bool GeoPackageWithoutSchema { get; private set; }

        public bool NoSchematron { get; private set; }

        /// <summary>True when the complete official WMO Schematron (Iwxxm.Net.Validation.Saxon) replaces the built-in subset.</summary>
        public bool OfficialSchematron { get; private set; }

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
                        options.GeoJsonPath = TryReadOptionalValue(args, ref i);
                        break;

                    case "--csv":
                        options.ExportCsv = true;
                        options.CsvPath = TryReadOptionalValue(args, ref i);
                        break;

                    case "--geopackage":
                        options.ExportGeoPackage = true;
                        options.GeoPackagePath = TryReadOptionalValue(args, ref i);
                        break;

                    case "--geopackage-no-schema":
                        options.GeoPackageWithoutSchema = true;
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

                    case "--official-schematron":
                        options.OfficialSchematron = true;
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

            if (options.NoSchematron && options.OfficialSchematron)
            {
                error = "Options '--no-schematron' and '--official-schematron' are mutually exclusive.";
                return false;
            }

            if (options.ExportGeoJson && options.ExportCsv &&
                options.GeoJsonPath == StdoutMarker && options.CsvPath == StdoutMarker)
            {
                error = "Options '--geojson -' and '--csv -' cannot both write to standard output.";
                return false;
            }

            if (options.GeoPackageWithoutSchema && !options.ExportGeoPackage)
            {
                error = "Option '--geopackage-no-schema' needs '--geopackage'.";
                return false;
            }

            if (options.ExportGeoPackage && options.GeoPackagePath == StdoutMarker)
            {
                error = "Option '--geopackage' does not support standard output ('-'): a GeoPackage is a SQLite file and needs a file path.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(options.InputPath))
            {
                error = "Missing input file. The input comes first: Iwxxm.Net.Demo <file.xml> [--geojson [path|-]] [options].";
                return false;
            }

            return true;
        }

        public string ResolveGeoJsonPath() => ResolveOutputPath(GeoJsonPath, ".geojson");

        public string ResolveCsvPath() => ResolveOutputPath(CsvPath, ".csv");

        public string ResolveGeoPackagePath() => ResolveOutputPath(GeoPackagePath, ".gpkg");

        private string ResolveOutputPath(string? explicitPath, string extension) =>
            !string.IsNullOrEmpty(explicitPath)
                ? explicitPath!
                : System.IO.Path.ChangeExtension(InputPath!, extension);

        private static string? TryReadOptionalValue(IReadOnlyList<string> args, ref int index)
        {
            if (index + 1 < args.Count && (args[index + 1] == StdoutMarker || !args[index + 1].StartsWith("-", StringComparison.Ordinal)))
            {
                return args[++index];
            }

            return null;
        }
    }
}
