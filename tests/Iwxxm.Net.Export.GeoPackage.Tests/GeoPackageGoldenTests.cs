// Like the core golden-master tests, these run on .NET only: the golden files are generated on .NET (net10.0), and on .NET
// Framework (and Mono) the math library behind the densified circles is not bit-identical, so 17-digit coordinates could
// differ in the last digit. The behaviour is covered on net48 by the tests that do not compare against a file.
#if !NETFRAMEWORK
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Iwxxm.Net.Reports;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Iwxxm.Net.Export.GeoPackage.Tests
{
    /// <summary>
    /// Golden canonical dumps (see <see cref="GeoPackageCanonicalDump"/>) of a few representative documents, written through
    /// the public API, are compared against the committed files in <c>Golden/</c>. A GeoPackage file itself is never compared
    /// byte for byte: its header carries the SQLite version and change counter.
    /// </summary>
    /// <remarks>
    /// Regenerate after an intentional, CHANGELOG-documented change of the file layout with the same variable the core
    /// golden-master tests use: <c>IWXXM_UPDATE_GOLDEN=1</c> writes to the source <c>Golden/</c> directory (found by walking
    /// up from the test output directory to this project file), and a path as the value writes there. Then review the diff:
    /// <c>IWXXM_UPDATE_GOLDEN=1 dotnet test tests/Iwxxm.Net.Export.GeoPackage.Tests -c Release -f net10.0 --filter FullyQualifiedName~GeoPackageGoldenTests</c>.
    /// </remarks>
    public sealed class GeoPackageGoldenTests : IDisposable
    {
        private const string UpdateVariable = "IWXXM_UPDATE_GOLDEN";
        private const string GoldenDirectoryName = "Golden";
        private const string ProjectFile = "Iwxxm.Net.Export.GeoPackage.Tests.csproj";

        // Fixed, so that the last_change of every table is the same on every run.
        private static readonly DateTimeOffset LastChange = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        private readonly string _directory = Path.Combine(Path.GetTempPath(), "iwxxm-gpkg-golden-" + Guid.NewGuid().ToString("N"));

        public GeoPackageGoldenTests() => Directory.CreateDirectory(_directory);

        public void Dispose() => Directory.Delete(_directory, recursive: true);

        // A METAR (points and attributes only), a SIGMET with a circle and a polygon, a small bulletin with several
        // reports, and the small SIGWX example (lines and polygons); the 14 MB SIGWX sample is not a golden.
        public static IEnumerable<object[]> Samples() => new[]
        {
            new object[] { "metar-A3-1.xml", false },
            new object[] { "sigmet-A6-2-TC.xml", false },
            new object[] { "metar-NIL-collect.xml", false },
            new object[] { "sigwx-swh-2025-2.xml", true },
        };

        [Theory]
        [MemberData(nameof(Samples))]
        public void CanonicalDump_MatchesTheGoldenFile(string sample, bool extra)
        {
            string path = Path.Combine(_directory, "golden.gpkg");
            Result<string> result = IwxxmParserGeoPackageExtensions.ParseFileToGeoPackage(
                extra ? SampleFiles.ExtraPathOf(sample) : SampleFiles.PathOf(sample), path, new GeoPackageExportOptions(GeoPackageVersion.Version14, LastChange));
            Assert.True(result.IsSuccess, string.Join(" | ", result.Errors));

            string dump;
            using (SqliteConnection connection = GeoPackageSqlite.Open(path))
                dump = GeoPackageCanonicalDump.Dump(connection);

            // The only value that follows the build: the version of the library that wrote the file.
            dump = System.Text.RegularExpressions.Regex.Replace(dump, @"(?m)^(\d+\tlibrary_version\t).*$", "$1<library version>");
            AssertMatchesGolden(Path.GetFileNameWithoutExtension(sample) + ".dump.txt", dump);
        }

        [Fact]
        public void Goldens_AreSmall_AndCoverTheGeometryFamilies()
        {
            if (ResolveUpdateDirectory() != null)
                return; // regenerating: the files are being written, not yet copied to the output directory

            string[] files = Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, GoldenDirectoryName), "*.dump.txt");
            Assert.Equal(Samples().Count(), files.Length);
            Assert.All(files, f => Assert.True(new FileInfo(f).Length < 300 * 1024, f + " is too large for a golden"));

            string all = string.Concat(files.Select(File.ReadAllText));
            foreach (string wkt in new[] { "POINT (", "MULTILINESTRING (", "MULTIPOLYGON (" })
                Assert.Contains(wkt, all);
        }

        private static void AssertMatchesGolden(string name, string actual)
        {
            string? updateDirectory = ResolveUpdateDirectory();
            if (updateDirectory != null)
            {
                Directory.CreateDirectory(updateDirectory);
                File.WriteAllText(Path.Combine(updateDirectory, name), actual, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                return;
            }

            string goldenPath = Path.Combine(AppContext.BaseDirectory, GoldenDirectoryName, name);
            Assert.True(File.Exists(goldenPath), $"Golden file '{name}' is missing. Regenerate with {UpdateVariable}=1 (see the remarks of {nameof(GeoPackageGoldenTests)}).");

            string expected = File.ReadAllText(goldenPath).Replace("\r\n", "\n");
            if (!string.Equals(expected, actual, StringComparison.Ordinal))
                Assert.Fail($"The dump differs from golden '{name}' at {DescribeFirstDifference(expected, actual)}. " +
                            $"If the change is intentional and documented in CHANGELOG.md, regenerate with {UpdateVariable}=1.");
        }

        private static string? ResolveUpdateDirectory()
        {
            string? value = Environment.GetEnvironmentVariable(UpdateVariable);
            if (string.IsNullOrWhiteSpace(value) || value == "0" || value.Equals("false", StringComparison.OrdinalIgnoreCase))
                return null;

            if (value == "1" || value.Equals("true", StringComparison.OrdinalIgnoreCase))
            {
                var directory = new DirectoryInfo(AppContext.BaseDirectory);
                while (directory != null && !File.Exists(Path.Combine(directory.FullName, ProjectFile)))
                    directory = directory.Parent;

                if (directory == null)
                    throw new InvalidOperationException(ProjectFile + " was not found above the test output directory.");

                return Path.Combine(directory.FullName, GoldenDirectoryName);
            }

            return value;
        }

        private static string DescribeFirstDifference(string expected, string actual)
        {
            int length = Math.Min(expected.Length, actual.Length);
            int index = 0;
            while (index < length && expected[index] == actual[index])
                index++;

            int line = 1 + expected.Take(index).Count(c => c == '\n');
            return $"line {line} (expected '{Snippet(expected, index)}', actual '{Snippet(actual, index)}')";
        }

        private static string Snippet(string text, int index) =>
            index >= text.Length ? "<end>" : text.Substring(index, Math.Min(60, text.Length - index)).Replace("\n", "\\n");
    }
}
#endif
