using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Xunit;

namespace Iwxxm.Net.Tests
{
    /// <summary>
    /// The error-code tables of <c>docs/REFERENCE.md</c> are the stable contract. This test keeps them honest. A code
    /// counts as emitted by <c>src/</c> in two ways only: a string literal in a file other than
    /// <c>IwxxmErrorCodes.cs</c> (the internal mapping codes), or a reference <c>IwxxmErrorCodes.&lt;Name&gt;</c> in a file
    /// other than <c>IwxxmErrorCodes.cs</c> (the public codes). The definition of a constant in <c>IwxxmErrorCodes.cs</c>
    /// never counts, so a constant nobody uses, or a documented code that only survives as a definition, is reported.
    /// Verified: every emitted code is documented; every documented code is still emitted; every public constant is
    /// documented with its value, is used outside its own class, and is written as a literal nowhere else in <c>src/</c>.
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

        private const string ErrorCodesFile = "IwxxmErrorCodes.cs";

        private static readonly Regex ConstantReference = new Regex(@"\bIwxxmErrorCodes\.(\w+)\b", RegexOptions.Compiled);

        // Every src/ file except build output and the file that defines the public constants.
        private static IEnumerable<string> SourceFilesOutsideTheCodeDefinitions()
        {
            string separator = Path.DirectorySeparatorChar.ToString();
            foreach (string file in Directory.EnumerateFiles(Path.Combine(RepositoryRoot(), "src"), "*.cs", SearchOption.AllDirectories))
            {
                if (file.Contains(separator + "obj" + separator) || file.Contains(separator + "bin" + separator)
                    || Path.GetFileName(file) == ErrorCodesFile)
                    continue;

                yield return file;
            }
        }

        // Source text without whole-line comments, so a mention in documentation is not mistaken for a use.
        private static string CodeOf(string file)
        {
            IEnumerable<string> lines = File.ReadAllLines(file).Where(l => !l.TrimStart().StartsWith("//", StringComparison.Ordinal));
            return string.Join("\n", lines);
        }

        // Names of the public IwxxmErrorCodes constants that some other src/ file refers to.
        private static SortedSet<string> PublicConstantsUsedBySource()
        {
            var used = new SortedSet<string>(StringComparer.Ordinal);
            foreach (string file in SourceFilesOutsideTheCodeDefinitions())
            {
                foreach (Match match in ConstantReference.Matches(CodeOf(file)))
                    used.Add(match.Groups[1].Value);
            }

            return used;
        }

        private static SortedSet<string> CodesEmittedBySource()
        {
            var codes = new SortedSet<string>(StringComparer.Ordinal);
            foreach (string file in SourceFilesOutsideTheCodeDefinitions())
            {
                string code = CodeOf(file);
                foreach (Match match in CodeLiteral.Matches(code))
                {
                    if (!NotErrorCodes.Contains(match.Groups[1].Value))
                        codes.Add(match.Groups[1].Value);
                }
            }

            SortedDictionary<string, string> publicConstants = PublicConstants();
            foreach (string name in PublicConstantsUsedBySource())
            {
                if (publicConstants.TryGetValue(name, out string? value))
                    codes.Add(value);
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

        private static readonly Regex ConstantRow = new Regex(
            "^\\| `IwxxmErrorCodes\\.(\\w+)` \\| `([A-Z][A-Z0-9]*(?:-[A-Z0-9]+)+)` \\|",
            RegexOptions.Compiled | RegexOptions.Multiline);

        private static SortedDictionary<string, string> PublicConstants()
        {
            var constants = new SortedDictionary<string, string>(StringComparer.Ordinal);
            foreach (FieldInfo field in typeof(IwxxmErrorCodes).GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                if (field.IsLiteral && !field.IsInitOnly && field.FieldType == typeof(string))
                    constants.Add(field.Name, (string)field.GetRawConstantValue()!);
            }

            return constants;
        }

        private static SortedDictionary<string, string> ConstantsDocumented()
        {
            string text = File.ReadAllText(Path.Combine(RepositoryRoot(), "docs", "REFERENCE.md"));
            int start = text.IndexOf("\n### Constants for the codes you branch on", StringComparison.Ordinal);
            Assert.True(start >= 0, "docs/REFERENCE.md must have a '### Constants for the codes you branch on' section.");
            int end = text.IndexOf("\n### ", start + 5, StringComparison.Ordinal);
            Assert.True(end > start, "The constants section must be followed by another '###' section.");

            var documented = new SortedDictionary<string, string>(StringComparer.Ordinal);
            foreach (Match row in ConstantRow.Matches(text.Substring(start, end - start)))
                documented.Add(row.Groups[1].Value, row.Groups[2].Value);

            return documented;
        }

        [Fact]
        public void EveryPublicConstant_IsDocumentedWithItsValue_AndEveryDocumentedConstantExists()
        {
            SortedDictionary<string, string> inCode = PublicConstants();
            SortedDictionary<string, string> inDocs = ConstantsDocumented();

            Assert.True(inCode.Count >= 20, "Only " + inCode.Count + " public constants were found by reflection; the scan is broken.");
            Assert.Equal(
                inCode.Select(c => c.Key + "=" + c.Value),
                inDocs.Select(c => c.Key + "=" + c.Value));
        }

        [Fact]
        public void EveryPublicConstant_IsEmittedBySource_AndIsInTheErrorCodeTables()
        {
            SortedSet<string> emitted = CodesEmittedBySource();
            SortedSet<string> documented = CodesDocumented();

            SortedSet<string> used = PublicConstantsUsedBySource();

            foreach (KeyValuePair<string, string> constant in PublicConstants())
            {
                Assert.True(used.Contains(constant.Key), "IwxxmErrorCodes." + constant.Key + " is never used by src/ outside IwxxmErrorCodes.cs.");
                Assert.True(emitted.Contains(constant.Value), "IwxxmErrorCodes." + constant.Key + " is not emitted by src/.");
                Assert.True(documented.Contains(constant.Value), "IwxxmErrorCodes." + constant.Key + " is missing from the error-code tables.");
            }

            Assert.Equal(PublicConstants().Count, PublicConstants().Values.Distinct(StringComparer.Ordinal).Count());
        }

        [Fact]
        public void PublicCodes_AreWrittenOnlyOnce_InIwxxmErrorCodes()
        {
            // Single source of truth: the library refers to these codes through the constants, never through a second literal.
            var publicCodes = new HashSet<string>(PublicConstants().Values, StringComparer.Ordinal);
            var duplicates = new List<string>();
            foreach (string file in SourceFilesOutsideTheCodeDefinitions())
            {
                foreach (Match match in CodeLiteral.Matches(File.ReadAllText(file)))
                {
                    if (publicCodes.Contains(match.Groups[1].Value))
                        duplicates.Add(Path.GetFileName(file) + ": " + match.Value);
                }
            }

            Assert.True(duplicates.Count == 0, "Use the IwxxmErrorCodes constant instead of a literal: " + string.Join(", ", duplicates));
        }

        [Fact]
        public void ErrorCategory_NumericValuesAreStable_AndEveryCategoryIsDocumented()
        {
            // Appended, never renumbered: persisted or serialized categories keep their meaning.
            Assert.Equal(0, (int)ErrorCategory.MalformedInput);
            Assert.Equal(1, (int)ErrorCategory.SchemaViolation);
            Assert.Equal(2, (int)ErrorCategory.BusinessRuleViolation);
            Assert.Equal(3, (int)ErrorCategory.Deserialization);
            Assert.Equal(4, (int)ErrorCategory.DomainMapping);
            Assert.Equal(5, (int)ErrorCategory.Io);
            Assert.Equal(6, Enum.GetValues(typeof(ErrorCategory)).Length);

            string text = File.ReadAllText(Path.Combine(RepositoryRoot(), "docs", "REFERENCE.md"));
            int start = text.IndexOf("\n### Error categories", StringComparison.Ordinal);
            Assert.True(start >= 0, "docs/REFERENCE.md must have an '### Error categories' section.");
            int end = text.IndexOf("\n### ", start + 5, StringComparison.Ordinal);
            string section = text.Substring(start, end - start);
            foreach (string name in Enum.GetNames(typeof(ErrorCategory)))
                Assert.Contains("| `" + name + "` |", section, StringComparison.Ordinal);
        }

        [Fact]
        public void TheSourceScan_FindsTheKnownCodes()
        {
            SortedSet<string> emitted = CodesEmittedBySource();

            Assert.Contains("STREAM-EMPTY", emitted);
            Assert.Contains("MAPPER-ERROR", emitted);
            Assert.Contains("SCHEMATRON-UNAVAILABLE", emitted);
            // The GeoPackage package keeps its code as a constant of its own: it is scanned like the rest of src/.
            Assert.Contains("GEOPACKAGE-EXPORT-ERROR", emitted);
            Assert.Contains("GEOPACKAGE-EXPORT-ERROR", CodesDocumented());
            Assert.True(emitted.Count > 100, "The scan found only " + emitted.Count + " codes; the scan is broken.");
        }
    }
}
