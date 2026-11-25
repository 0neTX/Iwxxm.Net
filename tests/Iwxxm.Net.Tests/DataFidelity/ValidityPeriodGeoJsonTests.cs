using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Iwxxm.Net.Reports;
using Xunit;
using static Iwxxm.Net.Tests.DataFidelity.SampleDocuments;

namespace Iwxxm.Net.Tests.DataFidelity
{
    /// <summary>
    /// Every GeoJSON feature of a SIGWX, SIGMET or AIRMET report carries the validity period of its report
    /// (<c>validFromUtc</c> / <c>validUntilUtc</c>, the keys and format of the TAF), right after the base properties, and
    /// only when the period is known.
    /// </summary>
    public sealed class ValidityPeriodGeoJsonTests
    {
        private static readonly DateTimeOffset T0 = new DateTimeOffset(2026, 3, 1, 6, 0, 0, TimeSpan.Zero);
        private static readonly string[] BaseKeys = { "featureType", "reportId", "reportType", "issueTimeUtc", "bulletinIdentifier" };

        private static string Instant(DateTimeOffset value) => value.ToString("O", System.Globalization.CultureInfo.InvariantCulture);

        private static List<JsonElement> Features(string geoJson, out JsonDocument json)
        {
            json = JsonDocument.Parse(geoJson);
            return json.RootElement.GetProperty("features").EnumerateArray().Select(f => f.GetProperty("properties")).ToList();
        }

        private static string[] KeysOf(JsonElement properties) => properties.EnumerateObject().Select(p => p.Name).ToArray();

        /// <summary>The validity keys come straight after the base keys, which are never reordered.</summary>
        private static void AssertValidityRightAfterTheBase(JsonElement properties, bool hasFrom, bool hasUntil)
        {
            string[] keys = KeysOf(properties);
            int firstNonBase = Array.FindIndex(keys, k => Array.IndexOf(BaseKeys, k) < 0);
            Assert.True(firstNonBase > 0);
            Assert.Equal("featureType", keys[0]);
            Assert.Equal("reportId", keys[1]);
            Assert.Equal("reportType", keys[2]);
            Assert.Equal("issueTimeUtc", keys[3]);

            var validity = new List<string>();
            if (hasFrom) validity.Add("validFromUtc");
            if (hasUntil) validity.Add("validUntilUtc");
            Assert.Equal(validity, keys.Skip(firstNonBase).Take(validity.Count));
            Assert.Equal(hasFrom, properties.TryGetProperty("validFromUtc", out _));
            Assert.Equal(hasUntil, properties.TryGetProperty("validUntilUtc", out _));
        }

        [Theory]
        [InlineData("Samples/wmo-examples/WAFS-Example.xml")]
        [InlineData("Samples/sigwx-swh-2025-2.xml")]
        public void Sigwx_EveryFeature_CarriesTheValidityPeriod(string path)
        {
            Sigwx sigwx = IwxxmParser.ParseFileAs<Sigwx>(path).Value;

            List<JsonElement> features = Features(IwxxmParser.ParseFileToGeoJson(path).Value, out JsonDocument json);
            using (json)
            {
                Assert.NotEmpty(features);
                Assert.All(features, p =>
                {
                    Assert.Equal(Instant(sigwx.ValidFromUtc), p.GetProperty("validFromUtc").GetString());
                    Assert.Equal(Instant(sigwx.ValidUntilUtc), p.GetProperty("validUntilUtc").GetString());
                    AssertValidityRightAfterTheBase(p, true, true);
                });
            }
        }

        [Theory]
        [InlineData("sigmet-A6-1a-TS.xml")]
        [InlineData("sigmet-A6-2-TC.xml")]
        [InlineData("sigmet-VA-EGGX.xml")]
        [InlineData("sigmet-multi-location-VA.xml")]
        public void Sigmet_EveryFeature_CarriesTheValidityPeriod(string file)
        {
            Sigmet sigmet = IwxxmParser.ParseFileAs<Sigmet>(PathOf(file)).Value;
            Assert.NotNull(sigmet.ValidFromUtc);
            Assert.NotNull(sigmet.ValidUntilUtc);

            List<JsonElement> features = Features(IwxxmParser.ParseFileToGeoJson(PathOf(file)).Value, out JsonDocument json);
            using (json)
            {
                Assert.NotEmpty(features);
                Assert.All(features, p =>
                {
                    Assert.Equal(Instant(sigmet.ValidFromUtc!.Value), p.GetProperty("validFromUtc").GetString());
                    Assert.Equal(Instant(sigmet.ValidUntilUtc!.Value), p.GetProperty("validUntilUtc").GetString());
                    AssertValidityRightAfterTheBase(p, true, true);
                });
            }
        }

        [Fact]
        public void Sigmet_EruptingVolcanoAndTropicalCycloneCentre_AlsoCarryTheValidityPeriod()
        {
            string volcano = IwxxmParser.ParseFileToGeoJson(PathOf("sigmet-VA-EGGX.xml")).Value;
            string cyclone = IwxxmParser.ParseFileToGeoJson(PathOf("sigmet-A6-2-TC.xml")).Value;

            List<JsonElement> all = Features(volcano, out JsonDocument json1).Concat(Features(cyclone, out JsonDocument json2)).ToList();
            using (json1)
            using (json2)
            {
                Assert.Contains(all, p => p.GetProperty("featureType").GetString() == "ERUPTING_VOLCANO");
                Assert.Contains(all, p => p.GetProperty("featureType").GetString() == "TROPICAL_CYCLONE_CENTRE");
                Assert.All(all, p => AssertValidityRightAfterTheBase(p, true, true));
            }
        }

        [Fact]
        public void Airmet_EveryFeature_CarriesTheValidityPeriod()
        {
            Airmet airmet = IwxxmParser.ParseFileAs<Airmet>(PathOf("airmet-A6-1a-TS.xml")).Value;
            Assert.NotNull(airmet.ValidFromUtc);
            Assert.NotNull(airmet.ValidUntilUtc);

            List<JsonElement> features = Features(IwxxmParser.ParseFileToGeoJson(PathOf("airmet-A6-1a-TS.xml")).Value, out JsonDocument json);
            using (json)
            {
                Assert.NotEmpty(features);
                Assert.All(features, p =>
                {
                    Assert.Equal(Instant(airmet.ValidFromUtc!.Value), p.GetProperty("validFromUtc").GetString());
                    Assert.Equal(Instant(airmet.ValidUntilUtc!.Value), p.GetProperty("validUntilUtc").GetString());
                    AssertValidityRightAfterTheBase(p, true, true);
                });
            }
        }

        [Fact]
        public void Taf_KeepsItsValidityKeysWhereTheyWere()
        {
            List<JsonElement> features = Features(IwxxmParser.ParseFileToGeoJson(PathOf("taf-A5-1.xml")).Value, out JsonDocument json);
            using (json)
            {
                string[] keys = KeysOf(features.Single());
                int aerodrome = Array.IndexOf(keys, "aerodromeName");
                Assert.True(aerodrome > 0);
                Assert.Equal("validFromUtc", keys[aerodrome + 1]);
                Assert.Equal("validUntilUtc", keys[aerodrome + 2]);
            }
        }

        private static GeoSurface Area() =>
            GeoPolygon.Create(new[] { new GeoCoordinate(0, 0), new GeoCoordinate(0, 10), new GeoCoordinate(10, 10), new GeoCoordinate(10, 0), new GeoCoordinate(0, 0) }).Value;

        private static EnRoutePhenomenonVolume Volume(string id, bool withTropicalCycloneCentre = false) =>
            new EnRoutePhenomenonVolume(id, Area(), null, null, tropicalCycloneCentre: withTropicalCycloneCentre ? new GeoCoordinate(5, 5) : (GeoCoordinate?)null);

        private static Sigmet SigmetOf(DateTimeOffset? validFrom, DateTimeOffset? validUntil, bool withVolcano = false)
        {
            Result<Sigmet> result = Sigmet.Create(
                "s1", IwxxmReportType.Sigmet, T0, ReportStatus.Normal, PermissibleUsage.Operational,
                "YUDD", null, null, null, null, "1", validFrom, validUntil,
                eruptingVolcano: withVolcano ? Volcano.Create("v0", "Test", null, new GeoCoordinate(1, 1)).Value : null,
                evolvingConditions: new[] { Volume("e1", withTropicalCycloneCentre: true) },
                forecastPositions: new[] { Volume("f1") });
            Assert.True(result.IsSuccess, string.Join(" | ", result.Errors));
            return result.Value;
        }

        [Fact]
        public void Sigmet_WithoutAValidityPeriod_OmitsBothKeys_OnEveryFeature()
        {
            List<JsonElement> features = Features(IwxxmGeoJsonExporter.ToGeoJson(SigmetOf(null, null, withVolcano: true)).Value, out JsonDocument json);
            using (json)
            {
                // volcano + evolving volume + its cyclone centre + forecast volume
                Assert.Equal(4, features.Count);
                Assert.All(features, p =>
                {
                    Assert.False(p.TryGetProperty("validFromUtc", out _));
                    Assert.False(p.TryGetProperty("validUntilUtc", out _));
                    Assert.DoesNotContain("validFromUtc", p.GetRawText());
                });
            }
        }

        [Fact]
        public void Sigmet_WithAPartialPeriod_EmitsOnlyTheKnownBound()
        {
            List<JsonElement> onlyFrom = Features(IwxxmGeoJsonExporter.ToGeoJson(SigmetOf(T0, null)).Value, out JsonDocument json1);
            List<JsonElement> onlyUntil = Features(IwxxmGeoJsonExporter.ToGeoJson(SigmetOf(null, T0.AddHours(4))).Value, out JsonDocument json2);
            using (json1)
            using (json2)
            {
                Assert.All(onlyFrom, p =>
                {
                    Assert.Equal(Instant(T0), p.GetProperty("validFromUtc").GetString());
                    AssertValidityRightAfterTheBase(p, true, false);
                });
                Assert.All(onlyUntil, p =>
                {
                    Assert.Equal(Instant(T0.AddHours(4)), p.GetProperty("validUntilUtc").GetString());
                    AssertValidityRightAfterTheBase(p, false, true);
                });
            }
        }

        [Fact]
        public void Airmet_WithoutAValidityPeriod_OmitsBothKeys_AndWithOneEmitsThem()
        {
            Airmet Create(DateTimeOffset? from, DateTimeOffset? until)
            {
                Result<Airmet> result = Airmet.Create(
                    "a1", T0, ReportStatus.Normal, PermissibleUsage.Operational,
                    "YUDD", null, null, null, null, "1", from, until,
                    evolvingConditions: new[] { new AirmetEvolvingCondition(Volume("c1")) });
                Assert.True(result.IsSuccess, string.Join(" | ", result.Errors));
                return result.Value;
            }

            List<JsonElement> without = Features(IwxxmGeoJsonExporter.ToGeoJson(Create(null, null)).Value, out JsonDocument json1);
            List<JsonElement> with = Features(IwxxmGeoJsonExporter.ToGeoJson(Create(T0, T0.AddHours(3))).Value, out JsonDocument json2);
            using (json1)
            using (json2)
            {
                Assert.Single(without);
                Assert.All(without, p => AssertValidityRightAfterTheBase(p, false, false));
                Assert.Single(with);
                Assert.All(with, p =>
                {
                    Assert.Equal(Instant(T0), p.GetProperty("validFromUtc").GetString());
                    Assert.Equal(Instant(T0.AddHours(3)), p.GetProperty("validUntilUtc").GetString());
                    AssertValidityRightAfterTheBase(p, true, true);
                });
            }
        }

        [Theory]
        [InlineData("va-advisory-A7-2.xml")]
        [InlineData("spacewx-A7-5.xml")]
        [InlineData("tc-advisory-A2-2.xml")]
        [InlineData("vona-A7-1.xml")]
        [InlineData("qvaci-Example.xml")]
        public void ReportsWithoutAValidityPeriod_DoNotGainTheKeys(string file)
        {
            // VAA, TCA and space weather advisories carry no validity period in the model (only phenomenon times).
            string geoJson = IwxxmParser.ParseFileToGeoJson(PathOf(file)).Value;

            Assert.DoesNotContain("validFromUtc", geoJson);
            Assert.DoesNotContain("validUntilUtc", geoJson);
        }
    }
}
