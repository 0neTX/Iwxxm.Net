using System;
using System.IO;
using System.Threading;
using Iwxxm.Net.Export.GeoPackage.Model;
using Microsoft.Data.Sqlite;

namespace Iwxxm.Net.Export.GeoPackage
{
    /// <summary>
    /// Writes a <see cref="GeoPackageDocumentModel"/> to a GeoPackage file: the base schema, the data tables and the
    /// registrations in <c>gpkg_contents</c> / <c>gpkg_geometry_columns</c>, in one transaction.
    /// </summary>
    /// <remarks>
    /// <para><b>The target is never half written.</b> The database is built in a temporary file in the directory of the
    /// target (same volume), and only a complete, committed and closed database is moved onto the target, replacing a
    /// file that is already there. When anything fails (a bad geometry, a full disk, a cancellation) the temporary file
    /// and the SQLite journal next to it are deleted, the exception propagates and the target is exactly as it was before:
    /// absent, or the previous file untouched. This is preferred over writing the target directly and deleting it on
    /// failure, which would also destroy a file that was there before the call and leaves a window in which a reader can
    /// open an incomplete file.</para>
    /// <para>The output is deterministic: no value comes from the clock (the <c>last_change</c> of every table is
    /// <see cref="GeoPackageWriteOptions.LastChange"/>) and rows are written in the feature order of the model. The SQLite
    /// file header embeds the version of the SQLite library that wrote it, so a comparison across SQLite versions must use
    /// the content (schema and rows), not the bytes.</para>
    /// </remarks>
    internal static class GeoPackageWriter
    {
        /// <summary>Writes the GeoPackage to <paramref name="path"/>, replacing the file if it exists.</summary>
        /// <exception cref="ArgumentNullException"><paramref name="path"/>, <paramref name="model"/> or <paramref name="options"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentException"><paramref name="path"/> is empty or white space, or a geometry cannot be encoded (for example a coordinate that is not finite).</exception>
        /// <exception cref="DirectoryNotFoundException">The directory of <paramref name="path"/> does not exist (it is not created).</exception>
        /// <exception cref="IOException"><paramref name="path"/> is an existing directory, or the file cannot be created or replaced.</exception>
        /// <exception cref="SqliteException">SQLite failed (for example, the disk is full).</exception>
        /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled; checked before every row.</exception>
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

            string temporary = Path.Combine(directory, "." + Path.GetFileName(target) + "." + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                WriteDatabase(temporary, model, options, cancellationToken);
                Publish(temporary, target);
            }
            catch
            {
                DeleteQuietly(temporary);
                DeleteQuietly(temporary + "-journal");
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
                transaction.Commit();
            }
        }

        private static void Publish(string temporary, string target)
        {
            if (File.Exists(target))
                File.Replace(temporary, target, null);
            else
                File.Move(temporary, target);
        }

        // A failed cleanup must not hide the exception that caused it.
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
