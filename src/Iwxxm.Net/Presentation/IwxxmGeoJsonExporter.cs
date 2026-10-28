using System;
using Iwxxm.Net.Application.Ports;
using Iwxxm.Net.Domain.Aggregates;
using Iwxxm.Net.Infrastructure.GeoJson;

namespace Iwxxm.Net.Presentation
{
    /// <summary>
    /// Public facade for exporting any parsed <see cref="IIwxxmReport"/> Domain Aggregate Root
    /// into an RFC 7946 compliant GeoJSON <c>FeatureCollection</c> string (<c>[longitude, latitude]</c> axis order)
    /// suitable for web mapping engines (OpenLayers, Mapbox GL, Leaflet, QGIS).
    /// </summary>
    public sealed class IwxxmGeoJsonExporter : IGeoJsonExporter
    {
        private static readonly Lazy<IGeoJsonExporter> DefaultExporterInstance =
            new Lazy<IGeoJsonExporter>(CreateExporter, isThreadSafe: true);

        private readonly IGeoJsonExporter _innerExporter;

        public IwxxmGeoJsonExporter()
        {
            _innerExporter = DefaultExporterInstance.Value;
        }

        /// <summary>
        /// Creates an instance of <see cref="IGeoJsonExporter"/> suitable for singleton Dependency Injection registration.
        /// </summary>
        public static IGeoJsonExporter CreateExporter() => new Rfc7946GeoJsonExporter();

        /// <summary>
        /// Serializes any <see cref="IIwxxmReport"/> Domain Aggregate Root into an RFC 7946 GeoJSON <c>FeatureCollection</c>.
        /// </summary>
        public static string ToGeoJson(IIwxxmReport report, GeoJsonExportOptions? options = null)
        {
            if (report == null)
                throw new ArgumentNullException(nameof(report));

            return DefaultExporterInstance.Value.Export(report, options);
        }

        /// <inheritdoc />
        public string Export(IIwxxmReport report, GeoJsonExportOptions? options = null)
        {
            return _innerExporter.Export(report, options);
        }
    }
}
