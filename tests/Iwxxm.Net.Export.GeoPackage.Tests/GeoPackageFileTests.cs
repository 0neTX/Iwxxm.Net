using System;
using System.Collections.Generic;
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
    /// Writes every WMO example and every SIGWX sample to a real file and reads it back: the checks SQLite itself offers,
    /// every geometry blob through the independent decoder, and the <c>properties</c> text against the GeoJSON exporter.
    /// </summary>
    public sealed class GeoPackageFileTests : IDisposable
    {
        private static readonly DateTimeOffset LastChange = new DateTimeOffset(2026, 5, 6, 7, 8, 9, 123, TimeSpan.Zero);

        private readonly string _directory = Path.Combine(Path.GetTempPath(), "iwxxm-gpkg-files-" + Guid.NewGuid().ToString("N"));

        public GeoPackageFileTests() => Directory.CreateDirectory(_directory);

        public void Dispose() => Directory.Delete(_directory, recursive: true);

        public static IEnumerable<object[]> AllSamples() => SampleFiles.AllPaths().Select(p => new object[] { p });

        private static IwxxmDocument Parse(string path)
        {
            var result = IwxxmParser.ParseFile(path);
            Assert.True(result.IsSuccess, string.Join(" | ", result.Errors));
            return result.Value;
        }

        private string Write(string sample, string name = "out.gpkg", GeoPackageVersion version = GeoPackageVersion.Version14)
        {
            string path = Path.Combine(_directory, name);
            GeoPackageWriter.Write(path, GeoPackageFeatureSource.Read(Parse(sample)), new GeoPackageWriteOptions(LastChange, version));
            return path;
        }

        [Theory]
        [MemberData(nameof(AllSamples))]
        public void File_PassesTheIntegrityAndForeignKeyChecks(string sample)
        {
            string path = Write(sample);

            using (SqliteConnection connection = GeoPackageSqlite.Open(path))
            {
                Assert.Equal("ok", Scalar(connection, "PRAGMA integrity_check"));
                Assert.Empty(Rows(connection, "PRAGMA foreign_key_check"));
                Assert.Equal("1196444487", Scalar(connection, "PRAGMA application_id"));
                Assert.Equal("10400", Scalar(connection, "PRAGMA user_version"));
            }
        }

        [Theory]
        [MemberData(nameof(AllSamples))]
        public void Rows_RoundTripTheFeatures_GeometryAndProperties(string sample)
        {
            IwxxmDocument document = Parse(sample);
            GeoPackageDocumentModel model = GeoPackageFeatureSource.Read(document);
            string path = Path.Combine(_directory, "out.gpkg");
            GeoPackageWriter.Write(path, model, new GeoPackageWriteOptions(LastChange));

            var geoJsonResult = IwxxmGeoJsonExporter.ToGeoJson(document);
            Assert.True(geoJsonResult.IsSuccess, string.Join(" | ", geoJsonResult.Errors));
            using (JsonDocument geoJson = JsonDocument.Parse(geoJsonResult.Value))
            using (SqliteConnection connection = GeoPackageSqlite.Open(path))
            {
                List<JsonElement> expectedFeatures = geoJson.RootElement.GetProperty("features").EnumerateArray().ToList();
                Assert.Equal(expectedFeatures.Count, model.Features.Count);

                // Rows of a table are the model features of that geometry kind, in feature order.
                var byTable = new Dictionary<string, List<int>>();
                for (int i = 0; i < model.Features.Count; i++)
                {
                    string table = model.Features[i].Geometry == null ? GeoPackageDataTables.AttributesTable : GeoPackageDataTables.TableFor(model.Features[i].Geometry!.Kind);
                    if (!byTable.TryGetValue(table, out List<int>? indexes))
                        byTable[table] = indexes = new List<int>();
                    indexes.Add(i);
                }

                foreach (KeyValuePair<string, List<int>> entry in byTable)
                {
                    bool hasGeometry = entry.Key != GeoPackageDataTables.AttributesTable;
                    using (SqliteCommand command = connection.CreateCommand())
                    {
                        command.CommandText = (hasGeometry ? "SELECT feature_id, feature_type, report_id, report_type, bulletin_identifier, issue_time_utc, properties, geom" :
                            "SELECT feature_id, feature_type, report_id, report_type, bulletin_identifier, issue_time_utc, properties, NULL") + $" FROM {entry.Key} ORDER BY fid";
                        using (SqliteDataReader reader = command.ExecuteReader())
                        {
                            foreach (int index in entry.Value)
                            {
                                Assert.True(reader.Read(), sample + ": missing row for feature " + index);
                                GeoPackageFeature feature = model.Features[index];
                                JsonElement expected = expectedFeatures[index];
                                string where = sample + " feature " + index;

                                Assert.Equal(feature.FeatureId, reader.IsDBNull(0) ? null : reader.GetString(0));
                                Assert.Equal(feature.FeatureType, reader.IsDBNull(1) ? null : reader.GetString(1));
                                Assert.Equal(feature.ReportId, reader.IsDBNull(2) ? null : reader.GetString(2));
                                Assert.Equal(feature.ReportType, reader.IsDBNull(3) ? null : reader.GetString(3));
                                Assert.Equal(feature.BulletinIdentifier, reader.IsDBNull(4) ? null : reader.GetString(4));
                                Assert.Equal(GeoPackageDateTime.Normalize(feature.IssueTimeText), reader.IsDBNull(5) ? null : reader.GetString(5));

                                string properties = reader.GetString(6);
                                using (JsonDocument stored = JsonDocument.Parse(properties))
                                    AssertSameJson(expected.GetProperty("properties"), stored.RootElement, where);

                                if (hasGeometry)
                                {
                                    byte[] blob = (byte[])reader.GetValue(7);
                                    GeoPackageGeometry decoded = GeoPackageGeometryEncoderTests.Decode(blob, out _);
                                    GeoPackageGeometryEncoderTests.AssertSameGeometry(feature.Geometry!, decoded, where);
                                }
                            }

                            Assert.False(reader.Read(), sample + ": extra rows in " + entry.Key);
                        }
                    }
                }
            }
        }

        [Theory]
        [MemberData(nameof(AllSamples))]
        public void File_WrittenTwice_HasTheSameCanonicalDump(string sample)
        {
            string first = Write(sample, "first.gpkg");
            string second = Write(sample, "second.gpkg");

            using (SqliteConnection a = GeoPackageSqlite.Open(first))
            using (SqliteConnection b = GeoPackageSqlite.Open(second))
                Assert.Equal(CanonicalDump(a), CanonicalDump(b));
        }

        [Fact]
        public void Versions13And14_DifferOnlyInTheUserVersionAndTheMetadata()
        {
            string v14 = Write(SampleFiles.PathOf("metar-NIL-collect.xml"), "v14.gpkg");
            string v13 = Write(SampleFiles.PathOf("metar-NIL-collect.xml"), "v13.gpkg", GeoPackageVersion.Version13);

            using (SqliteConnection a = GeoPackageSqlite.Open(v14))
            using (SqliteConnection b = GeoPackageSqlite.Open(v13))
            {
                Assert.Equal("10400", Scalar(a, "PRAGMA user_version"));
                Assert.Equal("10300", Scalar(b, "PRAGMA user_version"));
                Assert.Equal(
                    CanonicalDump(a).Replace("user_version=10400", "user_version=10300").Replace("geopackage_version\t1.4", "geopackage_version\t1.3"),
                    CanonicalDump(b));
            }
        }

        internal static void AssertSameJson(JsonElement expected, JsonElement actual, string where)
        {
            Assert.True(expected.ValueKind == actual.ValueKind, where + ": JSON kind");
            switch (expected.ValueKind)
            {
                case JsonValueKind.Object:
                    JsonProperty[] expectedProperties = expected.EnumerateObject().ToArray();
                    JsonProperty[] actualProperties = actual.EnumerateObject().ToArray();
                    Assert.Equal(expectedProperties.Select(p => p.Name), actualProperties.Select(p => p.Name));
                    for (int i = 0; i < expectedProperties.Length; i++)
                        AssertSameJson(expectedProperties[i].Value, actualProperties[i].Value, where + "/" + expectedProperties[i].Name);
                    break;

                case JsonValueKind.Array:
                    JsonElement[] expectedItems = expected.EnumerateArray().ToArray();
                    JsonElement[] actualItems = actual.EnumerateArray().ToArray();
                    Assert.Equal(expectedItems.Length, actualItems.Length);
                    for (int i = 0; i < expectedItems.Length; i++)
                        AssertSameJson(expectedItems[i], actualItems[i], where + "[" + i + "]");
                    break;

                default:
                    Assert.True(expected.GetRawText() == actual.GetRawText(), where + ": " + expected.GetRawText() + " vs " + actual.GetRawText());
                    break;
            }
        }
    }
}
