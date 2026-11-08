using System;
using Iwxxm.Net.Application.Ports.Out;
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
    /// <see cref="ToGeoJson"/> is a convenience wrapper over <see cref="Default"/>. An exporter failure is a value, like
    /// every other failure of the library: <see cref="Export"/> returns a <see cref="Result{T}"/> and throws only for a
    /// <c>null</c> document. This is the single place that decides what an export failure is; the
    /// <c>*ToGeoJson</c> members of <see cref="IwxxmParser"/> reuse it.
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

        /// <summary>
        /// Serializes <paramref name="document"/> to an RFC 7946 GeoJSON <c>FeatureCollection</c>.
        /// </summary>
        /// <returns>
        /// The GeoJSON text, or a failure with <see cref="IwxxmErrorCodes.GeoJsonExportError"/>
        /// (<see cref="ErrorCategory.DomainMapping"/>, located at the document id) when the exporter threw or returned nothing.
        /// </returns>
        /// <exception cref="ArgumentNullException"><paramref name="document"/> is <c>null</c> (a programming error).</exception>
        public Result<string> Export(IwxxmDocument document, GeoJsonExportOptions? options = null)
        {
            if (document == null)
                throw new ArgumentNullException(nameof(document));

            try
            {
                string json = _exporter.Export(document, options ?? GeoJsonExportOptions.Default);
                if (json == null)
                {
                    return Result<string>.Failure(
                        IwxxmErrorCodes.GeoJsonExportError,
                        "The GeoJSON exporter returned no document.",
                        ErrorCategory.DomainMapping,
                        document.Id);
                }

                return Result<string>.Success(json);
            }
            catch (Exception ex) when (!(ex is OutOfMemoryException))
            {
                return Result<string>.Failure(
                    IwxxmErrorCodes.GeoJsonExportError,
                    $"The GeoJSON export failed: {ex.Message}",
                    ErrorCategory.DomainMapping,
                    document.Id);
            }
        }

        /// <summary>Serializes <paramref name="document"/> to an RFC 7946 GeoJSON <c>FeatureCollection</c> using <see cref="Default"/>.</summary>
        /// <returns>See <see cref="Export"/>: the GeoJSON text or a <see cref="IwxxmErrorCodes.GeoJsonExportError"/> failure.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="document"/> is <c>null</c>.</exception>
        public static Result<string> ToGeoJson(IwxxmDocument document, GeoJsonExportOptions? options = null) =>
            Default.Export(document, options);
    }
}
