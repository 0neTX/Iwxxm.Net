using Iwxxm.Net.Reports;

namespace Iwxxm.Net.Extensibility
{
    /// <summary>
    /// Outbound (driven) port that maps a validated IWXXM 2025-2 / COLLECT document onto its
    /// Domain Aggregate Root (<see cref="IwxxmDocument"/>), dispatching on the root element.
    /// </summary>
    public interface IIwxxmDocumentMapper
    {
        /// <summary>Maps the validated document into a Domain Aggregate Root.</summary>
        /// <param name="validatedDocument">Sanitized context of a document that passed validation.</param>
        Result<IwxxmDocument> Map(IwxxmDocumentContext validatedDocument);
    }
}
