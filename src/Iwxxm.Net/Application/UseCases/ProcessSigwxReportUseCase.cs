using System;
using System.IO;
using Iwxxm.Net.Application.Ports;
using Iwxxm.Net.Extensibility;
using Iwxxm.Net.Reports;

namespace Iwxxm.Net.Application.UseCases
{
    /// <summary>
    /// Application Use Case that orchestrates the SIGWX processing pipeline:
    /// 1) Bounded buffer (enforcing <see cref="IwxxmParserOptions.MaxDocumentBytes"/>),
    /// 2) Schema validation, 3) Business-rule validation, 4) Mapping to the
    /// <see cref="Sigwx"/> Domain Aggregate Root.
    /// Depends exclusively on Domain types and outbound ports.
    /// </summary>
    internal sealed class ProcessSigwxReportUseCase : IIwxxmSigwxProcessor
    {
        private readonly ISchemaValidator _schemaValidator;
        private readonly ISchematronValidator _schematronValidator;
        private readonly ISigwxDocumentMapper _documentMapper;
        private readonly IwxxmParserOptions _options;
        private readonly IDocumentParser _parser;

        public ProcessSigwxReportUseCase(
            ISchemaValidator schemaValidator,
            ISchematronValidator schematronValidator,
            ISigwxDocumentMapper documentMapper,
            IDocumentParser parser,
            IwxxmParserOptions? options = null)
        {
            _parser = parser ?? throw new ArgumentNullException(nameof(parser));
            _schemaValidator = schemaValidator ?? throw new ArgumentNullException(nameof(schemaValidator));
            _schematronValidator = schematronValidator ?? throw new ArgumentNullException(nameof(schematronValidator));
            _documentMapper = documentMapper ?? throw new ArgumentNullException(nameof(documentMapper));
            _options = options ?? IwxxmParserOptions.Default;
        }

        public Result<Sigwx> Process(Stream xmlStream)
        {
            if (xmlStream == null)
                throw new ArgumentNullException(nameof(xmlStream));

            return ProcessBuffered(BoundedDocumentBuffer.Read(xmlStream, _options, emptyCode: "XML-EMPTY-STREAM", unreadableCode: "STREAM-NOT-READABLE"));
        }

        public Result<Sigwx> Process(TextReader xmlText)
        {
            if (xmlText == null)
                throw new ArgumentNullException(nameof(xmlText));

            return ProcessBuffered(BoundedDocumentBuffer.ReadText(xmlText, _options, emptyCode: "XML-EMPTY-STRING"));
        }

        private Result<Sigwx> ProcessBuffered(Result<byte[]> buffered)
        {
            if (buffered.IsFailure)
            {
                return Result<Sigwx>.Failure(buffered.Errors);
            }

            return ValidationPipeline.Run(buffered.Value, _options, _parser, _schemaValidator, _schematronValidator, document => _documentMapper.Map(document));
        }
    }
}
