using Iwxxm.Net.Reports;

namespace Iwxxm.Net.Application.Ports.Out
{
    /// <summary>
    /// Outbound (driven) port that serializes an <see cref="IwxxmDocument"/> Domain Aggregate Root
    /// into an RFC 7946 GeoJSON <c>FeatureCollection</c> string (<c>[longitude, latitude]</c> axis order).
    /// </summary>
    internal interface IGeoJsonExporter
    {
        /// <summary>Serializes <paramref name="document"/> using the given formatting <paramref name="options"/>.</summary>
        string Export(IwxxmDocument document, GeoJsonExportOptions options);
    }
}
