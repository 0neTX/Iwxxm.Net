using System;
using Iwxxm.Net.Infrastructure.Mapping.Wafs;
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
    /// <c>Infrastructure/Mapping/Wafs</c>.
    /// </remarks>
    internal sealed class SigwxMapper
    {
        public Result<Sigwx> MapToDomain(SigwxDocumentEnvelope envelope, UomAliasCollector aliases)
        {
            if (envelope == null)
                throw new ArgumentNullException(nameof(envelope));

            if (envelope.WafsForecast != null)
            {
                return WafsForecastMapper.Map(envelope.WafsForecast, aliases);
            }

            if (envelope.CompactReport != null)
            {
                return CompactSigwxReportMapper.Map(envelope.CompactReport, aliases);
            }

            return Result<Sigwx>.Failure(
                "ACL-EMPTY-ENVELOPE",
                "The deserialized SIGWX document envelope does not contain a report instance.",
                ErrorCategory.DomainMapping);
        }
    }
}
