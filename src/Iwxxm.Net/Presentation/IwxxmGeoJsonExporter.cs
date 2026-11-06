using System;
using Iwxxm.Net.Extensibility;
using Iwxxm.Net.Reports;

namespace Iwxxm.Net
{
    /// <summary>
    /// Public facade for exporting any parsed <see cref="IwxxmDocument"/> Domain Aggregate Root into an RFC 7946
    /// GeoJSON <c>FeatureCollection</c> string (<c>[longitude, latitude]</c> axis order) suitable for web mapping
    /// engines (OpenLayers, Mapbox GL, Leaflet, QGIS).
    /// </summary>
    /// <remarks>
    /// Instances are created by <see cref="IwxxmParserBuilder.BuildGeoJsonExporter"/> and delegate to the outbound
    /// <see cref="IGeoJsonExporter"/> port, applying <see cref="GeoJsonExportOptions.Default"/> when no options are given.
    /// <see cref="ToGeoJson"/> is a convenience wrapper over <see cref="Default"/>.
    /// </remarks>
    public sealed class IwxxmGeoJsonExporter
    {
        private static readonly Lazy<IwxxmGeoJsonExporter> DefaultInstance =
            new Lazy<IwxxmGeoJsonExporter>(() => new IwxxmParserBuilder().BuildGeoJsonExporter(), isThreadSafe: true);

        private readonly IGeoJsonExporter _exporter;

        /// <summary>Initializes a facade over the given outbound exporter.</summary>
        internal IwxxmGeoJsonExporter(IGeoJsonExporter exporter)
        {
            _exporter = exporter ?? throw new ArgumentNullException(nameof(exporter));
        }

        /// <summary>Gets the shared instance built with the default RFC 7946 exporter.</summary>
        public static IwxxmGeoJsonExporter Default => DefaultInstance.Value;

        /// <summary>Serializes <paramref name="document"/> to an RFC 7946 GeoJSON <c>FeatureCollection</c>.</summary>
        public string Export(IwxxmDocument document, GeoJsonExportOptions? options = null)
        {
            if (document == null)
                throw new ArgumentNullException(nameof(document));

            return _exporter.Export(document, options ?? GeoJsonExportOptions.Default);
        }

        /// <summary>Serializes <paramref name="document"/> to an RFC 7946 GeoJSON <c>FeatureCollection</c> using <see cref="Default"/>.</summary>
        public static string ToGeoJson(IwxxmDocument document, GeoJsonExportOptions? options = null) =>
            Default.Export(document, options);
    }
}
