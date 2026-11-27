using System;
using System.Globalization;
using System.IO;
using Microsoft.Data.Sqlite;
using Xunit;
using Xunit.Abstractions;

namespace Iwxxm.Net.Export.GeoPackage.Tests
{
    /// <summary>
    /// Verifies that the bundled native SQLite loads (with no initialization call) on every target of this test project and
    /// that a database written through the package seam can be reopened. On net48 this is the check that the native
    /// library is found next to the test assembly.
    /// </summary>
    public sealed class SqliteSmokeTests
    {
        private readonly ITestOutputHelper _output;

        public SqliteSmokeTests(ITestOutputHelper output) => _output = output;

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

        /// <summary>
        /// The test to look for in the first run on a new platform (the Windows CI job runs it on net10.0 and on the real
        /// .NET Framework 4.8, x64 and x86): the whole public path, with nothing mocked. Exporting a document loads the
        /// bundled native SQLite, creates the database, writes the document and closes it; the file must then be readable
        /// and, being a plain file, deletable at once (an open handle would make the delete fail on Windows).
        /// </summary>
        [Fact]
        public void NativeLoad_PublicExport_WritesAReadableGeoPackage_AndReleasesTheFile()
        {
            string path = Path.Combine(Path.GetTempPath(), "iwxxm-gpkg-native-" + Guid.NewGuid().ToString("N") + ".gpkg");
            try
            {
                var result = IwxxmParserGeoPackageExtensions.ParseFileToGeoPackage(SampleFiles.PathOf("metar-A3-1.xml"), path);

                Assert.True(result.IsSuccess, string.Join(" | ", result.Errors) + " (process " + IntPtr.Size * 8 + "-bit)");
                using (SqliteConnection connection = GeoPackageSqlite.Open(path))
                {
                    string version = Scalar(connection, "SELECT sqlite_version()");
                    // Shown by `dotnet test --logger "console;verbosity=detailed"`: which runtime and bitness ran this.
                    _output.WriteLine("NativeLoad: SQLite " + version + ", " + (Environment.Is64BitProcess ? "64" : "32") + "-bit process, " + System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription);
                    Assert.False(string.IsNullOrEmpty(version));
                    Assert.Equal("ok", Scalar(connection, "PRAGMA integrity_check"));
                    Assert.NotEqual("0", Scalar(connection, "SELECT count(*) FROM iwxxm_reports"));
                    Assert.NotEqual("0", Scalar(connection, "SELECT count(*) FROM gpkg_contents"));
                }
            }
            finally
            {
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
