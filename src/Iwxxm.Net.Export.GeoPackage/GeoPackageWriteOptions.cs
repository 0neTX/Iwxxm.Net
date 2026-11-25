using System;

namespace Iwxxm.Net.Export.GeoPackage
{
    /// <summary>The settings of one GeoPackage write.</summary>
    internal sealed class GeoPackageWriteOptions
    {
        /// <remarks>The version is validated once, by <see cref="GeoPackageExportOptions"/>; an undefined value that gets here anyway is refused by <see cref="GeoPackageVersionExtensions"/> when the file is written.</remarks>
        public GeoPackageWriteOptions(DateTimeOffset lastChange, GeoPackageVersion version = GeoPackageVersion.Version14)
        {
            LastChange = lastChange;
            Version = version;
        }

        /// <summary>
        /// The value of <c>gpkg_contents.last_change</c> of every table. It is always given explicitly, never read from the
        /// clock, so that the same document written twice yields the same file.
        /// </summary>
        public DateTimeOffset LastChange { get; }

        /// <summary>The GeoPackage version the file declares.</summary>
        public GeoPackageVersion Version { get; }
    }
}
