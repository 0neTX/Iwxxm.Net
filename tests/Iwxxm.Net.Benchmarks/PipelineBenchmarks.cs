using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using Iwxxm.Net.Infrastructure.Resources;
using Iwxxm.Net.Infrastructure.Validation;
using Iwxxm.Net.Reports;

namespace Iwxxm.Net.Benchmarks
{
    /// <summary>Locates the official sample documents regardless of the working directory of the benchmark process.</summary>
    internal static class SampleFiles
    {
        private static readonly string Root = FindRoot();

        public static byte[] Read(string relativePath) => File.ReadAllBytes(Path.Combine(Root, relativePath));

        public static IReadOnlyList<string> WmoExamples() =>
            Directory.GetFiles(Path.Combine(Root, "Samples", "wmo-examples"), "*.xml")
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToList();

        private static string FindRoot()
        {
            // The samples are copied next to the assembly; BenchmarkDotNet may run the benchmark from a generated folder,
            // so also walk up to the source tree (tests/Iwxxm.Net.Tests/Samples).
            var starts = new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory };
            foreach (string start in starts)
            {
                for (DirectoryInfo? directory = new DirectoryInfo(start); directory != null; directory = directory.Parent)
                {
                    if (Directory.Exists(Path.Combine(directory.FullName, "Samples", "wmo-examples")))
                        return directory.FullName;

                    string source = Path.Combine(directory.FullName, "tests", "Iwxxm.Net.Tests");
                    if (Directory.Exists(Path.Combine(source, "Samples", "wmo-examples")))
                        return source;
                }
            }

            throw new DirectoryNotFoundException("The official samples (Samples/wmo-examples) were not found.");
        }
    }

    /// <summary>
    /// AUD-L5 (a): a SIGWX report embedded in a SWIM bulletin used to be re-serialized to a <see cref="MemoryStream"/> and
    /// parsed again; it is now mapped from the element that was already parsed. The standalone parse is the baseline: the
    /// bulletin adds only the envelope, so the ratio and the allocated bytes stay close to 1.
    /// </summary>
    [MemoryDiagnoser]
    public class SigwxInBulletinBenchmarks
    {
        private byte[] _standalone = null!;
        private byte[] _inBulletin = null!;

        [GlobalSetup]
        public void Setup()
        {
            _standalone = SampleFiles.Read("Samples/wmo-examples/WAFS-Example.xml");

            // The same WAFS report as the single entry of a collect:MeteorologicalBulletin.
            string report = Encoding.UTF8.GetString(_standalone);
            int start = report.IndexOf("<iwxxm:WAFSSignificantWeatherForecast", StringComparison.Ordinal);
            string bulletin =
                "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
                "<collect:MeteorologicalBulletin xmlns:collect=\"http://def.wmo.int/collect/2014\" " +
                "xmlns:gml=\"http://www.opengis.net/gml/3.2\" gml:id=\"uuid.bench-bulletin\">" +
                "<collect:meteorologicalInformation>" + report.Substring(start) + "</collect:meteorologicalInformation>" +
                "<collect:bulletinIdentifier>A_LWAF31EGRR010000_C_EGRR_20010131000000.xml</collect:bulletinIdentifier>" +
                "</collect:MeteorologicalBulletin>";
            _inBulletin = Encoding.UTF8.GetBytes(bulletin);

            // Warm the process-wide caches (XSD set, compiled rules, code lists) and fail fast on an invalid fixture.
            Require(IwxxmSigwxParser.Parse(new MemoryStream(_standalone, writable: false)).IsSuccess, "standalone SIGWX");
            Result<MeteorologicalBulletin> parsed = IwxxmParser.ParseBulletin(new MemoryStream(_inBulletin, writable: false));
            Require(parsed.IsSuccess && parsed.Value.Reports.Count == 1, "SIGWX in a bulletin: " + string.Join(" | ", parsed.Errors));
        }

        [Benchmark(Baseline = true, Description = "SIGWX standalone (IwxxmSigwxParser)")]
        public Result<Sigwx> StandaloneSigwx()
        {
            using (var stream = new MemoryStream(_standalone, writable: false))
            {
                return IwxxmSigwxParser.Parse(stream);
            }
        }

        [Benchmark(Description = "SIGWX inside a SWIM bulletin (IwxxmParser.ParseBulletin)")]
        public Result<MeteorologicalBulletin> SigwxInsideBulletin()
        {
            using (var stream = new MemoryStream(_inBulletin, writable: false))
            {
                return IwxxmParser.ParseBulletin(stream);
            }
        }

        internal static void Require(bool condition, string what)
        {
            if (!condition)
                throw new InvalidOperationException("Benchmark fixture failed: " + what);
        }
    }

    /// <summary>
    /// AUD-L5 (b), steady state: the XSLT business rules read the official RDF code lists with <c>document()</c>. The lists
    /// are parsed once per process; a repeated call through the default facade pays for neither the rule compilation nor
    /// the code-list parse. <see cref="CodeListParsedEveryCall"/> is what every call would cost without the cache.
    /// </summary>
    [MemoryDiagnoser]
    public class ValidationCacheBenchmarks
    {
        private const string CodeListLocation = "../Codelists/codes.wmo.int-49-2-AerodromePresentOrForecastWeather.rdf";

        private byte[] _metar = null!;
        private Uri _codeList = null!;

        [GlobalSetup]
        public void Setup()
        {
            _metar = SampleFiles.Read("Samples/wmo-examples/metar-A3-1.xml");
            _codeList = new EmbeddedXmlUrlResolver().ResolveUri(
                new Uri(EmbeddedXmlUrlResolver.EmbeddedRootUri, "Schematron/iwxxm-2025-2-sigwx.xslt"),
                CodeListLocation);

            // Warm: first call compiles the XSD set and the rules and parses the code lists the METAR rules touch.
            SigwxInBulletinBenchmarks.Require(IwxxmParser.Parse(new MemoryStream(_metar, writable: false)).IsSuccess, "METAR");
            XsltSchematronValidator.SharedRuleDocuments.GetEntity(_codeList, null, null);
        }

        [Benchmark(Baseline = true, Description = "METAR through the default facade, repeated call (rules + code lists cached)")]
        public Result<IwxxmDocument> RepeatedParse()
        {
            using (var stream = new MemoryStream(_metar, writable: false))
            {
                return IwxxmParser.Parse(stream);
            }
        }

        [Benchmark(Description = "Code list from the shared resolver (cached XPathDocument)")]
        public object CodeListCached() => XsltSchematronValidator.SharedRuleDocuments.GetEntity(_codeList, null, null);

        [Benchmark(Description = "Code list parsed on every call (what a per-Transform parse would cost)")]
        public object CodeListParsedEveryCall()
        {
            return new CachedRuleDocumentResolver().GetEntity(_codeList, null, null);
        }
    }

    /// <summary>
    /// AUD-L5 (b), first call: one measurement per fresh process (<see cref="RunStrategy.ColdStart"/>), so it includes the
    /// one-time XSD schema set compilation, the XSLT rule compilation and the first code-list parses. Run it without a
    /// <c>--job</c> override: <c>--filter "*FirstCall*"</c>.
    /// </summary>
    [MemoryDiagnoser]
    [SimpleJob(RunStrategy.ColdStart, launchCount: 5, warmupCount: 0, iterationCount: 1, invocationCount: 1)]
    public class FirstCallBenchmarks
    {
        private byte[] _metar = null!;

        [GlobalSetup]
        public void Setup() => _metar = SampleFiles.Read("Samples/wmo-examples/metar-A3-1.xml");

        [Benchmark(Description = "First METAR parse in a fresh process (cold caches)")]
        public Result<IwxxmDocument> FirstParse()
        {
            using (var stream = new MemoryStream(_metar, writable: false))
            {
                return IwxxmParser.Parse(stream);
            }
        }
    }

    /// <summary>AUD-L5 (c): baseline throughput of the whole pipeline over the 29 official WMO examples (steady state).</summary>
    [MemoryDiagnoser]
    public class FullSuiteBaselineBenchmarks
    {
        private byte[][] _documents = null!;

        [GlobalSetup]
        public void Setup()
        {
            _documents = SampleFiles.WmoExamples().Select(File.ReadAllBytes).ToArray();

            int failures = 0;
            foreach (byte[] document in _documents)
            {
                if (!IwxxmParser.Parse(new MemoryStream(document, writable: false)).IsSuccess)
                    failures++;
            }

            // Examples named "*-translation-failed" are valid documents; all 29 official examples must parse.
            SigwxInBulletinBenchmarks.Require(failures == 0, failures + " official examples failed to parse");
        }

        [Benchmark(Description = "Parse all 29 official WMO examples (schema + rules + mapping)")]
        public int ParseAllOfficialExamples()
        {
            int succeeded = 0;
            foreach (byte[] document in _documents)
            {
                using (var stream = new MemoryStream(document, writable: false))
                {
                    if (IwxxmParser.Parse(stream).IsSuccess)
                        succeeded++;
                }
            }

            return succeeded;
        }
    }
}
