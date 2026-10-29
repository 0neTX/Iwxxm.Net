using System.Collections.Generic;
using System.IO;

namespace Iwxxm.Net.Extensibility
{
    /// <summary>
    /// Outbound (driven) port for structural validation of a raw IWXXM document against its schemas.
    /// </summary>
    /// <remarks>
    /// The use case passes a seekable, read-only, size-bounded buffer positioned at the start of the document.
    /// Implementations must not close the stream, must perform no network access, and must return an empty list
    /// when the document is structurally valid. Any returned finding stops the pipeline before
    /// business-rule validation.
    /// </remarks>
    public interface ISchemaValidator
    {
        /// <summary>Validates <paramref name="document"/> and returns every structural finding.</summary>
        /// <param name="document">Seekable stream containing the raw document bytes.</param>
        IReadOnlyList<ValidationError> Validate(Stream document);
    }
}
