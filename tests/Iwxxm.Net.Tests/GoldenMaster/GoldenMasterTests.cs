// The golden files are generated on .NET (net10.0). On .NET Framework (net48, Windows CI) "R" formatting, double parsing and
// the math library are not bit-identical to .NET, so the byte-for-byte characterization runs on .NET only; the behaviour is
// covered on net48 by every other test (AUD-M17).
#if !NETFRAMEWORK
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Iwxxm.Net.Reports;
using Xunit;

namespace Iwxxm.Net.Tests.GoldenMaster
{
    /// <summary>
    /// Characterization (golden-master) tests: the full mapped Domain model and the RFC 7946 GeoJSON output of every
    /// official WMO example and SIGWX sample are compared byte for byte against committed golden files under
    /// <c>Golden/</c>. Any behaviour change in the mappers or the exporter shows up as a golden diff.
    /// </summary>
    /// <remarks>
    /// To regenerate the goldens after an intentional, CHANGELOG-documented behaviour change, run the tests with the
    /// environment variable <c>IWXXM_UPDATE_GOLDEN=1</c>; the files are written to the source <c>Golden/</c> directory
    /// (located by walking up from the test output directory to <c>Iwxxm.Net.Tests.csproj</c>), or to the directory
    /// given as the variable value when it is a path.
    /// </remarks>
    public sealed class GoldenMasterTests
    {
        private const string UpdateVariable = "IWXXM_UPDATE_GOLDEN";
        private const string GoldenDirectoryName = "Golden";

        private static readonly GeoJsonExportOptions CompactWithoutBoundingBox =
            new GeoJsonExportOptions(indented: false, includeBoundingBox: false);

        public static IEnumerable<object[]> GetSuiteSamples() =>
            Directory.GetFiles("Samples/wmo-examples", "*.xml")
                .Select(Path.GetFileName)
                .OrderBy(name => name, StringComparer.Ordinal)
                .Select(name => new object[] { name! });

        public static IEnumerable<object[]> GetSigwxSamples() =>
            new[] { "WAFS-Example.xml", "sigwx-swh-2025-2.xml" }.Select(name => new object[] { name });

        [Fact]
        public void SuiteSamples_CoverAll29OfficialWmoExamples()
        {
            Assert.Equal(29, GetSuiteSamples().Count());
        }

        [Theory]
        [MemberData(nameof(GetSuiteSamples))]
        public void SuiteParser_DomainModelAndGeoJson_MatchGoldenMaster(string sampleName)
        {
            var result = IwxxmParser.ParseFile(Path.Combine("Samples", "wmo-examples", sampleName));
            Assert.True(result.IsSuccess, string.Join(" | ", result.Errors));

            AssertReportMatchesGoldens("suite", sampleName, result.Value);
        }

        [Theory]
        [MemberData(nameof(GetSigwxSamples))]
        public void SigwxParser_DomainModelAndGeoJson_MatchGoldenMaster(string sampleName)
        {
            string path = sampleName == "WAFS-Example.xml"
                ? Path.Combine("Samples", "wmo-examples", sampleName)
                : Path.Combine("Samples", sampleName);

            var result = IwxxmSigwxParser.ParseFile(path);
            Assert.True(result.IsSuccess, string.Join(" | ", result.Errors));

            AssertReportMatchesGoldens("sigwx", sampleName, result.Value);
        }

        private static void AssertReportMatchesGoldens(string family, string sampleName, IwxxmDocument report)
        {
            string stem = Path.GetFileNameWithoutExtension(sampleName);

            AssertMatchesGolden(Path.Combine(family, stem + ".domain.txt"), CanonicalDomainDumper.Dump(report));
            AssertMatchesGolden(Path.Combine(family, stem + ".pretty.geojson"), IwxxmGeoJsonExporter.ToGeoJson(report, GeoJsonExportOptions.Pretty) + "\n");
            AssertMatchesGolden(Path.Combine(family, stem + ".compact.geojson"), IwxxmGeoJsonExporter.ToGeoJson(report, CompactWithoutBoundingBox) + "\n");
        }

        private static void AssertMatchesGolden(string relativePath, string actual)
        {
            string? updateDirectory = ResolveUpdateDirectory();
            if (updateDirectory != null)
            {
                string target = Path.Combine(updateDirectory, relativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.WriteAllText(target, actual, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                return;
            }

            string goldenPath = Path.Combine(AppContext.BaseDirectory, GoldenDirectoryName, relativePath);
            Assert.True(File.Exists(goldenPath),
                $"Golden file '{relativePath}' is missing. Regenerate with {UpdateVariable}=1 (see CONTRIBUTING.md).");

            string expected = File.ReadAllText(goldenPath).Replace("\r\n", "\n");
            if (!string.Equals(expected, actual, StringComparison.Ordinal))
            {
                Assert.Fail($"Output differs from golden '{relativePath}' at {DescribeFirstDifference(expected, actual)}. " +
                            $"If the change is intentional and documented in CHANGELOG.md, regenerate with {UpdateVariable}=1.");
            }
        }

        private static string? ResolveUpdateDirectory()
        {
            string? value = Environment.GetEnvironmentVariable(UpdateVariable);
            if (string.IsNullOrWhiteSpace(value) || value == "0" || value.Equals("false", StringComparison.OrdinalIgnoreCase))
                return null;

            if (value == "1" || value.Equals("true", StringComparison.OrdinalIgnoreCase))
            {
                var directory = new DirectoryInfo(AppContext.BaseDirectory);
                while (directory != null && !File.Exists(Path.Combine(directory.FullName, "Iwxxm.Net.Tests.csproj")))
                {
                    directory = directory.Parent;
                }

                if (directory == null)
                    throw new InvalidOperationException("Iwxxm.Net.Tests.csproj was not found above the test output directory.");

                return Path.Combine(directory.FullName, GoldenDirectoryName);
            }

            return value;
        }

        private static string DescribeFirstDifference(string expected, string actual)
        {
            int length = Math.Min(expected.Length, actual.Length);
            int index = 0;
            while (index < length && expected[index] == actual[index])
            {
                index++;
            }

            int line = 1 + expected.Take(index).Count(c => c == '\n');
            string expectedSnippet = Snippet(expected, index);
            string actualSnippet = Snippet(actual, index);
            return $"line {line} (expected '{expectedSnippet}', actual '{actualSnippet}')";
        }

        private static string Snippet(string text, int index)
        {
            if (index >= text.Length)
                return "<end>";

            return text.Substring(index, Math.Min(60, text.Length - index)).Replace("\n", "\\n");
        }
    }
}
#endif
