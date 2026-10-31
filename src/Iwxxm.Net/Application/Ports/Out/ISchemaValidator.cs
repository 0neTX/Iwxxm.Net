using System.Collections.Generic;

namespace Iwxxm.Net.Extensibility
{
    /// <summary>
    /// Outbound (driven) port for structural validation of a raw IWXXM document against its schemas.
    /// </summary>
    /// <remarks>
    /// The use case passes the sanitized <see cref="IwxxmDocumentContext"/> of the document (well formed, within the
    /// limits, no DTD). Implementations must perform no network access and must return an empty list
    /// when the document is structurally valid. A finding with <see cref="ValidationSeverity.Error"/> stops the
    /// pipeline before business-rule validation; <see cref="ValidationSeverity.Warning"/> findings do not and are returned as
    /// <see cref="Result{T}.Warnings"/> of the successful result.
    /// </remarks>
    public interface ISchemaValidator
    {
        /// <summary>Validates <paramref name="document"/> and returns every structural finding.</summary>
        /// <param name="document">The document (<see cref="IwxxmDocumentContext.OpenRead"/> gives its raw bytes).</param>
        IReadOnlyList<ValidationError> Validate(IwxxmDocumentContext document);
    }
}
