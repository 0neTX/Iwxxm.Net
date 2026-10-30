using System.IO;
using Iwxxm.Net.Reports;

namespace Iwxxm.Net
{
    /// <summary>
    /// Inbound (driving) port: validates and maps any ICAO/WMO IWXXM 2025-2 report or SWIM
    /// <c>collect:MeteorologicalBulletin</c> envelope into the domain model.
    /// </summary>
    public interface IIwxxmReportDispatcher
    {
        /// <summary>
        /// Validates and parses a stream containing any IWXXM 2025-2 report or <c>collect:MeteorologicalBulletin</c>.
        /// </summary>
        /// <param name="xmlStream">Readable stream containing the IWXXM 2025-2 or Collect document.</param>
        /// <returns>A <see cref="Result{IwxxmReport}"/> containing the mapped Domain Aggregate Root or structured validation errors.</returns>
        Result<IwxxmReport> Process(Stream xmlStream);

        /// <summary>
        /// Validates and unwraps a SWIM <c>collect:MeteorologicalBulletin</c> stream into a <see cref="MeteorologicalBulletin"/> aggregate.
        /// </summary>
        /// <param name="xmlStream">Readable stream containing the <c>collect:MeteorologicalBulletin</c> document.</param>
        /// <returns>A <see cref="Result{MeteorologicalBulletin}"/> containing the bulletin and its child reports.</returns>
        Result<MeteorologicalBulletin> ProcessBulletin(Stream xmlStream);
    }
}
