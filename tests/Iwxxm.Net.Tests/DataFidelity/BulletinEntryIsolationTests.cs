using System;
using System.Linq;
using System.Xml.Linq;
using Iwxxm.Net.Infrastructure.Mapping.Reports;
using Iwxxm.Net.Reports;
using Xunit;
using static Iwxxm.Net.Tests.DataFidelity.SampleDocuments;

namespace Iwxxm.Net.Tests.DataFidelity
{
    /// <summary>
    /// A report mapper that throws (for example a custom one) must only fail its own bulletin entry, and the warnings
    /// of successfully mapped entries must reach the bulletin result.
    /// </summary>
    public sealed class BulletinEntryIsolationTests
    {
        private static XElement ThreeEntryBulletin() =>
            new XElement(CollectNs + "MeteorologicalBulletin",
                new XAttribute(GmlNs + "id", "b1"),
                new XElement(CollectNs + "bulletinIdentifier", "A_LTXX01"),
                new[] { "r0", "r1", "r2" }.Select(id =>
                    new XElement(CollectNs + "meteorologicalInformation",
                        new XElement(IwxxmNs + "METAR", new XAttribute(GmlNs + "id", id)))));

        private static IwxxmReport RealReport() =>
            (IwxxmReport)IwxxmParser.ParseFile(PathOf("metar-A3-1.xml")).Value;

        [Fact]
        public void ThrowingMapper_FailsOnlyItsOwnEntry()
        {
            IwxxmReport report = RealReport();
            var result = BulletinMapper.Map(ThreeEntryBulletin(), e =>
            {
                if ((string)e.Attribute(GmlNs + "id")! == "r1")
                    throw new InvalidOperationException("boom");
                return Result<IwxxmReport>.Success(report);
            });

            Assert.True(result.IsSuccess, string.Join(" | ", result.Errors));
            Assert.Equal(2, result.Value.Reports.Count);
            ItemMappingError error = Assert.Single(result.Value.ItemErrors);
            Assert.Equal(1, error.Index);
            Assert.Equal("r1", error.ItemId);
            ValidationError e0 = Assert.Single(error.Errors);
            Assert.Equal("MAPPER-ERROR", e0.Code);
            Assert.Contains("#1", e0.Message);
            Assert.Contains("boom", e0.Message);
            Assert.Contains(result.Warnings, w => w.Code == "MAPPER-ERROR");
        }

        [Fact]
        public void OutOfMemory_IsNotSwallowed()
        {
            Assert.Throws<OutOfMemoryException>(() =>
                BulletinMapper.Map(ThreeEntryBulletin(), _ => throw new OutOfMemoryException()));
        }

        [Fact]
        public void WarningsOfMappedEntries_ReachTheBulletinResult()
        {
            IwxxmReport report = RealReport();
            var warning = new ValidationError("TEST-WARN", "child warning", ErrorCategory.DomainMapping, ValidationSeverity.Warning, "r0");
            var result = BulletinMapper.Map(ThreeEntryBulletin(), e =>
                (string)e.Attribute(GmlNs + "id")! == "r0"
                    ? Result<IwxxmReport>.Success(report, new[] { warning })
                    : Result<IwxxmReport>.Success(report));

            Assert.True(result.IsSuccess);
            Assert.Empty(result.Value.ItemErrors);
            Assert.Contains(warning, result.Warnings);
        }
    }
}
