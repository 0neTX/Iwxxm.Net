using System;
using System.IO;
using Iwxxm.Net.Export.GeoPackage.Model;
using Microsoft.Data.Sqlite;
using Xunit;
using static Iwxxm.Net.Export.GeoPackage.Tests.GeoPackageTestDatabase;

namespace Iwxxm.Net.Export.GeoPackage.Tests
{
    public sealed class GeoPackageSchemaTests : IDisposable
    {
        private const string Wgs84 =
            "GEOGCS[\"WGS 84\",DATUM[\"WGS_1984\",SPHEROID[\"WGS 84\",6378137,298.257223563,AUTHORITY[\"EPSG\",\"7030\"]],AUTHORITY[\"EPSG\",\"6326\"]]," +
            "PRIMEM[\"Greenwich\",0,AUTHORITY[\"EPSG\",\"8901\"]],UNIT[\"degree\",0.0174532925199433,AUTHORITY[\"EPSG\",\"9122\"]]," +
            "AXIS[\"Latitude\",NORTH],AXIS[\"Longitude\",EAST],AUTHORITY[\"EPSG\",\"4326\"]]";

        private readonly string _path = NewPath();

        public void Dispose() => File.Delete(_path);

        private static void Within(SqliteConnection connection, Action<SqliteTransaction> action)
        {
            using (SqliteTransaction transaction = connection.BeginTransaction())
            {
                action(transaction);
                transaction.Commit();
            }
        }

        [Theory]
        [InlineData(false, "10400")]
        [InlineData(true, "10300")]
        public void CreateBase_WritesTheHeaderPragmas(bool version13, string userVersion)
        {
            GeoPackageVersion version = version13 ? GeoPackageVersion.Version13 : GeoPackageVersion.Version14;
            using (SqliteConnection connection = GeoPackageSqlite.Open(_path))
                Within(connection, t => GeoPackageSchema.CreateBase(t, version));

            using (SqliteConnection reader = GeoPackageSqlite.Open(_path))
            {
                Assert.Equal("1196444487", Scalar(reader, "PRAGMA application_id"));
                Assert.Equal(0x47504B47, int.Parse(Scalar(reader, "PRAGMA application_id")!));
                Assert.Equal(userVersion, Scalar(reader, "PRAGMA user_version"));
            }
        }

        [Fact]
        public void CreateBase_WritesTheThreeMandatoryReferenceSystemsExactly()
        {
            using (SqliteConnection connection = GeoPackageSqlite.Open(_path))
            {
                Within(connection, t => GeoPackageSchema.CreateBase(t, GeoPackageVersion.Version14));

                var rows = Rows(connection, "SELECT srs_name, srs_id, organization, organization_coordsys_id, definition, description FROM gpkg_spatial_ref_sys ORDER BY srs_id");
                Assert.Equal(3, rows.Count);
                Assert.Equal(new string?[] { "Undefined Cartesian SRS", "-1", "NONE", "-1", "undefined", "undefined Cartesian coordinate reference system" }, rows[0]);
                Assert.Equal(new string?[] { "Undefined geographic SRS", "0", "NONE", "0", "undefined", "undefined geographic coordinate reference system" }, rows[1]);
                Assert.Equal(new string?[] { "WGS 84 geodetic", "4326", "EPSG", "4326", Wgs84, "longitude/latitude coordinates in decimal degrees on the WGS 84 spheroid" }, rows[2]);
            }
        }

        [Fact]
        public void CreateBase_CreatesOnlyTheThreeMandatoryTables_AndPassesTheChecks()
        {
            using (SqliteConnection connection = GeoPackageSqlite.Open(_path))
            {
                Within(connection, t => GeoPackageSchema.CreateBase(t, GeoPackageVersion.Version14));

                Assert.Equal(new[] { "gpkg_contents", "gpkg_geometry_columns", "gpkg_spatial_ref_sys" }, TableNames(connection));
                Assert.Equal("0", Scalar(connection, "SELECT count(*) FROM sqlite_master WHERE type = 'trigger'"));
                Assert.Equal("ok", Scalar(connection, "PRAGMA integrity_check"));
                Assert.Empty(Rows(connection, "PRAGMA foreign_key_check"));
            }
        }

        [Fact]
        public void Open_EnforcesForeignKeys()
        {
            using (SqliteConnection connection = GeoPackageSqlite.Open(_path))
            {
                Assert.Equal("1", Scalar(connection, "PRAGMA foreign_keys"));
                Within(connection, t => GeoPackageSchema.CreateBase(t, GeoPackageVersion.Version14));

                using (SqliteTransaction transaction = connection.BeginTransaction())
                {
                    Assert.Throws<SqliteException>(() => GeoPackageSchema.AddContents(transaction, "t", "features", string.Empty, "2026-01-01T00:00:00.000Z", null, 9999));
                }
            }
        }

        [Fact]
        public void AddContents_AndAddGeometryColumn_RegisterAFeatureTable()
        {
            using (SqliteConnection connection = GeoPackageSqlite.Open(_path))
            {
                Within(connection, t =>
                {
                    GeoPackageSchema.CreateBase(t, GeoPackageVersion.Version14);
                    GeoPackageSqlite.Execute(t, "CREATE TABLE feat (fid INTEGER PRIMARY KEY NOT NULL, geom POINT)");
                    GeoPackageSchema.AddContents(t, "feat", "features", "some features", "2026-01-02T03:04:05.678Z", new GeoPackageEnvelope(-10.5, 20.25, -3, 4.75), 4326);
                    GeoPackageSchema.AddGeometryColumn(t, "feat", "geom", "POINT");
                    GeoPackageSqlite.Execute(t, "CREATE TABLE attrs (fid INTEGER PRIMARY KEY NOT NULL)");
                    GeoPackageSchema.AddContents(t, "attrs", "attributes", string.Empty, "2026-01-02T03:04:05.678Z", null, 0);
                });

                var contents = Rows(connection, "SELECT table_name, data_type, identifier, description, last_change, min_x, min_y, max_x, max_y, srs_id FROM gpkg_contents ORDER BY table_name");
                Assert.Equal(new string?[] { "attrs", "attributes", "attrs", string.Empty, "2026-01-02T03:04:05.678Z", null, null, null, null, "0" }, contents[0]);
                Assert.Equal(new string?[] { "feat", "features", "feat", "some features", "2026-01-02T03:04:05.678Z", "-10.5", "-3", "20.25", "4.75", "4326" }, contents[1]);

                Assert.Equal(new string?[] { "feat", "geom", "POINT", "4326", "0", "0" },
                    Assert.Single(Rows(connection, "SELECT table_name, column_name, geometry_type_name, srs_id, z, m FROM gpkg_geometry_columns")));

                Assert.Equal("ok", Scalar(connection, "PRAGMA integrity_check"));
                Assert.Empty(Rows(connection, "PRAGMA foreign_key_check"));
            }
        }
    }
}
