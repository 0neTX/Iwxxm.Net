using System;

namespace Iwxxm.Net.Export.GeoPackage
{
    /// <summary>The options of a GeoPackage export.</summary>
    /// <remarks>
    /// The constructors have no optional parameters on purpose: a new option will be added as a new constructor overload,
    /// so that code compiled against an earlier version keeps binding to the constructor it was compiled with.
    /// </remarks>
    public sealed class GeoPackageExportOptions
    {
        /// <summary>
        /// The value written when neither <see cref="LastChangeUtc"/> nor the issue time of the document is usable (the
        /// issue time is the default value of <see cref="DateTimeOffset"/>): <c>1970-01-01T00:00:00Z</c>, the Unix epoch.
        /// It is a constant, never the current time, so the output stays deterministic.
        /// </summary>
        public static readonly DateTimeOffset FallbackLastChangeUtc = new DateTimeOffset(1970, 1, 1, 0, 0, 0, TimeSpan.Zero);

        /// <summary>
        /// The default options: GeoPackage 1.4 and a <see cref="LastChangeUtc"/> taken from the document, so that the same
        /// document always yields the same file.
        /// </summary>
        public static readonly GeoPackageExportOptions Default = new GeoPackageExportOptions();

        /// <summary>Initializes the default options: <see cref="GeoPackageVersion.Version14"/> and a last change taken from the document.</summary>
        public GeoPackageExportOptions()
            : this(GeoPackageVersion.Version14)
        {
        }

        /// <summary>Initializes options for the given GeoPackage version, with a last change taken from the document.</summary>
        /// <param name="version">The GeoPackage version the file declares.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="version"/> is not a defined <see cref="GeoPackageVersion"/> (for example <c>default(GeoPackageVersion)</c>).</exception>
        public GeoPackageExportOptions(GeoPackageVersion version)
            : this(version, null)
        {
        }

        /// <summary>Initializes the GeoPackage export options.</summary>
        /// <param name="version">The GeoPackage version the file declares.</param>
        /// <param name="lastChangeUtc">
        /// The value of <c>gpkg_contents.last_change</c> of every table, or <c>null</c> to use the issue time of the
        /// document. The clock is never read: when the document has no usable issue time, <see cref="FallbackLastChangeUtc"/> is used.
        /// </param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="version"/> is not a defined <see cref="GeoPackageVersion"/> (for example <c>default(GeoPackageVersion)</c>).</exception>
        public GeoPackageExportOptions(GeoPackageVersion version, DateTimeOffset? lastChangeUtc)
        {
            if (!Enum.IsDefined(typeof(GeoPackageVersion), version))
                throw new ArgumentOutOfRangeException(nameof(version), version, "Unknown GeoPackage version.");

            Version = version;
            LastChangeUtc = lastChangeUtc;
        }

        /// <summary>Gets the GeoPackage version the file declares.</summary>
        public GeoPackageVersion Version { get; }

        /// <summary>
        /// Gets the explicit <c>last_change</c> of the file, or <c>null</c> when it is derived from the issue time of the
        /// document (see <see cref="FallbackLastChangeUtc"/>).
        /// </summary>
        public DateTimeOffset? LastChangeUtc { get; }

        /// <summary>The <c>last_change</c> to write for a document issued at <paramref name="documentIssueTimeUtc"/>: the explicit value, else its issue time, else the fallback.</summary>
        internal DateTimeOffset ResolveLastChange(DateTimeOffset documentIssueTimeUtc)
        {
            if (LastChangeUtc.HasValue)
                return LastChangeUtc.Value;

            return documentIssueTimeUtc == default(DateTimeOffset) ? FallbackLastChangeUtc : documentIssueTimeUtc;
        }
    }
}
