namespace Iwxxm.Net.Reports
{
    /// <summary>
    /// Error codes emitted by more than one layer (use cases, adapters, the presentation facade). Defined once so the
    /// stable codes of docs/REFERENCE.md cannot diverge between emitters.
    /// </summary>
    internal static class SharedErrorCodes
    {
        /// <summary>A document or report mapper threw or returned nothing.</summary>
        internal const string MapperError = "MAPPER-ERROR";

        /// <summary>The GeoJSON exporter threw.</summary>
        internal const string GeoJsonExportError = "GEOJSON-EXPORT-ERROR";

        /// <summary>The XML text or file is null, empty or whitespace.</summary>
        internal const string XmlEmptyString = "XML-EMPTY-STRING";

        /// <summary>The input exceeds <c>IwxxmParserOptions.MaxDocumentBytes</c>.</summary>
        internal const string XmlSizeLimit = "XML-SIZE-LIMIT";
    }
}
