using Iwxxm.Net.Reports;

namespace Iwxxm.Net
{
    /// <summary>
    /// Inbound (driving) port: exports any <see cref="IwxxmDocument"/> Domain Aggregate Root as an RFC 7946
    /// GeoJSON <c>FeatureCollection</c> string (<c>[longitude, latitude]</c> axis order).
    /// </summary>
    public interface IGeoJsonExportService
    {
        /// <summary>
        /// Exports the specified <paramref name="document"/> to an RFC 7946 GeoJSON <c>FeatureCollection</c> string.
        /// </summary>
        /// <param name="document">Domain Aggregate Root to export.</param>
        /// <param name="options">Formatting options; <see cref="GeoJsonExportOptions.Default"/> when <c>null</c>.</param>
        string Export(IwxxmDocument document, GeoJsonExportOptions? options = null);
    }
}
