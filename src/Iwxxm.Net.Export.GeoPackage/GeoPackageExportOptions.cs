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
        /// The default options: GeoPackage 1.4, a <see cref="LastChangeUtc"/> taken from the document, so that the same
        /// document always yields the same file, and <see cref="GeoPackageContent.SchemaExtension"/>.
        /// </summary>
        public static readonly GeoPackageExportOptions Default = new GeoPackageExportOptions();

        /// <summary>
        /// Initializes the default options: <see cref="GeoPackageVersion.Version14"/>, a last change taken from the document and
        /// <see cref="GeoPackageContent.SchemaExtension"/>.
        /// </summary>
        public GeoPackageExportOptions()
            : this(GeoPackageVersion.Version14)
        {
        }

        /// <summary>
        /// Initializes options for the given GeoPackage version, with a last change taken from the document and
        /// <see cref="GeoPackageContent.SchemaExtension"/>.
        /// </summary>
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
            : this(version, lastChangeUtc, GeoPackageContent.SchemaExtension)
        {
        }

        /// <summary>Initializes the GeoPackage export options, with the optional parts of the file selected explicitly.</summary>
        /// <param name="version">The GeoPackage version the file declares.</param>
        /// <param name="lastChangeUtc">
        /// The value of <c>gpkg_contents.last_change</c> of every table, or <c>null</c> to use the issue time of the
        /// document. The clock is never read: when the document has no usable issue time, <see cref="FallbackLastChangeUtc"/> is used.
        /// </param>
        /// <param name="content">
        /// The optional parts written next to the data tables: <see cref="GeoPackageContent.SchemaExtension"/> (the default of
        /// the other constructors) or <see cref="GeoPackageContent.None"/> for a file with only the data tables, for a reader
        /// that does not support the <c>gpkg_schema</c> extension.
        /// </param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="version"/> is not a defined <see cref="GeoPackageVersion"/>, or <paramref name="content"/> sets a bit that is not a defined <see cref="GeoPackageContent"/> flag.</exception>
        public GeoPackageExportOptions(GeoPackageVersion version, DateTimeOffset? lastChangeUtc, GeoPackageContent content)
        {
            if (!Enum.IsDefined(typeof(GeoPackageVersion), version))
                throw new ArgumentOutOfRangeException(nameof(version), version, "Unknown GeoPackage version.");
            if (!content.IsDefined())
                throw new ArgumentOutOfRangeException(nameof(content), content, "Unknown GeoPackage content flag.");

            Version = version;
            LastChangeUtc = lastChangeUtc;
            Content = content;
        }

        /// <summary>Gets the GeoPackage version the file declares.</summary>
        public GeoPackageVersion Version { get; }

        /// <summary>
        /// Gets the explicit <c>last_change</c> of the file, or <c>null</c> when it is derived from the issue time of the
        /// document (see <see cref="FallbackLastChangeUtc"/>).
        /// </summary>
        public DateTimeOffset? LastChangeUtc { get; }

        /// <summary>
        /// Gets the optional parts written next to the data tables: <see cref="GeoPackageContent.SchemaExtension"/> unless the
        /// options were created with another value. The data tables are always written.
        /// </summary>
        public GeoPackageContent Content { get; }

        /// <summary>The <c>last_change</c> to write for a document issued at <paramref name="documentIssueTimeUtc"/>: the explicit value, else its issue time, else the fallback.</summary>
        internal DateTimeOffset ResolveLastChange(DateTimeOffset documentIssueTimeUtc)
        {
            if (LastChangeUtc.HasValue)
                return LastChangeUtc.Value;

            return documentIssueTimeUtc == default(DateTimeOffset) ? FallbackLastChangeUtc : documentIssueTimeUtc;
        }
    }
}
