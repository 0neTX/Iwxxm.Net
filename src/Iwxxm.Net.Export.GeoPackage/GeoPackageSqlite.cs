using System;
using Microsoft.Data.Sqlite;

namespace Iwxxm.Net.Export.GeoPackage
{
    /// <summary>
    /// The one place that opens the SQLite database a GeoPackage is written to, so that every export uses the same
    /// connection settings.
    /// </summary>
    internal static class GeoPackageSqlite
    {
        /// <summary>
        /// Opens (creating it if it does not exist) the SQLite database at <paramref name="path"/>.
        /// </summary>
        /// <remarks>
        /// Connection pooling is off: a pooled connection keeps the file open after <see cref="IDisposable.Dispose"/>, which
        /// on Windows prevents the caller from deleting or moving the file right after the export. No native library
        /// initialization is needed: <c>Microsoft.Data.Sqlite</c> initializes its bundled SQLite on first use.
        /// </remarks>
        internal static SqliteConnection Open(string path)
        {
            if (path == null)
                throw new ArgumentNullException(nameof(path));

            var connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = path,
                Mode = SqliteOpenMode.ReadWriteCreate,
                Pooling = false,
            }.ToString();

            var connection = new SqliteConnection(connectionString);
            try
            {
                connection.Open();
                return connection;
            }
            catch
            {
                connection.Dispose();
                throw;
            }
        }
    }
}
