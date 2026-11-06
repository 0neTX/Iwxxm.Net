using Iwxxm.Net.Extensibility;
using Iwxxm.Net.Reports;

namespace Iwxxm.Net.Application.Ports.Out
{
    /// <summary>
    /// Outbound (driven) port that maps a validated SIGWX document onto the
    /// <see cref="Sigwx"/> Domain Aggregate Root.
    /// </summary>
    internal interface ISigwxDocumentMapper
    {
        /// <summary>Maps the validated SIGWX document into a <see cref="Sigwx"/>.</summary>
        /// <param name="validatedDocument">Sanitized context of a document that passed validation.</param>
        Result<Sigwx> Map(IwxxmDocumentContext validatedDocument);
    }
}
