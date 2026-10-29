using System;
using System.IO;
using Iwxxm.Net.Extensibility;
using Iwxxm.Net.Reports;

namespace Iwxxm.Net.Application.UseCases
{
    /// <summary>
    /// Application Use Case that orchestrates the SIGWX processing pipeline:
    /// 1) Bounded buffer (enforcing <see cref="IwxxmParserOptions.MaxDocumentBytes"/>),
    /// 2) Schema validation, 3) Business-rule validation, 4) Mapping to the
    /// <see cref="SignificantWeatherReport"/> Domain Aggregate Root.
    /// Depends exclusively on Domain types and outbound ports.
    /// </summary>
    internal sealed class ProcessSigwxReportUseCase : IIwxxmSigwxProcessor
    {
        private readonly ISchemaValidator _schemaValidator;
        private readonly ISchematronValidator _schematronValidator;
        private readonly ISigwxDocumentMapper _documentMapper;
        private readonly IwxxmParserOptions _options;

        public ProcessSigwxReportUseCase(
            ISchemaValidator schemaValidator,
            ISchematronValidator schematronValidator,
            ISigwxDocumentMapper documentMapper,
            IwxxmParserOptions? options = null)
        {
            _schemaValidator = schemaValidator ?? throw new ArgumentNullException(nameof(schemaValidator));
            _schematronValidator = schematronValidator ?? throw new ArgumentNullException(nameof(schematronValidator));
            _documentMapper = documentMapper ?? throw new ArgumentNullException(nameof(documentMapper));
            _options = options ?? IwxxmParserOptions.Default;
        }

        public Result<SignificantWeatherReport> Process(Stream xmlStream)
        {
            if (xmlStream == null)
                throw new ArgumentNullException(nameof(xmlStream));

            Result<byte[]> buffered = BoundedDocumentBuffer.Read(xmlStream, _options, emptyCode: "XML-EMPTY-STREAM", unreadableCode: "STREAM-NOT-READABLE");
            if (buffered.IsFailure)
            {
                return Result<SignificantWeatherReport>.Failure(buffered.Errors);
            }

            byte[] payload = buffered.Value;

            using (MemoryStream view = BoundedDocumentBuffer.OpenView(payload))
            {
                var schemaErrors = _schemaValidator.Validate(view);
                if (schemaErrors.Count > 0)
                {
                    return Result<SignificantWeatherReport>.Failure(schemaErrors);
                }
            }

            using (MemoryStream view = BoundedDocumentBuffer.OpenView(payload))
            {
                var ruleErrors = _schematronValidator.Validate(view);
                if (ruleErrors.Count > 0)
                {
                    return Result<SignificantWeatherReport>.Failure(ruleErrors);
                }
            }

            using (MemoryStream view = BoundedDocumentBuffer.OpenView(payload))
            {
                return _documentMapper.Map(view);
            }
        }
    }
}
