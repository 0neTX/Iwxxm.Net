using System;
using Iwxxm.Net.Extensibility;
using Iwxxm.Net.Reports;

namespace Iwxxm.Net.Application.UseCases
{
    /// <summary>
    /// Application Use Case that exports a Domain Aggregate Root to RFC 7946 GeoJSON through the
    /// outbound <see cref="IGeoJsonExporter"/> port, applying <see cref="GeoJsonExportOptions.Default"/> when no options are given.
    /// </summary>
    internal sealed class ExportGeoJsonUseCase : IGeoJsonExportService
    {
        private readonly IGeoJsonExporter _exporter;

        public ExportGeoJsonUseCase(IGeoJsonExporter exporter)
        {
            _exporter = exporter ?? throw new ArgumentNullException(nameof(exporter));
        }

        public string Export(IIwxxmReport report, GeoJsonExportOptions? options = null)
        {
            if (report == null)
                throw new ArgumentNullException(nameof(report));

            return _exporter.Export(report, options ?? GeoJsonExportOptions.Default);
        }
    }
}
