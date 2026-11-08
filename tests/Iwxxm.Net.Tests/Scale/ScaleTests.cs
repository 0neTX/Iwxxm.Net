using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Iwxxm.Net.Reports;
using Iwxxm.Net.Tests.DataFidelity;
using Xunit;
using Xunit.Abstractions;

namespace Iwxxm.Net.Tests.Scale
{
    /// <summary>Scale tests run alone: other test classes running in parallel would disturb the timings.</summary>
    [CollectionDefinition(Name, DisableParallelization = true)]
    public sealed class ScaleCollection
    {
        public const string Name = "Scale";
    }

    /// <summary>
    /// Algorithmic-complexity checks of the pipeline. The library bounds the document size (50 MiB) and the element depth (128) but
    /// not the time or the number of vertices, so a super-linear step would turn a legitimate large document into a CPU or memory
    /// denial of service. Every scenario builds documents of growing size (a real WMO example scaled through templating), measures the
    /// median wall-clock time and the bytes allocated by the thread (after a warm-up), and asserts that the cost of the largest input
    /// stays clearly below the quadratic cost: at most 10x the time and 6x the allocations of a 4x larger input (the same exponents
    /// for other ratios; see <see cref="ScaleSeries"/>). Absolute timings are never asserted (the measured table and the log-log
    /// exponent are written to the test output instead), except for the single real-world data point, which has a generous ceiling.
    /// Memory is not asserted on net48 (no per-thread allocation counter). Run only these tests with
    /// <c>--filter "Category=Scale"</c>.
    /// </summary>
    [Collection(ScaleCollection.Name)]
    [Trait("Category", "Scale")]
    public sealed class ScaleTests
    {
        private static readonly int[] VertexCounts = { 25_000, 50_000, 100_000, 200_000 };
        private static readonly int[] Crossings = { 16_000, 32_000, 64_000, 128_000 };
        private static readonly int[] ExportCrossings = { 16_000, 32_000, 64_000, 128_000 };
        private static readonly int[] PracticalCrossings = { 500, 1_000, 2_000, 4_000 };
        private static readonly int[] ReportCounts = { 200, 400, 800, 1_600 };

        private readonly ITestOutputHelper _output;

        public ScaleTests(ITestOutputHelper output)
        {
            _output = output;
        }

        // ---------------------------------------------------------------- 1. polygon with N vertices

        [Fact]
        public void Polygon_VertexCount_ParseMapAndExport_AreSubQuadratic()
        {
            PolygonRun run = PolygonRun.Value;

            AssertGrowth(run.Parse);
            AssertGrowth(run.Map);
            AssertGrowth(run.Export);
        }

        // ---------------------------------------------------------------- 2. many antimeridian crossings

        [Fact]
        public void AntimeridianZigZag_Crossings_ParseValidateAndMap_AreSubQuadratic()
        {
            ScaleSeries parse = MeasureParse("zigzag parse+validate+map (polygon crossings)", "crossings", Crossings, ZigZag);

            AssertGrowth(parse);
        }

        [Fact]
        public void AntimeridianZigZag_PracticalRange_ExportsOneWesternPiecePlusOneTipPerTooth()
        {
            // Up to a few thousand crossings (far beyond any real SIGMET) the export is fast; this guards the result, not the cost.
            ScaleSeries export = MeasureExport("zigzag GeoJSON export, practical range", "crossings", PracticalCrossings, ZigZag, out List<string> outputs);

            for (int i = 0; i < PracticalCrossings.Length; i++)
            {
                // The comb with n/2 teeth is one western piece plus one tooth tip east of the antimeridian per tooth.
                using (JsonDocument json = JsonDocument.Parse(outputs[i]))
                {
                    JsonElement geometry = json.RootElement.GetProperty("features")[0].GetProperty("geometry");
                    Assert.Equal("MultiPolygon", geometry.GetProperty("type").GetString());
                    Assert.Equal((PracticalCrossings[i] / 2) + 1, geometry.GetProperty("coordinates").GetArrayLength());
                }
            }

            _output.WriteLine(export.ToTable());
        }

        // TD-SCALE-1 (resolved): the export used to be quadratic in the number of crossings, because StripClipper.Clip scanned every arc
        // for every arc it appended to a ring (8,000 crossings 105 ms; 16,000 241 ms; 32,000 930 ms; 64,000 3,972 ms; exponent 1.77).
        // The successor is now found with a sorted index (SuccessorIndex), so the cost is O(n log n); the rings are identical to the
        // scanning algorithm (StripClipperDifferentialTests). See TECHNICAL_DEBT.md for the measured table.
        [Fact]
        public void AntimeridianZigZag_Crossings_Export_IsSubQuadratic()
        {
            ScaleSeries export = MeasureExport("zigzag GeoJSON export (AntimeridianSplitter/StripClipper)", "crossings", ExportCrossings, ZigZag, out List<string> outputs);

            for (int i = 0; i < ExportCrossings.Length; i++)
            {
                using (JsonDocument json = JsonDocument.Parse(outputs[i]))
                    Assert.Equal((ExportCrossings[i] / 2) + 1, json.RootElement.GetProperty("features")[0].GetProperty("geometry").GetProperty("coordinates").GetArrayLength());
            }

            AssertGrowth(export);
        }

        // ---------------------------------------------------------------- 3. polar caps

        [Fact]
        public void PolarCap_VertexCount_ParseAndExport_AreSubQuadratic()
        {
            ScaleSeries parse = MeasureParse("polar cap parse+validate+map", "vertices", VertexCounts, n => ScaleDocuments.SigmetWithRing(ScaleDocuments.NorthPolarRing(n)));
            ScaleSeries export = MeasureExport(
                "polar cap GeoJSON export (PolarCaps)", "vertices", VertexCounts, n => ScaleDocuments.SigmetWithRing(ScaleDocuments.NorthPolarRing(n)), out List<string> outputs);

            for (int i = 0; i < VertexCounts.Length; i++)
                AssertEveryVertexIsKept(outputs[i], VertexCounts[i]);

            AssertGrowth(parse);
            AssertGrowth(export);
        }

        [Fact]
        public void PolarCapWithALobeLeavingTheStrip_VertexCount_ParseAndExport_AreSubQuadratic()
        {
            ScaleSeries parse = MeasureParse(
                "polar cap with lobe parse+validate+map", "vertices", VertexCounts, n => ScaleDocuments.SigmetWithRing(ScaleDocuments.SouthPolarRingWithLobe(n)));
            ScaleSeries export = MeasureExport(
                "polar cap with lobe GeoJSON export (PolarCaps + StripClipper)", "vertices", VertexCounts, n => ScaleDocuments.SigmetWithRing(ScaleDocuments.SouthPolarRingWithLobe(n)), out List<string> outputs);

            for (int i = 0; i < VertexCounts.Length; i++)
                AssertEveryVertexIsKept(outputs[i], VertexCounts[i]);

            AssertGrowth(parse);
            AssertGrowth(export);
        }

        // ---------------------------------------------------------------- 4. bulletin of N reports

        [Fact]
        public void Bulletin_ReportCount_WholePipeline_IsSubQuadratic()
        {
            var points = new List<ScalePoint>();
            var exportPoints = new List<ScalePoint>();
            foreach (int count in ReportCounts)
            {
                string xml = ScaleDocuments.Bulletin(count);
                Result<MeteorologicalBulletin> parsed = IwxxmParser.ParseBulletin(xml);
                Assert.True(parsed.IsSuccess, string.Join(" | ", parsed.Errors.Take(3)));
                Assert.Equal(count, parsed.Value.Reports.Count);
                Assert.Empty(parsed.Value.ItemErrors);

                points.Add(ScaleMeasurement.Measure(count, () => IwxxmParser.ParseBulletin(xml)));
                MeteorologicalBulletin bulletin = parsed.Value;
                exportPoints.Add(ScaleMeasurement.Measure(count, () => IwxxmGeoJsonExporter.ToGeoJson(bulletin)));
            }

            AssertGrowth(new ScaleSeries("bulletin parse+validate+map (METAR/SIGMET reports)", "reports", points));
            AssertGrowth(new ScaleSeries("bulletin GeoJSON export", "reports", exportPoints));
        }

#if !NETFRAMEWORK
        // ---------------------------------------------------------------- 5. the real Met Office SIGWX product (one data point)

        [Fact]
        public void MetOfficeSigwx_RealProduct_IsOneDataPoint_WithAGenerousCeiling()
        {
            // The 14.2 MB product, as in ExternalSigwxMetOfficeTests (the missing gml:id values are added to a copy in memory).
            string xml = AddMissingIds(File.ReadAllText("Samples/external/metoffice/NEW(2025-2)SIGWX-260826T0600-06-48.xml", Encoding.UTF8));

            Result<MeteorologicalBulletin>? parsed = null;
            string? geoJson = null;
            long parseBytes = ScaleMeasurement.AllocatedBytes();
            var parseWatch = Stopwatch.StartNew();
            parsed = IwxxmParser.ParseBulletin(xml);
            parseWatch.Stop();
            parseBytes = ScaleMeasurement.AllocatedBytes() - parseBytes;
            Assert.True(parsed.IsSuccess, string.Join(" | ", parsed.Errors.Take(3)));

            long exportBytes = ScaleMeasurement.AllocatedBytes();
            var exportWatch = Stopwatch.StartNew();
            geoJson = IwxxmGeoJsonExporter.ToGeoJson(parsed.Value);
            exportWatch.Stop();
            exportBytes = ScaleMeasurement.AllocatedBytes() - exportBytes;

            _output.WriteLine(string.Format(
                CultureInfo.InvariantCulture,
                "[scale] Met Office SIGWX (single cold run, informational)\n  input {0:0.0} MB, GeoJSON {1:0.0} MB, {2} reports\n  parse+validate+map {3:0} ms, {4:0.0} MB allocated\n  GeoJSON export     {5:0} ms, {6:0.0} MB allocated",
                xml.Length / 1048576.0,
                geoJson.Length / 1048576.0,
                parsed.Value.Reports.Count,
                parseWatch.Elapsed.TotalMilliseconds,
                parseBytes / 1048576.0,
                exportWatch.Elapsed.TotalMilliseconds,
                exportBytes / 1048576.0));

            Assert.Equal(15, parsed.Value.Reports.Count);
            Assert.True(parseWatch.Elapsed < TimeSpan.FromSeconds(60), "parse took " + parseWatch.Elapsed);
            Assert.True(exportWatch.Elapsed < TimeSpan.FromSeconds(60), "export took " + exportWatch.Elapsed);
        }
#endif

        // ---------------------------------------------------------------- 6. memory amplification

        [Fact]
        public void Polygon_MemoryAmplification_StaysBelowAGenerousConstant()
        {
            PolygonRun run = PolygonRun.Value;

            var sb = new StringBuilder("[scale] memory amplification (polygon with N vertices)\n");
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "  {0,10} {1,10} {2,10} {3,12} {4,12} {5,12}", "vertices", "xml KB", "json x", "parse alloc x", "export alloc x", "retained x"));
            for (int i = 0; i < run.XmlBytes.Count; i++)
            {
                double xml = run.XmlBytes[i];
                sb.AppendLine(string.Format(
                    CultureInfo.InvariantCulture,
                    "  {0,10} {1,10:0.0} {2,10:0.00} {3,12} {4,12} {5,12:0.00}",
                    VertexCounts[i],
                    xml / 1024.0,
                    run.JsonBytes[i] / xml,
                    Ratio(run.Parse.Points[i].AllocatedBytes, xml),
                    Ratio(run.Export.Points[i].AllocatedBytes, xml),
                    run.RetainedBytes[i] / xml));
            }

            _output.WriteLine(sb.ToString());

            for (int i = 0; i < run.XmlBytes.Count; i++)
            {
                // The GeoJSON of a polygon is about as large as its GML posList: a constant factor, never a multiple that grows.
                Assert.True(run.JsonBytes[i] < 3.0 * run.XmlBytes[i], "GeoJSON is " + (run.JsonBytes[i] / run.XmlBytes[i]).ToString("0.0", CultureInfo.InvariantCulture) + " x the document");
                if (run.Parse.HasMemory)
                {
                    Assert.True(run.Parse.Points[i].AllocatedBytes < 150.0 * run.XmlBytes[i], "parse allocated " + Ratio(run.Parse.Points[i].AllocatedBytes, run.XmlBytes[i]) + " x the document");
                    Assert.True(run.Export.Points[i].AllocatedBytes < 40.0 * run.XmlBytes[i], "export allocated " + Ratio(run.Export.Points[i].AllocatedBytes, run.XmlBytes[i]) + " x the document");
                }
            }
        }

        // ---------------------------------------------------------------- helpers

        private void AssertGrowth(ScaleSeries series)
        {
            _output.WriteLine(series.ToTable());
            Assert.True(
                series.TimeRatio < series.MaxTimeRatio,
                string.Format(CultureInfo.InvariantCulture, "{0}: time grew x{1:0.0} for a x{2:0.#} larger input (limit x{3:0.0}, exponent {4:0.00})", series.Name, series.TimeRatio, series.SizeRatio, series.MaxTimeRatio, series.TimeExponent));
            if (series.HasMemory)
            {
                Assert.True(
                    series.MemoryRatio < series.MaxMemoryRatio,
                    string.Format(CultureInfo.InvariantCulture, "{0}: allocations grew x{1:0.0} for a x{2:0.#} larger input (limit x{3:0.0}, exponent {4:0.00})", series.Name, series.MemoryRatio, series.SizeRatio, series.MaxMemoryRatio, series.MemoryExponent));
            }
        }

        private static string ZigZag(int crossings) => ScaleDocuments.SigmetWithRing(ScaleDocuments.Comb(crossings / 2));

        private static ScaleSeries MeasureParse(string name, string unit, int[] sizes, Func<int, string> build)
        {
            var points = new List<ScalePoint>();
            foreach (int size in sizes)
            {
                string xml = build(size);
                Result<IwxxmDocument> parsed = IwxxmParser.Parse(xml);
                Assert.True(parsed.IsSuccess, string.Join(" | ", parsed.Errors.Take(3)));
                points.Add(ScaleMeasurement.Measure(size, () => IwxxmParser.Parse(xml)));
            }

            return new ScaleSeries(name, unit, points);
        }

        private static ScaleSeries MeasureExport(string name, string unit, int[] sizes, Func<int, string> build, out List<string> outputs)
        {
            var points = new List<ScalePoint>();
            outputs = new List<string>();
            foreach (int size in sizes)
            {
                Result<IwxxmDocument> parsed = IwxxmParser.Parse(build(size));
                Assert.True(parsed.IsSuccess, string.Join(" | ", parsed.Errors.Take(3)));
                IwxxmDocument document = parsed.Value;
                ScalePoint point = ScaleMeasurement.Measure(size, () => IwxxmGeoJsonExporter.ToGeoJson(document));
                points.Add(point);
                outputs.Add((string)point.LastResult!);
            }

            return new ScaleSeries(name, unit, points);
        }

        // A closed ring of n source vertices must keep each of them; a cap or a split adds a few computed positions at most per piece.
        private static void AssertEveryVertexIsKept(string geoJson, int vertices)
        {
            using (JsonDocument json = JsonDocument.Parse(geoJson))
            {
                JsonElement geometry = json.RootElement.GetProperty("features")[0].GetProperty("geometry");
                int positions = ScaleDocuments.CountPositions(geometry.GetProperty("coordinates"));
                Assert.True(positions >= vertices, positions + " positions exported for " + vertices + " vertices");
                Assert.True(positions < (3 * vertices) + 100, positions + " positions exported for " + vertices + " vertices");
            }
        }

        private static string Ratio(long bytes, double documentBytes) =>
            bytes < 0 ? "n/a" : (bytes / documentBytes).ToString("0.0", CultureInfo.InvariantCulture);

        private static string AddMissingIds(string xml)
        {
            int report = 0;
            xml = Regex.Replace(xml, "<iwxxm:WAFSSignificantWeatherForecast reportStatus",
                m => $"<iwxxm:WAFSSignificantWeatherForecast gml:id=\"uuid.00000000-0000-4000-8000-{++report:D12}\" reportStatus");
            int period = 0;
            xml = Regex.Replace(xml, "<gml:TimePeriod>", m => $"<gml:TimePeriod gml:id=\"uuid.00000000-0000-4000-8001-{++period:D12}\">");
            xml = xml.Replace("uom=\"kn_i\"", "uom=\"[kn_i]\"");
            Assert.Equal(15, report);
            return xml;
        }

        /// <summary>The scenario 1 measurements, shared with the memory-amplification test so that the largest documents are measured once.</summary>
        private sealed class PolygonRun
        {
            public static readonly PolygonRun Value = new PolygonRun();

            private PolygonRun()
            {
                var parse = new List<ScalePoint>();
                var map = new List<ScalePoint>();
                var export = new List<ScalePoint>();
                foreach (int vertices in VertexCounts)
                {
                    string xml = ScaleDocuments.SigmetWithRing(ScaleDocuments.Ellipse(vertices));
                    XmlBytes.Add(Encoding.UTF8.GetByteCount(xml));

                    long before = GC.GetTotalMemory(true);
                    Result<IwxxmDocument> parsed = IwxxmParser.Parse(xml);
                    Assert.True(parsed.IsSuccess, string.Join(" | ", parsed.Errors.Take(3)));
                    long retained = GC.GetTotalMemory(true) - before;
                    RetainedBytes.Add(Math.Max(0, retained));
                    IwxxmDocument document = parsed.Value;
                    GC.KeepAlive(document);

                    parse.Add(ScaleMeasurement.Measure(vertices, () => IwxxmParser.Parse(xml)));
                    map.Add(ScaleMeasurement.Measure(vertices, () => SampleDocuments.MapDirectly(xml)));
                    ScalePoint exported = ScaleMeasurement.Measure(vertices, () => IwxxmGeoJsonExporter.ToGeoJson(document));
                    export.Add(exported);
                    JsonBytes.Add(Encoding.UTF8.GetByteCount((string)exported.LastResult!));
                    Assert.Equal("Polygon", JsonType((string)exported.LastResult!));
                }

                Parse = new ScaleSeries("polygon parse+validate+map (XSD, Schematron subset, mapping)", "vertices", parse);
                Map = new ScaleSeries("polygon mapping only (no validation)", "vertices", map);
                Export = new ScaleSeries("polygon GeoJSON export", "vertices", export);
            }

            public List<double> XmlBytes { get; } = new List<double>();

            public List<double> JsonBytes { get; } = new List<double>();

            public List<double> RetainedBytes { get; } = new List<double>();

            public ScaleSeries Parse { get; }

            public ScaleSeries Map { get; }

            public ScaleSeries Export { get; }

            private static string? JsonType(string geoJson)
            {
                using (JsonDocument json = JsonDocument.Parse(geoJson))
                    return json.RootElement.GetProperty("features")[0].GetProperty("geometry").GetProperty("type").GetString();
            }
        }
    }
}
