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
        /// initialization is needed: <c>Microsoft.Data.Sqlite</c> initializes its bundled SQLite on first use. Foreign keys
        /// are enforced, so rows must be inserted in the order the GeoPackage tables reference each other.
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
                ForeignKeys = true,
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

        /// <summary>Creates a command that runs inside <paramref name="transaction"/>.</summary>
        internal static SqliteCommand CreateCommand(SqliteTransaction transaction, string sql)
        {
            if (transaction == null)
                throw new ArgumentNullException(nameof(transaction));

            SqliteCommand command = transaction.Connection!.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = sql;
            return command;
        }

        /// <summary>Runs a statement with no parameters (DDL, or a pragma with fixed text) inside <paramref name="transaction"/>.</summary>
        internal static void Execute(SqliteTransaction transaction, string sql)
        {
            using (SqliteCommand command = CreateCommand(transaction, sql))
                command.ExecuteNonQuery();
        }
    }
}
