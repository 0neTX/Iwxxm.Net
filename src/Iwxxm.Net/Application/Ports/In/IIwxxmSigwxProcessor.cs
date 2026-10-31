using System.IO;
using Iwxxm.Net.Reports;

namespace Iwxxm.Net
{
    /// <summary>
    /// Inbound (driving) port: validates and maps ICAO/WMO IWXXM 2025-2 SIGWX documents
    /// (<c>iwxxm:WAFSSignificantWeatherForecast</c> and the non-standard compact <c>iwxxm:SIGWXReport</c>).
    /// </summary>
    public interface IIwxxmSigwxProcessor
    {
        /// <summary>
        /// Processes a stream containing an IWXXM 2025-2 SIGWX document: bounded buffering, structural
        /// schema validation, business-rule validation and mapping to the domain model.
        /// </summary>
        /// <param name="xmlStream">Readable stream containing the IWXXM 2025-2 SIGWX document.</param>
        /// <returns>A <see cref="Result{Sigwx}"/> containing either the aggregate root or validation errors.</returns>
        Result<Sigwx> Process(Stream xmlStream);

        /// <summary>
        /// Processes already-decoded XML text containing an IWXXM 2025-2 SIGWX document; the encoding named by the XML
        /// declaration does not apply to text and is ignored.
        /// </summary>
        /// <param name="xmlText">Reader positioned at the start of the document (not closed).</param>
        Result<Sigwx> Process(TextReader xmlText);
    }
}
