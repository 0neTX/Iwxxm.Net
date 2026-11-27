using System;
using System.Globalization;
using Iwxxm.Net.Export.GeoPackage.Model;
using Microsoft.Data.Sqlite;

namespace Iwxxm.Net.Export.GeoPackage
{
    /// <summary>
    /// The tables every GeoPackage has (<c>gpkg_spatial_ref_sys</c>, <c>gpkg_contents</c>, <c>gpkg_geometry_columns</c>),
    /// the two header pragmas and the helpers that register a data table. The DDL and the three mandatory reference
    /// system rows are the ones GDAL 3.13 writes (and its validator accepts).
    /// </summary>
    /// <remarks>
    /// Rows are inserted in the order the foreign keys need: reference systems, then contents, then geometry columns. No
    /// triggers or R-tree are created: none of them is required for a plain feature table. The <c>gpkg_extensions</c> table
    /// is created only by <see cref="EnsureExtensionsTable"/>, when an extension is registered.
    /// </remarks>
    internal static class GeoPackageSchema
    {
        /// <summary>The SRS of every table: WGS 84 longitude/latitude.</summary>
        internal const int Wgs84SrsId = 4326;

        /// <summary>The reference system for data with no defined Cartesian coordinate system (required by the specification).</summary>
        internal const int UndefinedCartesianSrsId = -1;

        /// <summary>The reference system GDAL gives an attributes table (a table without geometry).</summary>
        internal const int UndefinedGeographicSrsId = 0;

        /// <summary>The <c>application_id</c> of every GeoPackage: the ASCII text "GPKG" (0x47504B47).</summary>
        internal const int ApplicationId = 0x47504B47;

        internal const string FeaturesDataType = "features";
        internal const string AttributesDataType = "attributes";

        // The WKT 1 text GDAL writes for EPSG:4326, also in a 1.4 file (there is no WKT 2 column).
        private const string Wgs84Definition =
            "GEOGCS[\"WGS 84\",DATUM[\"WGS_1984\",SPHEROID[\"WGS 84\",6378137,298.257223563,AUTHORITY[\"EPSG\",\"7030\"]]," +
            "AUTHORITY[\"EPSG\",\"6326\"]],PRIMEM[\"Greenwich\",0,AUTHORITY[\"EPSG\",\"8901\"]]," +
            "UNIT[\"degree\",0.0174532925199433,AUTHORITY[\"EPSG\",\"9122\"]],AXIS[\"Latitude\",NORTH],AXIS[\"Longitude\",EAST]," +
            "AUTHORITY[\"EPSG\",\"4326\"]]";

        // The double space in "definition  TEXT" is how the specification spells the column.
        private const string CreateSpatialRefSys =
            "CREATE TABLE gpkg_spatial_ref_sys (srs_name TEXT NOT NULL,srs_id INTEGER NOT NULL PRIMARY KEY," +
            "organization TEXT NOT NULL,organization_coordsys_id INTEGER NOT NULL,definition  TEXT NOT NULL,description TEXT)";

        private const string CreateContents =
            "CREATE TABLE gpkg_contents (table_name TEXT NOT NULL PRIMARY KEY,data_type TEXT NOT NULL,identifier TEXT UNIQUE," +
            "description TEXT DEFAULT '',last_change DATETIME NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now'))," +
            "min_x DOUBLE, min_y DOUBLE,max_x DOUBLE, max_y DOUBLE,srs_id INTEGER," +
            "CONSTRAINT fk_gc_r_srs_id FOREIGN KEY (srs_id) REFERENCES gpkg_spatial_ref_sys(srs_id))";

        private const string CreateGeometryColumns =
            "CREATE TABLE gpkg_geometry_columns (table_name TEXT NOT NULL,column_name TEXT NOT NULL," +
            "geometry_type_name TEXT NOT NULL,srs_id INTEGER NOT NULL,z TINYINT NOT NULL,m TINYINT NOT NULL," +
            "CONSTRAINT pk_geom_cols PRIMARY KEY (table_name, column_name),CONSTRAINT uk_gc_table_name UNIQUE (table_name)," +
            "CONSTRAINT fk_gc_tn FOREIGN KEY (table_name) REFERENCES gpkg_contents(table_name)," +
            "CONSTRAINT fk_gc_srs FOREIGN KEY (srs_id) REFERENCES gpkg_spatial_ref_sys (srs_id))";

        /// <summary>
        /// Writes the header pragmas (<c>application_id</c> "GPKG" and the <c>user_version</c> of
        /// <paramref name="version"/>), creates the three mandatory tables and inserts the three mandatory reference
        /// systems (undefined Cartesian, undefined geographic and WGS 84).
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="transaction"/> is <c>null</c>.</exception>
        public static void CreateBase(SqliteTransaction transaction, GeoPackageVersion version)
        {
            if (transaction == null)
                throw new ArgumentNullException(nameof(transaction));

            // PRAGMA values cannot be bound; these are constants and enum-derived numbers, never built from input.
            GeoPackageSqlite.Execute(transaction, "PRAGMA application_id = " + ApplicationId.ToString(CultureInfo.InvariantCulture));
            GeoPackageSqlite.Execute(transaction, "PRAGMA user_version = " + version.ToUserVersion().ToString(CultureInfo.InvariantCulture));

            GeoPackageSqlite.Execute(transaction, CreateSpatialRefSys);
            GeoPackageSqlite.Execute(transaction, CreateContents);
            GeoPackageSqlite.Execute(transaction, CreateGeometryColumns);

            InsertSpatialRef(transaction, "Undefined Cartesian SRS", UndefinedCartesianSrsId, "NONE", UndefinedCartesianSrsId, "undefined", "undefined Cartesian coordinate reference system");
            InsertSpatialRef(transaction, "Undefined geographic SRS", UndefinedGeographicSrsId, "NONE", UndefinedGeographicSrsId, "undefined", "undefined geographic coordinate reference system");
            InsertSpatialRef(transaction, "WGS 84 geodetic", Wgs84SrsId, "EPSG", Wgs84SrsId, Wgs84Definition, "longitude/latitude coordinates in decimal degrees on the WGS 84 spheroid");
        }

        /// <summary>
        /// Registers a table in <c>gpkg_contents</c>. For a features table <paramref name="extent"/> is the plain minimum and
        /// maximum of the written coordinates (not a GeoJSON bounding box); for an attributes table it is <c>null</c>.
        /// </summary>
        public static void AddContents(
            SqliteTransaction transaction,
            string tableName,
            string dataType,
            string description,
            string lastChange,
            GeoPackageEnvelope? extent,
            int srsId)
        {
            using (SqliteCommand command = GeoPackageSqlite.CreateCommand(
                transaction,
                "INSERT INTO gpkg_contents (table_name, data_type, identifier, description, last_change, min_x, min_y, max_x, max_y, srs_id) " +
                "VALUES ($table, $type, $identifier, $description, $lastChange, $minX, $minY, $maxX, $maxY, $srs)"))
            {
                command.Parameters.AddWithValue("$table", tableName);
                command.Parameters.AddWithValue("$type", dataType);
                command.Parameters.AddWithValue("$identifier", tableName);
                command.Parameters.AddWithValue("$description", description);
                command.Parameters.AddWithValue("$lastChange", lastChange);
                command.Parameters.AddWithValue("$minX", extent.HasValue ? extent.Value.MinX : (object)DBNull.Value);
                command.Parameters.AddWithValue("$minY", extent.HasValue ? extent.Value.MinY : (object)DBNull.Value);
                command.Parameters.AddWithValue("$maxX", extent.HasValue ? extent.Value.MaxX : (object)DBNull.Value);
                command.Parameters.AddWithValue("$maxY", extent.HasValue ? extent.Value.MaxY : (object)DBNull.Value);
                command.Parameters.AddWithValue("$srs", srsId);
                command.ExecuteNonQuery();
            }
        }

        /// <summary>Registers the geometry column of a features table in <c>gpkg_geometry_columns</c> (2D: z and m are 0).</summary>
        public static void AddGeometryColumn(SqliteTransaction transaction, string tableName, string columnName, string geometryTypeName)
        {
            using (SqliteCommand command = GeoPackageSqlite.CreateCommand(
                transaction,
                "INSERT INTO gpkg_geometry_columns (table_name, column_name, geometry_type_name, srs_id, z, m) " +
                "VALUES ($table, $column, $type, $srs, 0, 0)"))
            {
                command.Parameters.AddWithValue("$table", tableName);
                command.Parameters.AddWithValue("$column", columnName);
                command.Parameters.AddWithValue("$type", geometryTypeName);
                command.Parameters.AddWithValue("$srs", Wgs84SrsId);
                command.ExecuteNonQuery();
            }
        }

        // The DDL GDAL 3.13 writes for the registry of extensions (the specification, 1.4, Annex F).
        private const string CreateExtensions =
            "CREATE TABLE gpkg_extensions (table_name TEXT,column_name TEXT,extension_name TEXT NOT NULL,definition TEXT NOT NULL," +
            "scope TEXT NOT NULL,CONSTRAINT ge_tce UNIQUE (table_name, column_name, extension_name))";

        /// <summary>Creates <c>gpkg_extensions</c> unless the database already has it.</summary>
        /// <exception cref="ArgumentNullException"><paramref name="transaction"/> is <c>null</c>.</exception>
        public static void EnsureExtensionsTable(SqliteTransaction transaction)
        {
            if (transaction == null)
                throw new ArgumentNullException(nameof(transaction));

            using (SqliteCommand command = GeoPackageSqlite.CreateCommand(transaction, "SELECT count(*) FROM sqlite_master WHERE type = 'table' AND name = 'gpkg_extensions'"))
            {
                if (Convert.ToInt64(command.ExecuteScalar(), CultureInfo.InvariantCulture) == 0)
                    GeoPackageSqlite.Execute(transaction, CreateExtensions);
            }
        }

        /// <summary>
        /// Registers an extension for a whole table (<paramref name="columnName"/> is <c>null</c>) or for one column of it, in
        /// <c>gpkg_extensions</c>, which must exist.
        /// </summary>
        public static void AddExtension(SqliteTransaction transaction, string tableName, string? columnName, string extensionName, string definition, string scope)
        {
            using (SqliteCommand command = GeoPackageSqlite.CreateCommand(
                transaction,
                "INSERT INTO gpkg_extensions (table_name, column_name, extension_name, definition, scope) " +
                "VALUES ($table, $column, $extension, $definition, $scope)"))
            {
                command.Parameters.AddWithValue("$table", tableName);
                command.Parameters.AddWithValue("$column", columnName == null ? DBNull.Value : (object)columnName);
                command.Parameters.AddWithValue("$extension", extensionName);
                command.Parameters.AddWithValue("$definition", definition);
                command.Parameters.AddWithValue("$scope", scope);
                command.ExecuteNonQuery();
            }
        }

        private static void InsertSpatialRef(
            SqliteTransaction transaction,
            string name,
            int id,
            string organization,
            int organizationId,
            string definition,
            string description)
        {
            using (SqliteCommand command = GeoPackageSqlite.CreateCommand(
                transaction,
                "INSERT INTO gpkg_spatial_ref_sys (srs_name, srs_id, organization, organization_coordsys_id, definition, description) " +
                "VALUES ($name, $id, $organization, $organizationId, $definition, $description)"))
            {
                command.Parameters.AddWithValue("$name", name);
                command.Parameters.AddWithValue("$id", id);
                command.Parameters.AddWithValue("$organization", organization);
                command.Parameters.AddWithValue("$organizationId", organizationId);
                command.Parameters.AddWithValue("$definition", definition);
                command.Parameters.AddWithValue("$description", description);
                command.ExecuteNonQuery();
            }
        }
    }
}
