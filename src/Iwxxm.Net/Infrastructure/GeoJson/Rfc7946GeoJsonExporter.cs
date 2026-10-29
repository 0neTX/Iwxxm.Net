using System;
using System.Collections.Generic;
using Iwxxm.Net.Extensibility;
using Iwxxm.Net.Infrastructure.GeoJson.Writing;
using Iwxxm.Net.Reports;

namespace Iwxxm.Net.Infrastructure.GeoJson
{
    /// <summary>
    /// Zero-dependency RFC 7946 GeoJSON exporter for all ICAO/WMO IWXXM 2025-2 Domain Aggregate Roots.
    /// Translates Domain spatial primitives (<see cref="GeoCoordinate"/>, <see cref="GeoPolygon"/> with interior holes,
    /// <see cref="VerticalLimit"/>) into RFC 7946 <c>FeatureCollection</c> JSON with <c>[longitude, latitude]</c> axis order.
    /// </summary>
    /// <remarks>
    /// Features are collected through the Domain visitor (<see cref="GeoJsonFeatureCollector"/>, one feature builder per
    /// report family in <c>Infrastructure/GeoJson/Features</c>) and written by <see cref="GeoJsonFeatureCollectionWriter"/>.
    /// </remarks>
    internal sealed class Rfc7946GeoJsonExporter : IGeoJsonExporter
    {
        public string Export(IIwxxmReport report, GeoJsonExportOptions options)
        {
            if (report == null)
                throw new ArgumentNullException(nameof(report));

            IReadOnlyList<GeoJsonFeature> features = report.Accept(new GeoJsonFeatureCollector());
            return GeoJsonFeatureCollectionWriter.Write(report, features, options ?? GeoJsonExportOptions.Default);
        }
    }
}
