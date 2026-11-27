using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Iwxxm.Net.Export.GeoPackage.Model;
using Microsoft.Data.Sqlite;

namespace Iwxxm.Net.Export.GeoPackage
{
    /// <summary>
    /// Writes a <see cref="GeoPackageDocumentModel"/> to a GeoPackage file: the base schema, the data tables, the
    /// registrations in <c>gpkg_contents</c> / <c>gpkg_geometry_columns</c> and, unless <see cref="GeoPackageWriteOptions.Content"/>
    /// leaves it out, the <c>gpkg_schema</c> extension (see <see cref="GeoPackageSchemaExtension"/>), in one transaction.
    /// </summary>
    /// <remarks>
    /// <para><b>The target is never half written.</b> The database is built in a temporary file in the directory of the
    /// target (same volume, named <c>.iwxxm-&lt;32 hex digits&gt;.tmp</c> so that a long target name cannot overflow the
    /// file name limit), and only a complete, committed and closed database is moved onto the target, replacing a file that
    /// is already there. When anything fails (a bad geometry, a full disk, a cancellation) the temporary file and the SQLite
    /// journal next to it are deleted, the exception propagates and the target is exactly as it was before: absent, or the
    /// previous file untouched. This is preferred over writing the target directly and deleting it on failure, which would
    /// also destroy a file that was there before the call and leaves a window in which a reader can open an incomplete
    /// file.</para>
    /// <para><b>Limits of that guarantee.</b></para>
    /// <list type="bullet">
    /// <item>A process that is killed can leave the hidden temporary file behind.</item>
    /// <item>A target, existing or not, that has a <c>-wal</c> or <c>-journal</c> file next to it is in use or needs
    /// recovery: nothing is published, because SQLite would apply a stale sidecar to the new database.</item>
    /// <item>The swap is retried twice, with a short back-off, when the operating system refuses it for a reason that can
    /// pass (for example while another process has the target open); there is no copy fallback, and a permanent failure is
    /// not retried.</item>
    /// <item>On Windows the replaced file is first moved to a backup name (<c>.&lt;name&gt;.&lt;guid&gt;.bak</c> next to the target)
    /// that is deleted after the swap. If the swap fails after the target was moved aside, the target is absent, the new file
    /// is kept as the hidden temporary file, the old one stays as the backup, and the message names both. If the swap
    /// succeeded but the backup cannot be deleted (the old file is still held open), the export still succeeds and the
    /// <c>.bak</c> file is left next to the target.</item>
    /// <item>The replaced file is the target path itself, so a symbolic link is replaced, not written through; on Unix the new
    /// file keeps its own mode and owner, not those of the file it replaces. The directory is not flushed to disk, so a power
    /// failure right after the call can lose the swap.</item>
    /// <item>Two writers must not use the same target at the same time. The database is built next to the target, so a
    /// network share without reliable file locking is not supported (a SQLite restriction).</item>
    /// </list>
    /// <para>The output is deterministic: no value comes from the clock (the <c>last_change</c> of every table is
    /// <see cref="GeoPackageWriteOptions.LastChange"/>) and rows are written in the feature order of the model. The SQLite
    /// file header embeds the version of the SQLite library that wrote it, so a comparison across SQLite versions must use
    /// the content (schema and rows), not the bytes. The <c>properties</c> text of a negative zero is <c>-0</c> on .NET and <c>0</c> on
    /// .NET Framework and Mono (the formatting of the core's JSON writer), so such a value is not the same text on every runtime.</para>
    /// </remarks>
    internal static class GeoPackageWriter
    {
        /// <summary>Writes the GeoPackage to <paramref name="path"/>, replacing the file if it exists.</summary>
        /// <exception cref="ArgumentNullException"><paramref name="path"/>, <paramref name="model"/> or <paramref name="options"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentException"><paramref name="path"/> is empty or white space, or a geometry cannot be encoded (for example a coordinate that is not finite).</exception>
        /// <exception cref="DirectoryNotFoundException">The directory of <paramref name="path"/> does not exist (it is not created).</exception>
        /// <exception cref="SqliteException">SQLite failed (for example, the disk is full).</exception>
        /// <exception cref="IOException"><paramref name="path"/> is an existing directory, or the file cannot be created or replaced, or the target has a <c>-wal</c> or <c>-journal</c> file next to it.</exception>
        /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled; checked before starting and before every feature, attribute and report row (the metadata rows are not checked).</exception>
        public static void Write(string path, GeoPackageDocumentModel model, GeoPackageWriteOptions options, CancellationToken cancellationToken = default)
        {
            if (path == null)
                throw new ArgumentNullException(nameof(path));
            if (model == null)
                throw new ArgumentNullException(nameof(model));
            if (options == null)
                throw new ArgumentNullException(nameof(options));
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("The path of the GeoPackage file must not be empty.", nameof(path));

            string target = Path.GetFullPath(path);
            string? directory = Path.GetDirectoryName(target);
            if (directory == null || !Directory.Exists(directory))
                throw new DirectoryNotFoundException($"The directory of the GeoPackage file does not exist: '{directory}'.");
            if (Directory.Exists(target))
                throw new IOException($"The GeoPackage path is a directory: '{target}'.");

            cancellationToken.ThrowIfCancellationRequested();

            string temporary = Path.Combine(directory, TemporaryPrefix + Guid.NewGuid().ToString("N") + TemporarySuffix);
            try
            {
                WriteDatabase(temporary, model, options, cancellationToken);
                Publish(temporary, target);
            }
            catch (Exception ex)
            {
                // The new database is the only copy of the data when the target was moved aside and not put back: keep it.
                if (!(ex is TemporaryFileKeptException))
                    GeoPackageExportFailure.DeleteQuietly(temporary);

                GeoPackageExportFailure.DeleteQuietly(temporary + "-journal");
                throw;
            }
        }

        private static void WriteDatabase(string path, GeoPackageDocumentModel model, GeoPackageWriteOptions options, CancellationToken cancellationToken)
        {
            // The connection is closed (pooling is off) before the caller moves or deletes the file.
            using (SqliteConnection connection = GeoPackageSqlite.Open(path))
            using (SqliteTransaction transaction = connection.BeginTransaction())
            {
                GeoPackageSchema.CreateBase(transaction, options.Version);
                GeoPackageDataTables.Write(transaction, model, options, cancellationToken);
                if (options.WritesSchemaExtension)
                    GeoPackageSchemaExtension.Write(transaction);

                transaction.Commit();
            }
        }

        private const string TemporaryPrefix = ".iwxxm-";
        private const string TemporarySuffix = ".tmp";

        // Three attempts in all: a first one and a retry after each of these pauses (milliseconds).
        private static readonly int[] RetryDelaysMilliseconds = { 50, 200 };

        /// <summary>
        /// Moves the complete database <paramref name="temporary"/> onto <paramref name="target"/>, replacing a file that is
        /// there. Refuses when a <c>-wal</c> or <c>-journal</c> file is next to the target, whether the target exists or not: SQLite
        /// would apply a stale one of an earlier database to the new file. A transient refusal of the system is retried;
        /// a permanent one (the target is a directory, the temporary file or the directory is gone) is not.
        /// </summary>
        internal static void Publish(string temporary, string target)
        {
            if (File.Exists(target + "-wal") || File.Exists(target + "-journal"))
            {
                throw new IOException(
                    $"The target is in use or needs recovery: '{target}-wal' or '{target}-journal' exists next to it. Nothing was published.");
            }

            // On Unix File.Replace with a backup name renames the target away first, which is not atomic: without one it is
            // a single rename. On Windows the backup keeps the replaced file when the swap fails half way.
            bool windows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

            for (int attempt = 0; ; attempt++)
            {
                // Evaluated again for every attempt: the target may have appeared or gone since the last one.
                bool targetExists = File.Exists(target);
                string? backup = targetExists && windows ? target + "." + Guid.NewGuid().ToString("N") + ".bak" : null;
                try
                {
                    if (targetExists)
                        File.Replace(temporary, target, backup);
                    else
                        File.Move(temporary, target);

                    // The swap is done; a backup that cannot be deleted (the old file is still held open) is left next to the target.
                    if (backup != null)
                        GeoPackageExportFailure.DeleteQuietly(backup);

                    return;
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                {
                    // Only a backup this call made (a name with a fresh GUID) proves that this call moved the target aside.
                    if (backup != null && File.Exists(backup) && !File.Exists(target))
                    {
                        throw new TemporaryFileKeptException(
                            $"The target '{target}' was moved aside to '{backup}' and could not be replaced ({ex.Message}). The new GeoPackage was kept at '{temporary}'.",
                            ex);
                    }

                    if (attempt >= RetryDelaysMilliseconds.Length || !MayBeTransient(ex, temporary, target))
                        throw;

                    Thread.Sleep(RetryDelaysMilliseconds[attempt]);
                }
            }
        }

        // A sharing violation or a lock held for a moment can pass; a missing directory or file, a path that is too long and a
        // target that is a directory cannot.
        private static bool MayBeTransient(Exception exception, string temporary, string target) =>
            !(exception is DirectoryNotFoundException || exception is FileNotFoundException || exception is PathTooLongException)
            && !Directory.Exists(target)
            && File.Exists(temporary);

        /// <summary>The swap failed after this call had moved the target aside: the temporary file is the only copy and must not be deleted.</summary>
        private sealed class TemporaryFileKeptException : IOException
        {
            public TemporaryFileKeptException(string message, Exception inner)
                : base(message, inner)
            {
            }
        }
    }
}
