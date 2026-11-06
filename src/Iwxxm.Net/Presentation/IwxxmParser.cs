using System;
using System.IO;
using Iwxxm.Net.Application.Ports;
using Iwxxm.Net.Application.UseCases;
using Iwxxm.Net.Application.Ports.Out;
using Iwxxm.Net.Extensibility;
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
    /// <para>Instances are created by <see cref="IwxxmParserBuilder"/>. They are immutable and thread-safe; register one
    /// as a singleton. Each call runs the pipeline: bounded buffer, schema validation, business-rule validation, then
    /// polymorphic mapping to the Domain Aggregate Root.</para>
    /// <para>The static <c>Parse*</c> members are convenience wrappers over <see cref="Default"/>, an instance built with
    /// the default adapters and <see cref="IwxxmParserOptions.Default"/>.</para>
    /// <para><b>Error contract.</b> Every member returning <see cref="Result{T}"/> reports I/O and adapter failures as
    /// typed errors and never throws for them: <c>FILE-NOT-FOUND</c> (missing file or directory), <c>ACCESS-DENIED</c>
    /// (permission denied, or the path is a directory), <c>FILE-READ-ERROR</c> (sharing violation, path too long, malformed
    /// path, other I/O errors), <c>SCHEMA-VALIDATOR-ERROR</c>, <c>RULE-VALIDATOR-ERROR</c>, <c>MAPPER-ERROR</c> and
    /// <c>GEOJSON-EXPORT-ERROR</c> (an adapter threw or returned nothing). Only argument validation throws:
    /// <see cref="ArgumentNullException"/> for a <c>null</c> stream and <see cref="ArgumentException"/> for a <c>null</c>,
    /// empty or whitespace file path.</para>
    /// </remarks>
    public sealed class IwxxmParser
    {
        private static readonly Lazy<IwxxmParser> DefaultInstance =
            new Lazy<IwxxmParser>(() => new IwxxmParserBuilder().Build(), isThreadSafe: true);

        private readonly ISchemaValidator _schemaValidator;
        private readonly ISchematronValidator _schematronValidator;
        private readonly IIwxxmDocumentMapper _documentMapper;
        private readonly IGeoJsonExporter _geoJsonExporter;
        private readonly IDocumentParser _parser;
        private readonly IwxxmParserOptions _options;

        internal IwxxmParser(
            ISchemaValidator schemaValidator,
            ISchematronValidator schematronValidator,
            IIwxxmDocumentMapper documentMapper,
            IGeoJsonExporter geoJsonExporter,
            IDocumentParser parser,
            IwxxmParserOptions? options = null)
        {
            _schemaValidator = schemaValidator ?? throw new ArgumentNullException(nameof(schemaValidator));
            _schematronValidator = schematronValidator ?? throw new ArgumentNullException(nameof(schematronValidator));
            _documentMapper = documentMapper ?? throw new ArgumentNullException(nameof(documentMapper));
            _geoJsonExporter = geoJsonExporter ?? throw new ArgumentNullException(nameof(geoJsonExporter));
            _parser = parser ?? throw new ArgumentNullException(nameof(parser));
            _options = options ?? IwxxmParserOptions.Default;
        }

        /// <summary>
        /// Gets the shared instance built with the default adapters and <see cref="IwxxmParserOptions.Default"/>
        /// (created on first access, thread-safe).
        /// </summary>
        public static IwxxmParser Default => DefaultInstance.Value;

        // =====================================================================
        // Instance API
        // =====================================================================

        /// <summary>Validates and parses a stream containing any IWXXM 2025-2 report or bulletin.</summary>
        public Result<IwxxmDocument> Process(Stream xmlStream)
        {
            if (xmlStream == null)
                throw new ArgumentNullException(nameof(xmlStream));

            return ProcessBuffered(BoundedDocumentBuffer.Read(xmlStream, _options));
        }

        /// <summary>Validates and unwraps a <c>collect:MeteorologicalBulletin</c> stream.</summary>
        public Result<MeteorologicalBulletin> ProcessBulletin(Stream xmlStream) => AsBulletin(Process(xmlStream));

        /// <summary>Validates and parses already-decoded XML text.</summary>
        public Result<IwxxmDocument> Process(TextReader xmlText)
        {
            if (xmlText == null)
                throw new ArgumentNullException(nameof(xmlText));

            return ProcessBuffered(BoundedDocumentBuffer.ReadText(xmlText, _options, emptyCode: SharedErrorCodes.XmlEmptyString));
        }

        /// <summary>Validates and unwraps a bulletin from already-decoded XML text.</summary>
        public Result<MeteorologicalBulletin> ProcessBulletin(TextReader xmlText) => AsBulletin(Process(xmlText));

        /// <summary>
        /// Validates and parses any IWXXM 2025-2 report or <c>collect:MeteorologicalBulletin</c> from an XML string. The
        /// string is read as text: its declared encoding (e.g. <c>UTF-16</c>, <c>ISO-8859-1</c>) is ignored.
        /// </summary>
        public Result<IwxxmDocument> Process(string xmlContent) =>
            WithTextReader(xmlContent, Process);

        /// <summary>Validates and parses any IWXXM 2025-2 report or <c>collect:MeteorologicalBulletin</c> XML file from disk.</summary>
        public Result<IwxxmDocument> ProcessFile(string filePath) =>
            WithFileStream(filePath, "IWXXM file", Process);

        /// <summary>Validates and parses an XML stream expecting the aggregate type <typeparamref name="TDocument"/>.</summary>
        public Result<TDocument> ProcessAs<TDocument>(Stream xmlStream)
            where TDocument : IwxxmDocument =>
            CastReportResult<TDocument>(Process(xmlStream));

        /// <summary>Validates and parses an XML string expecting the aggregate type <typeparamref name="TDocument"/>.</summary>
        public Result<TDocument> ProcessAs<TDocument>(string xmlContent)
            where TDocument : IwxxmDocument =>
            CastReportResult<TDocument>(Process(xmlContent));

        /// <summary>Validates and parses an XML file expecting the aggregate type <typeparamref name="TDocument"/>.</summary>
        public Result<TDocument> ProcessFileAs<TDocument>(string filePath)
            where TDocument : IwxxmDocument =>
            CastReportResult<TDocument>(ProcessFile(filePath));

        /// <summary>Validates and unwraps a SWIM / AMHS <c>collect:MeteorologicalBulletin</c> from an XML string.</summary>
        public Result<MeteorologicalBulletin> ProcessBulletin(string xmlContent) =>
            WithTextReader(xmlContent, ProcessBulletin);

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
        public static Result<IwxxmDocument> Parse(Stream xmlStream) => Default.Process(xmlStream);

        /// <summary>Validates and parses any IWXXM 2025-2 report or bulletin from an XML string using <see cref="Default"/>.</summary>
        public static Result<IwxxmDocument> Parse(string xmlContent) => Default.Process(xmlContent);

        /// <summary>Validates and parses any IWXXM 2025-2 report or bulletin XML file using <see cref="Default"/>.</summary>
        public static Result<IwxxmDocument> ParseFile(string filePath) => Default.ProcessFile(filePath);

        /// <summary>Parses a stream expecting <typeparamref name="TDocument"/> using <see cref="Default"/>.</summary>
        public static Result<TDocument> ParseAs<TDocument>(Stream xmlStream)
            where TDocument : IwxxmDocument => Default.ProcessAs<TDocument>(xmlStream);

        /// <summary>Parses an XML string expecting <typeparamref name="TDocument"/> using <see cref="Default"/>.</summary>
        public static Result<TDocument> ParseAs<TDocument>(string xmlContent)
            where TDocument : IwxxmDocument => Default.ProcessAs<TDocument>(xmlContent);

        /// <summary>Parses an XML file expecting <typeparamref name="TDocument"/> using <see cref="Default"/>.</summary>
        public static Result<TDocument> ParseFileAs<TDocument>(string filePath)
            where TDocument : IwxxmDocument => Default.ProcessFileAs<TDocument>(filePath);

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

        private Result<IwxxmDocument> ProcessBuffered(Result<byte[]> buffered)
        {
            if (buffered.IsFailure)
            {
                return Result<IwxxmDocument>.Failure(buffered.Errors);
            }

            return ValidationPipeline.Run(buffered.Value, _options, _parser, _schemaValidator, _schematronValidator, document => _documentMapper.Map(document));
        }

        private static Result<MeteorologicalBulletin> AsBulletin(Result<IwxxmDocument> dispatchResult)
        {
            if (dispatchResult.IsFailure)
            {
                return Result<MeteorologicalBulletin>.Failure(dispatchResult.Errors);
            }

            if (dispatchResult.Value is MeteorologicalBulletin bulletin)
            {
                return Result<MeteorologicalBulletin>.Success(bulletin, dispatchResult.Warnings);
            }

            return Result<MeteorologicalBulletin>.Failure(
                "DISPATCH-NOT-A-BULLETIN",
                $"Expected a 'collect:MeteorologicalBulletin' document, but parsed '{dispatchResult.Value.ReportType}' ('{dispatchResult.Value.Id}').",
                ErrorCategory.Deserialization,
                dispatchResult.Value.Id);
        }

        private Result<string> ToGeoJsonResult(Result<IwxxmDocument> result, GeoJsonExportOptions? options)
        {
            if (result.IsFailure)
                return Result<string>.Failure(result.Errors);

            try
            {
                string json = _geoJsonExporter.Export(result.Value, options ?? GeoJsonExportOptions.Default);
                if (json == null)
                {
                    return Result<string>.Failure(
                        SharedErrorCodes.GeoJsonExportError,
                        "The GeoJSON exporter returned no document.",
                        ErrorCategory.DomainMapping,
                        result.Value.Id);
                }

                return Result<string>.Success(json, result.Warnings);
            }
            catch (Exception ex) when (!(ex is OutOfMemoryException))
            {
                return Result<string>.Failure(
                    SharedErrorCodes.GeoJsonExportError,
                    $"The GeoJSON export failed: {ex.Message}",
                    ErrorCategory.DomainMapping,
                    result.Value.Id);
            }
        }

        internal static Result<T> WithTextReader<T>(string xmlContent, Func<TextReader, Result<T>> process)
        {
            if (string.IsNullOrWhiteSpace(xmlContent))
            {
                return Result<T>.Failure(
                    SharedErrorCodes.XmlEmptyString,
                    "The provided XML string is null or empty.",
                    ErrorCategory.MalformedInput);
            }

            using (var reader = new StringReader(xmlContent))
            {
                return process(reader);
            }
        }

        internal static Result<T> WithFileStream<T>(string filePath, string description, Func<Stream, Result<T>> process)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("File path cannot be null or whitespace.", nameof(filePath));

            FileStream fileStream;
            try
            {
                fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            }
            catch (Exception ex) when (ex is FileNotFoundException || ex is DirectoryNotFoundException)
            {
                return FileFailure<T>("FILE-NOT-FOUND", $"The specified {description} does not exist: '{filePath}'.", filePath);
            }
            catch (UnauthorizedAccessException ex)
            {
                return FileFailure<T>("ACCESS-DENIED", $"Access to the specified {description} '{filePath}' is denied: {ex.Message}", filePath);
            }
            catch (Exception ex) when (ex is IOException || ex is NotSupportedException || ex is ArgumentException || ex is System.Security.SecurityException)
            {
                // IOException covers PathTooLongException and sharing violations; ArgumentException / NotSupportedException
                // cover malformed paths (the null / whitespace check above is the only argument validation that throws).
                return FileFailure<T>("FILE-READ-ERROR", $"The specified {description} '{filePath}' cannot be opened: {ex.Message}", filePath);
            }

            using (fileStream)
            {
                return process(fileStream);
            }
        }

        private static Result<T> FileFailure<T>(string code, string message, string filePath) =>
            Result<T>.Failure(new ValidationError(code, message, ErrorCategory.MalformedInput, ValidationSeverity.Error, filePath));

        private static Result<TDocument> CastReportResult<TDocument>(Result<IwxxmDocument> result)
            where TDocument : IwxxmDocument
        {
            if (result.IsFailure)
                return Result<TDocument>.Failure(result.Errors);

            if (!(result.Value is TDocument typed))
            {
                return Result<TDocument>.Failure(
                    "IWXXM-UNEXPECTED-REPORT-TYPE",
                    $"Expected report aggregate of type '{typeof(TDocument).Name}', but parsed '{result.Value.GetType().Name}' (ReportType: {result.Value.ReportType}).",
                    ErrorCategory.DomainMapping);
            }

            return Result<TDocument>.Success(typed, result.Warnings);
        }
    }
}
