using Iwxxm.Net.Reports;

namespace Iwxxm.Net.Extensibility
{
    /// <summary>
    /// Outbound (driven) port that serializes an <see cref="IIwxxmReport"/> Domain Aggregate Root
    /// into an RFC 7946 GeoJSON <c>FeatureCollection</c> string (<c>[longitude, latitude]</c> axis order).
    /// </summary>
    public interface IGeoJsonExporter
    {
        /// <summary>Serializes <paramref name="report"/> using the given formatting <paramref name="options"/>.</summary>
        string Export(IIwxxmReport report, GeoJsonExportOptions options);
    }
}
