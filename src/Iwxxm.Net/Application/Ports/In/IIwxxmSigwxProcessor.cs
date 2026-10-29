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
        /// <returns>A <see cref="Result{SignificantWeatherReport}"/> containing either the aggregate root or validation errors.</returns>
        Result<SignificantWeatherReport> Process(Stream xmlStream);
    }
}
