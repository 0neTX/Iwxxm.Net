using System.Collections.Generic;
using System.IO;

namespace Iwxxm.Net.Extensibility
{
    /// <summary>
    /// Outbound (driven) port for business-rule validation of a structurally valid IWXXM document
    /// (Schematron rules and code-list membership).
    /// </summary>
    /// <remarks>
    /// Invoked only after <see cref="ISchemaValidator"/> reported no error. The stream is a seekable, read-only,
    /// size-bounded buffer positioned at the start of the document; implementations must not close it and must perform
    /// no network access. Each finding should use <see cref="ErrorCategory.BusinessRuleViolation"/> and the rule
    /// identifier as <see cref="ValidationError.Code"/>. Any returned finding stops the pipeline before domain mapping.
    /// </remarks>
    public interface ISchematronValidator
    {
        /// <summary>Validates <paramref name="document"/> and returns every business-rule finding.</summary>
        /// <param name="document">Seekable stream containing the raw document bytes.</param>
        IReadOnlyList<ValidationError> Validate(Stream document);
    }
}
