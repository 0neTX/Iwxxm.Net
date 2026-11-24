using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Iwxxm.Net.Export.GeoPackage.Model;
using Iwxxm.Net.Reports;
using Xunit;

namespace Iwxxm.Net.Export.GeoPackage.Tests
{
    /// <summary>
    /// The GeoPackage package must see exactly the features the GeoJSON exporter writes: same count, ids, property keys
    /// and values, in the same order.
    /// </summary>
    public sealed class GeoPackageFeatureSourceTests
    {
        public static IEnumerable<object[]> Samples() => SampleFiles.All();

        private static IwxxmDocument Parse(string sample)
        {
            var result = IwxxmParser.ParseFile(SampleFiles.PathOf(sample));
            Assert.True(result.IsSuccess, string.Join(" | ", result.Errors));
            return result.Value;
        }

        private static JsonDocument GeoJsonOf(IwxxmDocument document)
        {
            var result = IwxxmGeoJsonExporter.ToGeoJson(document);
            Assert.True(result.IsSuccess, string.Join(" | ", result.Errors));
            return JsonDocument.Parse(result.Value);
        }

        [Fact]
        public void Samples_IncludeTheBulletinAndTheSigwxExamples()
        {
            var names = SampleFiles.All().Select(a => (string)a[0]).ToList();
            Assert.Contains("metar-NIL-collect.xml", names);
            Assert.Contains("WAFS-Example.xml", names);
            Assert.Equal(29, names.Count);
        }

        [Theory]
        [MemberData(nameof(Samples))]
        public void Features_AreTheOnesOfTheGeoJsonExporter(string sample)
        {
            IwxxmDocument document = Parse(sample);
            GeoPackageDocumentModel model = GeoPackageFeatureSource.Read(document);
            using JsonDocument geoJson = GeoJsonOf(document);
            List<JsonElement> expected = geoJson.RootElement.GetProperty("features").EnumerateArray().ToList();

            Assert.Equal(expected.Count, model.Features.Count);
            for (int i = 0; i < expected.Count; i++)
            {
                JsonElement feature = expected[i];
                GeoPackageFeature actual = model.Features[i];
                string where = sample + " feature " + i;

                string? expectedId = feature.TryGetProperty("id", out JsonElement id) ? id.GetString() : null;
                Assert.True(expectedId == actual.FeatureId, where + ": id");

                JsonProperty[] expectedProperties = feature.GetProperty("properties").EnumerateObject().ToArray();
                var written = actual.Properties.Where(p => p.Value != null).ToList();
                Assert.Equal(expectedProperties.Select(p => p.Name), written.Select(p => p.Key));
                for (int p = 0; p < written.Count; p++)
                    AssertSameValue(expectedProperties[p].Value, written[p].Value!, where + " / " + written[p].Key);

                AssertColumn(expectedProperties, "featureType", actual.FeatureType, where);
                AssertColumn(expectedProperties, "reportId", actual.ReportId, where);
                AssertColumn(expectedProperties, "reportType", actual.ReportType, where);
                AssertColumn(expectedProperties, "bulletinIdentifier", actual.BulletinIdentifier, where);
                AssertColumn(expectedProperties, "issueTimeUtc", actual.IssueTimeText, where);
            }
        }

        [Theory]
        [MemberData(nameof(Samples))]
        public void DocumentInformation_MatchesTheGeoJsonMetadata(string sample)
        {
            IwxxmDocument document = Parse(sample);
            GeoPackageDocumentModel model = GeoPackageFeatureSource.Read(document);
            using JsonDocument geoJson = GeoJsonOf(document);
            JsonElement metadata = geoJson.RootElement.GetProperty("metadata");

            Assert.Equal(metadata.GetProperty("id").GetString(), model.DocumentId);
            Assert.Equal(metadata.GetProperty("reportType").GetString(), model.DocumentType.ToString());
            string? bulletinId = metadata.TryGetProperty("bulletinIdentifier", out JsonElement b) ? b.GetString() : null;
            Assert.Equal(bulletinId, model.BulletinIdentifier);

            if (document is MeteorologicalBulletin bulletin)
            {
                Assert.Equal(bulletin.Reports.Count, model.Reports.Count);
                Assert.Equal(metadata.GetProperty("reportCount").GetInt32(), model.Reports.Count);
                Assert.All(model.Reports, r => Assert.Equal(bulletin.BulletinIdentifier, r.BulletinIdentifier));
            }
            else
            {
                GeoPackageReportRecord record = Assert.Single(model.Reports);
                Assert.Equal(document.Id, record.ReportId);
                Assert.Null(record.BulletinIdentifier);
                Assert.Equal(metadata.GetProperty("reportStatus").GetString(), record.ReportStatus);
                Assert.Equal(metadata.GetProperty("permissibleUsage").GetString(), record.PermissibleUsage);
                Assert.Equal(metadata.GetProperty("issueTimeUtc").GetString(), record.IssueTime.ToString("O", System.Globalization.CultureInfo.InvariantCulture));
            }

            // Every feature belongs to one of the reports of the document.
            Assert.All(model.Features, f => Assert.Contains(model.Reports, r => r.ReportId == f.ReportId));
        }

        [Fact]
        public void Bulletin_FeaturesCarryTheBulletinIdentifier()
        {
            IwxxmDocument document = Parse("metar-NIL-collect.xml");
            var bulletin = Assert.IsType<MeteorologicalBulletin>(document);
            GeoPackageDocumentModel model = GeoPackageFeatureSource.Read(document);

            Assert.NotEmpty(model.Features);
            Assert.Equal(bulletin.BulletinIdentifier, model.BulletinIdentifier);
            Assert.All(model.Features, f => Assert.Equal(bulletin.BulletinIdentifier, f.BulletinIdentifier));
            Assert.All(model.Reports, r => Assert.Equal(bulletin.BulletinIdentifier, r.BulletinIdentifier));
        }

        [Fact]
        public void StandaloneReport_FeaturesHaveNoBulletinIdentifier()
        {
            GeoPackageDocumentModel model = GeoPackageFeatureSource.Read(Parse("metar-A3-1.xml"));

            Assert.NotEmpty(model.Features);
            Assert.Null(model.BulletinIdentifier);
            Assert.All(model.Features, f => Assert.Null(f.BulletinIdentifier));
            Assert.All(model.Features, f => Assert.Equal("METAR_OBSERVATION", f.FeatureType));
        }

        [Fact]
        public void SigwxExample_ProducesManyFeaturesOfSeveralTypes()
        {
            GeoPackageDocumentModel model = GeoPackageFeatureSource.Read(Parse("WAFS-Example.xml"));

            Assert.True(model.Features.Count > 1);
            Assert.True(model.Features.Select(f => f.FeatureType).Distinct().Count() > 1);
            Assert.All(model.Features, f => Assert.NotNull(f.FeatureType));
        }

        [Fact]
        public void Read_NullDocument_Throws() =>
            Assert.Throws<ArgumentNullException>(() => GeoPackageFeatureSource.Read(null!));

        [Fact]
        public void StringProperty_MissingKey_IsNull() =>
            Assert.Null(GeoPackageFeature.StringProperty(new List<(string, object?)> { ("a", "x") }, "b"));

        [Fact]
        public void StringProperty_NullOrNonStringValue_IsNull()
        {
            var properties = new List<(string, object?)> { ("a", null), ("b", 3), ("c", new[] { "x" }) };
            Assert.Null(GeoPackageFeature.StringProperty(properties, "a"));
            Assert.Null(GeoPackageFeature.StringProperty(properties, "b"));
            Assert.Null(GeoPackageFeature.StringProperty(properties, "c"));
        }

        [Fact]
        public void StringProperty_FirstStringValueWins()
        {
            var properties = new List<(string, object?)> { ("a", null), ("a", "first"), ("a", "second") };
            Assert.Equal("first", GeoPackageFeature.StringProperty(properties, "a"));
        }

        private static void AssertColumn(JsonProperty[] expectedProperties, string key, string? actual, string where)
        {
            string? expected = expectedProperties.Where(p => p.Name == key).Select(p => p.Value.GetString()).FirstOrDefault();
            Assert.True(expected == actual, where + ": column " + key + " expected '" + expected + "' but was '" + actual + "'");
        }

        private static void AssertSameValue(JsonElement expected, object actual, string where)
        {
            switch (actual)
            {
                case string s:
                    Assert.Equal(JsonValueKind.String, expected.ValueKind);
                    Assert.True(s == expected.GetString(), where);
                    break;
                case int i:
                    Assert.Equal(i, expected.GetInt32());
                    break;
                case double d:
                    if (double.IsNaN(d) || double.IsInfinity(d))
                        Assert.Equal(JsonValueKind.Null, expected.ValueKind);
                    else
                        Assert.Equal(d, expected.GetDouble());
                    break;
                case bool b:
                    Assert.Equal(b ? JsonValueKind.True : JsonValueKind.False, expected.ValueKind);
                    break;
                case IReadOnlyList<double> numbers:
                    Assert.Equal(numbers.Count, expected.GetArrayLength());
                    for (int n = 0; n < numbers.Count; n++)
                        AssertSameValue(expected[n], numbers[n], where + "[" + n + "]");
                    break;
                case IReadOnlyList<string?> texts:
                    Assert.Equal(texts.Count, expected.GetArrayLength());
                    for (int n = 0; n < texts.Count; n++)
                        Assert.True(texts[n] == expected[n].GetString(), where + "[" + n + "]");
                    break;
                default:
                    Assert.Equal(actual.ToString(), expected.GetString());
                    break;
            }
        }
    }
}
