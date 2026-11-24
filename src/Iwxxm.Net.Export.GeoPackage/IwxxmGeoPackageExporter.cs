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
    /// <para>An export failure is a value, like the CSV export: <see cref="Export"/> returns a <see cref="Result{T}"/> with
    /// <see cref="GeoPackageErrorCodes.ExportError"/> and throws only for a <c>null</c> document. The export cannot be
    /// cancelled, like the rest of the library.</para>
    /// <para>Instances are immutable and thread-safe. Concurrent exports must not write to the same path.</para>
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
        public Result<string> Export(IwxxmDocument document, string path, GeoPackageExportOptions? options = null)
        {
            if (document == null)
                throw new ArgumentNullException(nameof(document));

            if (string.IsNullOrWhiteSpace(path))
                return Failure(document, "The path of the GeoPackage file must not be empty.");

            try
            {
                GeoPackageExportOptions effective = options ?? GeoPackageExportOptions.Default;
                GeoPackageDocumentModel model = GeoPackageFeatureSource.Read(document);
                var writeOptions = new GeoPackageWriteOptions(effective.ResolveLastChange(document.IssueTimeUtc), effective.Version);
                GeoPackageWriter.Write(path, model, writeOptions);
                return Result<string>.Success(Path.GetFullPath(path));
            }
            catch (Exception ex) when (!(ex is OutOfMemoryException))
            {
                return Failure(document, ex.Message);
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
                return Result<byte[]>.Failure(GeoPackageErrorCodes.ExportError, $"The GeoPackage export failed: {ex.Message}", ErrorCategory.DomainMapping, document.Id);
            }
            finally
            {
                if (temporary != null)
                    DeleteQuietly(temporary);
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

        private static Result<string> Failure(IwxxmDocument document, string reason) =>
            Result<string>.Failure(GeoPackageErrorCodes.ExportError, $"The GeoPackage export failed: {reason}", ErrorCategory.DomainMapping, document.Id);

        // A failed cleanup must not hide the outcome of the export.
        private static void DeleteQuietly(string path)
        {
            try
            {
                File.Delete(path);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
