using System;
using System.IO;
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

        public DispatchIwxxmReportUseCase(
            ISchemaValidator schemaValidator,
            ISchematronValidator schematronValidator,
            IIwxxmDocumentMapper documentMapper,
            IwxxmParserOptions? options = null)
        {
            _schemaValidator = schemaValidator ?? throw new ArgumentNullException(nameof(schemaValidator));
            _schematronValidator = schematronValidator ?? throw new ArgumentNullException(nameof(schematronValidator));
            _documentMapper = documentMapper ?? throw new ArgumentNullException(nameof(documentMapper));
            _options = options ?? IwxxmParserOptions.Default;
        }

        public Result<IIwxxmReport> Process(Stream xmlStream)
        {
            if (xmlStream == null)
                throw new ArgumentNullException(nameof(xmlStream));

            Result<byte[]> buffered = BoundedDocumentBuffer.Read(xmlStream, _options, emptyCode: "STREAM-EMPTY", unreadableCode: "STREAM-UNREADABLE");
            if (buffered.IsFailure)
            {
                return Result<IIwxxmReport>.Failure(buffered.Errors);
            }

            byte[] payload = buffered.Value;

            // Step 1: Structural validation against the official IWXXM 2025-2 / COLLECT schema closure
            using (MemoryStream view = BoundedDocumentBuffer.OpenView(payload))
            {
                var schemaErrors = PipelineGuard.Validate(() => _schemaValidator.Validate(view), PipelineGuard.SchemaValidatorErrorCode, ErrorCategory.SchemaViolation, "schema validator");
                if (schemaErrors.Count > 0)
                {
                    return Result<IIwxxmReport>.Failure(schemaErrors);
                }
            }

            // Step 2: Business-rule and code-list validation
            using (MemoryStream view = BoundedDocumentBuffer.OpenView(payload))
            {
                var ruleErrors = PipelineGuard.Validate(() => _schematronValidator.Validate(view), PipelineGuard.RuleValidatorErrorCode, ErrorCategory.BusinessRuleViolation, "business-rule validator");
                if (ruleErrors.Count > 0)
                {
                    return Result<IIwxxmReport>.Failure(ruleErrors);
                }
            }

            // Step 3: Polymorphic root element dispatch and Anti-Corruption Layer (ACL) Domain mapping
            using (MemoryStream view = BoundedDocumentBuffer.OpenView(payload))
            {
                return PipelineGuard.Map(() => _documentMapper.Map(view));
            }
        }

        public Result<MeteorologicalBulletin> ProcessBulletin(Stream xmlStream)
        {
            Result<IIwxxmReport> dispatchResult = Process(xmlStream);
            if (dispatchResult.IsFailure)
            {
                return Result<MeteorologicalBulletin>.Failure(dispatchResult.Errors);
            }

            if (dispatchResult.Value is MeteorologicalBulletin bulletin)
            {
                return Result<MeteorologicalBulletin>.Success(bulletin);
            }

            return Result<MeteorologicalBulletin>.Failure(
                "DISPATCH-NOT-A-BULLETIN",
                $"Expected a 'collect:MeteorologicalBulletin' document, but parsed '{dispatchResult.Value.ReportType}' ('{dispatchResult.Value.Id}').",
                ErrorCategory.Deserialization,
                dispatchResult.Value.Id);
        }
    }
}
