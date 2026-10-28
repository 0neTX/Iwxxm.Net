using System.IO;
using Iwxxm.Net.Domain.Aggregates;
using Iwxxm.Net.Domain.Common;

namespace Iwxxm.Net.Application.Ports
{
    /// <summary>
    /// Inbound Application Port for polymorphic processing, dual validation (XSD + Schematron XSLT
    /// with offline WMO RDF/SKOS code-list registry), and Domain mapping of any ICAO/WMO IWXXM 2025-2
    /// report or SWIM <c>collect:MeteorologicalBulletin</c> envelope.
    /// </summary>
    public interface IIwxxmReportDispatcher
    {
        /// <summary>
        /// Validates and parses an XML stream containing any IWXXM 2025-2 report or <c>collect:MeteorologicalBulletin</c>.
        /// </summary>
        /// <param name="xmlStream">Readable stream containing the IWXXM 2025-2 or Collect XML document.</param>
        /// <returns>A <see cref="Result{IIwxxmReport}"/> containing the mapped Domain Aggregate Root or structured validation errors.</returns>
        Result<IIwxxmReport> Process(Stream xmlStream);

        /// <summary>
        /// Validates and unwraps a SWIM <c>collect:MeteorologicalBulletin</c> stream into a <see cref="MeteorologicalBulletin"/> aggregate.
        /// </summary>
        /// <param name="xmlStream">Readable stream containing the <c>collect:MeteorologicalBulletin</c> XML document.</param>
        /// <returns>A <see cref="Result{MeteorologicalBulletin}"/> containing the bulletin and its child reports.</returns>
        Result<MeteorologicalBulletin> ProcessBulletin(Stream xmlStream);
    }

    /// <summary>
    /// Outbound Application Port for inspecting the root XML element of a validated IWXXM 2025-2 / Collect stream
    /// and mapping it into its corresponding pure Domain Aggregate Root (<see cref="IIwxxmReport"/>).
    /// </summary>
    internal interface IIwxxmSuiteDocumentMapper
    {
        Result<IIwxxmReport> MapValidatedStream(Stream xmlStream);
    }
}
