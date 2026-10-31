using System.Collections.Generic;
using System.IO;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using Iwxxm.Net.Reports;
using Iwxxm.Net.Infrastructure.Mapping;

namespace Iwxxm.Net.Benchmarks
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
        }
    }

    /// <summary>
    /// Performance and GC memory allocation benchmarks for <see cref="IwxxmParser"/>,
    /// <see cref="IwxxmSigwxParser"/>, and the zero-array-allocation <see cref="GmlCoordinateTokenizer"/>.
    /// </summary>
    [MemoryDiagnoser]
    public class IwxxmParserBenchmarks
    {
        private byte[] _wafsSigwxPayload = null!;
        private byte[] _qvaciPayload = null!;
        private byte[] _collectBulletinPayload = null!;
        private string _samplePosList = null!;

        [GlobalSetup]
        public void Setup()
        {
            _wafsSigwxPayload = SampleFiles.Read("Samples/wmo-examples/WAFS-Example.xml");
            _qvaciPayload = SampleFiles.Read("Samples/wmo-examples/qvaci-Example.xml");
            _collectBulletinPayload = SampleFiles.Read("Samples/wmo-examples/metar-NIL-collect.xml");
            _samplePosList =
                "35.4 -104.7 36.2 -101.5 37.8 -98.1 39.0 -94.6 40.1 -91.2 " +
                "40.9 -88.0 41.3 -85.1 41.6 -82.4 41.8 -80.2 41.9 -78.9 " +
                "35.4 -104.7";

            // Warm up lazy XSD and Schematron XSLT caches prior to steady-state measurement
            using (var ms = new MemoryStream(_wafsSigwxPayload, writable: false))
            {
                IwxxmSigwxParser.Parse(ms);
            }
            using (var ms = new MemoryStream(_qvaciPayload, writable: false))
            {
                IwxxmParser.Parse(ms);
            }
        }

        [Benchmark(Description = "WAFS SIGWX 2025-2 Full Dual Validation + ACL Mapping")]
        public Result<Sigwx> ParseWafsSigwxForecast()
        {
            using (var stream = new MemoryStream(_wafsSigwxPayload, writable: false))
            {
                return IwxxmSigwxParser.Parse(stream);
            }
        }

        [Benchmark(Description = "QVACI Volcanic Ash Full Suite Validation + Hollow Polygon Mapping")]
        public Result<IwxxmDocument> ParseQvaciReport()
        {
            using (var stream = new MemoryStream(_qvaciPayload, writable: false))
            {
                return IwxxmParser.Parse(stream);
            }
        }

        [Benchmark(Description = "SWIM Collective Bulletin Unwrapping (METAR + SPECI)")]
        public Result<MeteorologicalBulletin> ParseSwimCollectiveBulletin()
        {
            using (var stream = new MemoryStream(_collectBulletinPayload, writable: false))
            {
                return IwxxmParser.ParseBulletin(stream);
            }
        }

        [Benchmark(Description = "GmlCoordinateTokenizer Zero-Array-Allocation gml:posList Scan")]
        public Result<IReadOnlyList<GeoCoordinate>> TokenizeGmlPosList()
        {
            return GmlCoordinateTokenizer.ParsePosList(_samplePosList, 2, "Benchmark/posList");
        }
    }
}
