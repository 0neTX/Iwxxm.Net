using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using Iwxxm.Net.Export.GeoPackage.Model;
using Iwxxm.Net.Reports;
using Microsoft.Data.Sqlite;
using Xunit;
using static Iwxxm.Net.Export.GeoPackage.Tests.GeoPackageTestDatabase;

namespace Iwxxm.Net.Export.GeoPackage.Tests
{
    /// <summary>
    /// The file the public API writes (through the parser entry point, from the XML file) must hold exactly the features of
    /// the GeoJSON exporter for the same document, for every WMO example and every SIGWX sample: same identifiers, types,
    /// report columns, <c>properties</c> text and geometry, each feature in the table of its geometry family. The file is
    /// read back with plain SQL and its blobs with the independent decoder of the tests, so nothing of the writer is reused.
    /// </summary>
    public sealed class GeoPackageGeoJsonParityTests : IDisposable
    {
        private readonly string _directory = Path.Combine(Path.GetTempPath(), "iwxxm-gpkg-parity-" + Guid.NewGuid().ToString("N"));

        public GeoPackageGeoJsonParityTests() => Directory.CreateDirectory(_directory);

        public void Dispose() => Directory.Delete(_directory, recursive: true);

        public static IEnumerable<object[]> Samples() => SampleFiles.AllPaths().Select(p => new object[] { p });

        // The table a GeoJSON geometry type is stored in, from the documented layout (not from the writer): lines and
        // polygons are promoted to their multi form, features without a geometry go to the attributes table.
        private static string TableOf(JsonElement geometry)
        {
            if (geometry.ValueKind == JsonValueKind.Null)
                return "iwxxm_attributes";

            switch (geometry.GetProperty("type").GetString())
            {
                case "Point": return "iwxxm_points";
                case "LineString":
                case "MultiLineString": return "iwxxm_lines";
                case "Polygon":
                case "MultiPolygon": return "iwxxm_polygons";
                default: throw new InvalidOperationException("Unexpected GeoJSON geometry type " + geometry.GetProperty("type").GetString());
            }
        }

        private static string? Text(JsonElement element, string name) =>
            element.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

        private static string? Column(SqliteDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);

        [Theory]
        [MemberData(nameof(Samples))]
        public void File_HoldsTheFeaturesOfTheGeoJsonExporter_InTheTableOfTheirGeometry(string sample)
        {
            string path = Path.Combine(_directory, "parity.gpkg");
            Result<string> written = IwxxmParserGeoPackageExtensions.ParseFileToGeoPackage(sample, path);
            Assert.True(written.IsSuccess, string.Join(" | ", written.Errors));

            Result<IwxxmDocument> parsed = IwxxmParser.ParseFile(sample);
            Assert.True(parsed.IsSuccess, string.Join(" | ", parsed.Errors));
            Result<string> geoJsonResult = IwxxmGeoJsonExporter.ToGeoJson(parsed.Value, GeoJsonExportOptions.Default);
            Assert.True(geoJsonResult.IsSuccess, string.Join(" | ", geoJsonResult.Errors));

            using (JsonDocument geoJson = JsonDocument.Parse(geoJsonResult.Value))
            using (SqliteConnection connection = GeoPackageSqlite.Open(path))
            {
                List<JsonElement> features = geoJson.RootElement.GetProperty("features").EnumerateArray().ToList();
                var byTable = features.Select((f, i) => (Feature: f, Index: i)).GroupBy(f => TableOf(f.Feature.GetProperty("geometry"))).ToDictionary(g => g.Key, g => g.ToList());

                // A geometry table without features is not written; the attributes table always is.
                List<string> tables = TableNames(connection);
                foreach (string table in new[] { "iwxxm_points", "iwxxm_lines", "iwxxm_polygons" })
                    Assert.True(byTable.ContainsKey(table) == tables.Contains(table), sample + ": table " + table + (byTable.ContainsKey(table) ? " is missing" : " must not exist"));
                Assert.Contains("iwxxm_attributes", tables);

                int rowsInFeatureTables = 0;
                foreach (string table in new[] { "iwxxm_points", "iwxxm_lines", "iwxxm_polygons", "iwxxm_attributes" })
                {
                    List<(JsonElement Feature, int Index)> expected = byTable.TryGetValue(table, out var list) ? list : new List<(JsonElement, int)>();
                    bool hasGeometry = table != "iwxxm_attributes";
                    if (hasGeometry && expected.Count == 0)
                        continue;

                    Assert.Equal(expected.Count.ToString(CultureInfo.InvariantCulture), Scalar(connection, "SELECT count(*) FROM " + table));
                    rowsInFeatureTables += expected.Count;
                    AssertRows(connection, table, hasGeometry, expected, sample);
                }

                Assert.Equal(features.Count, rowsInFeatureTables);
            }
        }

        private static void AssertRows(SqliteConnection connection, string table, bool hasGeometry, List<(JsonElement Feature, int Index)> expected, string sample)
        {
            using (SqliteCommand command = connection.CreateCommand())
            {
                command.CommandText = "SELECT feature_id, feature_type, report_id, report_type, bulletin_identifier, issue_time_utc, properties" +
                    (hasGeometry ? ", geom" : string.Empty) + " FROM " + table + " ORDER BY fid";
                using (SqliteDataReader reader = command.ExecuteReader())
                {
                    foreach ((JsonElement feature, int index) in expected)
                    {
                        string where = sample + " / " + table + " / feature " + index;
                        Assert.True(reader.Read(), where + ": no row");
                        JsonElement properties = feature.GetProperty("properties");

                        Assert.True(Text(feature, "id") == Column(reader, 0), where + ": feature_id");
                        Assert.True(Text(properties, "featureType") == Column(reader, 1), where + ": feature_type");
                        Assert.True(Text(properties, "reportId") == Column(reader, 2), where + ": report_id");
                        Assert.True(Text(properties, "reportType") == Column(reader, 3), where + ": report_type");
                        Assert.True(Text(properties, "bulletinIdentifier") == Column(reader, 4), where + ": bulletin_identifier");
                        AssertSameInstant(Text(properties, "issueTimeUtc"), Column(reader, 5), where);

                        string storedProperties = reader.GetString(6);
                        string expectedProperties = properties.GetRawText();
                        if (storedProperties != expectedProperties)
                        {
                            // Say whether the difference is one of content or only of text.
                            using (JsonDocument stored = JsonDocument.Parse(storedProperties))
                                GeoPackageFileTests.AssertSameJson(properties, stored.RootElement, where + " properties");
                            Assert.Fail(where + ": properties are the same JSON but not the same text");
                        }

                        if (hasGeometry)
                        {
                            GeoPackageGeometry decoded = GeoPackageGeometryEncoderTests.Decode((byte[])reader.GetValue(7), out _);
                            GeoPackageGeometryTests.AssertSame(feature.GetProperty("geometry"), decoded, where);
                        }
                    }

                    Assert.False(reader.Read(), sample + " / " + table + ": more rows than GeoJSON features");
                }
            }
        }

        private static void AssertSameInstant(string? geoJsonValue, string? column, string where)
        {
            if (geoJsonValue == null || column == null)
            {
                Assert.True(geoJsonValue == column, where + ": issue_time_utc");
                return;
            }

            Assert.EndsWith("Z", column);
            DateTimeOffset expected = DateTimeOffset.Parse(geoJsonValue, CultureInfo.InvariantCulture);
            DateTimeOffset actual = DateTimeOffset.Parse(column, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);
            Assert.True(expected.UtcDateTime == actual.UtcDateTime, where + ": issue_time_utc " + geoJsonValue + " vs " + column);
        }

        [Theory]
        [MemberData(nameof(Samples))]
        public void ReportsAndMetadataTables_DescribeTheReportsOfTheDocument(string sample)
        {
            string path = Path.Combine(_directory, "reports.gpkg");
            Result<string> written = IwxxmParserGeoPackageExtensions.ParseFileToGeoPackage(sample, path);
            Assert.True(written.IsSuccess, string.Join(" | ", written.Errors));

            Result<IwxxmDocument> parsed = IwxxmParser.ParseFile(sample);
            Assert.True(parsed.IsSuccess, string.Join(" | ", parsed.Errors));
            IwxxmDocument document = parsed.Value;
            var bulletin = document as MeteorologicalBulletin;
            IwxxmDocument[] reports = bulletin != null ? bulletin.Reports.Cast<IwxxmDocument>().ToArray() : new[] { document };
            string? bulletinIdentifier = bulletin?.BulletinIdentifier;

            using (JsonDocument geoJson = JsonDocument.Parse(IwxxmGeoJsonExporter.ToGeoJson(document, GeoJsonExportOptions.Default).Value))
            using (SqliteConnection connection = GeoPackageSqlite.Open(path))
            {
                List<string?[]> rows = Rows(connection, "SELECT report_id, report_type, report_status, permissible_usage, issue_time_utc, bulletin_identifier, permissible_usage_reason, permissible_usage_supplementary FROM iwxxm_reports ORDER BY fid");
                Assert.Equal(reports.Length, rows.Count);
                for (int i = 0; i < reports.Length; i++)
                {
                    string where = sample + " / report " + i;
                    Assert.True(reports[i].Id == rows[i][0], where + ": report_id");
                    Assert.True(reports[i].ReportType.ToString() == rows[i][1], where + ": report_type");
                    Assert.False(string.IsNullOrEmpty(rows[i][2]), where + ": report_status");
                    Assert.False(string.IsNullOrEmpty(rows[i][3]), where + ": permissible_usage");
                    Assert.EndsWith("Z", rows[i][4]);
                    Assert.True(reports[i].IssueTimeUtc.UtcDateTime == DateTimeOffset.Parse(rows[i][4]!, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal).UtcDateTime, where + ": issue_time_utc");
                    Assert.True(bulletinIdentifier == rows[i][5], where + ": bulletin_identifier");
                }

                // A standalone report: its status and usage are the ones of the GeoJSON metadata.
                if (bulletin == null)
                {
                    JsonElement metadata = geoJson.RootElement.GetProperty("metadata");
                    Assert.Equal(metadata.GetProperty("reportStatus").GetString(), rows[0][2]);
                    Assert.Equal(metadata.GetProperty("permissibleUsage").GetString(), rows[0][3]);
                }

                // Every report, standalone or inside a bulletin: the usage reason and supplementary text are the ones of the
                // metadata block of the GeoJSON export of that report (absent there means NULL here).
                for (int i = 0; i < reports.Length; i++)
                {
                    using (JsonDocument single = JsonDocument.Parse(IwxxmGeoJsonExporter.ToGeoJson(reports[i], GeoJsonExportOptions.Default).Value))
                    {
                        JsonElement metadata = single.RootElement.GetProperty("metadata");
                        string where = sample + " / report " + i;
                        Assert.True(Text(metadata, "permissibleUsageReason") == rows[i][6], where + ": permissible_usage_reason");
                        Assert.True(Text(metadata, "permissibleUsageSupplementary") == rows[i][7], where + ": permissible_usage_supplementary");
                        Assert.True(Text(metadata, "reportStatus") == rows[i][2], where + ": report_status");
                        Assert.True(Text(metadata, "permissibleUsage") == rows[i][3], where + ": permissible_usage");
                    }
                }

                // Every feature belongs to a report of the file.
                var reportIds = new HashSet<string?>(rows.Select(r => r[0]));
                foreach (JsonElement feature in geoJson.RootElement.GetProperty("features").EnumerateArray())
                    Assert.Contains(Text(feature.GetProperty("properties"), "reportId"), reportIds);

                Dictionary<string, string> metadataRows = Rows(connection, "SELECT key, value FROM iwxxm_metadata").ToDictionary(r => r[0]!, r => r[1]!);
                Assert.Equal(document.Id, metadataRows["source_document_id"]);
                Assert.Equal(document.ReportType.ToString(), metadataRows["source_document_type"]);
                Assert.Equal(reports.Length.ToString(CultureInfo.InvariantCulture), metadataRows["report_count"]);
                Assert.Equal("1.4", metadataRows["geopackage_version"]);
                Assert.Equal(bulletinIdentifier != null, metadataRows.ContainsKey("bulletin_identifier"));
                if (bulletinIdentifier != null)
                    Assert.Equal(bulletinIdentifier, metadataRows["bulletin_identifier"]);
            }
        }

        [Theory]
        [MemberData(nameof(Samples))]
        public void EveryRowOfEveryFeatureTable_HasAnIssueTime(string sample)
        {
            string path = Path.Combine(_directory, "issue-time.gpkg");
            Result<string> written = IwxxmParserGeoPackageExtensions.ParseFileToGeoPackage(sample, path);
            Assert.True(written.IsSuccess, string.Join(" | ", written.Errors));

            using (SqliteConnection connection = GeoPackageSqlite.Open(path))
            {
                foreach (string table in TableNames(connection).Where(t => t != "iwxxm_reports" && t != "iwxxm_metadata" && t.StartsWith("iwxxm_", StringComparison.Ordinal)))
                    Assert.True(Scalar(connection, "SELECT count(*) FROM " + table + " WHERE issue_time_utc IS NULL") == "0", sample + ": " + table + " has a row without issue_time_utc");
            }
        }

        [Fact]
        public void ABulletinOfReportsWithDifferentIssueTimes_UsesTheEarliestAsTheDefaultLastChange()
        {
            // The 15-report SIGWX bulletin has one issue time in every report. The text of the third report is changed to an
            // earlier instant so that the reports differ and the earliest is not the first.
            const string issueTime = "<gml:timePosition>2099-01-01T05:32:57Z</gml:timePosition>";
            string xml = File.ReadAllText(SampleFiles.ExtraPathOf("example-iwxxm-2025-2-sigwx-non-operational.xml"));
            int third = -1;
            for (int i = 0; i < 3; i++)
            {
                third = xml.IndexOf(issueTime, third + 1, StringComparison.Ordinal);
                Assert.True(third >= 0, "the sample must hold at least three reports with this issue time");
            }

            xml = xml.Substring(0, third) + "<gml:timePosition>2098-12-31T23:15:00Z</gml:timePosition>" + xml.Substring(third + issueTime.Length);

            Result<IwxxmDocument> parsed = IwxxmParser.Parse(xml);
            Assert.True(parsed.IsSuccess, string.Join(" | ", parsed.Errors));
            var bulletin = Assert.IsType<MeteorologicalBulletin>(parsed.Value);
            DateTimeOffset[] times = bulletin.Reports.Select(r => r.IssueTimeUtc).ToArray();
            Assert.True(times.Distinct().Count() > 1);
            Assert.NotEqual(times[0], times.Min());

            string path = Path.Combine(_directory, "last-change.gpkg");
            Assert.True(IwxxmParserGeoPackageExtensions.ParseToGeoPackage(xml, path).IsSuccess);

            using (SqliteConnection connection = GeoPackageSqlite.Open(path))
            {
                Assert.Equal(times.Min(), bulletin.IssueTimeUtc);
                Assert.Equal(new[] { GeoPackageDateTime.Format(times.Min()) }, Rows(connection, "SELECT DISTINCT last_change FROM gpkg_contents").Select(r => r[0]).ToArray());
            }
        }

        [Fact]
        public void Samples_IncludeStandaloneReportsAndABulletinOfSeveralReports()
        {
            // Guards the test above against passing vacuously (the multi-report bulletin is the large SIGWX sample).
            var parsed = SampleFiles.AllPaths().Select(p => IwxxmParser.ParseFile(p).Value).ToList();
            Assert.Contains(parsed, d => !(d is MeteorologicalBulletin));
            Assert.Contains(parsed, d => d is MeteorologicalBulletin b && b.Reports.Count > 1);
        }

        [Fact]
        public void Samples_ReachEveryTableOfTheLayout()
        {
            // Guards the test above against passing vacuously: across the samples every table kind has features.
            var tablesSeen = new HashSet<string>();
            foreach (string sample in SampleFiles.All().Select(a => SampleFiles.PathOf((string)a[0])).Concat(new[] { SampleFiles.ExtraPathOf("sigwx-swh-2025-2.xml") }))
            {
                Result<IwxxmDocument> parsed = IwxxmParser.ParseFile(sample);
                Assert.True(parsed.IsSuccess, string.Join(" | ", parsed.Errors));
                using (JsonDocument geoJson = JsonDocument.Parse(IwxxmGeoJsonExporter.ToGeoJson(parsed.Value).Value))
                    foreach (JsonElement feature in geoJson.RootElement.GetProperty("features").EnumerateArray())
                        tablesSeen.Add(TableOf(feature.GetProperty("geometry")));
            }

            Assert.Equal(new[] { "iwxxm_attributes", "iwxxm_lines", "iwxxm_points", "iwxxm_polygons" }, tablesSeen.OrderBy(t => t, StringComparer.Ordinal).ToArray());
        }
    }
}
