namespace Iwxxm.Net
{
    /// <summary>
    /// Formatting and RFC 7946 options for exporting IWXXM 2025-2 Domain Aggregate Roots to GeoJSON.
    /// </summary>
    public sealed class GeoJsonExportOptions
    {
        /// <summary>
        /// Compact single-line RFC 7946 GeoJSON output with bounding box enabled.
        /// </summary>
        public static readonly GeoJsonExportOptions Default = new GeoJsonExportOptions(indented: false, includeBoundingBox: true);

        /// <summary>
        /// Pretty-printed (indented) RFC 7946 GeoJSON output with bounding box enabled.
        /// </summary>
        public static readonly GeoJsonExportOptions Pretty = new GeoJsonExportOptions(indented: true, includeBoundingBox: true);

        /// <summary>
        /// Gets a value indicating whether the GeoJSON output should be formatted with indentation and newlines.
        /// </summary>
        public bool Indented { get; }

        /// <summary>
        /// Gets a value indicating whether the root <c>FeatureCollection</c> should include a computed RFC 7946 <c>"bbox"</c>
        /// array (<c>[minLon, minLat, maxLon, maxLat]</c>) when spatial coordinates are present.
        /// </summary>
        public bool IncludeBoundingBox { get; }

        /// <summary>Initializes the export options.</summary>
        /// <param name="indented"><c>true</c> to write indented, multi-line GeoJSON; <c>false</c> (default) for a single compact line.</param>
        /// <param name="includeBoundingBox"><c>true</c> (default) to add the RFC 7946 <c>bbox</c> member to the root <c>FeatureCollection</c> when it has coordinates.</param>
        public GeoJsonExportOptions(bool indented = false, bool includeBoundingBox = true)
        {
            Indented = indented;
            IncludeBoundingBox = includeBoundingBox;
        }
    }
}
