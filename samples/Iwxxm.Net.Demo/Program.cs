using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using Iwxxm.Net;
using Iwxxm.Net.Reports;
using Iwxxm.Net.Validation.Saxon;

namespace Iwxxm.Net.Demo
{
    /// <summary>Exit codes of the demo.</summary>
    internal static class ExitCode
    {
        public const int Ok = 0;
        public const int ParseOrValidationErrors = 1;
        public const int UsageError = 2;
        public const int IoError = 3;
    }

    internal static class Program
    {
        private const string Usage =
@"Iwxxm.Net.Demo - parse an IWXXM 2025-2 document and print what the library found.

Usage:
  Iwxxm.Net.Demo <file.xml> [options]

Options:
  --geojson [path|-]   Also export RFC 7946 GeoJSON. Default target: the input path
                       with a .geojson extension; '-' writes the GeoJSON to stdout
                       (the human-readable report then goes to stderr).
  --csv [path|-]       Also export RFC 4180 CSV (with OGC WKT geometries). Default
                       target: the input path with a .csv extension; '-' writes
                       the CSV to stdout (the report then goes to stderr).
  -o, --output <path>  Same as --geojson <path> (use '-' for stdout).
  --pretty             Indent the GeoJSON.
  --no-schematron      Skip the built-in business rules (XSD validation only).
  --official-schematron
                       Run the complete official WMO Schematron (all 165 rules, SaxonCS-HE)
                       instead of the built-in subset. The first run compiles the rules
                       (about a second). Cannot be combined with --no-schematron.
  --no-color           Never use ANSI colours (also off when output is redirected
                       or NO_COLOR is set).
  -h, --help           Show this help.

Exit codes:
  0  parsed (warnings may be present) and, if requested, exported
  1  parse or validation errors, or the GeoJSON export failed, or the official
     Schematron engine could not be initialised (SCHEMATRON-UNAVAILABLE)
  2  usage error
  3  I/O error (input not found or unreadable, output not writable)
";

        private static int Main(string[] args)
        {
            try
            {
                return Run(args);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                Console.Error.WriteLine($"I/O error: {ex.Message}");
                return ExitCode.IoError;
            }
            catch (Exception ex)
            {
                // Last resort: the library reports bad input as values, so reaching this is a bug in the demo or the library.
                Console.Error.WriteLine($"Unexpected error ({ex.GetType().Name}): {ex.Message}");
                return ExitCode.ParseOrValidationErrors;
            }
        }

        private static int Run(string[] args)
        {
            if (!CliOptions.TryParse(args, out CliOptions options, out string? usageError))
            {
                Console.Error.WriteLine("Error: " + usageError);
                Console.Error.WriteLine("Run with --help for usage.");
                return ExitCode.UsageError;
            }

            if (options.ShowHelp)
            {
                Console.Out.Write(Usage);
                return ExitCode.Ok;
            }

            string path = options.InputPath!;
            bool anyToStdout =
                (options.ExportGeoJson && options.ResolveGeoJsonPath() == CliOptions.StdoutMarker) ||
                (options.ExportCsv && options.ResolveCsvPath() == CliOptions.StdoutMarker);

            // With "--geojson -" or "--csv -" stdout carries only the exported payload, so the human-readable report moves to stderr.
            TextWriter reportWriter = anyToStdout ? Console.Error : Console.Out;
            bool redirected = anyToStdout ? Console.IsErrorRedirected : Console.IsOutputRedirected;
            var report = new ConsoleReport(reportWriter, ConsoleReport.ShouldUseColor(redirected, options.NoColor));

            report.Banner("Iwxxm.Net demo");
            report.Info("Input", path);

            // One builder configures both the parser and the GeoJSON exporter.
            IwxxmParserBuilder builder = CreateBuilder(options);

            report.Info("Validation", DescribeValidation(options));
            Stopwatch clock = Stopwatch.StartNew();
            Result<IwxxmDocument> result;
            try
            {
                IwxxmParser parser = options.OfficialSchematron || options.NoSchematron ? builder.Build() : IwxxmParser.Default;
                result = parser.ProcessFile(path);
            }
            catch (Exception ex) when (options.OfficialSchematron && !(ex is OutOfMemoryException))
            {
                // The library reports problems as values; a throw here means the Saxon engine itself could not be loaded or
                // initialised (for example a missing SaxonCS assembly). Say so plainly: it is not an input or I/O problem.
                report.Verdict(false, $"FAILED: the official Schematron engine could not be initialised ({ex.GetType().Name}: {ex.Message}).");
                return ExitCode.ParseOrValidationErrors;
            }

            clock.Stop();
            if (options.OfficialSchematron)
            {
                report.Info("Parse + validate", $"{clock.Elapsed.TotalSeconds:F1} s (the first run includes compiling the official rules)");
            }

            if (result.IsFailure)
            {
                report.Diagnostics("Diagnostics", result.Errors, result.Warnings);
                report.Verdict(false, $"FAILED: {result.Errors.Count} error(s), {result.Warnings.Count} warning(s).");
                if (HasEngineFailure(result))
                {
                    Console.Error.WriteLine("The official Schematron engine failed (see the finding above); the document itself was not judged.");
                }

                return IsIoFailure(result) ? ExitCode.IoError : ExitCode.ParseOrValidationErrors;
            }

            IwxxmDocument document = result.Value;
            report.Sheet(DocumentSummaryVisitor.Summarize(document));
            report.Diagnostics("Diagnostics", result.Errors, result.Warnings);
            report.Verdict(true, $"OK: {document.ReportType} parsed, {result.Warnings.Count} warning(s).");

            if (options.ExportGeoJson)
            {
                int geoExit = ExportGeoJson(document, builder, options, report);
                if (geoExit != ExitCode.Ok)
                    return geoExit;
            }

            if (options.ExportCsv)
            {
                int csvExit = ExportCsv(document, builder, options, report);
                if (csvExit != ExitCode.Ok)
                    return csvExit;
            }

            return ExitCode.Ok;
        }

        /// <summary>
        /// Pipeline selection, as the guide prescribes: <see cref="IwxxmParser"/> reads every product, including the WAFS SIGWX
        /// forecast (both <c>iwxxm:WAFSSignificantWeatherForecast</c> and the compact <c>iwxxm:SIGWXReport</c>) and collective
        /// bulletins, so no fallback to <see cref="IwxxmSigwxParser"/> is needed. The default instance serves the common case;
        /// the builder is used only when a non-default pipeline was requested.
        /// </summary>
        private static IwxxmParserBuilder CreateBuilder(CliOptions options)
        {
            var builder = new IwxxmParserBuilder();
            if (options.NoSchematron)
            {
                builder.WithoutSchematron();
            }
            else if (options.OfficialSchematron)
            {
                // Extension method of Iwxxm.Net.Validation.Saxon: replaces the built-in subset with the 165 official rules.
                builder.UseOfficialSchematron();
            }

            return builder;
        }

        private static string DescribeValidation(CliOptions options)
        {
            if (options.NoSchematron)
            {
                return "XSD only (--no-schematron)";
            }

            return options.OfficialSchematron
                ? "XSD + official WMO Schematron (all 165 rules, SaxonCS-HE)"
                : "XSD + built-in business rules (subset: 11 of the 165 official rules)";
        }

        private static bool HasEngineFailure(Result<IwxxmDocument> result)
        {
            foreach (ValidationError error in result.Errors)
            {
                if (error.Code == OfficialSchematronValidator.UnavailableCode || error.Code == OfficialSchematronValidator.ExecutionErrorCode)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsIoFailure(Result<IwxxmDocument> result)
        {
            // ErrorCategory.Io groups the failures where the message was never read (missing file, no permission,
            // unreadable stream); IwxxmErrorCodes names the individual causes if a caller needs them.
            foreach (ValidationError error in result.Errors)
            {
                if (error.Category == ErrorCategory.Io)
                {
                    return true;
                }
            }

            return false;
        }

        private static int ExportGeoJson(IwxxmDocument document, IwxxmParserBuilder builder, CliOptions options, ConsoleReport report)
        {
            Result<string> exported = builder.BuildGeoJsonExporter().Export(
                document,
                options.PrettyGeoJson ? GeoJsonExportOptions.Pretty : GeoJsonExportOptions.Default);

            return WriteExport(
                "GeoJSON",
                options.ResolveGeoJsonPath(),
                options.InputPath!,
                exported,
                content => $"{CountFeatures(content)} feature(s)",
                report);
        }

        private static int ExportCsv(IwxxmDocument document, IwxxmParserBuilder builder, CliOptions options, ConsoleReport report)
        {
            Result<string> exported = builder.BuildCsvExporter().Export(document, CsvExportOptions.Default);

            return WriteExport(
                "CSV",
                options.ResolveCsvPath(),
                options.InputPath!,
                exported,
                content => $"{CountCsvDataRows(content)} row(s)",
                report);
        }

        private static int WriteExport(
            string label,
            string target,
            string inputPath,
            Result<string> exported,
            Func<string, string> describeCount,
            ConsoleReport report)
        {
            bool toStdout = target == CliOptions.StdoutMarker;
            if (!toStdout && Path.GetFullPath(target) == Path.GetFullPath(inputPath))
            {
                Console.Error.WriteLine($"Error: the {label} target is the input file; choose another path.");
                return ExitCode.UsageError;
            }

            if (exported.IsFailure)
            {
                report.Diagnostics($"{label} export", exported.Errors, exported.Warnings);
                report.Verdict(false, $"FAILED: {label} export.");
                return ExitCode.ParseOrValidationErrors;
            }

            string payload = exported.Value;
            string countSummary = describeCount(payload);
            var utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
            byte[] bytes = utf8NoBom.GetBytes(payload);

            if (toStdout)
            {
                using Stream stdout = Console.OpenStandardOutput();
                stdout.Write(bytes, 0, bytes.Length);
                stdout.Flush();
                report.Info(label, $"{countSummary} written to standard output ({bytes.Length} bytes)");
                return ExitCode.Ok;
            }

            File.WriteAllBytes(target, bytes);
            report.Info(label, $"{countSummary} written to {target} ({bytes.Length} bytes)");
            return ExitCode.Ok;
        }

        private static int CountCsvDataRows(string csv)
        {
            int lines = 0;
            for (int i = 0; i < csv.Length; i++)
            {
                if (csv[i] == '\n')
                    lines++;
            }

            // First line is the header row.
            return Math.Max(0, lines - 1);
        }

        private static int CountFeatures(string geoJson)
        {
            using JsonDocument parsed = JsonDocument.Parse(geoJson);
            return parsed.RootElement.TryGetProperty("features", out JsonElement features) && features.ValueKind == JsonValueKind.Array
                ? features.GetArrayLength()
                : 0;
        }
    }
}
