namespace Iwxxm.Net.Export.GeoPackage
{
    /// <summary>
    /// The stable <see cref="ValidationError.Code"/> of the GeoPackage export, defined once. It is listed, with the codes of
    /// the core library, in <c>docs/REFERENCE.md</c> (a test keeps the list and the code in sync).
    /// </summary>
    public static class GeoPackageErrorCodes
    {
        /// <summary>
        /// The GeoPackage could not be written: the target path is empty, invalid or not writable, its directory does not
        /// exist, or the exporter failed (<see cref="ErrorCategory.DomainMapping"/>, located at the document id).
        /// </summary>
        public const string ExportError = "GEOPACKAGE-EXPORT-ERROR";
    }
}
