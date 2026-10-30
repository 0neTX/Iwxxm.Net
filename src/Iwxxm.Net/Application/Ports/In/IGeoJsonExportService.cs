using Iwxxm.Net.Reports;

namespace Iwxxm.Net
{
    /// <summary>
    /// Inbound (driving) port: exports any <see cref="IwxxmReport"/> Domain Aggregate Root as an RFC 7946
    /// GeoJSON <c>FeatureCollection</c> string (<c>[longitude, latitude]</c> axis order).
    /// </summary>
    public interface IGeoJsonExportService
    {
        /// <summary>
        /// Exports the specified <paramref name="report"/> to an RFC 7946 GeoJSON <c>FeatureCollection</c> string.
        /// </summary>
        /// <param name="report">Domain Aggregate Root to export.</param>
        /// <param name="options">Formatting options; <see cref="GeoJsonExportOptions.Default"/> when <c>null</c>.</param>
        string Export(IwxxmReport report, GeoJsonExportOptions? options = null);
    }
}
