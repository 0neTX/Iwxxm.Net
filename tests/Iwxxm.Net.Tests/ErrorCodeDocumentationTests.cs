using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace Iwxxm.Net.Tests
{
    /// <summary>
    /// The error-code tables of <c>docs/REFERENCE.md</c> are the stable contract. This test keeps them honest: every
    /// stable code literal emitted by <c>src/</c> must be documented, and every documented code must still exist.
    /// </summary>
    public sealed class ErrorCodeDocumentationTests
    {
        // Upper-case, dash-separated string literals of src/ that are not error codes.
        private static readonly HashSet<string> NotErrorCodes = new HashSet<string>(StringComparer.Ordinal)
        {
            "UTF-8",
            "NON-OPERATIONAL",
            "W180-E180",
        };

        private static readonly Regex CodeLiteral = new Regex("\"([A-Z][A-Z0-9]*(?:-[A-Z0-9]+)+)\"", RegexOptions.Compiled);
        private static readonly Regex DocumentedCode = new Regex("`([A-Z][A-Z0-9]*(?:-[A-Z0-9]+)+)`", RegexOptions.Compiled);

        private static string RepositoryRoot()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null && !File.Exists(Path.Combine(directory.FullName, "Iwxxm.Net.slnx")))
                directory = directory.Parent;

            Assert.True(directory != null, "Iwxxm.Net.slnx was not found above the test output directory.");
            return directory!.FullName;
        }

        private static SortedSet<string> CodesEmittedBySource()
        {
            string src = Path.Combine(RepositoryRoot(), "src");
            var codes = new SortedSet<string>(StringComparer.Ordinal);
            foreach (string file in Directory.EnumerateFiles(src, "*.cs", SearchOption.AllDirectories))
            {
                string separator = Path.DirectorySeparatorChar.ToString();
                if (file.Contains(separator + "obj" + separator) || file.Contains(separator + "bin" + separator))
                    continue;

                foreach (Match match in CodeLiteral.Matches(File.ReadAllText(file)))
                {
                    if (!NotErrorCodes.Contains(match.Groups[1].Value))
                        codes.Add(match.Groups[1].Value);
                }
            }

            return codes;
        }

        private static SortedSet<string> CodesDocumented()
        {
            string text = File.ReadAllText(Path.Combine(RepositoryRoot(), "docs", "REFERENCE.md"));
            int start = text.IndexOf("\n## Error codes", StringComparison.Ordinal);
            int end = text.IndexOf("\n### Locations of findings", StringComparison.Ordinal);
            Assert.True(start >= 0 && end > start, "docs/REFERENCE.md must have an 'Error codes' section followed by 'Locations of findings'.");

            var codes = new SortedSet<string>(StringComparer.Ordinal);
            foreach (Match match in DocumentedCode.Matches(text.Substring(start, end - start)))
                codes.Add(match.Groups[1].Value);

            return codes;
        }

        [Fact]
        public void EveryCodeEmittedBySource_IsDocumented()
        {
            List<string> missing = CodesEmittedBySource().Except(CodesDocumented()).ToList();

            Assert.True(missing.Count == 0, "Add these codes to the error-code tables of docs/REFERENCE.md: " + string.Join(", ", missing));
        }

        [Fact]
        public void EveryDocumentedCode_IsStillEmittedBySource()
        {
            List<string> stale = CodesDocumented().Except(CodesEmittedBySource()).ToList();

            Assert.True(stale.Count == 0, "These documented codes are no longer emitted by src/ (remove or rename them in docs/REFERENCE.md): " + string.Join(", ", stale));
        }

        [Fact]
        public void TheSourceScan_FindsTheKnownCodes()
        {
            SortedSet<string> emitted = CodesEmittedBySource();

            Assert.Contains("STREAM-EMPTY", emitted);
            Assert.Contains("MAPPER-ERROR", emitted);
            Assert.Contains("SCHEMATRON-UNAVAILABLE", emitted);
            Assert.True(emitted.Count > 100, "The scan found only " + emitted.Count + " codes; the scan is broken.");
        }
    }
}
