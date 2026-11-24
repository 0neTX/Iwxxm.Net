using System;

namespace Iwxxm.Net.Export.GeoPackage
{
    /// <summary>The GeoPackage specification version a file declares in its <c>user_version</c> header field.</summary>
    internal enum GeoPackageVersion
    {
        /// <summary>GeoPackage 1.4.0 (<c>user_version</c> 10400), the default.</summary>
        Version14,

        /// <summary>GeoPackage 1.3.x (<c>user_version</c> 10300).</summary>
        Version13
    }

    /// <summary>The settings of one GeoPackage write.</summary>
    internal sealed class GeoPackageWriteOptions
    {
        public GeoPackageWriteOptions(DateTimeOffset lastChange, GeoPackageVersion version = GeoPackageVersion.Version14)
        {
            if (!Enum.IsDefined(typeof(GeoPackageVersion), version))
                throw new ArgumentOutOfRangeException(nameof(version), version, "Unknown GeoPackage version.");

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
