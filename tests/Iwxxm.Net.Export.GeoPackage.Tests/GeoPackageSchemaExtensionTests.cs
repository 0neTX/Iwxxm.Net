using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Data.Sqlite;
using Xunit;
using static Iwxxm.Net.Export.GeoPackage.Tests.GeoPackageTestDatabase;

namespace Iwxxm.Net.Export.GeoPackage.Tests
{
    /// <summary>The <c>gpkg_schema</c> extension: <c>gpkg_extensions</c>, <c>gpkg_data_columns</c> and <c>gpkg_data_column_constraints</c>.</summary>
    public sealed class GeoPackageSchemaExtensionTests : IDisposable
    {
        private static readonly DateTimeOffset LastChange = new DateTimeOffset(2026, 5, 6, 7, 8, 9, 123, TimeSpan.Zero);

        private static readonly string[] SchemaTables = { "gpkg_data_column_constraints", "gpkg_data_columns", "gpkg_extensions" };

        private readonly string _directory = Path.Combine(Path.GetTempPath(), "iwxxm-gpkg-schema-ext-" + Guid.NewGuid().ToString("N"));

        public GeoPackageSchemaExtensionTests() => Directory.CreateDirectory(_directory);

        public void Dispose() => Directory.Delete(_directory, recursive: true);

        public static IEnumerable<object[]> AllSamples() => SampleFiles.AllPaths().Select(p => new object[] { p });

        private string Write(string sample, GeoPackageContent content, GeoPackageVersion version = GeoPackageVersion.Version14)
        {
            string path = Path.Combine(_directory, Guid.NewGuid().ToString("N") + ".gpkg");
            GeoPackageWriter.Write(path, GeoPackageDataTablesTests.Read(sample), new GeoPackageWriteOptions(LastChange, version, content));
            return path;
        }

        private static string[] ColumnsOf(SqliteConnection connection, string table) =>
            Rows(connection, $"SELECT name FROM pragma_table_info('{table}') ORDER BY cid").Select(r => r[0]!).ToArray();

        [Fact]
        public void TheContentFlags_AreStable_AndOnlyTheDefinedBitsAreDefined()
        {
            Assert.Equal(0, (int)GeoPackageContent.None);
            Assert.Equal(1, (int)GeoPackageContent.SchemaExtension);
            Assert.Equal(GeoPackageContent.SchemaExtension, GeoPackageContentExtensions.Default);
            Assert.True(GeoPackageContent.None.IsDefined());
            Assert.True(GeoPackageContent.SchemaExtension.IsDefined());
            Assert.False(((GeoPackageContent)2).IsDefined());
            Assert.False(((GeoPackageContent)3).IsDefined());
            Assert.False((~GeoPackageContent.None).IsDefined());
        }

        [Fact]
        public void TheDefaultWriteOptions_WriteTheSchemaExtension()
        {
            var options = new GeoPackageWriteOptions(LastChange);
            Assert.Equal(GeoPackageContent.SchemaExtension, options.Content);
            Assert.True(options.WritesSchemaExtension);
            Assert.False(new GeoPackageWriteOptions(LastChange, GeoPackageVersion.Version14, GeoPackageContent.None).WritesSchemaExtension);
        }

        [Fact]
        public void Extension_RegistersBothTables_AsReadWrite_InAFixedOrder()
        {
            using (SqliteConnection connection = GeoPackageSqlite.Open(Write(SampleFiles.PathOf("sigmet-A6-2-TC.xml"), GeoPackageContent.SchemaExtension)))
            {
                Assert.Equal(
                    new[]
                    {
                        new string?[] { "gpkg_data_columns", null, "gpkg_schema", "http://www.geopackage.org/spec121/#extension_schema", "read-write" },
                        new string?[] { "gpkg_data_column_constraints", null, "gpkg_schema", "http://www.geopackage.org/spec121/#extension_schema", "read-write" },
                    },
                    Rows(connection, "SELECT table_name, column_name, extension_name, definition, scope FROM gpkg_extensions ORDER BY rowid"));
                Assert.Empty(Rows(connection, "SELECT * FROM gpkg_data_column_constraints"));
                Assert.Equal("ok", Scalar(connection, "PRAGMA integrity_check"));
                Assert.Empty(Rows(connection, "PRAGMA foreign_key_check"));
            }
        }

        [Fact]
        public void DataColumns_HoldTheTextOfEveryColumn_AndTheJsonTypeOfPropertiesOnly()
        {
            using (SqliteConnection connection = GeoPackageSqlite.Open(Write(SampleFiles.PathOf("sigmet-A6-2-TC.xml"), GeoPackageContent.SchemaExtension)))
            {
                string?[] validFrom = Assert.Single(Rows(connection, "SELECT name, title, description, mime_type, constraint_name FROM gpkg_data_columns WHERE table_name = 'iwxxm_reports' AND column_name = 'valid_from_utc'"));
                Assert.Equal("Valid from", validFrom[0]);
                Assert.Equal("Validity start (UTC)", validFrom[1]);
                Assert.Equal("Report validity start (UTC); NULL when the report has no validity period or does not state it", validFrom[2]);
                Assert.Null(validFrom[3]);
                Assert.Null(validFrom[4]);

                Assert.Equal(
                    new[] { "iwxxm_attributes", "iwxxm_lines", "iwxxm_points", "iwxxm_polygons" }.Where(t => TableNames(connection).Contains(t)).ToArray(),
                    Rows(connection, "SELECT DISTINCT table_name FROM gpkg_data_columns WHERE mime_type IS NOT NULL ORDER BY table_name").Select(r => r[0]!).ToArray());
                Assert.Empty(Rows(connection, "SELECT table_name, column_name FROM gpkg_data_columns WHERE mime_type IS NOT NULL AND (column_name <> 'properties' OR mime_type <> 'application/json')"));
                Assert.Empty(Rows(connection, "SELECT table_name, column_name FROM gpkg_data_columns WHERE column_name = 'properties' AND mime_type IS NOT 'application/json'"));
                Assert.Empty(Rows(connection, "SELECT * FROM gpkg_data_columns WHERE name IS NULL OR title IS NULL OR description IS NULL OR constraint_name IS NOT NULL"));
                Assert.Equal("Point geometry, longitude and latitude in decimal degrees (WGS 84, EPSG:4326)",
                    Scalar(connection, "SELECT description FROM gpkg_data_columns WHERE table_name = 'iwxxm_points' AND column_name = 'geom'"));
            }
        }

        [Fact]
        public void DataColumns_AreWrittenTableByTable_InColumnOrder()
        {
            using (SqliteConnection connection = GeoPackageSqlite.Open(Write(SampleFiles.PathOf("sigmet-A6-2-TC.xml"), GeoPackageContent.SchemaExtension)))
            {
                var expected = new List<string>();
                foreach (string table in GeoPackageSchemaExtension.Tables.Where(t => TableNames(connection).Contains(t)))
                    expected.AddRange(ColumnsOf(connection, table).Select(c => table + "." + c));

                Assert.Equal(expected, Rows(connection, "SELECT table_name || '.' || column_name FROM gpkg_data_columns ORDER BY rowid").Select(r => r[0]!).ToList());
            }
        }

        // The contract: the text is kept in one table, and a column that is added to a data table (or removed from it) without
        // touching that table fails here, for every table that any sample produces.
        [Theory]
        [MemberData(nameof(AllSamples))]
        public void EveryColumnOfEveryDataTable_HasExactlyOneEntry_AndNoEntryIsLeftOver(string sample)
        {
            using (SqliteConnection connection = GeoPackageSqlite.Open(Write(sample, GeoPackageContent.SchemaExtension)))
            {
                List<string> tables = TableNames(connection).Where(t => t.StartsWith("iwxxm_", StringComparison.Ordinal)).ToList();
                Assert.Equal(tables.OrderBy(t => t, StringComparer.Ordinal), GeoPackageSchemaExtension.Tables.Where(tables.Contains).OrderBy(t => t, StringComparer.Ordinal));

                foreach (string table in tables)
                {
                    Assert.Equal(ColumnsOf(connection, table), GeoPackageSchemaExtension.ColumnsOf(table).Select(e => e.Column).ToArray());
                    Assert.Equal(
                        ColumnsOf(connection, table),
                        Rows(connection, $"SELECT column_name FROM gpkg_data_columns WHERE table_name = '{table}' ORDER BY rowid").Select(r => r[0]!).ToArray());
                }

                // Nothing is described that is not a column of a table of the file.
                Assert.Empty(Rows(connection, "SELECT d.table_name, d.column_name FROM gpkg_data_columns d LEFT JOIN gpkg_contents c ON c.table_name = d.table_name WHERE c.table_name IS NULL"));
                Assert.Equal(
                    tables.Sum(t => ColumnsOf(connection, t).Length).ToString(System.Globalization.CultureInfo.InvariantCulture),
                    Scalar(connection, "SELECT count(*) FROM gpkg_data_columns"));
            }
        }

        [Fact]
        public void TheSamples_ProduceEveryTableThatHasEntries()
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (string sample in SampleFiles.AllPaths())
            {
                using (SqliteConnection connection = GeoPackageSqlite.Open(Write(sample, GeoPackageContent.SchemaExtension)))
                    seen.UnionWith(Rows(connection, "SELECT table_name FROM gpkg_data_columns").Select(r => r[0]!));
            }

            Assert.Equal(GeoPackageSchemaExtension.Tables.OrderBy(t => t, StringComparer.Ordinal), seen.OrderBy(t => t, StringComparer.Ordinal));
        }

        [Fact]
        public void TheEntries_AreComplete_UniquePerTable_AndOnlyPropertiesIsJson()
        {
            Assert.Equal(6, GeoPackageSchemaExtension.Tables.Count);
            Assert.Empty(GeoPackageSchemaExtension.ColumnsOf("iwxxm_unknown"));

            foreach (string table in GeoPackageSchemaExtension.Tables)
            {
                IReadOnlyList<GeoPackageSchemaExtension.ColumnEntry> entries = GeoPackageSchemaExtension.ColumnsOf(table);
                Assert.Equal(entries.Count, entries.Select(e => e.Column).Distinct(StringComparer.Ordinal).Count());
                Assert.Equal(entries.Count, entries.Select(e => e.Name).Distinct(StringComparer.Ordinal).Count()); // the specification: UNIQUE (table_name, name)
                Assert.Equal("fid", entries[0].Column);
                Assert.All(entries, e =>
                {
                    Assert.False(string.IsNullOrWhiteSpace(e.Name), table + "." + e.Column);
                    Assert.False(string.IsNullOrWhiteSpace(e.Title), table + "." + e.Column);
                    Assert.False(string.IsNullOrWhiteSpace(e.Description), table + "." + e.Column);
                    Assert.NotEqual(e.Column, e.Name, StringComparer.OrdinalIgnoreCase); // GDAL drops an alias that only repeats the column name
                    Assert.True(e.Name.Length <= 24, "the name is an alias and must stay short: " + table + "." + e.Column);
                    Assert.Equal(e.Column == "properties" ? "application/json" : null, e.MimeType);
                });
            }

            Assert.Equal("geom", GeoPackageSchemaExtension.ColumnsOf("iwxxm_points")[1].Column);
            Assert.Equal("feature_id", GeoPackageSchemaExtension.ColumnsOf("iwxxm_attributes")[1].Column);
            Assert.Contains("valid_from_utc", GeoPackageSchemaExtension.ColumnsOf("iwxxm_polygons").Select(e => e.Column));
            Assert.Contains("valid_until_utc", GeoPackageSchemaExtension.ColumnsOf("iwxxm_reports").Select(e => e.Column));
        }

        // The reference lists the text of every row (it is documentation of a file format: a change of the text is a visible change).
        [Fact]
        public void TheReference_ListsTheTextOfEveryEntry()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null && !File.Exists(Path.Combine(directory.FullName, "Iwxxm.Net.slnx")))
                directory = directory.Parent;
            Assert.True(directory != null, "Iwxxm.Net.slnx was not found above the test output directory.");
            string reference = File.ReadAllText(Path.Combine(directory!.FullName, "docs", "REFERENCE.md"));

            foreach (string table in GeoPackageSchemaExtension.Tables)
            {
                foreach (GeoPackageSchemaExtension.ColumnEntry entry in GeoPackageSchemaExtension.ColumnsOf(table))
                {
                    // geom differs per table: its text is quoted in a paragraph instead of the table row.
                    if (entry.Column != "geom")
                        Assert.Contains("| `" + entry.Column + "` | " + entry.Name + " | " + entry.Title + " | " + entry.Description + " |", reference, StringComparison.Ordinal);
                    else
                        Assert.Contains("\"" + entry.Description + "\"", reference, StringComparison.Ordinal);
                }
            }
        }

        [Fact]
        public void WithContentNone_NoSchemaTableIsWritten()
        {
            foreach (string sample in new[] { "sigmet-A6-2-TC.xml", "metar-A3-1.xml", "metar-NIL-collect.xml" })
            {
                using (SqliteConnection connection = GeoPackageSqlite.Open(Write(SampleFiles.PathOf(sample), GeoPackageContent.None)))
                {
                    List<string> tables = TableNames(connection);
                    Assert.DoesNotContain(tables, t => SchemaTables.Contains(t));
                    Assert.Equal(0, tables.Count(t => t.StartsWith("gpkg_", StringComparison.Ordinal) && t != "gpkg_contents" && t != "gpkg_geometry_columns" && t != "gpkg_spatial_ref_sys"));
                    Assert.Equal("0", Scalar(connection, "SELECT count(*) FROM sqlite_master WHERE sql LIKE '%gpkg_schema%' OR name LIKE '%data_column%'"));
                    Assert.Equal("ok", Scalar(connection, "PRAGMA integrity_check"));
                }
            }
        }

        // With the content off the file is the file that was written before the extension existed: the same canonical dump,
        // and with it on the dump is that one plus the three tables (their schema objects and rows) and nothing else.
        [Theory]
        [InlineData("metar-A3-1.xml", false)]
        [InlineData("sigmet-A6-2-TC.xml", false)]
        [InlineData("metar-NIL-collect.xml", false)]
        [InlineData("sigwx-swh-2025-2.xml", true)]
        public void TheBaseTables_AreTheSameCanonicalDump_WithAndWithoutTheExtension(string sample, bool extra)
        {
            string path = extra ? SampleFiles.ExtraPathOf(sample) : SampleFiles.PathOf(sample);
            string with, without;
            using (SqliteConnection connection = GeoPackageSqlite.Open(Write(path, GeoPackageContent.SchemaExtension)))
                with = GeoPackageCanonicalDump.Dump(connection);
            using (SqliteConnection connection = GeoPackageSqlite.Open(Write(path, GeoPackageContent.None)))
                without = GeoPackageCanonicalDump.Dump(connection);

            Assert.Contains("# gpkg_data_columns:", with);
            Assert.DoesNotContain("gpkg_data_columns", without);
            Assert.Equal(without, WithoutTables(with, SchemaTables));
        }

        [Fact]
        public void TheSameDocument_YieldsTheSameDump_Twice_WithTheExtension()
        {
            string sample = SampleFiles.PathOf("sigmet-A6-2-TC.xml");
            using (SqliteConnection first = GeoPackageSqlite.Open(Write(sample, GeoPackageContent.SchemaExtension)))
            using (SqliteConnection second = GeoPackageSqlite.Open(Write(sample, GeoPackageContent.SchemaExtension)))
                Assert.Equal(GeoPackageCanonicalDump.Dump(first), GeoPackageCanonicalDump.Dump(second));
        }

        [Theory]
        [InlineData(GeoPackageVersion.Version13)]
        [InlineData(GeoPackageVersion.Version14)]
        public void APlainSqliteReader_StillReadsTheBaseTables_WhateverTheContent(GeoPackageVersion version)
        {
            foreach (GeoPackageContent content in new[] { GeoPackageContent.None, GeoPackageContent.SchemaExtension })
            {
                using (SqliteConnection connection = GeoPackageSqlite.Open(Write(SampleFiles.PathOf("sigmet-A6-2-TC.xml"), content, version)))
                {
                    Assert.NotEqual("0", Scalar(connection, "SELECT count(*) FROM iwxxm_polygons"));
                    Assert.Equal(Scalar(connection, "SELECT count(*) FROM iwxxm_polygons"), Scalar(connection, "SELECT count(*) FROM iwxxm_polygons WHERE json_valid(properties) AND geom IS NOT NULL"));
                    Assert.Equal("1", Scalar(connection, "SELECT count(*) FROM iwxxm_reports"));
                    Assert.Equal(version == GeoPackageVersion.Version13 ? "1.3" : "1.4", Scalar(connection, "SELECT value FROM iwxxm_metadata WHERE key = 'geopackage_version'"));
                    Assert.Equal(
                        new[] { "iwxxm_metadata", "iwxxm_polygons", "iwxxm_reports" }.Where(t => TableNames(connection).Contains(t)).Count().ToString(System.Globalization.CultureInfo.InvariantCulture),
                        Scalar(connection, "SELECT count(*) FROM gpkg_contents WHERE table_name IN ('iwxxm_metadata', 'iwxxm_polygons', 'iwxxm_reports')"));
                }
            }
        }

        [Fact]
        public void Write_RefusesATableWhoseColumnsAreNotTheDescribedOnes()
        {
            string path = Path.Combine(_directory, "defect.gpkg");
            using (SqliteConnection connection = GeoPackageSqlite.Open(path))
            using (SqliteTransaction transaction = connection.BeginTransaction())
            {
                GeoPackageSchema.CreateBase(transaction, GeoPackageVersion.Version14);
                GeoPackageSqlite.Execute(transaction, "CREATE TABLE iwxxm_metadata (fid INTEGER PRIMARY KEY NOT NULL, key TEXT NOT NULL, value TEXT NOT NULL, extra TEXT)");

                var failure = Assert.Throws<InvalidOperationException>(() => GeoPackageSchemaExtension.Write(transaction));
                Assert.Contains("iwxxm_metadata", failure.Message);
                Assert.Contains("extra", failure.Message);
            }
        }

        [Fact]
        public void Write_UsesTheExistingExtensionsTable_AndKeepsItsRows()
        {
            string path = Path.Combine(_directory, "existing.gpkg");
            using (SqliteConnection connection = GeoPackageSqlite.Open(path))
            using (SqliteTransaction transaction = connection.BeginTransaction())
            {
                GeoPackageSchema.CreateBase(transaction, GeoPackageVersion.Version14);
                GeoPackageSchema.EnsureExtensionsTable(transaction);
                GeoPackageSchema.AddExtension(transaction, "other", "col", "vendor_other", "http://example.org/other", "write-only");
                GeoPackageSchema.EnsureExtensionsTable(transaction); // a second call changes nothing

                GeoPackageSchemaExtension.Write(transaction);
                transaction.Commit();
            }

            using (SqliteConnection connection = GeoPackageSqlite.Open(path))
            {
                Assert.Equal(
                    new[] { "other|col|vendor_other", "gpkg_data_columns||gpkg_schema", "gpkg_data_column_constraints||gpkg_schema" },
                    Rows(connection, "SELECT table_name || '|' || ifnull(column_name, '') || '|' || extension_name FROM gpkg_extensions ORDER BY rowid").Select(r => r[0]!).ToArray());
                Assert.Empty(Rows(connection, "SELECT * FROM gpkg_data_columns")); // no data table exists in this file
            }
        }

        // Removes the tables from a canonical dump (see GeoPackageCanonicalDump): their schema lines and their sections.
        private static string WithoutTables(string dump, IReadOnlyCollection<string> tables)
        {
            var kept = new List<string>();
            bool inSchema = false;
            bool skipping = false;
            foreach (string line in dump.Split('\n'))
            {
                if (line.StartsWith("# ", StringComparison.Ordinal))
                {
                    inSchema = line == "# schema";
                    skipping = !inSchema && tables.Contains(line.Substring(2, line.IndexOf(':') - 2));
                }
                else if (inSchema)
                {
                    // type, name, tbl_name, sql: an index that SQLite made for a constraint of one of the tables has its tbl_name.
                    string[] parts = line.Split('\t');
                    if (parts.Length >= 3 && tables.Contains(parts[2]))
                        continue;
                }

                if (!skipping)
                    kept.Add(line);
            }

            return string.Join("\n", kept);
        }
    }
}
