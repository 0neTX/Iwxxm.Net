using System.Linq;
using Iwxxm.Net.Infrastructure.Validation;
using Iwxxm.Net.Tests.DataFidelity;
using Xunit;

namespace Iwxxm.Net.Tests
{
    /// <summary>
    /// AUD-L5, non-timing evidence: the official RDF code lists read by the XSLT rules are parsed once per process, so the
    /// number of parses is bounded by the number of embedded lists and does not grow with the number of validations.
    /// (Timings and allocations are measured by <c>tests/Iwxxm.Net.Benchmarks</c>.)
    /// </summary>
    public sealed class RuleCacheReuseTests
    {
        [Fact]
        public void RepeatedValidationThroughTheDefaultFacade_DoesNotReparseTheCodeLists()
        {
            int embeddedCodeLists = typeof(IwxxmParser).Assembly.GetManifestResourceNames()
                .Count(name => name.Contains("Codelists") && name.EndsWith(".rdf", System.StringComparison.Ordinal));
            Assert.True(embeddedCodeLists > 0);

            string metar = SampleDocuments.Text("metar-A3-1.xml");
            Assert.True(IwxxmParser.Parse(metar).IsSuccess);
            int afterFirstCall = XsltSchematronValidator.SharedRuleDocuments.ParseCount;
            Assert.True(afterFirstCall > 0, "the METAR rules read at least one code list");

            // Without the cache every call would add at least one parse, i.e. far more than the lists that exist. Other tests
            // may legitimately load further lists in parallel, hence the upper bound instead of an exact count.
            for (int i = 0; i < 3 * embeddedCodeLists; i++)
            {
                Assert.True(IwxxmParser.Parse(metar).IsSuccess);
            }

            Assert.InRange(XsltSchematronValidator.SharedRuleDocuments.ParseCount, afterFirstCall, embeddedCodeLists);
        }
    }
}
