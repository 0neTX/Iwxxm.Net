using System;
using System.IO;
using System.Text;
using Iwxxm.Net.Application.Ports;
using Iwxxm.Net.Application.UseCases;
using Iwxxm.Net.Domain.Aggregates;
using Iwxxm.Net.Domain.Common;
using Iwxxm.Net.Infrastructure.Mapping;
using Iwxxm.Net.Infrastructure.Validation;

namespace Iwxxm.Net.Presentation
{
    /// <summary>
    /// Public facade and composition root for dual-validating (XSD + Schematron XSLT + Offline WMO RDF Codelists)
    /// and polymorphically parsing the complete ICAO/WMO IWXXM 2025-2 report suite (<c>METAR</c>, <c>SPECI</c>,
    /// <c>TAF</c>, <c>SIGMET</c>, <c>VolcanicAshSIGMET</c>, <c>TropicalCycloneSIGMET</c>, <c>AIRMET</c>,
    /// <c>VolcanoObservatoryNoticeForAviation</c> [VONA], <c>QuantitativeVolcanicAshConcentrationInformation</c> [QVACI],
    /// <c>VolcanicAshAdvisory</c>, <c>TropicalCycloneAdvisory</c>, <c>SpaceWeatherAdvisory</c>,
    /// <c>WAFSSignificantWeatherForecast</c>) as well as SWIM/AMHS <c>collect:MeteorologicalBulletin</c> containers.
    /// </summary>
    public sealed class IwxxmParser : IIwxxmReportDispatcher
    {
        private static readonly Lazy<IIwxxmReportDispatcher> DefaultDispatcherInstance =
            new Lazy<IIwxxmReportDispatcher>(() => CreateDispatcher(), isThreadSafe: true);

        private readonly IIwxxmReportDispatcher _innerDispatcher;

        /// <summary>
        /// Initializes a new instance of <see cref="IwxxmParser"/> wired with the default full-suite
        /// embedded XSD validator, Schematron XSLT validator (with offline WMO RDF code-list lookup),
        /// and polymorphic IWXXM 2025-2 mapper.
        /// </summary>
        public IwxxmParser(IwxxmParserOptions? options = null)
        {
            _innerDispatcher = options == null
                ? DefaultDispatcherInstance.Value
                : CreateDispatcher(options);
        }

        /// <summary>
        /// Creates and returns an instance of <see cref="IIwxxmReportDispatcher"/> suitable for singleton
        /// registration in Dependency Injection containers (.NET 8 IServiceCollection, Autofac, etc.).
        /// </summary>
        public static IIwxxmReportDispatcher CreateDispatcher(IwxxmParserOptions? options = null)
        {
            var effectiveOptions = options ?? IwxxmParserOptions.Default;
            return new DispatchIwxxmReportUseCase(
                xsdValidator: XsdValidator.CreateFullSuiteValidator(effectiveOptions),
                schematronValidator: new XsltSchematronValidator(effectiveOptions),
                documentMapper: new IwxxmSuiteMapper(effectiveOptions),
                options: effectiveOptions);
        }

        /// <summary>
        /// Validates (XSD + Schematron XSLT) and polymorphically parses any IWXXM 2025-2 report
        /// or <c>collect:MeteorologicalBulletin</c> from an input <see cref="Stream"/>.
        /// </summary>
        public static Result<IIwxxmReport> Parse(Stream xmlStream)
        {
            if (xmlStream == null)
                throw new ArgumentNullException(nameof(xmlStream));

            return DefaultDispatcherInstance.Value.Process(xmlStream);
        }

        /// <summary>
        /// Validates (XSD + Schematron XSLT) and polymorphically parses any IWXXM 2025-2 report
        /// or <c>collect:MeteorologicalBulletin</c> from an XML string.
        /// </summary>
        public static Result<IIwxxmReport> Parse(string xmlContent)
        {
            if (string.IsNullOrWhiteSpace(xmlContent))
            {
                return Result<IIwxxmReport>.Failure(
                    "XML-EMPTY-STRING",
                    "The provided XML string is null or empty.",
                    ErrorCategory.XmlWellFormedness);
            }

            byte[] utf8Bytes = Encoding.UTF8.GetBytes(xmlContent);
            using (var memoryStream = new MemoryStream(utf8Bytes, writable: false))
            {
                return DefaultDispatcherInstance.Value.Process(memoryStream);
            }
        }

        /// <summary>
        /// Validates (XSD + Schematron XSLT) and polymorphically parses any IWXXM 2025-2 report
        /// or <c>collect:MeteorologicalBulletin</c> XML file from disk.
        /// </summary>
        public static Result<IIwxxmReport> ParseFile(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("File path cannot be null or whitespace.", nameof(filePath));

            if (!File.Exists(filePath))
            {
                return Result<IIwxxmReport>.Failure(
                    "FILE-NOT-FOUND",
                    $"The specified IWXXM file does not exist: '{filePath}'.",
                    ErrorCategory.XmlWellFormedness);
            }

            using (var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                return DefaultDispatcherInstance.Value.Process(fileStream);
            }
        }

        /// <summary>
        /// Validates and parses an IWXXM 2025-2 XML stream expecting a specific aggregate type <typeparamref name="TReport"/>.
        /// Returns a domain mapping failure if the root element resolves to a different IWXXM report type.
        /// </summary>
        public static Result<TReport> ParseAs<TReport>(Stream xmlStream)
            where TReport : class, IIwxxmReport
        {
            var result = Parse(xmlStream);
            return CastReportResult<TReport>(result);
        }

        /// <summary>
        /// Validates and parses an IWXXM 2025-2 XML string expecting a specific aggregate type <typeparamref name="TReport"/>.
        /// Returns a domain mapping failure if the root element resolves to a different IWXXM report type.
        /// </summary>
        public static Result<TReport> ParseAs<TReport>(string xmlContent)
            where TReport : class, IIwxxmReport
        {
            var result = Parse(xmlContent);
            return CastReportResult<TReport>(result);
        }

        /// <summary>
        /// Validates and parses an IWXXM 2025-2 XML file expecting a specific aggregate type <typeparamref name="TReport"/>.
        /// Returns a domain mapping failure if the root element resolves to a different IWXXM report type.
        /// </summary>
        public static Result<TReport> ParseFileAs<TReport>(string filePath)
            where TReport : class, IIwxxmReport
        {
            var result = ParseFile(filePath);
            return CastReportResult<TReport>(result);
        }

        /// <summary>
        /// Validates and unwraps a SWIM / AMHS <c>collect:MeteorologicalBulletin</c> from an XML stream.
        /// </summary>
        public static Result<MeteorologicalBulletin> ParseBulletin(Stream xmlStream)
        {
            if (xmlStream == null)
                throw new ArgumentNullException(nameof(xmlStream));

            return DefaultDispatcherInstance.Value.ProcessBulletin(xmlStream);
        }

        /// <summary>
        /// Validates and unwraps a SWIM / AMHS <c>collect:MeteorologicalBulletin</c> from an XML string.
        /// </summary>
        public static Result<MeteorologicalBulletin> ParseBulletin(string xmlContent)
        {
            if (string.IsNullOrWhiteSpace(xmlContent))
            {
                return Result<MeteorologicalBulletin>.Failure(
                    "XML-EMPTY-STRING",
                    "The provided XML string is null or empty.",
                    ErrorCategory.XmlWellFormedness);
            }

            byte[] utf8Bytes = Encoding.UTF8.GetBytes(xmlContent);
            using (var memoryStream = new MemoryStream(utf8Bytes, writable: false))
            {
                return DefaultDispatcherInstance.Value.ProcessBulletin(memoryStream);
            }
        }

        /// <summary>
        /// Validates and unwraps a SWIM / AMHS <c>collect:MeteorologicalBulletin</c> XML file from disk.
        /// </summary>
        public static Result<MeteorologicalBulletin> ParseBulletinFile(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("File path cannot be null or whitespace.", nameof(filePath));

            if (!File.Exists(filePath))
            {
                return Result<MeteorologicalBulletin>.Failure(
                    "FILE-NOT-FOUND",
                    $"The specified IWXXM bulletin file does not exist: '{filePath}'.",
                    ErrorCategory.XmlWellFormedness);
            }

            using (var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                return DefaultDispatcherInstance.Value.ProcessBulletin(fileStream);
            }
        }

        /// <summary>
        /// Validates, parses, and exports any IWXXM 2025-2 report or <c>collect:MeteorologicalBulletin</c>
        /// from an XML stream directly to an RFC 7946 GeoJSON <c>FeatureCollection</c> string.
        /// </summary>
        public static Result<string> ParseToGeoJson(Stream xmlStream, GeoJsonExportOptions? options = null)
        {
            var result = Parse(xmlStream);
            return result.IsSuccess
                ? Result<string>.Success(IwxxmGeoJsonExporter.ToGeoJson(result.Value, options))
                : Result<string>.Failure(result.Errors);
        }

        /// <summary>
        /// Validates, parses, and exports any IWXXM 2025-2 report or <c>collect:MeteorologicalBulletin</c>
        /// from an XML string directly to an RFC 7946 GeoJSON <c>FeatureCollection</c> string.
        /// </summary>
        public static Result<string> ParseToGeoJson(string xmlContent, GeoJsonExportOptions? options = null)
        {
            var result = Parse(xmlContent);
            return result.IsSuccess
                ? Result<string>.Success(IwxxmGeoJsonExporter.ToGeoJson(result.Value, options))
                : Result<string>.Failure(result.Errors);
        }

        /// <summary>
        /// Validates, parses, and exports any IWXXM 2025-2 report or <c>collect:MeteorologicalBulletin</c>
        /// XML file from disk directly to an RFC 7946 GeoJSON <c>FeatureCollection</c> string.
        /// </summary>
        public static Result<string> ParseFileToGeoJson(string filePath, GeoJsonExportOptions? options = null)
        {
            var result = ParseFile(filePath);
            return result.IsSuccess
                ? Result<string>.Success(IwxxmGeoJsonExporter.ToGeoJson(result.Value, options))
                : Result<string>.Failure(result.Errors);
        }

        /// <inheritdoc />
        public Result<IIwxxmReport> Process(Stream xmlStream)
        {
            return _innerDispatcher.Process(xmlStream);
        }

        /// <inheritdoc />
        public Result<MeteorologicalBulletin> ProcessBulletin(Stream xmlStream)
        {
            return _innerDispatcher.ProcessBulletin(xmlStream);
        }

        private static Result<TReport> CastReportResult<TReport>(Result<IIwxxmReport> result)
            where TReport : class, IIwxxmReport
        {
            if (result.IsFailure)
                return Result<TReport>.Failure(result.Errors);

            var typed = result.Value as TReport;
            if (typed == null)
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
