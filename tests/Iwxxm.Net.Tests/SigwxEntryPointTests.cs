using System.IO;
using System.Linq;
using System.Xml.Linq;
using Iwxxm.Net.Reports;
using Iwxxm.Net.Tests.GoldenMaster;
using Xunit;

namespace Iwxxm.Net.Tests
{
    /// <summary>
    /// SIGWX needs no entry point of its own: <see cref="IwxxmParser"/> reads the official WAFS root, the compact
    /// <c>SIGWXReport</c> root and bulletins through the same mapper as <see cref="IwxxmSigwxParser"/>, which only adds the
    /// typed result and an early refusal of any other root.
    /// </summary>
    public sealed class SigwxEntryPointTests
    {
        private const string WafsPath = "Samples/wmo-examples/WAFS-Example.xml";
        private const string CompactPath = "Samples/sigwx-swh-2025-2.xml";
        private const string MetarPath = "Samples/wmo-examples/metar-A3-1.xml";
        private static readonly XNamespace CollectNs = "http://def.wmo.int/collect/2014";
        private static readonly XNamespace GmlNs = "http://www.opengis.net/gml/3.2";

        private static string BulletinOfWafsChart() =>
            new XElement(CollectNs + "MeteorologicalBulletin",
                new XAttribute(XNamespace.Xmlns + "collect", CollectNs),
                new XAttribute(GmlNs + "id", "b1"),
                new XElement(CollectNs + "meteorologicalInformation", XDocument.Load(WafsPath).Root),
                new XElement(CollectNs + "bulletinIdentifier", "A_LTUK31EGRR260600_C_EGRR_20260826060000.xml")).ToString();

        [Theory]
        [InlineData(WafsPath)]
        [InlineData(CompactPath)]
        public void GeneralParser_ReadsBothSigwxRoots_AndGivesTheSameModelAsTheSigwxFacade(string path)
        {
            Result<Sigwx> general = IwxxmParser.ParseFileAs<Sigwx>(path);
            Result<Sigwx> narrow = IwxxmSigwxParser.ParseFile(path);

            Assert.True(general.IsSuccess, string.Join(" | ", general.Errors));
            Assert.True(narrow.IsSuccess, string.Join(" | ", narrow.Errors));
            Assert.Equal(CanonicalDomainDumper.Dump(narrow.Value), CanonicalDomainDumper.Dump(general.Value));
            Assert.Equal(narrow.Warnings.Select(w => w.Code), general.Warnings.Select(w => w.Code));
        }

        [Theory]
        [InlineData(WafsPath)]
        [InlineData(CompactPath)]
        public void GeneralParser_AllInputShapes_ReturnTheSigwxAggregate(string path)
        {
            string xml = File.ReadAllText(path);

            Assert.IsType<Sigwx>(IwxxmParser.Parse(xml).Value);
            Assert.IsType<Sigwx>(IwxxmParser.ParseFile(path).Value);
            using (FileStream stream = File.OpenRead(path))
            {
                Assert.IsType<Sigwx>(IwxxmParser.Parse(stream).Value);
            }

            Assert.True(IwxxmParser.ParseAs<Sigwx>(xml).IsSuccess);
            Assert.True(IwxxmParser.Default.ProcessAs<Sigwx>(xml).IsSuccess);
        }

        [Fact]
        public void GeneralParser_ReadsSigwxReportsInsideABulletin_WhichTheSigwxFacadeRefuses()
        {
            string xml = BulletinOfWafsChart();

            Result<MeteorologicalBulletin> bulletin = IwxxmParser.ParseBulletin(xml);

            Assert.True(bulletin.IsSuccess, string.Join(" | ", bulletin.Errors));
            Assert.IsType<Sigwx>(Assert.Single(bulletin.Value.Reports));

            ValidationError refusal = Assert.Single(IwxxmSigwxParser.Parse(xml).Errors);
            Assert.Equal(IwxxmErrorCodes.XsdUnexpectedRootElement, refusal.Code);
        }

        [Fact]
        public void TheDifference_SigwxFacadeRefusesOtherRootsEarly_GeneralParserFailsOnTheTypeAfterMapping()
        {
            ValidationError early = Assert.Single(IwxxmSigwxParser.ParseFile(MetarPath).Errors);
            ValidationError late = Assert.Single(IwxxmParser.ParseFileAs<Sigwx>(MetarPath).Errors);

            Assert.Equal(IwxxmErrorCodes.XsdUnexpectedRootElement, early.Code);
            Assert.Equal(ErrorCategory.SchemaViolation, early.Category);
            Assert.Equal(IwxxmErrorCodes.UnexpectedReportType, late.Code);
            Assert.Equal(ErrorCategory.DomainMapping, late.Category);
        }
    }
}
