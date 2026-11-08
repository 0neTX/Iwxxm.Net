using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Iwxxm.Net.Infrastructure.Validation;
using Xunit;

namespace Iwxxm.Net.Tests.DataFidelity
{
    /// <summary>
    /// AUD-N2: many documents validated and parsed in parallel against the shared official <c>XmlSchemaSet</c>, the
    /// SIGWX legacy set, the XSLT 1.0 Schematron validator and the builder-default facade give the same results as a
    /// sequential run.
    /// </summary>
    public sealed class ConcurrencyStressTests
    {
        private const int Rounds = 8;

        private static readonly string[] Samples = Directory.GetFiles(Path.Combine("Samples", "wmo-examples"), "*.xml")
            .Concat(new[] { Path.Combine("Samples", "sigwx-swh-2025-2.xml") })
            .OrderBy(f => f, StringComparer.Ordinal)
            .ToArray();

        // A few invalid variants so that the error paths are exercised concurrently too.
        private static readonly string[] Invalid =
        {
            File.ReadAllText(Path.Combine("Samples", "wmo-examples", "metar-A3-1.xml")).Replace("<iwxxm:qnh uom=\"hPa\">1018</iwxxm:qnh>", "<iwxxm:qnh uom=\"hPa\">abc</iwxxm:qnh>"),
            File.ReadAllText(Path.Combine("Samples", "wmo-examples", "taf-A5-1.xml")).Replace("reportStatus=\"NORMAL\"", "reportStatus=\"BOGUS\"")
        };

        private static IEnumerable<byte[]> Inputs() =>
            Samples.Select(File.ReadAllBytes).Concat(Invalid.Select(x => System.Text.Encoding.UTF8.GetBytes(x)));

        private static string Signature(IReadOnlyList<ValidationError> errors) =>
            string.Join("|", errors.Select(e => e.Code + ":" + e.LineNumber + ":" + e.Message));

        private static void AssertParallelMatchesSequential(Func<byte[], string> run)
        {
            byte[][] inputs = Inputs().ToArray();
            string[] expected = inputs.Select(run).ToArray();

            var actual = new ConcurrentBag<(int Index, string Result)>();
            Parallel.For(0, inputs.Length * Rounds, new ParallelOptions { MaxDegreeOfParallelism = Math.Max(4, Environment.ProcessorCount * 2) }, i =>
            {
                int index = i % inputs.Length;
                actual.Add((index, run(inputs[index])));
            });

            Assert.Equal(inputs.Length * Rounds, actual.Count);
            Assert.All(actual, a => Assert.Equal(expected[a.Index], a.Result));
        }

        [Fact]
        public void XsdValidator_SharedSchemaSets_AreDeterministicUnderParallelLoad()
        {
            XsdValidator validator = XsdValidator.CreateFullSuiteValidator();
            AssertParallelMatchesSequential(bytes => Signature(validator.Validate(new Extensibility.IwxxmDocumentContext(bytes))));
            Assert.Contains(Inputs().Select(b => validator.Validate(new Extensibility.IwxxmDocumentContext(b)).Count), c => c > 0);
        }

        [Fact]
        public void XsltSchematronValidator_IsDeterministicUnderParallelLoad()
        {
            var validator = new XsltSchematronValidator();
            AssertParallelMatchesSequential(bytes => Signature(validator.Validate(new Extensibility.IwxxmDocumentContext(bytes))));
        }

        [Fact]
        public void BuilderDefaultFacade_IsDeterministicUnderParallelLoad()
        {
            AssertParallelMatchesSequential(bytes =>
            {
                using var stream = new MemoryStream(bytes);
                var result = IwxxmParser.Parse(stream);
                return result.IsSuccess
                    ? result.Value.ReportType + ":" + result.Value.Id + ":" + IwxxmGeoJsonExporter.ToGeoJson(result.Value).Value.Length
                    : "failure:" + Signature(result.Errors);
            });
        }

        [Fact]
        public void UnitAliasWarning_IsAggregatedPerDocument_WithoutCrossTalkBetweenThreads()
        {
            string ucum = File.ReadAllText(Path.Combine("Samples", "sigwx-swh-2025-2.xml"));
            string alias = ucum.Replace("uom=\"[kn_i]\"", "uom=\"kn_i\"");
            Assert.NotEqual(ucum, alias);

            var failures = new ConcurrentBag<string>();
            Parallel.For(0, 64, new ParallelOptions { MaxDegreeOfParallelism = Math.Max(4, Environment.ProcessorCount * 2) }, i =>
            {
                bool withAlias = i % 2 == 0;
                var result = IwxxmSigwxParser.Parse(withAlias ? alias : ucum);
                var aliasWarnings = result.Warnings.Where(w => w.Code == "ACL-MEASURE-UOM-NON-UCUM-ALIAS").ToList();

                if (!result.IsSuccess)
                    failures.Add($"#{i}: {string.Join(" | ", result.Errors)}");
                else if (withAlias && (aliasWarnings.Count != 1 || !aliasWarnings[0].Message.Contains("2 'windSpeed' value(s)")))
                    failures.Add($"#{i}: expected one aggregated warning counting 2 values, got {aliasWarnings.Count}: {string.Join(" | ", aliasWarnings.Select(w => w.Message))}");
                else if (!withAlias && aliasWarnings.Count != 0)
                    failures.Add($"#{i}: unexpected alias warning on a UCUM document");
            });

            Assert.Empty(failures);
        }
    }
}
