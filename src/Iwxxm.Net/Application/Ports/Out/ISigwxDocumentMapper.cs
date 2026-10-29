using System.IO;
using Iwxxm.Net.Reports;

namespace Iwxxm.Net.Extensibility
{
    /// <summary>
    /// Outbound (driven) port that maps a validated SIGWX document onto the
    /// <see cref="SignificantWeatherReport"/> Domain Aggregate Root.
    /// </summary>
    public interface ISigwxDocumentMapper
    {
        /// <summary>Maps the validated SIGWX document into a <see cref="SignificantWeatherReport"/>.</summary>
        /// <param name="validatedDocument">Seekable stream positioned at the start of a document that passed validation.</param>
        Result<SignificantWeatherReport> Map(Stream validatedDocument);
    }
}
