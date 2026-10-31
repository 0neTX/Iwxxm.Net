using System.Collections.Generic;

namespace Iwxxm.Net.Extensibility
{
    /// <summary>
    /// Outbound (driven) port for business-rule validation of a structurally valid IWXXM document
    /// (Schematron rules and code-list membership).
    /// </summary>
    /// <remarks>
    /// Invoked only after <see cref="ISchemaValidator"/> reported no error, with the sanitized
    /// <see cref="IwxxmDocumentContext"/> of the document; implementations must perform no network access. Each finding should use <see cref="ErrorCategory.BusinessRuleViolation"/> and the rule
    /// identifier as <see cref="ValidationError.Code"/>. A finding with <see cref="ValidationSeverity.Error"/> stops the pipeline before
    /// domain mapping; <see cref="ValidationSeverity.Warning"/> findings (e.g. a <c>warning</c> role or a fired
    /// <c>svrl:successful-report</c>) do not and are returned as <see cref="Result{T}.Warnings"/> of the successful result.
    /// </remarks>
    public interface ISchematronValidator
    {
        /// <summary>Validates <paramref name="document"/> and returns every business-rule finding.</summary>
        /// <param name="document">The document (<see cref="IwxxmDocumentContext.OpenRead"/> gives its raw bytes).</param>
        IReadOnlyList<ValidationError> Validate(IwxxmDocumentContext document);
    }
}
