using System;
using System.IO;
using System.Text;
using System.Text.Json;
using Iwxxm.Net;
using Iwxxm.Net.Reports;

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
  -o, --output <path>  Same as --geojson <path> (use '-' for stdout).
  --pretty             Indent the GeoJSON.
  --no-schematron      Skip the built-in business rules (XSD validation only).
  --no-color           Never use ANSI colours (also off when output is redirected
                       or NO_COLOR is set).
  -h, --help           Show this help.

Exit codes:
  0  parsed (warnings may be present) and, if requested, exported
  1  parse or validation errors, or the GeoJSON export failed
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
            bool geoJsonToStdout = options.ExportGeoJson && options.ResolveGeoJsonPath() == CliOptions.StdoutMarker;

            // With "--geojson -" stdout carries only GeoJSON, so the human-readable report moves to stderr.
            TextWriter reportWriter = geoJsonToStdout ? Console.Error : Console.Out;
            bool redirected = geoJsonToStdout ? Console.IsErrorRedirected : Console.IsOutputRedirected;
            var report = new ConsoleReport(reportWriter, ConsoleReport.ShouldUseColor(redirected, options.NoColor));

            report.Banner("Iwxxm.Net demo");
            report.Info("Input", path);

            Result<IwxxmDocument> result = Parse(path, options.NoSchematron);
            report.Info("Validation", options.NoSchematron ? "XSD only (--no-schematron)" : "XSD + built-in business rules");

            if (result.IsFailure)
            {
                report.Diagnostics("Diagnostics", result.Errors, result.Warnings);
                report.Verdict(false, $"FAILED: {result.Errors.Count} error(s), {result.Warnings.Count} warning(s).");
                return IsIoFailure(result) ? ExitCode.IoError : ExitCode.ParseOrValidationErrors;
            }

            IwxxmDocument document = result.Value;
            report.Sheet(DocumentSummaryVisitor.Summarize(document));
            report.Diagnostics("Diagnostics", result.Errors, result.Warnings);
            report.Verdict(true, $"OK: {document.ReportType} parsed, {result.Warnings.Count} warning(s).");

            return options.ExportGeoJson ? ExportGeoJson(document, options, report) : ExitCode.Ok;
        }

        /// <summary>
        /// Entry-point selection, as the guide prescribes: <see cref="IwxxmParser"/> reads every product, including the WAFS SIGWX
        /// forecast (both <c>iwxxm:WAFSSignificantWeatherForecast</c> and the compact <c>iwxxm:SIGWXReport</c>) and collective
        /// bulletins, so no fallback to <see cref="IwxxmSigwxParser"/> is needed. That one only matters if you want the
        /// SIGWX-only pipeline or a typed <c>Result&lt;Sigwx&gt;</c> directly.
        /// </summary>
        private static Result<IwxxmDocument> Parse(string path, bool noSchematron)
        {
            // The default instance for the common case; the builder only when a non-default pipeline was requested.
            IwxxmParser parser = noSchematron
                ? new IwxxmParserBuilder().WithoutSchematron().Build()
                : IwxxmParser.Default;

            return parser.ProcessFile(path);
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

        private static int ExportGeoJson(IwxxmDocument document, CliOptions options, ConsoleReport report)
        {
            string target = options.ResolveGeoJsonPath();
            bool toStdout = target == CliOptions.StdoutMarker;

            if (!toStdout && Path.GetFullPath(target) == Path.GetFullPath(options.InputPath!))
            {
                Console.Error.WriteLine("Error: the GeoJSON target is the input file; choose another path with -o.");
                return ExitCode.UsageError;
            }

            string json;
            try
            {
                // Export takes the already-parsed document (no second parse) and throws on an adapter failure.
                json = IwxxmGeoJsonExporter.ToGeoJson(document, options.PrettyGeoJson ? GeoJsonExportOptions.Pretty : GeoJsonExportOptions.Default);
            }
            catch (Exception ex)
            {
                report.Diagnostics("GeoJSON export", new[]
                {
                    new ValidationError("GEOJSON-EXPORT-ERROR", $"{ex.GetType().Name}: {ex.Message}", ErrorCategory.DomainMapping),
                }, Array.Empty<ValidationError>());
                report.Verdict(false, "FAILED: GeoJSON export.");
                return ExitCode.ParseOrValidationErrors;
            }

            int features = CountFeatures(json);
            var utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
            byte[] bytes = utf8NoBom.GetBytes(json);

            if (toStdout)
            {
                using Stream stdout = Console.OpenStandardOutput();
                stdout.Write(bytes, 0, bytes.Length);
                stdout.Flush();
                report.Info("GeoJSON", $"{features} feature(s) written to standard output ({bytes.Length} bytes)");
                return ExitCode.Ok;
            }

            File.WriteAllBytes(target, bytes);
            report.Info("GeoJSON", $"{features} feature(s) written to {target} ({bytes.Length} bytes)");
            return ExitCode.Ok;
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
