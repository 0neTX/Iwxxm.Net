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
    /// Public facade for validating and parsing ICAO/WMO IWXXM 2025-2 Significant Weather (SIGWX) documents
    /// (<c>iwxxm:WAFSSignificantWeatherForecast</c> and the non-standard compact <c>iwxxm:SIGWXReport</c>).
    /// </summary>
    /// <remarks>
    /// Instances are created by <see cref="IwxxmParserBuilder.BuildSigwxParser"/>; they are immutable and thread-safe.
    /// The static members are convenience wrappers over <see cref="Default"/>.
    /// <para>I/O and adapter failures are returned as typed errors (<c>FILE-NOT-FOUND</c>, <c>ACCESS-DENIED</c>,
    /// <c>FILE-READ-ERROR</c>, <c>SCHEMA-VALIDATOR-ERROR</c>, <c>RULE-VALIDATOR-ERROR</c>, <c>MAPPER-ERROR</c>); only a
    /// <c>null</c> stream (<see cref="ArgumentNullException"/>) or a <c>null</c>/whitespace path
    /// (<see cref="ArgumentException"/>) throws.</para>
    /// </remarks>
    public sealed class IwxxmSigwxParser
    {
        private static readonly Lazy<IwxxmSigwxParser> DefaultInstance =
            new Lazy<IwxxmSigwxParser>(() => new IwxxmParserBuilder().BuildSigwxParser(), isThreadSafe: true);

        private readonly ISchemaValidator _schemaValidator;
        private readonly ISchematronValidator _schematronValidator;
        private readonly ISigwxDocumentMapper _documentMapper;
        private readonly IDocumentParser _parser;
        private readonly IwxxmParserOptions _options;

        internal IwxxmSigwxParser(
            ISchemaValidator schemaValidator,
            ISchematronValidator schematronValidator,
            ISigwxDocumentMapper documentMapper,
            IDocumentParser parser,
            IwxxmParserOptions? options = null)
        {
            _schemaValidator = schemaValidator ?? throw new ArgumentNullException(nameof(schemaValidator));
            _schematronValidator = schematronValidator ?? throw new ArgumentNullException(nameof(schematronValidator));
            _documentMapper = documentMapper ?? throw new ArgumentNullException(nameof(documentMapper));
            _parser = parser ?? throw new ArgumentNullException(nameof(parser));
            _options = options ?? IwxxmParserOptions.Default;
        }

        /// <summary>Gets the shared instance built with the default adapters and <see cref="IwxxmParserOptions.Default"/>.</summary>
        public static IwxxmSigwxParser Default => DefaultInstance.Value;

        /// <summary>Validates and maps a SIGWX stream.</summary>
        public Result<Sigwx> Process(Stream xmlStream)
        {
            if (xmlStream == null)
                throw new ArgumentNullException(nameof(xmlStream));

            return ProcessBuffered(BoundedDocumentBuffer.Read(xmlStream, _options));
        }

        /// <summary>Validates and maps already-decoded SIGWX XML text.</summary>
        public Result<Sigwx> Process(TextReader xmlText)
        {
            if (xmlText == null)
                throw new ArgumentNullException(nameof(xmlText));

            return ProcessBuffered(BoundedDocumentBuffer.ReadText(xmlText, _options, emptyCode: IwxxmErrorCodes.XmlEmptyString));
        }

        /// <summary>
        /// Validates and maps an XML string containing an IWXXM 2025-2 SIGWX document. The string is read as text: its
        /// declared encoding is ignored.
        /// </summary>
        public Result<Sigwx> Process(string xmlContent) =>
            IwxxmParser.WithTextReader(xmlContent, Process);

        /// <summary>Validates and maps an IWXXM 2025-2 SIGWX XML file from disk.</summary>
        public Result<Sigwx> ProcessFile(string filePath) =>
            IwxxmParser.WithFileStream(filePath, "IWXXM file", Process);

        private Result<Sigwx> ProcessBuffered(Result<byte[]> buffered)
        {
            if (buffered.IsFailure)
            {
                return Result<Sigwx>.Failure(buffered.Errors);
            }

            return ValidationPipeline.Run(buffered.Value, _options, _parser, _schemaValidator, _schematronValidator, document => _documentMapper.Map(document));
        }

        /// <summary>Validates and maps a SIGWX stream using <see cref="Default"/>.</summary>
        public static Result<Sigwx> Parse(Stream xmlStream) => Default.Process(xmlStream);

        /// <summary>Validates and maps a SIGWX XML string using <see cref="Default"/>.</summary>
        public static Result<Sigwx> Parse(string xmlContent) => Default.Process(xmlContent);

        /// <summary>Validates and maps a SIGWX XML file using <see cref="Default"/>.</summary>
        public static Result<Sigwx> ParseFile(string filePath) => Default.ProcessFile(filePath);
    }
}
