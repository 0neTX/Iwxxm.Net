using System.IO;
using Iwxxm.Net.Domain.Aggregates;
using Iwxxm.Net.Domain.Common;

namespace Iwxxm.Net.Application.Ports
{
    /// <summary>
    /// Primary inbound port of the Application layer for processing, dual-validating (XSD + Schematron XSLT),
    /// and mapping ICAO/WMO IWXXM 2025-2 SIGWX reports into the pure Domain model.
    /// </summary>
    public interface IIwxxmSigwxProcessor
    {
        /// <summary>
        /// Processes an input <see cref="Stream"/> containing an IWXXM 2025-2 SIGWX XML document by orchestrating:
        /// 1) Safe stream buffering, 2) XSD structural validation, 3) Schematron XSLT business rules validation,
        /// 4) XML deserialization via XmlSerializer, and 5) Anti-Corruption Layer (ACL) mapping to Domain.
        /// </summary>
        /// <param name="xmlStream">Readable stream containing the IWXXM 2025-2 XML document.</param>
        /// <returns>A <see cref="Result{SignificantWeatherReport}"/> containing either the valid aggregate root or validation errors.</returns>
        Result<SignificantWeatherReport> Process(Stream xmlStream);
    }
}
