using System;
using System.IO;
using System.Text;
using Iwxxm20252.Generated;
using Iwxxm.Net.Application.Ports;
using Iwxxm.Net.Application.UseCases;
using Iwxxm.Net.Domain.Aggregates;
using Iwxxm.Net.Domain.Common;
using Iwxxm.Net.Infrastructure.Mapping;
using Iwxxm.Net.Infrastructure.Serialization;
using Iwxxm.Net.Infrastructure.Validation;

namespace Iwxxm.Net.Presentation
{
    /// <summary>
    /// Public facade and composition root of the NuGet package for dual-validating (XSD + Schematron XSLT)
    /// and parsing ICAO/WMO IWXXM 2025-2 Significant Weather (SIGWX) reports.
    /// Can be consumed statically or instantiated via <see cref="IIwxxmSigwxProcessor"/> for Dependency Injection.
    /// </summary>
    public sealed class IwxxmSigwxParser : IIwxxmSigwxProcessor
    {
        private static readonly Lazy<IIwxxmSigwxProcessor> DefaultProcessorInstance =
            new Lazy<IIwxxmSigwxProcessor>(() => CreateProcessor(), isThreadSafe: true);

        private readonly IIwxxmSigwxProcessor _innerProcessor;

        /// <summary>
        /// Initializes a new instance of <see cref="IwxxmSigwxParser"/> wired with the default
        /// embedded XSD validator, Schematron XSLT validator, XmlSerializer adapter, and ACL mapper.
        /// </summary>
        public IwxxmSigwxParser(IwxxmParserOptions? options = null)
        {
            _innerProcessor = options == null
                ? DefaultProcessorInstance.Value
                : CreateProcessor(options);
        }

        /// <summary>
        /// Creates and returns an instance of <see cref="IIwxxmSigwxProcessor"/> suitable for singleton
        /// registration in Dependency Injection containers (.NET 8 IServiceCollection, Autofac, etc.).
        /// </summary>
        public static IIwxxmSigwxProcessor CreateProcessor(IwxxmParserOptions? options = null)
        {
            var effectiveOptions = options ?? IwxxmParserOptions.Default;
            return new ProcessSigwxReportUseCase<SigwxDocumentEnvelope>(
                xsdValidator: new XsdValidator(useFullSuite: false, options: effectiveOptions),
                schematronValidator: new XsltSchematronValidator(effectiveOptions),
                xmlDeserializer: new XmlSerializerSigwxDeserializer(effectiveOptions),
                aclMapper: new SigwxMapper(),
                options: effectiveOptions);
        }

        /// <summary>
        /// Processes, validates (XSD + Schematron XSLT), and maps an input <see cref="Stream"/>
        /// containing an IWXXM 2025-2 SIGWX report.
        /// </summary>
        public static Result<SignificantWeatherReport> Parse(Stream xmlStream)
        {
            if (xmlStream == null)
                throw new ArgumentNullException(nameof(xmlStream));

            return DefaultProcessorInstance.Value.Process(xmlStream);
        }

        /// <summary>
        /// Processes, validates (XSD + Schematron XSLT), and maps an XML string containing
        /// an IWXXM 2025-2 SIGWX report.
        /// </summary>
        public static Result<SignificantWeatherReport> Parse(string xmlContent)
        {
            if (string.IsNullOrWhiteSpace(xmlContent))
            {
                return Result<SignificantWeatherReport>.Failure(
                    "XML-EMPTY-STRING",
                    "The provided XML string is null or empty.",
                    ErrorCategory.XmlWellFormedness);
            }

            byte[] utf8Bytes = Encoding.UTF8.GetBytes(xmlContent);
            using (var memoryStream = new MemoryStream(utf8Bytes, writable: false))
            {
                return DefaultProcessorInstance.Value.Process(memoryStream);
            }
        }

        /// <summary>
        /// Processes, validates (XSD + Schematron XSLT), and maps an IWXXM 2025-2 SIGWX XML file from disk.
        /// </summary>
        public static Result<SignificantWeatherReport> ParseFile(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("File path cannot be null or whitespace.", nameof(filePath));

            if (!File.Exists(filePath))
            {
                return Result<SignificantWeatherReport>.Failure(
                    "FILE-NOT-FOUND",
                    $"The specified IWXXM file does not exist: '{filePath}'.",
                    ErrorCategory.XmlWellFormedness);
            }

            using (var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                return DefaultProcessorInstance.Value.Process(fileStream);
            }
        }

        /// <inheritdoc />
        public Result<SignificantWeatherReport> Process(Stream xmlStream)
        {
            return _innerProcessor.Process(xmlStream);
        }
    }
}
