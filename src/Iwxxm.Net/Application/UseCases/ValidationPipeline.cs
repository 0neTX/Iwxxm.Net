using System;
using System.Collections.Generic;
using System.Linq;
using Iwxxm.Net.Application.Ports;
using Iwxxm.Net.Application.Ports.Out;
using Iwxxm.Net.Extensibility;

namespace Iwxxm.Net.Application.UseCases
{
    /// <summary>
    /// The validation and mapping pipeline shared by the use cases (HEX-01): the size-bounded buffer becomes an
    /// <see cref="IwxxmDocumentContext"/>, which is parsed once with the hardened reader settings (<see cref="IDocumentParser"/>)
    /// before schema validation, business-rule validation and mapping receive that same context.
    /// <para>
    /// Only findings with <see cref="ValidationSeverity.Error"/> stop the pipeline (AUD-U5). Warnings (for example
    /// Schematron assertions with a <c>warning</c> role or fired <c>svrl:successful-report</c>s) let it continue and are
    /// returned as <see cref="Result{T}.Warnings"/> of the successful result, before the mapper's own warnings. When a stage
    /// fails, the result carries every finding of that stage (errors and warnings, as reported).
    /// </para>
    /// </summary>
    internal static class ValidationPipeline
    {
        public static Result<T> Run<T>(
            byte[] payload,
            IwxxmParserOptions options,
            IDocumentParser parser,
            ISchemaValidator schemaValidator,
            ISchematronValidator schematronValidator,
            Func<IwxxmDocumentContext, Result<T>> map)
        {
            var warnings = new List<ValidationError>();
            IwxxmDocumentContext document = IwxxmDocumentContext.FromBuffer(payload, options);

            // Step 0: parse once (well-formedness, DTD prohibition and character quotas), shared by every adapter.
            IReadOnlyList<ValidationError> parseErrors = PipelineGuard.Validate(() => parser.Parse(document), "XML-MALFORMED", ErrorCategory.MalformedInput, "document parser");
            if (parseErrors.Count > 0)
            {
                return Result<T>.Failure(parseErrors);
            }

            // Step 1: structural validation.
            IReadOnlyList<ValidationError> schemaFindings =
                PipelineGuard.Validate(() => schemaValidator.Validate(document), PipelineGuard.SchemaValidatorErrorCode, ErrorCategory.SchemaViolation, "schema validator");

            if (ContainsError(schemaFindings))
            {
                return Result<T>.Failure(schemaFindings);
            }

            warnings.AddRange(schemaFindings);

            // Step 2: business-rule and code-list validation.
            IReadOnlyList<ValidationError> ruleFindings =
                PipelineGuard.Validate(() => schematronValidator.Validate(document), PipelineGuard.RuleValidatorErrorCode, ErrorCategory.BusinessRuleViolation, "business-rule validator");

            if (ContainsError(ruleFindings))
            {
                return Result<T>.Failure(ruleFindings);
            }

            warnings.AddRange(ruleFindings);

            // Step 3: mapping to the Domain (Anti-Corruption Layer).
            Result<T> mapped = PipelineGuard.Map(() => map(document));

            if (mapped.IsFailure || warnings.Count == 0)
            {
                return mapped;
            }

            return Result<T>.Success(mapped.Value, warnings.Concat(mapped.Warnings));
        }

        private static bool ContainsError(IReadOnlyList<ValidationError> findings) =>
            findings.Any(finding => finding == null || finding.Severity != ValidationSeverity.Warning);
    }
}
