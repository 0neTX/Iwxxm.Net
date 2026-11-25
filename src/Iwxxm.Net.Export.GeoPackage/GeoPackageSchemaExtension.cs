using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Data.Sqlite;

namespace Iwxxm.Net.Export.GeoPackage
{
    /// <summary>
    /// The <c>gpkg_schema</c> extension (GeoPackage Annex F, scope <c>read-write</c>): the tables <c>gpkg_data_columns</c> and
    /// <c>gpkg_data_column_constraints</c>, both registered in <c>gpkg_extensions</c>, and one <c>gpkg_data_columns</c> row for
    /// every column of every data table that exists.
    /// </summary>
    /// <remarks>
    /// <para>The text of every row is in <see cref="ColumnsOf"/>, the only place that holds it. Readers use these columns
    /// differently: GDAL and QGIS show <c>name</c> as the alias of a field and <c>description</c> as its comment (the tooltip),
    /// and ignore <c>title</c>; <c>name</c> must therefore be short, unique within a table (the specification makes it
    /// unique) and different from the column name, ignoring case (GDAL drops an alias that only repeats the name). The <c>mime_type</c> <c>application/json</c> on <c>properties</c> is a convention of GDAL and QGIS, not of the
    /// standard: they report the field as JSON instead of text.</para>
    /// <para><c>gpkg_data_column_constraints</c> is created empty: Requirement 141 asks for both tables, and no column has an
    /// enumerated or ranged constraint. Every value is bound as a parameter; rows are written in a fixed order (the table order
    /// of <see cref="Tables"/>, then the column order of the table).</para>
    /// </remarks>
    internal static class GeoPackageSchemaExtension
    {
        internal const string ExtensionName = "gpkg_schema";
        internal const string ExtensionDefinition = "http://www.geopackage.org/spec121/#extension_schema";
        internal const string ReadWriteScope = "read-write";
        internal const string DataColumnsTable = "gpkg_data_columns";
        internal const string ConstraintsTable = "gpkg_data_column_constraints";
        internal const string JsonMimeType = "application/json";

        // The DDL GDAL 3.13 writes for both tables.
        private const string CreateDataColumns =
            "CREATE TABLE gpkg_data_columns (table_name TEXT NOT NULL,column_name TEXT NOT NULL,name TEXT,title TEXT,description TEXT," +
            "mime_type TEXT,constraint_name TEXT,CONSTRAINT pk_gdc PRIMARY KEY (table_name, column_name),CONSTRAINT gdc_tn UNIQUE (table_name, name))";

        private const string CreateConstraints =
            "CREATE TABLE gpkg_data_column_constraints (constraint_name TEXT NOT NULL,constraint_type TEXT NOT NULL,value TEXT,min NUMERIC," +
            "min_is_inclusive BOOLEAN,max NUMERIC,max_is_inclusive BOOLEAN,description TEXT,CONSTRAINT gdcc_ntv UNIQUE (constraint_name, constraint_type, value))";

        /// <summary>The text of one column in <c>gpkg_data_columns</c>.</summary>
        internal sealed class ColumnEntry
        {
            public ColumnEntry(string column, string name, string title, string description, string? mimeType = null)
            {
                Column = column;
                Name = name;
                Title = title;
                Description = description;
                MimeType = mimeType;
            }

            /// <summary>The column of the data table (<c>column_name</c>).</summary>
            public string Column { get; }

            /// <summary>The short alias (<c>name</c>): what GDAL and QGIS show as the name of the field. Unique within a table.</summary>
            public string Name { get; }

            /// <summary>The human readable label (<c>title</c>).</summary>
            public string Title { get; }

            /// <summary>The precise description in English (<c>description</c>): the comment of the field in GDAL and QGIS.</summary>
            public string Description { get; }

            /// <summary>The <c>mime_type</c>, or <c>null</c>.</summary>
            public string? MimeType { get; }
        }

        private static ColumnEntry RowId(string description) =>
            new ColumnEntry("fid", "Row ID", "Row identifier", description);

        private static ColumnEntry Geometry(string description) =>
            new ColumnEntry(GeoPackageDataTables.GeometryColumn, "Geometry", "Geometry", description);

        // The columns that the feature tables and the attributes table share, after fid and the geometry, in table order.
        private static readonly ColumnEntry[] FeatureColumns =
        {
            new ColumnEntry("feature_id", "Feature ID", "Feature identifier", "Identifier of the feature (the id of the GeoJSON feature); NULL when the feature has none"),
            new ColumnEntry("feature_type", "Feature type", "Feature type", "Kind of feature, for example METAR_OBSERVATION, SIGMET_EVOLVING_VOLUME or JETSTREAM"),
            new ColumnEntry("report_id", "Report ID", "Report identifier", "Identifier of the report the feature belongs to"),
            new ColumnEntry("report_type", "Report type", "Report type", "Type of the report the feature belongs to, for example Metar or TropicalCycloneSigmet"),
            new ColumnEntry("bulletin_identifier", "Bulletin ID", "Bulletin identifier", "Identifier of the bulletin that contains the report; NULL when the report is not in a bulletin"),
            new ColumnEntry("issue_time_utc", "Issue time", "Issue time (UTC)", "Issue time of the report (UTC)"),
            new ColumnEntry("valid_from_utc", "Valid from", "Validity start (UTC)", "Start of the validity period of the report (UTC); NULL when the report has none or does not state it"),
            new ColumnEntry("valid_until_utc", "Valid until", "Validity end (UTC)", "End of the validity period of the report (UTC); NULL when the report has none or does not state it"),
            new ColumnEntry("properties", "Properties (JSON)", "Feature properties", "All properties of the feature as a JSON object, the same text as the properties member of the GeoJSON export", JsonMimeType),
        };

        private static readonly ColumnEntry[] ReportColumns =
        {
            RowId("Row identifier, numbered in the order of the reports of the document"),
            new ColumnEntry("report_id", "Report ID", "Report identifier", "Identifier of the report"),
            new ColumnEntry("report_type", "Report type", "Report type", "Type of the report, for example Metar or TropicalCycloneSigmet"),
            new ColumnEntry("report_status", "Status", "Report status", "Status code of the report, for example NORMAL"),
            new ColumnEntry("permissible_usage", "Usage", "Permissible usage", "Permissible usage code of the report, for example OPERATIONAL"),
            new ColumnEntry("permissible_usage_reason", "Usage reason", "Permissible usage reason", "Reason code (TEST or EXERCISE) of a non-operational report; NULL when the report has none"),
            new ColumnEntry("permissible_usage_supplementary", "Usage note", "Permissible usage supplementary text", "Free text that supplements the permissible usage; NULL when the report has none"),
            new ColumnEntry("issue_time_utc", "Issue time", "Issue time (UTC)", "Issue time of the report (UTC)"),
            new ColumnEntry("valid_from_utc", "Valid from", "Validity start (UTC)", "Report validity start (UTC); NULL when the report has no validity period or does not state it"),
            new ColumnEntry("valid_until_utc", "Valid until", "Validity end (UTC)", "Report validity end (UTC); NULL when the report has no validity period or does not state it"),
            new ColumnEntry("bulletin_identifier", "Bulletin ID", "Bulletin identifier", "Identifier of the bulletin that contains the report; NULL when the report is not in a bulletin"),
        };

        private static readonly ColumnEntry[] MetadataColumns =
        {
            RowId("Row identifier, numbered in the order the keys are written"),
            new ColumnEntry("key", "Entry key", "Key", "Name of the entry: geopackage_version, library_version, source_document_id, source_document_type, bulletin_identifier or report_count"),
            new ColumnEntry("value", "Entry value", "Value", "Value of the entry"),
        };

        private static readonly Dictionary<string, ColumnEntry[]> ByTable = BuildByTable();

        /// <summary>The data tables that have entries, in the order their rows are written.</summary>
        internal static IReadOnlyList<string> Tables { get; } = new[]
        {
            GeoPackageDataTables.PointsTable,
            GeoPackageDataTables.LinesTable,
            GeoPackageDataTables.PolygonsTable,
            GeoPackageDataTables.AttributesTable,
            GeoPackageDataTables.ReportsTable,
            GeoPackageDataTables.MetadataTable,
        };

        /// <summary>
        /// The entries of <paramref name="table"/>, in the column order of the table. A data table that is not in
        /// <see cref="Tables"/> has none.
        /// </summary>
        internal static IReadOnlyList<ColumnEntry> ColumnsOf(string table) =>
            ByTable.TryGetValue(table, out ColumnEntry[]? entries) ? entries! : Array.Empty<ColumnEntry>();

        private static Dictionary<string, ColumnEntry[]> BuildByTable()
        {
            ColumnEntry[] Feature(ColumnEntry? geometry)
            {
                var entries = new List<ColumnEntry> { RowId("Row identifier, numbered in the order of the features of the document") };
                if (geometry != null)
                    entries.Add(geometry);
                entries.AddRange(FeatureColumns);
                return entries.ToArray();
            }

            return new Dictionary<string, ColumnEntry[]>(StringComparer.Ordinal)
            {
                [GeoPackageDataTables.PointsTable] = Feature(Geometry("Point geometry, longitude and latitude in decimal degrees (WGS 84, EPSG:4326)")),
                [GeoPackageDataTables.LinesTable] = Feature(Geometry("Line geometry (MultiLineString), longitude and latitude in decimal degrees (WGS 84, EPSG:4326)")),
                [GeoPackageDataTables.PolygonsTable] = Feature(Geometry("Polygon geometry (MultiPolygon), longitude and latitude in decimal degrees (WGS 84, EPSG:4326)")),
                [GeoPackageDataTables.AttributesTable] = Feature(null),
                [GeoPackageDataTables.ReportsTable] = ReportColumns,
                [GeoPackageDataTables.MetadataTable] = MetadataColumns,
            };
        }

        /// <summary>
        /// Creates <c>gpkg_extensions</c> (if absent), <c>gpkg_data_columns</c> and <c>gpkg_data_column_constraints</c>,
        /// registers the extension and writes the rows for the data tables that exist in the database. The data tables must
        /// already be written and registered in <c>gpkg_contents</c>.
        /// </summary>
        /// <exception cref="InvalidOperationException">A data table has a column without an entry, or an entry for a column that it does not have (a defect of this library).</exception>
        public static void Write(SqliteTransaction transaction)
        {
            if (transaction == null)
                throw new ArgumentNullException(nameof(transaction));

            HashSet<string> existing = TableNamesOf(transaction);

            GeoPackageSchema.EnsureExtensionsTable(transaction);
            GeoPackageSqlite.Execute(transaction, CreateDataColumns);
            GeoPackageSqlite.Execute(transaction, CreateConstraints);
            GeoPackageSchema.AddExtension(transaction, DataColumnsTable, null, ExtensionName, ExtensionDefinition, ReadWriteScope);
            GeoPackageSchema.AddExtension(transaction, ConstraintsTable, null, ExtensionName, ExtensionDefinition, ReadWriteScope);

            using (SqliteCommand command = GeoPackageSqlite.CreateCommand(
                transaction,
                "INSERT INTO gpkg_data_columns (table_name, column_name, name, title, description, mime_type, constraint_name) " +
                "VALUES ($table, $column, $name, $title, $description, $mime, NULL)"))
            {
                SqliteParameter table = command.Parameters.Add("$table", SqliteType.Text);
                SqliteParameter column = command.Parameters.Add("$column", SqliteType.Text);
                SqliteParameter name = command.Parameters.Add("$name", SqliteType.Text);
                SqliteParameter title = command.Parameters.Add("$title", SqliteType.Text);
                SqliteParameter description = command.Parameters.Add("$description", SqliteType.Text);
                SqliteParameter mime = command.Parameters.Add("$mime", SqliteType.Text);

                foreach (string dataTable in Tables.Where(existing.Contains))
                {
                    IReadOnlyList<ColumnEntry> entries = ColumnsOf(dataTable);
                    RequireSameColumns(transaction, dataTable, entries);

                    foreach (ColumnEntry entry in entries)
                    {
                        table.Value = dataTable;
                        column.Value = entry.Column;
                        name.Value = entry.Name;
                        title.Value = entry.Title;
                        description.Value = entry.Description;
                        mime.Value = entry.MimeType == null ? DBNull.Value : (object)entry.MimeType;
                        command.ExecuteNonQuery();
                    }
                }
            }
        }

        private static HashSet<string> TableNamesOf(SqliteTransaction transaction)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            using (SqliteCommand command = GeoPackageSqlite.CreateCommand(transaction, "SELECT name FROM sqlite_master WHERE type = 'table'"))
            using (SqliteDataReader reader = command.ExecuteReader())
            {
                while (reader.Read())
                    names.Add(reader.GetString(0));
            }

            return names;
        }

        // The entries are written by hand next to DDL written by hand: this stops a column added to one of them without the other
        // from producing a file that documents the wrong columns (the unit tests check the same for every table).
        private static void RequireSameColumns(SqliteTransaction transaction, string table, IReadOnlyList<ColumnEntry> entries)
        {
            var actual = new List<string>();
            using (SqliteCommand command = GeoPackageSqlite.CreateCommand(transaction, "SELECT name FROM pragma_table_info($table) ORDER BY cid"))
            {
                command.Parameters.AddWithValue("$table", table);
                using (SqliteDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                        actual.Add(reader.GetString(0));
                }
            }

            if (!actual.SequenceEqual(entries.Select(e => e.Column), StringComparer.Ordinal))
            {
                throw new InvalidOperationException(
                    $"The schema extension describes the columns [{string.Join(", ", entries.Select(e => e.Column))}] of '{table}', which has [{string.Join(", ", actual)}].");
            }
        }
    }
}
