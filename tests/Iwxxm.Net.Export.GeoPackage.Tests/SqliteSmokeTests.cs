using System;
using System.Globalization;
using System.IO;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Iwxxm.Net.Export.GeoPackage.Tests
{
    /// <summary>
    /// Verifies that the bundled native SQLite loads (with no initialization call) on every target of this test project and
    /// that a database written through the package seam can be reopened. On net48 this is the check that the native
    /// library is found next to the test assembly.
    /// </summary>
    public sealed class SqliteSmokeTests
    {
        [Fact]
        public void BlankDatabase_WrittenThroughTheSeam_ReopensAndPassesIntegrityCheck()
        {
            string path = Path.Combine(Path.GetTempPath(), "iwxxm-gpkg-smoke-" + Guid.NewGuid().ToString("N") + ".sqlite");
            try
            {
                using (SqliteConnection writer = GeoPackageSqlite.Open(path))
                {
                    Execute(writer, "CREATE TABLE t (id INTEGER PRIMARY KEY, name TEXT NOT NULL)");
                    Execute(writer, "INSERT INTO t (name) VALUES ('a')");
                }

                Assert.True(File.Exists(path));

                using (SqliteConnection reader = GeoPackageSqlite.Open(path))
                {
                    Assert.Equal("ok", Scalar(reader, "PRAGMA integrity_check"));
                    Assert.Equal("1", Scalar(reader, "SELECT count(*) FROM t"));
                    Assert.False(string.IsNullOrEmpty(Scalar(reader, "SELECT sqlite_version()")));
                }
            }
            finally
            {
                // Pooling=False in the seam: the file is released as soon as the connections are disposed (it would stay
                // locked on Windows otherwise), so this delete must succeed.
                File.Delete(path);
            }

            Assert.False(File.Exists(path));
        }

        [Fact]
        public void Open_WithANullPath_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => GeoPackageSqlite.Open(null!));
        }

        private static void Execute(SqliteConnection connection, string sql)
        {
            using (SqliteCommand command = connection.CreateCommand())
            {
                command.CommandText = sql;
                command.ExecuteNonQuery();
            }
        }

        private static string Scalar(SqliteConnection connection, string sql)
        {
            using (SqliteCommand command = connection.CreateCommand())
            {
                command.CommandText = sql;
                return Convert.ToString(command.ExecuteScalar(), CultureInfo.InvariantCulture) ?? string.Empty;
            }
        }
    }
}
