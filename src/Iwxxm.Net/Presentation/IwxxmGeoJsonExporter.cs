using System;
using Iwxxm.Net.Reports;

namespace Iwxxm.Net
{
    /// <summary>
    /// Public facade for exporting any parsed <see cref="IIwxxmReport"/> Domain Aggregate Root into an RFC 7946
    /// GeoJSON <c>FeatureCollection</c> string (<c>[longitude, latitude]</c> axis order) suitable for web mapping
    /// engines (OpenLayers, Mapbox GL, Leaflet, QGIS).
    /// </summary>
    /// <remarks>
    /// Instances are created by <see cref="IwxxmParserBuilder.BuildGeoJsonExporter"/> and depend only on the inbound
    /// port <see cref="IGeoJsonExportService"/>. <see cref="ToGeoJson"/> is a convenience wrapper over <see cref="Default"/>.
    /// </remarks>
    public sealed class IwxxmGeoJsonExporter : IGeoJsonExportService
    {
        private static readonly Lazy<IwxxmGeoJsonExporter> DefaultInstance =
            new Lazy<IwxxmGeoJsonExporter>(() => new IwxxmParserBuilder().BuildGeoJsonExporter(), isThreadSafe: true);

        private readonly IGeoJsonExportService _service;

        /// <summary>Initializes a facade over the given inbound port.</summary>
        public IwxxmGeoJsonExporter(IGeoJsonExportService service)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
        }

        /// <summary>Gets the shared instance built with the default RFC 7946 exporter.</summary>
        public static IwxxmGeoJsonExporter Default => DefaultInstance.Value;

        /// <inheritdoc />
        public string Export(IIwxxmReport report, GeoJsonExportOptions? options = null)
        {
            if (report == null)
                throw new ArgumentNullException(nameof(report));

            return _service.Export(report, options);
        }

        /// <summary>Serializes <paramref name="report"/> to an RFC 7946 GeoJSON <c>FeatureCollection</c> using <see cref="Default"/>.</summary>
        public static string ToGeoJson(IIwxxmReport report, GeoJsonExportOptions? options = null) =>
            Default.Export(report, options);
    }
}
