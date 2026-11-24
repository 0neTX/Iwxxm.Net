using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using Iwxxm.Net.Export.GeoPackage.Model;
using Microsoft.Data.Sqlite;
using Xunit;
using static Iwxxm.Net.Export.GeoPackage.Tests.GeoPackageTestDatabase;

namespace Iwxxm.Net.Export.GeoPackage.Tests
{
    public sealed class GeoPackageDataTablesTests
    {
        private static readonly DateTimeOffset LastChange = new DateTimeOffset(2026, 5, 6, 7, 8, 9, 123, TimeSpan.Zero);
        private const string LastChangeText = "2026-05-06T07:08:09.123Z";

        public static IEnumerable<object[]> AllSamples() => SampleFiles.AllPaths().Select(p => new object[] { p });

        internal static GeoPackageDocumentModel Read(string path)
        {
            var result = IwxxmParser.ParseFile(path);
            Assert.True(result.IsSuccess, string.Join(" | ", result.Errors));
            return GeoPackageFeatureSource.Read(result.Value);
        }

        private static string Tables(GeoPackageDocumentModel model, GeoPackageVersion version = GeoPackageVersion.Version14)
        {
            string path = NewPath();
            try
            {
                var options = new GeoPackageWriteOptions(LastChange, version);
                using (SqliteConnection connection = GeoPackageSqlite.Open(path))
                using (SqliteTransaction transaction = connection.BeginTransaction())
                {
                    GeoPackageSchema.CreateBase(transaction, version);
                    GeoPackageDataTables.Write(transaction, model, options, CancellationToken.None);
                    transaction.Commit();
                }

                return path;
            }
            catch
            {
                File.Delete(path);
                throw;
            }
        }

        private static string TableOf(GeoPackageGeometryKind kind) =>
            kind == GeoPackageGeometryKind.Point ? "iwxxm_points"
            : kind == GeoPackageGeometryKind.MultiLineString ? "iwxxm_lines"
            : "iwxxm_polygons";

        private static string[] ColumnsOf(SqliteConnection connection, string table) =>
            Rows(connection, $"SELECT name || ' ' || type || ' ' || \"notnull\" || ' ' || pk FROM pragma_table_info('{table}') ORDER BY cid").Select(r => r[0]!).ToArray();

        [Theory]
        [MemberData(nameof(AllSamples))]
        public void Tables_ExistOnlyForTheGeometryFamiliesWithRows_AndCountTheFeatures(string sample)
        {
            GeoPackageDocumentModel model = Read(sample);
            string path = Tables(model);
            try
            {
                using (SqliteConnection connection = GeoPackageSqlite.Open(path))
                {
                    var expected = new List<string> { "gpkg_contents", "gpkg_geometry_columns", "gpkg_spatial_ref_sys", "iwxxm_attributes", "iwxxm_metadata", "iwxxm_reports" };
                    foreach (var group in model.Features.Where(f => f.Geometry != null).GroupBy(f => TableOf(f.Geometry!.Kind)))
                    {
                        expected.Add(group.Key);
                        Assert.Equal(group.Count().ToString(CultureInfo.InvariantCulture), Scalar(connection, $"SELECT count(*) FROM {group.Key}"));
                    }

                    expected.Sort(StringComparer.Ordinal);
                    Assert.Equal(expected, TableNames(connection));
                    Assert.Equal(model.Features.Count(f => f.Geometry == null).ToString(CultureInfo.InvariantCulture), Scalar(connection, "SELECT count(*) FROM iwxxm_attributes"));
                    Assert.Equal(model.Reports.Count.ToString(CultureInfo.InvariantCulture), Scalar(connection, "SELECT count(*) FROM iwxxm_reports"));

                    // Contents and geometry columns list exactly the data tables.
                    Assert.Equal(
                        expected.Where(t => t.StartsWith("iwxxm_", StringComparison.Ordinal)).ToList(),
                        Rows(connection, "SELECT table_name FROM gpkg_contents ORDER BY table_name").Select(r => r[0]!).ToList());
                    Assert.Equal(
                        expected.Where(t => t == "iwxxm_points" || t == "iwxxm_lines" || t == "iwxxm_polygons").ToList(),
                        Rows(connection, "SELECT table_name FROM gpkg_geometry_columns ORDER BY table_name").Select(r => r[0]!).ToList());
                }
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Theory]
        [MemberData(nameof(AllSamples))]
        public void Contents_CarryThePlainExtentOfTheWrittenCoordinates(string sample)
        {
            GeoPackageDocumentModel model = Read(sample);
            string path = Tables(model);
            try
            {
                using (SqliteConnection connection = GeoPackageSqlite.Open(path))
                {
                    foreach (var group in model.Features.Where(f => f.Geometry != null).GroupBy(f => TableOf(f.Geometry!.Kind)))
                    {
                        GeoPackageEnvelope[] envelopes = group.Select(f => f.Geometry!.Envelope()!.Value).ToArray();
                        string?[] row = Assert.Single(Rows(connection, $"SELECT min_x, min_y, max_x, max_y, srs_id, data_type, last_change FROM gpkg_contents WHERE table_name = '{group.Key}'"));
                        Assert.Equal(envelopes.Min(e => e.MinX), double.Parse(row[0]!, CultureInfo.InvariantCulture));
                        Assert.Equal(envelopes.Min(e => e.MinY), double.Parse(row[1]!, CultureInfo.InvariantCulture));
                        Assert.Equal(envelopes.Max(e => e.MaxX), double.Parse(row[2]!, CultureInfo.InvariantCulture));
                        Assert.Equal(envelopes.Max(e => e.MaxY), double.Parse(row[3]!, CultureInfo.InvariantCulture));
                        Assert.Equal("4326", row[4]);
                        Assert.Equal("features", row[5]);
                        Assert.Equal(LastChangeText, row[6]);
                    }

                    foreach (string?[] row in Rows(connection, "SELECT table_name, min_x, min_y, max_x, max_y, srs_id, last_change FROM gpkg_contents WHERE data_type = 'attributes'"))
                    {
                        Assert.Equal(new string?[] { null, null, null, null }, row.Skip(1).Take(4).ToArray());
                        Assert.Equal("0", row[5]);
                        Assert.Equal(LastChangeText, row[6]);
                    }
                }
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public void Tables_HaveTheFixedColumns()
        {
            string sample = SampleFiles.AllPaths().First(p => ContainsKinds(Read(p), GeoPackageGeometryKind.Point, GeoPackageGeometryKind.MultiPolygon));
            string path = Tables(Read(sample));
            try
            {
                using (SqliteConnection connection = GeoPackageSqlite.Open(path))
                {
                    string[] common =
                    {
                        "feature_id TEXT 0 0", "feature_type TEXT 0 0", "report_id TEXT 0 0", "report_type TEXT 0 0",
                        "bulletin_identifier TEXT 0 0", "issue_time_utc DATETIME 0 0", "properties TEXT 1 0"
                    };

                    Assert.Equal(new[] { "fid INTEGER 1 1", "geom POINT 1 0" }.Concat(common), ColumnsOf(connection, "iwxxm_points"));
                    Assert.Equal(new[] { "fid INTEGER 1 1", "geom MULTIPOLYGON 1 0" }.Concat(common), ColumnsOf(connection, "iwxxm_polygons"));
                    Assert.Equal(new[] { "fid INTEGER 1 1" }.Concat(common), ColumnsOf(connection, "iwxxm_attributes"));
                    Assert.Equal(
                        new[]
                        {
                            "fid INTEGER 1 1", "report_id TEXT 1 0", "report_type TEXT 1 0", "report_status TEXT 1 0",
                            "permissible_usage TEXT 1 0", "issue_time_utc DATETIME 1 0", "bulletin_identifier TEXT 0 0"
                        },
                        ColumnsOf(connection, "iwxxm_reports"));
                    Assert.Equal(new[] { "fid INTEGER 1 1", "key TEXT 1 0", "value TEXT 1 0" }, ColumnsOf(connection, "iwxxm_metadata"));

                    // No AUTOINCREMENT, hence no sqlite_sequence, and no triggers.
                    Assert.Equal("0", Scalar(connection, "SELECT count(*) FROM sqlite_master WHERE name = 'sqlite_sequence' OR type = 'trigger'"));
                }
            }
            finally
            {
                File.Delete(path);
            }
        }

        private static bool ContainsKinds(GeoPackageDocumentModel model, params GeoPackageGeometryKind[] kinds) =>
            kinds.All(k => model.Features.Any(f => f.Geometry != null && f.Geometry.Kind == k));

        [Fact]
        public void GeometryColumns_DeclareThePromotedTypes()
        {
            GeoPackageDocumentModel model = Read(SampleFiles.ExtraPathOf("sigwx-swh-2025-2.xml"));
            string path = Tables(model);
            try
            {
                using (SqliteConnection connection = GeoPackageSqlite.Open(path))
                {
                    var expected = new Dictionary<string, string>
                    {
                        ["iwxxm_points"] = "POINT",
                        ["iwxxm_lines"] = "MULTILINESTRING",
                        ["iwxxm_polygons"] = "MULTIPOLYGON",
                    };
                    foreach (var kind in model.Features.Where(f => f.Geometry != null).Select(f => TableOf(f.Geometry!.Kind)).Distinct())
                    {
                        Assert.Equal(
                            new string?[] { kind, "geom", expected[kind], "4326", "0", "0" },
                            Assert.Single(Rows(connection, $"SELECT table_name, column_name, geometry_type_name, srs_id, z, m FROM gpkg_geometry_columns WHERE table_name = '{kind}'")));
                    }
                }
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public void ReportsAndMetadata_DescribeTheBulletin()
        {
            GeoPackageDocumentModel model = Read(SampleFiles.PathOf("metar-NIL-collect.xml"));
            string path = Tables(model);
            try
            {
                using (SqliteConnection connection = GeoPackageSqlite.Open(path))
                {
                    var reports = Rows(connection, "SELECT report_id, report_type, report_status, permissible_usage, issue_time_utc, bulletin_identifier FROM iwxxm_reports ORDER BY fid");
                    Assert.Equal(model.Reports.Count, reports.Count);
                    for (int i = 0; i < reports.Count; i++)
                    {
                        GeoPackageReportRecord expected = model.Reports[i];
                        Assert.Equal(
                            new string?[]
                            {
                                expected.ReportId, expected.ReportType.ToString(), expected.ReportStatus, expected.PermissibleUsage,
                                GeoPackageDateTime.Format(expected.IssueTime), expected.BulletinIdentifier
                            },
                            reports[i]);
                    }

                    Dictionary<string, string?> metadata = Rows(connection, "SELECT key, value FROM iwxxm_metadata").ToDictionary(r => r[0]!, r => r[1]);
                    Assert.Equal("1.4", metadata["geopackage_version"]);
                    Assert.False(string.IsNullOrEmpty(metadata["library_version"]));
                    Assert.Equal(model.DocumentId, metadata["source_document_id"]);
                    Assert.Equal("MeteorologicalBulletin", metadata["source_document_type"]);
                    Assert.Equal(model.BulletinIdentifier, metadata["bulletin_identifier"]);
                    Assert.Equal(model.Reports.Count.ToString(CultureInfo.InvariantCulture), metadata["report_count"]);
                }
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public void Metadata_OfAStandaloneReport_HasNoBulletinIdentifier_AndTheSelectedVersion()
        {
            string path = Tables(Read(SampleFiles.PathOf("metar-A3-1.xml")), GeoPackageVersion.Version13);
            try
            {
                using (SqliteConnection connection = GeoPackageSqlite.Open(path))
                {
                    Assert.Equal("1.3", Scalar(connection, "SELECT value FROM iwxxm_metadata WHERE key = 'geopackage_version'"));
                    Assert.Null(Scalar(connection, "SELECT value FROM iwxxm_metadata WHERE key = 'bulletin_identifier'"));
                    Assert.Equal("1", Scalar(connection, "SELECT count(*) FROM iwxxm_reports WHERE bulletin_identifier IS NULL"));
                }
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public void Values_AreBoundAsParameters_NeverConcatenatedIntoTheSql()
        {
            // A quote in a value must be stored as data: all values are bound parameters.
            GeoPackageDocumentModel model = Read(SampleFiles.PathOf("metar-A3-1.xml"));
            var report = model.Reports[0];
            var hostile = new GeoPackageReportRecord("x'); DROP TABLE gpkg_contents; --", report.ReportType, report.ReportStatus, report.PermissibleUsage, report.IssueTime, "b'1");
            var changed = new GeoPackageDocumentModel("d'oc", model.DocumentType, null, new[] { hostile }, model.Features);
            string path = Tables(changed);
            try
            {
                using (SqliteConnection connection = GeoPackageSqlite.Open(path))
                {
                    Assert.Equal("x'); DROP TABLE gpkg_contents; --", Scalar(connection, "SELECT report_id FROM iwxxm_reports"));
                    Assert.Equal("d'oc", Scalar(connection, "SELECT value FROM iwxxm_metadata WHERE key = 'source_document_id'"));
                    Assert.NotEqual("0", Scalar(connection, "SELECT count(*) FROM gpkg_contents"));
                }
            }
            finally
            {
                File.Delete(path);
            }
        }
    }
}
