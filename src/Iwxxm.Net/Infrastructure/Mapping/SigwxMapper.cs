using System;
using Iwxxm.Net.Infrastructure.Mapping.Sigwx;
using Iwxxm.Net.Reports;
using Iwxxm20252.Generated;

namespace Iwxxm.Net.Infrastructure.Mapping
{
    /// <summary>
    /// Anti-Corruption Layer (ACL) mapper that translates the XSD-generated XML object graph
    /// (<see cref="WAFSSignificantWeatherForecastType"/> or <see cref="SIGWXReportType"/>)
    /// into pure Domain Entities and Value Objects, isolating the Domain from OGC GML 3.2.1 primitives.
    /// </summary>
    /// <remarks>
    /// Thin coordinator: the document-level mapping lives in <see cref="WafsForecastMapper"/> and
    /// <see cref="CompactSigwxReportMapper"/>, and each WAFS phenomenon family has its own extractor in
    /// <c>Infrastructure/Mapping/Sigwx</c>.
    /// </remarks>
    internal sealed class SigwxMapper
    {
        public Result<SignificantWeatherReport> MapToDomain(SigwxDocumentEnvelope envelope)
        {
            if (envelope == null)
                throw new ArgumentNullException(nameof(envelope));

            if (envelope.WafsForecast != null)
            {
                return WafsForecastMapper.Map(envelope.WafsForecast);
            }

            if (envelope.CompactReport != null)
            {
                return CompactSigwxReportMapper.Map(envelope.CompactReport);
            }

            return Result<SignificantWeatherReport>.Failure(
                "ACL-EMPTY-ENVELOPE",
                "The deserialized SIGWX document envelope does not contain a report instance.",
                ErrorCategory.DomainMapping);
        }
    }
}
