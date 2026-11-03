using System;
using System.IO;
using Iwxxm.Net.Application.Ports;
using Iwxxm.Net.Extensibility;
using Iwxxm.Net.Reports;

namespace Iwxxm.Net.Application.UseCases
{
    /// <summary>
    /// Application Use Case that orchestrates the end-to-end validation and polymorphic dispatching
    /// pipeline for the full ICAO/WMO IWXXM 2025-2 suite and SWIM <c>collect:MeteorologicalBulletin</c> envelopes:
    /// 1) Bounded buffer -> 2) Schema validation -> 3) Business-rule validation -> 4) Dispatch &amp; map to Domain.
    /// Depends exclusively on Domain types and outbound ports.
    /// </summary>
    internal sealed class DispatchIwxxmReportUseCase : IIwxxmReportDispatcher
    {
        private readonly ISchemaValidator _schemaValidator;
        private readonly ISchematronValidator _schematronValidator;
        private readonly IIwxxmDocumentMapper _documentMapper;
        private readonly IwxxmParserOptions _options;
        private readonly IDocumentParser _parser;

        public DispatchIwxxmReportUseCase(
            ISchemaValidator schemaValidator,
            ISchematronValidator schematronValidator,
            IIwxxmDocumentMapper documentMapper,
            IDocumentParser parser,
            IwxxmParserOptions? options = null)
        {
            _parser = parser ?? throw new ArgumentNullException(nameof(parser));
            _schemaValidator = schemaValidator ?? throw new ArgumentNullException(nameof(schemaValidator));
            _schematronValidator = schematronValidator ?? throw new ArgumentNullException(nameof(schematronValidator));
            _documentMapper = documentMapper ?? throw new ArgumentNullException(nameof(documentMapper));
            _options = options ?? IwxxmParserOptions.Default;
        }

        public Result<IwxxmDocument> Process(Stream xmlStream)
        {
            if (xmlStream == null)
                throw new ArgumentNullException(nameof(xmlStream));

            return ProcessBuffered(BoundedDocumentBuffer.Read(xmlStream, _options));
        }

        public Result<IwxxmDocument> Process(TextReader xmlText)
        {
            if (xmlText == null)
                throw new ArgumentNullException(nameof(xmlText));

            return ProcessBuffered(BoundedDocumentBuffer.ReadText(xmlText, _options, emptyCode: SharedErrorCodes.XmlEmptyString));
        }

        private Result<IwxxmDocument> ProcessBuffered(Result<byte[]> buffered)
        {
            if (buffered.IsFailure)
            {
                return Result<IwxxmDocument>.Failure(buffered.Errors);
            }

            return ValidationPipeline.Run(buffered.Value, _options, _parser, _schemaValidator, _schematronValidator, document => _documentMapper.Map(document));
        }

        public Result<MeteorologicalBulletin> ProcessBulletin(Stream xmlStream) => AsBulletin(Process(xmlStream));

        public Result<MeteorologicalBulletin> ProcessBulletin(TextReader xmlText) => AsBulletin(Process(xmlText));

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
    }
}
