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
    /// <c>iwxxm:WAFSSignificantWeatherForecast</c> reports (T+06 to T+48) and 14.2 MB; it is well below the default 50 MiB size limit.
    /// <para>
    /// The file as published is NOT valid IWXXM 2025-2: it omits the mandatory <c>gml:id</c> of every report and of every
    /// <c>gml:TimePeriod</c>, so the XSD stage rejects it (a defect of the product, not of the library). To exercise the
    /// rest of the pipeline (Schematron, mapping, domain, GeoJSON) on the real content, the tests add the missing ids to a
    /// copy in memory (see <see cref="AddMissingIds"/>); the file on disk is never modified. The product also writes the
    /// non-UCUM wind speed unit <c>kn_i</c> (accepted with a warning, AUD-R5) and publishes each chart as an instant
    /// (accepted, AUD-R2).
    /// </para>
    /// </summary>
    public sealed class ExternalSigwxMetOfficeTests
    {
        private const string SampleDirectory = "Samples/external/metoffice";
        private const string FilePath = SampleDirectory + "/example-iwxxm-2025-2-sigwx-non-operational.xml";
        private const string ExpectedSha256 = "561b8884148eb715f8c0bb2f540c069ba2d7e0b1aa1cdc0134cb0a9d43caa30c";
        private const long ExpectedLength = 14068939;

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
        public void DefaultLimits_DoNotRejectTheBulletinBySize()
        {
            // ~14 MB is below the default 50 MiB cap: the synthetic bulletin parses cleanly.
            var result = IwxxmParser.ParseBulletinFile(FilePath);

            Assert.True(ExpectedLength < IwxxmParserOptions.Default.MaxDocumentBytes);
            Assert.DoesNotContain(result.Errors, e => e.Code == "XML-SIZE-LIMIT");
            Assert.True(result.IsSuccess, string.Join(" | ", result.Errors.Take(3)));
        }

        [Fact]
        public void ALowerLimit_RejectsTheBulletinAndTheSigwxEntryPoint_WithXmlSizeLimit()
        {
            // A deployment can still lower the cap: the real file is then refused before it is buffered.
            var options = new IwxxmParserOptions(maxDocumentBytes: 10L * 1024 * 1024);
            var bulletin = new IwxxmParserBuilder().WithOptions(options).Build().ProcessBulletinFile(FilePath);

            ValidationError error = Assert.Single(bulletin.Errors);
            Assert.Equal("XML-SIZE-LIMIT", error.Code);
            Assert.Equal(ErrorCategory.MalformedInput, error.Category);
            Assert.Contains("14068939", error.Message);
            Assert.Contains("10485760", error.Message);
        }

        [Fact]
        public void ALowerLimit_OnANonSeekableStream_StopsReadingJustAfterTheCap()
        {
            var options = new IwxxmParserOptions(maxDocumentBytes: 10L * 1024 * 1024);
            using (var counting = new NonSeekableCountingStream(File.OpenRead(FilePath)))
            {
                var result = new IwxxmParserBuilder().WithOptions(options).Build().Process(counting);

                Assert.Equal("XML-SIZE-LIMIT", Assert.Single(result.Errors).Code);
                Assert.InRange(counting.BytesRead, options.MaxDocumentBytes + 1, options.MaxDocumentBytes + 8192);
            }
        }

#if !NETFRAMEWORK
        // The remaining tests hold the 14 MB document as a string, an XDocument and the mapped model (several hundred MB
        // in total) and parse it three times: that does not fit comfortably in a 32-bit net48 test host, so they run on
        // .NET only (the Windows job still runs the integrity and size-limit tests above on net48).

        private static readonly Lazy<string> OriginalText = new Lazy<string>(() => File.ReadAllText(FilePath, Encoding.UTF8));
        private static readonly Lazy<XDocument> Source = new Lazy<XDocument>(() => XDocument.Parse(OriginalText.Value));

        // The product with only the gml:id values added: kn_i and the instant validity are kept as published.
        private static readonly Lazy<Result<MeteorologicalBulletin>> IdsOnly = new Lazy<Result<MeteorologicalBulletin>>(
            () => Process(AddMissingIds(OriginalText.Value, bracketKnots: false)));

        // The same with the UCUM knot unit "[kn_i]": fully valid, no warning.
        private static readonly Lazy<Result<MeteorologicalBulletin>> Repaired = new Lazy<Result<MeteorologicalBulletin>>(
            () => Process(AddMissingIds(OriginalText.Value, bracketKnots: true)));

        private static readonly Lazy<string> RepairedGeoJson = new Lazy<string>(() =>
        {
            var geoJson = IwxxmGeoJsonExporter.ToGeoJson(Repaired.Value.Value).Value;
            return geoJson;
        });

        [Fact]
        public void TheProductAsPublished_IsRejectedByTheXsdBecauseGmlIdIsMissing()
        {
            // Simulate missing gml:id on every report and TimePeriod to verify XSD rejection and line/position reporting.
            string withoutIds = Regex.Replace(
                OriginalText.Value,
                @"(<(?:iwxxm:WAFSSignificantWeatherForecast|gml:TimePeriod)\b[^>]*?)\s+gml:id=""[^""]+""",
                "$1");
            var result = IwxxmParser.ParseBulletin(withoutIds);

            Assert.True(result.IsFailure);
            Assert.Equal(30, result.Errors.Count); // 15 reports + 15 time periods, below MaxValidationErrors (100)
            Assert.All(result.Errors, e =>
            {
                Assert.Equal("XSD-SCHEMA-VIOLATION", e.Code);
                Assert.Contains("gml/3.2:id' is missing", e.Message);
            });

            // Independent check: the offending elements are exactly the reports and the bounding periods of the file.
            string[] lines = withoutIds.Split('\n');
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
        public void WithTheIdsAdded_PassesTheXsdAndTheBuiltInSchematronSubset()
        {
            // With the gml:id values added, the XSD and the built-in Schematron subset report nothing (the non-UCUM kn_i unit
            // is a warning of the mapping stage, see the AUD-R5 test below).
            var result = IdsOnly.Value;

            Assert.DoesNotContain(result.Errors, e => e.Category == ErrorCategory.SchemaViolation);
            Assert.DoesNotContain(result.Errors, e => e.Category == ErrorCategory.BusinessRuleViolation);
        }

        [Fact]
        public void WithOnlyTheIdsAdded_MapsAll15Reports_EvenThoughEveryChartIsAnInstant_AudR2()
        {
            var result = IdsOnly.Value;

            Assert.True(result.IsSuccess, string.Join(" | ", result.Errors.Take(3)));
            Assert.Equal(15, result.Value.Reports.Count);
            Assert.Empty(result.Value.ItemErrors);
            Assert.All(result.Value.Reports.Cast<Sigwx>(), r => Assert.Equal(r.ValidFromUtc, r.ValidUntilUtc));
        }

        [Fact]
        public void Repaired_FullPipeline_Succeeds_WithTheExpectedBulletinAndReportMetadata()
        {
            var result = Repaired.Value;

            Assert.True(result.IsSuccess, string.Join(" | ", result.Errors.Take(3)));
            Assert.Empty(result.Warnings);
            MeteorologicalBulletin bulletin = result.Value;
            Assert.Equal("uuid.00000000-0000-0000-0000-000000000001", bulletin.Id);
            Assert.Equal("uuid.00000000-0000-0000-0000-000000016261", bulletin.BulletinIdentifier);
            Assert.Equal(15, bulletin.Reports.Count);
            Assert.Equal(15, bulletin.ReportResults.Count);
            Assert.Empty(bulletin.ItemErrors);

            var lower = VerticalLimit.Create(110, VerticalUnit.FlightLevel, VerticalReference.Std).Value;
            var upper = VerticalLimit.Create(600, VerticalUnit.FlightLevel, VerticalReference.Std).Value;
            var issue = new DateTimeOffset(2099, 1, 1, 5, 32, 57, TimeSpan.Zero);
            var firstValid = new DateTimeOffset(2099, 1, 1, 6, 0, 0, TimeSpan.Zero);

            // T+06, T+09, ... T+48 from the 00:00Z base time: one chart every three hours, valid at an instant.
            for (int i = 0; i < 15; i++)
            {
                var sigwx = Assert.IsType<Sigwx>(bulletin.Reports[i]);
                Assert.Equal("London", sigwx.IssuingCentre);
                Assert.Equal(ReportStatus.Normal, sigwx.ReportStatus);
                Assert.Equal(PermissibleUsage.NonOperational, sigwx.PermissibleUsage);
                Assert.Equal(issue, sigwx.IssueTimeUtc);
                Assert.Equal(firstValid.AddHours(3 * i), sigwx.ValidFromUtc);
                Assert.Equal(sigwx.ValidFromUtc, sigwx.ValidUntilUtc); // an instant, as published
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
        public void TheNonUcumUnitKnI_IsAcceptedAsKnots_WithOneAggregatedWarning_AudR5()
        {
            // The Met Office writes uom="kn_i" on 1850 wind symbols; UCUM requires "[kn_i]". The alias is accepted, so all
            // 574 jet streams map, and the non-conformance stays visible as ONE warning for the whole bulletin.
            var result = IdsOnly.Value;

            Assert.True(result.IsSuccess, string.Join(" | ", result.Errors.Take(3)));
            var reports = result.Value.Reports.Cast<Sigwx>().ToList();
            Assert.Equal(574, reports.Sum(s => s.JetStreams.Count));
            Assert.Empty(reports.SelectMany(s => s.FeatureErrors));
            Assert.Equal(4252, reports.Sum(CountFeatures));

            ValidationError warning = Assert.Single(result.Warnings);
            Assert.Equal("ACL-MEASURE-UOM-NON-UCUM-ALIAS", warning.Code);
            Assert.Equal(ValidationSeverity.Warning, warning.Severity);
            Assert.Equal(ErrorCategory.DomainMapping, warning.Category);
            Assert.Contains("'kn_i'", warning.Message);
            Assert.Contains("'[kn_i]'", warning.Message);
            Assert.Contains("1850", warning.Message);
            Assert.Contains("'windSpeed'", warning.Message);

            // The values are the same as with the UCUM spelling.
            var ucum = Repaired.Value.Value.Reports.Cast<Sigwx>().ToList();
            Assert.Equal(ucum.SelectMany(s => s.JetStreams).Select(j => j.MaxWindSpeed), reports.SelectMany(s => s.JetStreams).Select(j => j.MaxWindSpeed));
            Assert.All(reports.SelectMany(s => s.JetStreams).SelectMany(j => j.CorePoints), p => Assert.Equal(SpeedUnit.Knots, p.WindSpeed.Unit));
        }

        [Fact]
        public void Repaired_JetStreamAndGeometrySpotChecks_MatchTheSource()
        {
            var first = (Sigwx)Repaired.Value.Value.Reports[0];

            // First jet stream of the first chart: core FL360 MSL, max wind 195 kt as reported (no conversion).
            JetStream jet = first.JetStreams[0];
            Assert.Equal("uuid.00000000-0000-0000-0000-000000000574", jet.Id);
            Assert.Equal(new Speed(195, SpeedUnit.Knots), jet.MaxWindSpeed);
            Assert.Equal(VerticalLimit.Create(360, VerticalUnit.FlightLevel, VerticalReference.Msl).Value, jet.CoreLevel);
            Assert.Equal(new GeoCoordinate(-17.75, 49.0), jet.AxisTrajectory[0]); // "Lat Long" order in the source

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

                // 4252 source features + 1850 jet stream core points; a tropopause contour is ONE polygon that carries its
                // level (no spot height per vertex, AUD-R3) and the product has no isolated tropopause spot heights.
                Assert.Equal(574, byType["JETSTREAM"]);
                Assert.Equal(1850, byType["JETSTREAM_CORE_POINT"]);
                Assert.Equal(733, byType["CLOUD"]);
                Assert.Equal(585, byType["TURBULENCE"]);
                Assert.Equal(1903, byType["AIRFRAME_ICING"]);
                Assert.Equal(301, byType["TROPOPAUSE_CONTOUR"]);
                Assert.False(byType.ContainsKey("TROPOPAUSE_SPOT_HEIGHT"));
                Assert.Equal(135, byType["VOLCANO"]);
                Assert.Equal(21, byType["TROPICAL_CYCLONE"]);
                Assert.Equal(4252 + 1850, features.Count);

                var contours = features.Where(f => f.GetProperty("properties").GetProperty("featureType").GetString() == "TROPOPAUSE_CONTOUR").ToList();
                Assert.All(contours, f =>
                {
                    var properties = f.GetProperty("properties");
                    Assert.False(string.IsNullOrEmpty(properties.GetProperty("levelUom").GetString()));
                    Assert.True(properties.GetProperty("level").GetDouble() > 0);
                });
            }
        }

        [Fact]
        public void Repaired_GeoJsonExport_HasASaneGlobalBoundingBox_AudR4()
        {
            // The features cover every longitude (polar caps): the box is the plain [-180, 180] extent, not a near-empty
            // west > east box such as [52.45, -90, 52.38, 90].
            using (var doc = System.Text.Json.JsonDocument.Parse(RepairedGeoJson.Value))
            {
                double[] bbox = doc.RootElement.GetProperty("bbox").EnumerateArray().Select(e => e.GetDouble()).ToArray();

                Assert.Equal(4, bbox.Length);
                Assert.Equal(-180.0, bbox[0]);
                Assert.Equal(180.0, bbox[2]);
                Assert.True(bbox[1] >= -90.0 && bbox[1] < -60.0, "south " + bbox[1]);
                Assert.True(bbox[3] <= 90.0 && bbox[3] > 60.0, "north " + bbox[3]);

                foreach (var feature in doc.RootElement.GetProperty("features").EnumerateArray())
                {
                    foreach (var position in Flatten(feature.GetProperty("geometry").GetProperty("coordinates")))
                    {
                        Assert.InRange(position[0], bbox[0], bbox[2]);
                        Assert.InRange(position[1], bbox[1], bbox[3]);
                    }
                }
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
        public void Repaired_GeoJsonExport_KeepsEverySourceVertex()
        {
            Assert.Empty(MissingVerticesPerFeature());
        }

        [Theory]
        [InlineData("uuid.00000000-0000-0000-0000-000000000910")]
        [InlineData("uuid.00000000-0000-0000-0000-000000001990")]
        [InlineData("uuid.00000000-0000-0000-0000-000000003034")]
        public void Repaired_SouthPolarContoursWithALobeAcrossTheAntimeridian_KeepAllVerticesAndStayValidPolygons_AudR1(string featureId)
        {
            Assert.DoesNotContain(featureId, MissingVerticesPerFeature().Keys);

            using (var doc = System.Text.Json.JsonDocument.Parse(RepairedGeoJson.Value))
            {
                var feature = doc.RootElement.GetProperty("features").EnumerateArray().Single(f => f.GetProperty("id").GetString() == featureId);
                var geometry = feature.GetProperty("geometry");
                string type = geometry.GetProperty("type").GetString()!;
                Assert.Contains(type, new[] { "Polygon", "MultiPolygon" });

                var rings = Rings(type, geometry.GetProperty("coordinates")).ToList();
                Assert.NotEmpty(rings);
                foreach (var ring in rings)
                {
                    Assert.Equal(ring[0], ring[ring.Count - 1]);
                    Assert.All(ring, p => Assert.InRange(p.Item1, -180.0, 180.0));
                }

                // The region is the one between the contour and the south pole: it reaches latitude -90.
                Assert.Contains(rings.SelectMany(r => r), p => p.Item2 == -90.0);
            }
        }

        // -180 and 180 are the same point: a line that starts or ends on the antimeridian is written on the side it lies on,
        // without a zero-length part (TD-GEO-2), so a source vertex at one of them may appear with the other sign.
        private static (double, double) Key(double lon, double lat) => (Math.Round(Math.Abs(lon) == 180.0 ? 180.0 : lon, 6), Math.Round(lat, 6));

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
                        Flatten(feature.GetProperty("geometry").GetProperty("coordinates")).Select(p => Key(p[0], p[1])));
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
                        expected.Add(Key(values[k + 1], values[k])); // source order is "Lat Long"
                }

                foreach (XElement pos in feature.Element(Iwxxm + "phenomenonGeometry")!.Descendants(Gml + "pos"))
                {
                    double[] values = pos.Value.Split((char[])null!, StringSplitOptions.RemoveEmptyEntries)
                        .Select(t => double.Parse(t, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
                    expected.Add(Key(values[1], values[0]));
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
            IwxxmParser.ParseBulletin(xml);

        /// <summary>
        /// Adds the missing <c>gml:id</c> values to a copy of the product in memory (UUID-shaped, as the official Schematron
        /// rule Common.BasicReport-3 requires) and optionally replaces the non-UCUM unit <c>kn_i</c> with <c>[kn_i]</c>.
        /// Nothing else is touched: the instant validity periods stay as published.
        /// </summary>
        private static string AddMissingIds(string xml, bool bracketKnots)
        {
            if (bracketKnots)
                xml = xml.Replace("uom=\"kn_i\"", "uom=\"[kn_i]\"");

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
