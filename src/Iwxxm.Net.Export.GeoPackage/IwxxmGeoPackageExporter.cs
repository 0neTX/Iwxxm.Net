using System;
using System.IO;
using Iwxxm.Net.Export.GeoPackage.Model;
using Iwxxm.Net.Reports;

namespace Iwxxm.Net.Export.GeoPackage
{
    /// <summary>
    /// Public facade for exporting any parsed <see cref="IwxxmDocument"/> to an OGC GeoPackage (<c>.gpkg</c>) file with the
    /// features of the GeoJSON export, one table per geometry kind, the reports and the document metadata.
    /// </summary>
    /// <remarks>
    /// <para>SQLite writes to a file, so the main operation takes a file path (<see cref="Export"/>); the bytes of the file
    /// are offered as a convenience (<see cref="ExportToBytes"/>) through a temporary file that is always deleted.</para>
    /// <para><b>The target is never half written.</b> The file is built under a temporary name in the directory of the
    /// target and moved onto it only when it is complete. When the export fails, a file that was already at the path is
    /// untouched and no temporary file is left behind.</para>
    /// <para><b>The output is deterministic.</b> No value comes from the clock: <c>last_change</c> is
    /// <see cref="GeoPackageExportOptions.LastChangeUtc"/>, by default the issue time of the document. Two exports of the
    /// same document with the same options have the same content (the SQLite file header records the version of the SQLite
    /// library that wrote it, so compare content, not bytes, across SQLite versions).</para>
    /// <para><b>The schema extension.</b> By default the file also holds the <c>gpkg_schema</c> extension (a short name, a title
    /// and a description for every column, and the JSON type of <c>properties</c>), which GDAL and QGIS use for field aliases
    /// and comments. It is part of the GeoPackage standard with the scope <c>read-write</c>: a reader that does not support
    /// it should be given a file written with options that have <see cref="GeoPackageContent.None"/>.</para>
    /// <para>An export failure is a value, like the CSV export: <see cref="Export"/> returns a <see cref="Result{T}"/> with
    /// <see cref="GeoPackageErrorCodes.ExportError"/> and throws only for a <c>null</c> document. The export cannot be
    /// cancelled, like the rest of the library.</para>
    /// <para>Instances are immutable and thread-safe. Two exports to the same path, at the same time or one after the other
    /// while another process holds the file, are not supported.</para>
    /// <para><b>Things to know when you call it from a service.</b> The message of a failure can contain host paths, library
    /// locations and the text of an internal exception (it ends with the innermost exception when that is a different one):
    /// log it, do not return it to end users. <see cref="OutOfMemoryException"/> is not converted to a failure. The path is
    /// used as given (it is not confined to a directory): never pass text from an untrusted source as the path. A symbolic
    /// link at the path is replaced, not written through. A target, existing or not, that has a <c>-wal</c> or
    /// <c>-journal</c> file next to it is in use or needs recovery: nothing is published. On Unix the new file keeps its own
    /// mode and owner, not those of the file it replaces. The directory is not flushed to disk, so a power failure right
    /// after the call can lose the swap. A process that is killed can leave a hidden <c>.iwxxm-*.tmp</c> file in the
    /// directory. On Windows, if the swap fails after the target was moved aside, the target is absent, the new file stays
    /// as the hidden <c>.iwxxm-*.tmp</c> file, the old one as a <c>.bak</c> file, and the message names them; if the swap
    /// succeeded but the old file was still held open, a <c>.bak</c> file can be left next to the target. The database is
    /// built next to the target, so a network share without reliable file locking is not supported (a SQLite restriction).
    /// <see cref="ExportToBytes"/> uses the shared temporary directory of the machine for a moment: on a multi-user host
    /// another user may be able to read it, so call <see cref="Export"/> with a path in a private directory instead.</para>
    /// <para>The add-on reads internals of <c>Iwxxm.Net</c> and needs the version of <c>Iwxxm.Net</c> it was built against
    /// (never an older one); with another version an export fails with <see cref="GeoPackageErrorCodes.ExportError"/> and a
    /// message that says so.</para>
    /// </remarks>
    public sealed class IwxxmGeoPackageExporter
    {
        private static readonly Lazy<IwxxmGeoPackageExporter> DefaultInstance =
            new Lazy<IwxxmGeoPackageExporter>(() => new IwxxmGeoPackageExporter(), isThreadSafe: true);

        private IwxxmGeoPackageExporter()
        {
        }

        /// <summary>Gets the shared instance.</summary>
        public static IwxxmGeoPackageExporter Default => DefaultInstance.Value;

        /// <summary>Writes <paramref name="document"/> to the GeoPackage file <paramref name="path"/>, replacing a file that is already there.</summary>
        /// <param name="document">The parsed document.</param>
        /// <param name="path">The file to write. Its directory must exist (it is not created).</param>
        /// <param name="options">The options, or <c>null</c> for <see cref="GeoPackageExportOptions.Default"/>.</param>
        /// <returns>
        /// The full path of the file written, or a failure with <see cref="GeoPackageErrorCodes.ExportError"/>
        /// (<see cref="ErrorCategory.DomainMapping"/>, located at the document id) when the path is empty or invalid, its
        /// directory does not exist, it is a directory or cannot be written, or the export failed. The target is then as it
        /// was before the call.
        /// </returns>
        /// <exception cref="ArgumentNullException"><paramref name="document"/> is <c>null</c> (a programming error).</exception>
        public Result<string> Export(IwxxmDocument document, string path, GeoPackageExportOptions? options = null) =>
            ExportReadingModelWith(document, path, options, GeoPackageFeatureSource.Read);

        /// <summary>
        /// The export with the reading of the model injected, so that a test can make the write fail after it has started
        /// (an <see cref="IwxxmDocument"/> cannot be built outside the core to carry data the writer rejects).
        /// </summary>
        internal Result<string> ExportReadingModelWith(IwxxmDocument document, string path, GeoPackageExportOptions? options, Func<IwxxmDocument, GeoPackageDocumentModel> readModel)
        {
            if (document == null)
                throw new ArgumentNullException(nameof(document));

            if (string.IsNullOrWhiteSpace(path))
                return GeoPackageExportFailure.Create<string>(document.Id, "The path of the GeoPackage file must not be empty.");

            try
            {
                GeoPackageExportOptions effective = options ?? GeoPackageExportOptions.Default;
                GeoPackageDocumentModel model = readModel(document);
                var writeOptions = new GeoPackageWriteOptions(effective.ResolveLastChange(document.IssueTimeUtc), effective.Version, effective.Content);
                GeoPackageWriter.Write(path, model, writeOptions);
                return Result<string>.Success(Path.GetFullPath(path));
            }
            catch (Exception ex) when (!(ex is OutOfMemoryException))
            {
                return GeoPackageExportFailure.Create<string>(document.Id, ex);
            }
        }

        /// <summary>
        /// Exports <paramref name="document"/> and returns the bytes of the GeoPackage file. SQLite only writes to a file, so
        /// the file is written to a temporary path that is deleted before the method returns, whatever the outcome.
        /// </summary>
        /// <param name="document">The parsed document.</param>
        /// <param name="options">The options, or <c>null</c> for <see cref="GeoPackageExportOptions.Default"/>.</param>
        /// <returns>The file content, or a failure with <see cref="GeoPackageErrorCodes.ExportError"/>, as for <see cref="Export"/>.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="document"/> is <c>null</c>.</exception>
        public Result<byte[]> ExportToBytes(IwxxmDocument document, GeoPackageExportOptions? options = null)
        {
            if (document == null)
                throw new ArgumentNullException(nameof(document));

            string? temporary = null;
            try
            {
                temporary = Path.Combine(Path.GetTempPath(), "iwxxm-" + Guid.NewGuid().ToString("N") + ".gpkg");
                Result<string> written = Export(document, temporary, options);
                if (written.IsFailure)
                    return Result<byte[]>.Failure(written.Errors);

                return Result<byte[]>.Success(File.ReadAllBytes(temporary));
            }
            catch (Exception ex) when (!(ex is OutOfMemoryException))
            {
                return GeoPackageExportFailure.Create<byte[]>(document.Id, ex);
            }
            finally
            {
                if (temporary != null)
                    GeoPackageExportFailure.DeleteQuietly(temporary);
            }
        }

        /// <summary>Writes <paramref name="document"/> to the GeoPackage file <paramref name="path"/> using <see cref="Default"/>.</summary>
        /// <returns>See <see cref="Export"/>: the full path of the file or a <see cref="GeoPackageErrorCodes.ExportError"/> failure.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="document"/> is <c>null</c>.</exception>
        public static Result<string> ToGeoPackage(IwxxmDocument document, string path, GeoPackageExportOptions? options = null) =>
            Default.Export(document, path, options);

        /// <summary>Exports <paramref name="document"/> to the bytes of a GeoPackage file using <see cref="Default"/>.</summary>
        /// <returns>See <see cref="ExportToBytes"/>: the file content or a <see cref="GeoPackageErrorCodes.ExportError"/> failure.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="document"/> is <c>null</c>.</exception>
        public static Result<byte[]> ToGeoPackageBytes(IwxxmDocument document, GeoPackageExportOptions? options = null) =>
            Default.ExportToBytes(document, options);
    }
}
