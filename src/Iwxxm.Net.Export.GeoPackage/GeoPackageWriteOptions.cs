using System;

namespace Iwxxm.Net.Export.GeoPackage
{
    /// <summary>The GeoPackage specification version a file declares in its <c>user_version</c> header field.</summary>
    /// <remarks>
    /// The numeric values are part of the public contract and never change. Zero is deliberately not a member, so an
    /// uninitialized <c>default(GeoPackageVersion)</c> is rejected as an unknown version instead of silently selecting one.
    /// </remarks>
    public enum GeoPackageVersion
    {
        /// <summary>GeoPackage 1.3.x (<c>user_version</c> 10300).</summary>
        Version13 = 1,

        /// <summary>GeoPackage 1.4.0 (<c>user_version</c> 10400), the default of <see cref="GeoPackageExportOptions"/>.</summary>
        Version14 = 2
    }

    /// <summary>The text and header value a <see cref="GeoPackageVersion"/> stands for, defined once for every writer.</summary>
    internal static class GeoPackageVersionExtensions
    {
        /// <summary>The version as written to the metadata table: "1.3" or "1.4".</summary>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="version"/> is not a defined value.</exception>
        public static string ToText(this GeoPackageVersion version)
        {
            switch (version)
            {
                case GeoPackageVersion.Version14:
                    return "1.4";
                case GeoPackageVersion.Version13:
                    return "1.3";
                default:
                    throw new ArgumentOutOfRangeException(nameof(version), version, "Unknown GeoPackage version.");
            }
        }

        /// <summary>The <c>PRAGMA user_version</c> of the version: 10400 or 10300.</summary>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="version"/> is not a defined value.</exception>
        public static int ToUserVersion(this GeoPackageVersion version)
        {
            switch (version)
            {
                case GeoPackageVersion.Version14:
                    return 10400;
                case GeoPackageVersion.Version13:
                    return 10300;
                default:
                    throw new ArgumentOutOfRangeException(nameof(version), version, "Unknown GeoPackage version.");
            }
        }
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
