using Iwxxm.Net.Reports;

namespace Iwxxm.Net.Application.Ports.Out
{
    /// <summary>
    /// Outbound (driven) port that serializes an <see cref="IwxxmDocument"/> Domain Aggregate Root
    /// into an RFC 4180 CSV document.
    /// </summary>
    internal interface ICsvExporter
    {
        /// <summary>Serializes <paramref name="document"/> using the given formatting <paramref name="options"/>.</summary>
        string Export(IwxxmDocument document, CsvExportOptions options);
    }
}
