using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Iwxxm.Net.Reports;
using Xunit;

namespace Iwxxm.Net.Tests
{
    /// <summary>
    /// Real-world test data: an unmodified operational WAFS SIGWX update package published by the UK Met Office
    /// (<c>Samples/external/metoffice</c>, see the README there). It is a <c>collect:MeteorologicalBulletin</c> with 15
    /// <c>iwxxm:WAFSSignificantWeatherForecast</c> reports (T+06 to T+48) and 14.2 MB, so it exceeds the default 10 MB limit.
    /// <para>
    /// The file as published is NOT valid IWXXM 2025-2: it omits the mandatory <c>gml:id</c> of every report and of every
    /// <c>gml:TimePeriod</c>, so the XSD stage rejects it (a defect of the product, not of the library). To exercise the
    /// rest of the pipeline (Schematron, mapping, domain, GeoJSON) on the real content, the tests repair a copy in memory
    /// (see <see cref="Repair"/>); the file on disk is never modified.
    /// </para>
    /// </summary>
    public sealed class ExternalSigwxMetOfficeTests
    {
        private const string SampleDirectory = "Samples/external/metoffice";
        private const string FilePath = SampleDirectory + "/NEW(2025-2)SIGWX-260826T0600-06-48.xml";
        private const string ExpectedSha256 = "70d84973a7a2ef67b0f24eaa647ffa327ff786aff6f534d32f0aba3fdc6c6490";
        private const long ExpectedLength = 14227220;
        private const string ZeroLengthPeriodSkip =
            "AUD-R2: the product publishes each chart as an instant (gml:TimePeriod with beginPosition == endPosition) and " +
            "the SIGWX domain rule DOMAIN-SIGWX-VALID-PERIOD requires end > begin, so every report is rejected. Enable when " +
            "the rule accepts a zero-length validity period (TECHNICAL_DEBT.md).";
        private const string VertexLossSkip =
            "AUD-R1: three south-polar TROPOPAUSE contours lose 6, 8 and 9 source vertices in the GeoJSON export " +
            "(lobes across the antimeridian are cut off). Enable when the polar-cap polygon keeps every source vertex " +
            "(TECHNICAL_DEBT.md).";

        private static readonly XNamespace Iwxxm = "http://icao.int/iwxxm/2025-2";
        private static readonly XNamespace Gml = "http://www.opengis.net/gml/3.2";
        private static readonly XNamespace Xlink = "http://www.w3.org/1999/xlink";

        [Fact]
        public void SampleFile_IsTheUnmodifiedDownload()
        {
            Assert.Equal(ExpectedLength, new FileInfo(FilePath).Length);

            using (var sha = SHA256.Create())
            using (var stream = File.OpenRead(FilePath))
            {
                string actual = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty).ToLowerInvariant();
                Assert.Equal(ExpectedSha256, actual);
            }
        }

        [Fact]
        public void DefaultLimits_RejectTheBulletinAndTheSigwxEntryPoint_WithXmlSizeLimit()
        {
            var bulletin = IwxxmParser.ParseBulletinFile(FilePath);
            var sigwx = IwxxmSigwxParser.ParseFile(FilePath);

            foreach (ValidationError error in new[] { Assert.Single(bulletin.Errors), Assert.Single(sigwx.Errors) })
            {
                Assert.Equal("XML-SIZE-LIMIT", error.Code);
                Assert.Equal(ErrorCategory.MalformedInput, error.Category);
                Assert.Contains("14227220", error.Message);
            }
        }

        [Fact]
        public void DefaultLimits_OnANonSeekableStream_StopReadingJustAfterTheCap()
        {
            using (var counting = new NonSeekableCountingStream(File.OpenRead(FilePath)))
            {
                var result = IwxxmParser.Parse(counting);

                Assert.Equal("XML-SIZE-LIMIT", Assert.Single(result.Errors).Code);
                long cap = IwxxmParserOptions.Default.MaxDocumentBytes;
                Assert.InRange(counting.BytesRead, cap + 1, cap + 8192);
            }
        }

#if !NETFRAMEWORK
        // The remaining tests hold the 14 MB document as a string, an XDocument and the mapped model (several hundred MB
        // in total) and parse it three times: that does not fit comfortably in a 32-bit net48 test host, so they run on
        // .NET only (the Windows job still runs the integrity and size-limit tests above on net48).

        private static readonly IwxxmParserOptions RaisedLimits =
            new IwxxmParserOptions(maxDocumentBytes: 32L * 1024 * 1024, maxCharactersInDocument: 32L * 1024 * 1024);

        private static readonly Lazy<string> OriginalText = new Lazy<string>(() => File.ReadAllText(FilePath, Encoding.UTF8));
        private static readonly Lazy<XDocument> Source = new Lazy<XDocument>(() => XDocument.Parse(OriginalText.Value));

        // The product with only the gml:id values added.
        private static readonly Lazy<Result<MeteorologicalBulletin>> WithIdsOnly = new Lazy<Result<MeteorologicalBulletin>>(
            () => Process(Repair(OriginalText.Value, extendPeriods: false, bracketKnots: false)));

        // The product with gml:id values, a one second validity period and the UCUM knot unit "[kn_i]" (fully valid).
        private static readonly Lazy<Result<MeteorologicalBulletin>> Repaired = new Lazy<Result<MeteorologicalBulletin>>(
            () => Process(Repair(OriginalText.Value, extendPeriods: true, bracketKnots: true)));

        // The same, but the non-UCUM unit "kn_i" published by the Met Office is left as is.
        private static readonly Lazy<Result<MeteorologicalBulletin>> RepairedKeepingKnI = new Lazy<Result<MeteorologicalBulletin>>(
            () => Process(Repair(OriginalText.Value, extendPeriods: true, bracketKnots: false)));

        private static readonly Lazy<string> RepairedGeoJson = new Lazy<string>(() =>
        {
            var geoJson = IwxxmGeoJsonExporter.ToGeoJson(Repaired.Value.Value);
            return geoJson;
        });

        [Fact]
        public void RaisedLimits_TheProductAsPublished_IsRejectedByTheXsdBecauseGmlIdIsMissing()
        {
            // Defect of the product: gml:id is mandatory on every feature/GML object (and the official WAFS example has them).
            var result = new IwxxmParserBuilder().WithOptions(RaisedLimits).Build().ProcessBulletinFile(FilePath);

            Assert.True(result.IsFailure);
            Assert.Equal(30, result.Errors.Count); // 15 reports + 15 time periods, below MaxValidationErrors (100)
            Assert.All(result.Errors, e =>
            {
                Assert.Equal("XSD-SCHEMA-VIOLATION", e.Code);
                Assert.Contains("gml/3.2:id' is missing", e.Message);
            });

            // Independent check: the offending elements are exactly the reports and the bounding periods of the file.
            string[] lines = OriginalText.Value.Split('\n');
            var expected = new List<(int, int)>();
            for (int i = 0; i < lines.Length; i++)
            {
                int report = lines[i].IndexOf("<iwxxm:WAFSSignificantWeatherForecast", StringComparison.Ordinal);
                int period = lines[i].IndexOf("<gml:TimePeriod>", StringComparison.Ordinal);
                if (report >= 0) expected.Add((i + 1, report + 2));
                if (period >= 0) expected.Add((i + 1, period + 2));
            }

            Assert.Equal(30, expected.Count);
            Assert.Equal(
                expected.OrderBy(p => p.Item1).ToList(),
                result.Errors.Select(e => (e.LineNumber!.Value, e.LinePosition!.Value)).OrderBy(p => p.Item1).ToList());
        }

        [Fact]
        public void RaisedLimits_WithTheIdsAdded_PassesTheXsdAndTheBuiltInSchematronSubset()
        {
            // With the gml:id values added, the XSD and the built-in Schematron subset report nothing: the only remaining
            // failure comes from the mapping stage (AUD-R2 tests below).
            var result = WithIdsOnly.Value;

            Assert.DoesNotContain(result.Errors, e => e.Category == ErrorCategory.SchemaViolation);
            Assert.DoesNotContain(result.Errors, e => e.Category == ErrorCategory.BusinessRuleViolation);
        }

        [Fact]
        public void RaisedLimits_WithOnlyTheIdsAdded_CurrentlyFailsEveryReportOnTheZeroLengthValidityPeriod_AudR2()
        {
            // Current behaviour, pinned until AUD-R2 is decided: begin == end is rejected.
            var result = WithIdsOnly.Value;

            Assert.True(result.IsFailure);
            Assert.Equal(15, result.Errors.Count(e => e.Code == "DOMAIN-SIGWX-VALID-PERIOD"));
            Assert.Contains(result.Errors, e => e.Code == "DOMAIN-BULLETIN-EMPTY");
        }

        [Fact(Skip = ZeroLengthPeriodSkip)]
        public void RaisedLimits_WithOnlyTheIdsAdded_MapsAll15Reports()
        {
            var result = WithIdsOnly.Value;

            Assert.True(result.IsSuccess, string.Join(" | ", result.Errors.Take(3)));
            Assert.Equal(15, result.Value.Reports.Count);
        }

        [Fact]
        public void Repaired_FullPipeline_Succeeds_WithTheExpectedBulletinAndReportMetadata()
        {
            var result = Repaired.Value;

            Assert.True(result.IsSuccess, string.Join(" | ", result.Errors.Take(3)));
            Assert.Empty(result.Warnings);
            MeteorologicalBulletin bulletin = result.Value;
            Assert.Equal("uuid.edea10e3-b5e9-4036-8c30-821932c914ab", bulletin.Id);
            Assert.Equal("uuid.8e37954b-9d15-448d-b4a0-bc7fe6cddfb1", bulletin.BulletinIdentifier);
            Assert.Equal(15, bulletin.Reports.Count);
            Assert.Equal(15, bulletin.ReportResults.Count);
            Assert.Empty(bulletin.ItemErrors);

            var lower = VerticalLimit.Create(100, VerticalUnit.FlightLevel, VerticalReference.Std).Value;
            var upper = VerticalLimit.Create(600, VerticalUnit.FlightLevel, VerticalReference.Std).Value;
            var issue = new DateTimeOffset(2026, 8, 26, 11, 32, 57, TimeSpan.Zero);
            var firstValid = new DateTimeOffset(2026, 8, 26, 12, 0, 0, TimeSpan.Zero);

            // T+06, T+09, ... T+48 from the 06:00Z base time: one chart every three hours, valid at an instant.
            for (int i = 0; i < 15; i++)
            {
                var sigwx = Assert.IsType<Sigwx>(bulletin.Reports[i]);
                Assert.Equal("London", sigwx.IssuingCentre);
                Assert.Equal(ReportStatus.Normal, sigwx.ReportStatus);
                Assert.Equal(PermissibleUsage.Operational, sigwx.PermissibleUsage);
                Assert.Equal(issue, sigwx.IssueTimeUtc);
                Assert.Equal(firstValid.AddHours(3 * i), sigwx.ValidFromUtc);
                Assert.Equal(sigwx.ValidFromUtc.AddSeconds(1), sigwx.ValidUntilUtc); // the repair's one second
                Assert.Equal(lower, sigwx.LowerLimit);
                Assert.Equal(upper, sigwx.UpperLimit);
                Assert.Empty(sigwx.FeatureErrors);
                Assert.Empty(sigwx.Sandstorms);
                Assert.Empty(sigwx.RadiationIncidents); // RADIATION is listed in phenomenaList but no feature is published
            }
        }

        [Fact]
        public void Repaired_MappedFeatureCounts_EqualAnIndependentCountOfTheXml()
        {
            var sigwxReports = Repaired.Value.Value.Reports.Cast<Sigwx>().ToList();
            var xmlReports = Source.Value.Descendants(Iwxxm + "WAFSSignificantWeatherForecast").ToList();
            Assert.Equal(xmlReports.Count, sigwxReports.Count);

            for (int i = 0; i < xmlReports.Count; i++)
            {
                Dictionary<string, int> byPhenomenon = xmlReports[i]
                    .Elements(Iwxxm + "feature").Elements(Iwxxm + "MeteorologicalFeature")
                    .GroupBy(f => ((string)f.Element(Iwxxm + "phenomenon")!.Attribute(Xlink + "href")!).Split('/').Last())
                    .ToDictionary(g => g.Key, g => g.Count());
                int Count(string key) => byPhenomenon.TryGetValue(key, out int n) ? n : 0;

                Sigwx model = sigwxReports[i];
                Assert.Equal(Count("JETSTREAM"), model.JetStreams.Count);
                Assert.Equal(Count("CLOUD"), model.Clouds.Count);
                Assert.Equal(Count("TURBULENCE"), model.Turbulences.Count);
                Assert.Equal(Count("AIRFRAME_ICING"), model.AirframeIcings.Count);
                Assert.Equal(Count("TROPOPAUSE"), model.Tropopauses.Count);
                Assert.Equal(Count("VOLCANO"), model.Volcanoes.Count);
                Assert.Equal(Count("TROPICAL_CYCLONE"), model.TropicalCyclones.Count);
                Assert.Equal(byPhenomenon.Values.Sum(), CountFeatures(model));
            }

            // Totals over the 15 charts (also verified with an external script when the sample was added).
            Assert.Equal(574, sigwxReports.Sum(s => s.JetStreams.Count));
            Assert.Equal(733, sigwxReports.Sum(s => s.Clouds.Count));
            Assert.Equal(585, sigwxReports.Sum(s => s.Turbulences.Count));
            Assert.Equal(1903, sigwxReports.Sum(s => s.AirframeIcings.Count));
            Assert.Equal(301, sigwxReports.Sum(s => s.Tropopauses.Count));
            Assert.Equal(135, sigwxReports.Sum(s => s.Volcanoes.Count));
            Assert.Equal(21, sigwxReports.Sum(s => s.TropicalCyclones.Count));
            Assert.Equal(4252, sigwxReports.Sum(CountFeatures));
            Assert.Equal(1850, sigwxReports.Sum(s => s.JetStreams.Sum(j => j.CorePoints.Count)));
        }

        [Fact]
        public void KeepingTheNonUcumUnitKnI_EveryJetStreamIsSkippedWithAFeatureErrorAndAWarning()
        {
            // The Met Office writes uom="kn_i"; UCUM (and the library) require "[kn_i]". The library assumes no unit, so each
            // of the 574 jet streams is a per-feature error (and a warning) while all other phenomena are still mapped.
            var result = RepairedKeepingKnI.Value;

            Assert.True(result.IsSuccess, string.Join(" | ", result.Errors.Take(3)));
            var reports = result.Value.Reports.Cast<Sigwx>().ToList();
            Assert.Equal(0, reports.Sum(s => s.JetStreams.Count));
            Assert.Equal(574, reports.Sum(s => s.FeatureErrors.Count));
            Assert.All(reports.SelectMany(s => s.FeatureErrors).SelectMany(f => f.Errors), e => Assert.Equal("ACL-MEASURE-UOM-UNSUPPORTED", e.Code));
            Assert.Equal(574, result.Warnings.Count(w => w.Code == "ACL-MEASURE-UOM-UNSUPPORTED"));
            Assert.Equal(4252 - 574, reports.Sum(CountFeatures));
        }

        [Fact]
        public void Repaired_JetStreamAndGeometrySpotChecks_MatchTheSource()
        {
            var first = (Sigwx)Repaired.Value.Value.Reports[0];

            // First jet stream of the first chart: core FL350 MSL, max wind 190 kt as reported (no conversion).
            JetStream jet = first.JetStreams[0];
            Assert.Equal("uuid.620d1d06-6f9d-4fd0-89ca-72bbfc265d9d", jet.Id);
            Assert.Equal(new Speed(190, SpeedUnit.Knots), jet.MaxWindSpeed);
            Assert.Equal(VerticalLimit.Create(350, VerticalUnit.FlightLevel, VerticalReference.Msl).Value, jet.CoreLevel);
            Assert.Equal(new GeoCoordinate(-19, 46.5), jet.AxisTrajectory[0]); // "Lat Long" order in the source

            // Every mapped coordinate is a valid WGS 84 position.
            foreach (Sigwx report in Repaired.Value.Value.Reports)
            {
                foreach (JetStream j in report.JetStreams)
                    Assert.All(j.AxisTrajectory, c => Assert.True(Math.Abs(c.Lat) <= 90 && Math.Abs(c.Lon) <= 180));
                foreach (Volcano v in report.Volcanoes)
                    Assert.NotNull(v.Name);
            }
        }

        [Fact]
        public void Repaired_GeoJsonExport_IsValidJson_WithTheExpectedFeatureCounts()
        {
            using (var doc = System.Text.Json.JsonDocument.Parse(RepairedGeoJson.Value))
            {
                var root = doc.RootElement;
                Assert.Equal("FeatureCollection", root.GetProperty("type").GetString());
                Assert.Equal(15, root.GetProperty("metadata").GetProperty("reportCount").GetInt32());

                var features = root.GetProperty("features").EnumerateArray().ToList();
                var byType = features
                    .GroupBy(f => f.GetProperty("properties").GetProperty("featureType").GetString()!)
                    .ToDictionary(g => g.Key, g => g.Count());

                // 4252 source features + 1850 jet stream core points + one spot height per tropopause contour vertex.
                int tropopauseVertices = Source.Value.Descendants(Iwxxm + "MeteorologicalFeature")
                    .Where(f => ((string)f.Element(Iwxxm + "phenomenon")!.Attribute(Xlink + "href")!).EndsWith("/TROPOPAUSE", StringComparison.Ordinal))
                    .Sum(f => f.Descendants(Gml + "posList").Sum(p => p.Value.Split((char[])null!, StringSplitOptions.RemoveEmptyEntries).Length / 2));

                Assert.Equal(574, byType["JETSTREAM"]);
                Assert.Equal(1850, byType["JETSTREAM_CORE_POINT"]);
                Assert.Equal(733, byType["CLOUD"]);
                Assert.Equal(585, byType["TURBULENCE"]);
                Assert.Equal(1903, byType["AIRFRAME_ICING"]);
                Assert.Equal(301, byType["TROPOPAUSE_CONTOUR"]);
                Assert.Equal(tropopauseVertices, byType["TROPOPAUSE_SPOT_HEIGHT"]);
                Assert.Equal(135, byType["VOLCANO"]);
                Assert.Equal(21, byType["TROPICAL_CYCLONE"]);
                Assert.Equal(4252 + 1850 + tropopauseVertices, features.Count);
            }
        }

        [Fact]
        public void Repaired_GeoJsonExport_RespectsRfc7946Basics()
        {
            using (var doc = System.Text.Json.JsonDocument.Parse(RepairedGeoJson.Value))
            {
                int rings = 0;
                foreach (var feature in doc.RootElement.GetProperty("features").EnumerateArray())
                {
                    var geometry = feature.GetProperty("geometry");
                    string type = geometry.GetProperty("type").GetString()!;
                    var coordinates = geometry.GetProperty("coordinates");

                    foreach (var position in Flatten(coordinates))
                    {
                        Assert.InRange(position[0], -180.0, 180.0); // longitude first
                        Assert.InRange(position[1], -90.0, 90.0);
                    }

                    foreach (var ring in Rings(type, coordinates))
                    {
                        rings++;
                        Assert.True(ring.Count >= 4);
                        Assert.Equal(ring[0], ring[ring.Count - 1]); // closed
                    }
                }

                Assert.True(rings > 3000);
            }
        }

        [Fact]
        public void Repaired_GeoJsonExport_KeepsEverySourceVertex_ExceptThreePolarContours_AudR1()
        {
            // Current behaviour, pinned until AUD-R1 is fixed: exactly three TROPOPAUSE contours lose vertices.
            var missing = MissingVerticesPerFeature();

            Assert.Equal(new[] { 6, 8, 9 }, missing.Values.OrderBy(v => v).ToArray());
        }

        [Fact(Skip = VertexLossSkip)]
        public void Repaired_GeoJsonExport_KeepsEverySourceVertex()
        {
            Assert.Empty(MissingVerticesPerFeature());
        }

        private static Dictionary<string, int> MissingVerticesPerFeature()
        {
            var geoJson = new Dictionary<string, HashSet<(double, double)>>();
            using (var doc = System.Text.Json.JsonDocument.Parse(RepairedGeoJson.Value))
            {
                foreach (var feature in doc.RootElement.GetProperty("features").EnumerateArray())
                {
                    string id = feature.GetProperty("id").GetString()!;
                    if (id.Contains('#'))
                        continue;
                    geoJson[id] = new HashSet<(double, double)>(
                        Flatten(feature.GetProperty("geometry").GetProperty("coordinates")).Select(p => (Math.Round(p[0], 6), Math.Round(p[1], 6))));
                }
            }

            var missing = new Dictionary<string, int>();
            foreach (XElement feature in Source.Value.Descendants(Iwxxm + "MeteorologicalFeature"))
            {
                string id = (string)feature.Attribute(Gml + "id")!;
                var expected = new HashSet<(double, double)>();
                foreach (XElement posList in feature.Element(Iwxxm + "phenomenonGeometry")!.Descendants(Gml + "posList"))
                {
                    double[] values = posList.Value.Split((char[])null!, StringSplitOptions.RemoveEmptyEntries)
                        .Select(t => double.Parse(t, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
                    for (int k = 0; k + 1 < values.Length; k += 2)
                        expected.Add((Math.Round(values[k + 1], 6), Math.Round(values[k], 6))); // source order is "Lat Long"
                }

                foreach (XElement pos in feature.Element(Iwxxm + "phenomenonGeometry")!.Descendants(Gml + "pos"))
                {
                    double[] values = pos.Value.Split((char[])null!, StringSplitOptions.RemoveEmptyEntries)
                        .Select(t => double.Parse(t, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
                    expected.Add((Math.Round(values[1], 6), Math.Round(values[0], 6)));
                }

                expected.ExceptWith(geoJson[id]);
                if (expected.Count > 0)
                    missing[id] = expected.Count;
            }

            return missing;
        }

        private static IEnumerable<double[]> Flatten(System.Text.Json.JsonElement coordinates)
        {
            if (coordinates.GetArrayLength() > 0 && coordinates[0].ValueKind == System.Text.Json.JsonValueKind.Number)
            {
                yield return new[] { coordinates[0].GetDouble(), coordinates[1].GetDouble() };
                yield break;
            }

            foreach (var child in coordinates.EnumerateArray())
                foreach (var position in Flatten(child))
                    yield return position;
        }

        private static IEnumerable<List<(double, double)>> Rings(string type, System.Text.Json.JsonElement coordinates)
        {
            IEnumerable<System.Text.Json.JsonElement> ringElements =
                type == "Polygon" ? coordinates.EnumerateArray()
                : type == "MultiPolygon" ? coordinates.EnumerateArray().SelectMany(p => p.EnumerateArray())
                : Enumerable.Empty<System.Text.Json.JsonElement>();

            foreach (var ring in ringElements)
                yield return ring.EnumerateArray().Select(p => (p[0].GetDouble(), p[1].GetDouble())).ToList();
        }

        private static int CountFeatures(Sigwx s) =>
            s.JetStreams.Count + s.Clouds.Count + s.Turbulences.Count + s.AirframeIcings.Count + s.Tropopauses.Count
            + s.Volcanoes.Count + s.TropicalCyclones.Count + s.Sandstorms.Count + s.RadiationIncidents.Count;

        private static Result<MeteorologicalBulletin> Process(string xml) =>
            new IwxxmParserBuilder().WithOptions(RaisedLimits).Build().ProcessBulletin(xml);

        /// <summary>
        /// Repairs a copy of the product in memory: adds the missing <c>gml:id</c> values (UUID-shaped, as the official
        /// Schematron rule Common.BasicReport-3 requires), optionally extends each zero-length validity period by one second
        /// and optionally replaces the non-UCUM unit <c>kn_i</c> with <c>[kn_i]</c>. Nothing else is touched.
        /// </summary>
        private static string Repair(string xml, bool extendPeriods, bool bracketKnots)
        {
            int report = 0;
            xml = Regex.Replace(xml, "<iwxxm:WAFSSignificantWeatherForecast reportStatus",
                m => $"<iwxxm:WAFSSignificantWeatherForecast gml:id=\"uuid.00000000-0000-4000-8000-{++report:D12}\" reportStatus");
            int period = 0;
            xml = Regex.Replace(xml, "<gml:TimePeriod>", m => $"<gml:TimePeriod gml:id=\"uuid.00000000-0000-4000-8001-{++period:D12}\">");

            if (extendPeriods)
                xml = Regex.Replace(xml, @"<gml:endPosition>\s*[^<]*</gml:endPosition>", m => m.Value.Replace(":00:00Z", ":00:01Z"));
            if (bracketKnots)
                xml = xml.Replace("uom=\"kn_i\"", "uom=\"[kn_i]\"");

            Assert.Equal(15, report);
            Assert.Equal(15, period);
            return xml;
        }
#endif

        private sealed class NonSeekableCountingStream : Stream
        {
            private readonly Stream _inner;

            public NonSeekableCountingStream(Stream inner) => _inner = inner;

            public long BytesRead { get; private set; }

            public override bool CanRead => true;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => throw new NotSupportedException();
            public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
            public override void Flush() { }
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

            public override int Read(byte[] buffer, int offset, int count)
            {
                int read = _inner.Read(buffer, offset, count);
                BytesRead += read;
                return read;
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing) _inner.Dispose();
                base.Dispose(disposing);
            }
        }
    }
}
