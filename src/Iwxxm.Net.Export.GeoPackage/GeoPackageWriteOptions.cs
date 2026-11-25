using System;

namespace Iwxxm.Net.Export.GeoPackage
{
    /// <summary>The settings of one GeoPackage write.</summary>
    internal sealed class GeoPackageWriteOptions
    {
        /// <remarks>
        /// The version and the content are validated once, by <see cref="GeoPackageExportOptions"/>; an undefined version that
        /// gets here anyway is refused by <see cref="GeoPackageVersionExtensions"/> when the file is written, and undefined
        /// content bits are ignored.
        /// </remarks>
        public GeoPackageWriteOptions(
            DateTimeOffset lastChange,
            GeoPackageVersion version = GeoPackageVersion.Version14,
            GeoPackageContent content = GeoPackageContentExtensions.Default)
        {
            LastChange = lastChange;
            Version = version;
            Content = content;
        }

        /// <summary>
        /// The value of <c>gpkg_contents.last_change</c> of every table. It is always given explicitly, never read from the
        /// clock, so that the same document written twice yields the same file.
        /// </summary>
        public DateTimeOffset LastChange { get; }

        /// <summary>The GeoPackage version the file declares.</summary>
        public GeoPackageVersion Version { get; }

        /// <summary>The optional parts that are written next to the data tables.</summary>
        public GeoPackageContent Content { get; }

        /// <summary><c>true</c> when the <c>gpkg_schema</c> extension is written.</summary>
        public bool WritesSchemaExtension => (Content & GeoPackageContent.SchemaExtension) != 0;
    }
}
