using System;
using System.Collections.Generic;
using Iwxxm.Net.Extensibility;
using Iwxxm.Net.Infrastructure.GeoJson.Writing;
using Iwxxm.Net.Reports;

namespace Iwxxm.Net.Infrastructure.GeoJson
{
    /// <summary>
    /// Zero-dependency RFC 7946 GeoJSON exporter for every mapped document (IWXXM 2025-2 reports and COLLECT bulletins).
    /// Translates Domain spatial primitives (<see cref="GeoCoordinate"/>, <see cref="GeoPolygon"/> with interior holes,
    /// <see cref="VerticalLimit"/>) into RFC 7946 <c>FeatureCollection</c> JSON with <c>[longitude, latitude]</c> axis order.
    /// </summary>
    /// <remarks>
    /// Features are collected through the Domain visitor (<see cref="GeoJsonFeatureCollector"/>, one feature builder per
    /// report family in <c>Infrastructure/GeoJson/Features</c>) and written by <see cref="GeoJsonFeatureCollectionWriter"/>.
    /// </remarks>
    internal sealed class Rfc7946GeoJsonExporter : IGeoJsonExporter
    {
        public string Export(IwxxmDocument document, GeoJsonExportOptions options)
        {
            if (document == null)
                throw new ArgumentNullException(nameof(document));

            IReadOnlyList<GeoJsonFeature> features = document.Accept(new GeoJsonFeatureCollector());
            return GeoJsonFeatureCollectionWriter.Write(document, features, options ?? GeoJsonExportOptions.Default);
        }
    }
}
