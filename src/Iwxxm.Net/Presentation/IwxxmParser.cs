using System;
using System.IO;
using System.Text;
using Iwxxm.Net.Reports;

namespace Iwxxm.Net
{
    /// <summary>
    /// Public facade for validating (schemas + business rules + offline WMO code lists) and polymorphically parsing the
    /// complete ICAO/WMO IWXXM 2025-2 report suite (<c>METAR</c>, <c>SPECI</c>, <c>TAF</c>, <c>SIGMET</c>,
    /// <c>VolcanicAshSIGMET</c>, <c>TropicalCycloneSIGMET</c>, <c>AIRMET</c>, <c>VolcanoObservatoryNoticeForAviation</c> [VONA],
    /// <c>QuantitativeVolcanicAshConcentrationInformation</c> [QVACI], <c>VolcanicAshAdvisory</c>,
    /// <c>TropicalCycloneAdvisory</c>, <c>SpaceWeatherAdvisory</c>, <c>WAFSSignificantWeatherForecast</c>) as well as
    /// SWIM/AMHS <c>collect:MeteorologicalBulletin</c> containers.
    /// </summary>
    /// <remarks>
    /// <para>Instances are created by <see cref="IwxxmParserBuilder"/> (or directly from inbound ports) and depend only on
    /// the inbound ports <see cref="IIwxxmReportDispatcher"/> and <see cref="IGeoJsonExportService"/>. They are immutable
    /// and thread-safe; register one as a singleton.</para>
    /// <para>The static <c>Parse*</c> members are convenience wrappers over <see cref="Default"/>, an instance built with
    /// the default adapters and <see cref="IwxxmParserOptions.Default"/>.</para>
    /// </remarks>
    public sealed class IwxxmParser : IIwxxmReportDispatcher
    {
        private static readonly Lazy<IwxxmParser> DefaultInstance =
            new Lazy<IwxxmParser>(() => new IwxxmParserBuilder().Build(), isThreadSafe: true);

        private readonly IIwxxmReportDispatcher _dispatcher;
        private readonly IGeoJsonExportService _geoJsonExporter;

        /// <summary>
        /// Initializes a facade over the given inbound ports.
        /// </summary>
        /// <param name="dispatcher">Validation and mapping use case.</param>
        /// <param name="geoJsonExporter">GeoJSON export use case used by the <c>*ToGeoJson</c> members.</param>
        public IwxxmParser(IIwxxmReportDispatcher dispatcher, IGeoJsonExportService geoJsonExporter)
        {
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
            _geoJsonExporter = geoJsonExporter ?? throw new ArgumentNullException(nameof(geoJsonExporter));
        }

        /// <summary>
        /// Gets the shared instance built with the default adapters and <see cref="IwxxmParserOptions.Default"/>
        /// (created on first access, thread-safe).
        /// </summary>
        public static IwxxmParser Default => DefaultInstance.Value;

        // =====================================================================
        // Instance API (inbound ports)
        // =====================================================================

        /// <inheritdoc />
        public Result<IIwxxmReport> Process(Stream xmlStream)
        {
            if (xmlStream == null)
                throw new ArgumentNullException(nameof(xmlStream));

            return _dispatcher.Process(xmlStream);
        }

        /// <inheritdoc />
        public Result<MeteorologicalBulletin> ProcessBulletin(Stream xmlStream)
        {
            if (xmlStream == null)
                throw new ArgumentNullException(nameof(xmlStream));

            return _dispatcher.ProcessBulletin(xmlStream);
        }

        /// <summary>Validates and parses any IWXXM 2025-2 report or <c>collect:MeteorologicalBulletin</c> from an XML string.</summary>
        public Result<IIwxxmReport> Process(string xmlContent) =>
            WithUtf8Stream(xmlContent, Process);

        /// <summary>Validates and parses any IWXXM 2025-2 report or <c>collect:MeteorologicalBulletin</c> XML file from disk.</summary>
        public Result<IIwxxmReport> ProcessFile(string filePath) =>
            WithFileStream(filePath, "IWXXM file", Process);

        /// <summary>Validates and parses an XML stream expecting the aggregate type <typeparamref name="TReport"/>.</summary>
        public Result<TReport> ProcessAs<TReport>(Stream xmlStream)
            where TReport : class, IIwxxmReport =>
            CastReportResult<TReport>(Process(xmlStream));

        /// <summary>Validates and parses an XML string expecting the aggregate type <typeparamref name="TReport"/>.</summary>
        public Result<TReport> ProcessAs<TReport>(string xmlContent)
            where TReport : class, IIwxxmReport =>
            CastReportResult<TReport>(Process(xmlContent));

        /// <summary>Validates and parses an XML file expecting the aggregate type <typeparamref name="TReport"/>.</summary>
        public Result<TReport> ProcessFileAs<TReport>(string filePath)
            where TReport : class, IIwxxmReport =>
            CastReportResult<TReport>(ProcessFile(filePath));

        /// <summary>Validates and unwraps a SWIM / AMHS <c>collect:MeteorologicalBulletin</c> from an XML string.</summary>
        public Result<MeteorologicalBulletin> ProcessBulletin(string xmlContent) =>
            WithUtf8Stream(xmlContent, ProcessBulletin);

        /// <summary>Validates and unwraps a SWIM / AMHS <c>collect:MeteorologicalBulletin</c> XML file from disk.</summary>
        public Result<MeteorologicalBulletin> ProcessBulletinFile(string filePath) =>
            WithFileStream(filePath, "IWXXM bulletin file", ProcessBulletin);

        /// <summary>Validates, parses and exports a document from a stream to an RFC 7946 GeoJSON <c>FeatureCollection</c>.</summary>
        public Result<string> ProcessToGeoJson(Stream xmlStream, GeoJsonExportOptions? options = null) =>
            ToGeoJsonResult(Process(xmlStream), options);

        /// <summary>Validates, parses and exports a document from an XML string to an RFC 7946 GeoJSON <c>FeatureCollection</c>.</summary>
        public Result<string> ProcessToGeoJson(string xmlContent, GeoJsonExportOptions? options = null) =>
            ToGeoJsonResult(Process(xmlContent), options);

        /// <summary>Validates, parses and exports an XML file to an RFC 7946 GeoJSON <c>FeatureCollection</c>.</summary>
        public Result<string> ProcessFileToGeoJson(string filePath, GeoJsonExportOptions? options = null) =>
            ToGeoJsonResult(ProcessFile(filePath), options);

        // =====================================================================
        // Static convenience API (wrappers over Default)
        // =====================================================================

        /// <summary>Validates and parses any IWXXM 2025-2 report or bulletin from a stream using <see cref="Default"/>.</summary>
        public static Result<IIwxxmReport> Parse(Stream xmlStream) => Default.Process(xmlStream);

        /// <summary>Validates and parses any IWXXM 2025-2 report or bulletin from an XML string using <see cref="Default"/>.</summary>
        public static Result<IIwxxmReport> Parse(string xmlContent) => Default.Process(xmlContent);

        /// <summary>Validates and parses any IWXXM 2025-2 report or bulletin XML file using <see cref="Default"/>.</summary>
        public static Result<IIwxxmReport> ParseFile(string filePath) => Default.ProcessFile(filePath);

        /// <summary>Parses a stream expecting <typeparamref name="TReport"/> using <see cref="Default"/>.</summary>
        public static Result<TReport> ParseAs<TReport>(Stream xmlStream)
            where TReport : class, IIwxxmReport => Default.ProcessAs<TReport>(xmlStream);

        /// <summary>Parses an XML string expecting <typeparamref name="TReport"/> using <see cref="Default"/>.</summary>
        public static Result<TReport> ParseAs<TReport>(string xmlContent)
            where TReport : class, IIwxxmReport => Default.ProcessAs<TReport>(xmlContent);

        /// <summary>Parses an XML file expecting <typeparamref name="TReport"/> using <see cref="Default"/>.</summary>
        public static Result<TReport> ParseFileAs<TReport>(string filePath)
            where TReport : class, IIwxxmReport => Default.ProcessFileAs<TReport>(filePath);

        /// <summary>Unwraps a <c>collect:MeteorologicalBulletin</c> stream using <see cref="Default"/>.</summary>
        public static Result<MeteorologicalBulletin> ParseBulletin(Stream xmlStream) => Default.ProcessBulletin(xmlStream);

        /// <summary>Unwraps a <c>collect:MeteorologicalBulletin</c> XML string using <see cref="Default"/>.</summary>
        public static Result<MeteorologicalBulletin> ParseBulletin(string xmlContent) => Default.ProcessBulletin(xmlContent);

        /// <summary>Unwraps a <c>collect:MeteorologicalBulletin</c> XML file using <see cref="Default"/>.</summary>
        public static Result<MeteorologicalBulletin> ParseBulletinFile(string filePath) => Default.ProcessBulletinFile(filePath);

        /// <summary>Parses a stream and exports it to GeoJSON using <see cref="Default"/>.</summary>
        public static Result<string> ParseToGeoJson(Stream xmlStream, GeoJsonExportOptions? options = null) =>
            Default.ProcessToGeoJson(xmlStream, options);

        /// <summary>Parses an XML string and exports it to GeoJSON using <see cref="Default"/>.</summary>
        public static Result<string> ParseToGeoJson(string xmlContent, GeoJsonExportOptions? options = null) =>
            Default.ProcessToGeoJson(xmlContent, options);

        /// <summary>Parses an XML file and exports it to GeoJSON using <see cref="Default"/>.</summary>
        public static Result<string> ParseFileToGeoJson(string filePath, GeoJsonExportOptions? options = null) =>
            Default.ProcessFileToGeoJson(filePath, options);

        // =====================================================================
        // Helpers
        // =====================================================================

        private Result<string> ToGeoJsonResult(Result<IIwxxmReport> result, GeoJsonExportOptions? options) =>
            result.IsSuccess
                ? Result<string>.Success(_geoJsonExporter.Export(result.Value, options))
                : Result<string>.Failure(result.Errors);

        internal static Result<T> WithUtf8Stream<T>(string xmlContent, Func<Stream, Result<T>> process)
        {
            if (string.IsNullOrWhiteSpace(xmlContent))
            {
                return Result<T>.Failure(
                    "XML-EMPTY-STRING",
                    "The provided XML string is null or empty.",
                    ErrorCategory.MalformedInput);
            }

            byte[] utf8Bytes = Encoding.UTF8.GetBytes(xmlContent);
            using (var memoryStream = new MemoryStream(utf8Bytes, writable: false))
            {
                return process(memoryStream);
            }
        }

        internal static Result<T> WithFileStream<T>(string filePath, string description, Func<Stream, Result<T>> process)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("File path cannot be null or whitespace.", nameof(filePath));

            if (!File.Exists(filePath))
            {
                return Result<T>.Failure(
                    "FILE-NOT-FOUND",
                    $"The specified {description} does not exist: '{filePath}'.",
                    ErrorCategory.MalformedInput);
            }

            using (var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                return process(fileStream);
            }
        }

        private static Result<TReport> CastReportResult<TReport>(Result<IIwxxmReport> result)
            where TReport : class, IIwxxmReport
        {
            if (result.IsFailure)
                return Result<TReport>.Failure(result.Errors);

            if (!(result.Value is TReport typed))
            {
                return Result<TReport>.Failure(
                    "IWXXM-UNEXPECTED-REPORT-TYPE",
                    $"Expected report aggregate of type '{typeof(TReport).Name}', but parsed '{result.Value.GetType().Name}' (ReportType: {result.Value.ReportType}).",
                    ErrorCategory.DomainMapping);
            }

            return Result<TReport>.Success(typed);
        }
    }
}
