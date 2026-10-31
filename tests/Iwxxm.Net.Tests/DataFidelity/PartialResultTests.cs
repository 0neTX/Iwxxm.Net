using System.Linq;
using System.Xml.Linq;
using Iwxxm.Net.Reports;
using Xunit;
using static Iwxxm.Net.Tests.DataFidelity.SampleDocuments;

namespace Iwxxm.Net.Tests.DataFidelity
{
    /// <summary>
    /// AUD-M12 / AUD-U4: a bulletin keeps its successfully mapped reports and lists the others as item errors; a WAFS
    /// chart keeps its mapped features and lists the failed ones. The result succeeds with warnings while at least one
    /// item maps; with none it fails (<c>DOMAIN-BULLETIN-EMPTY</c> / <c>DOMAIN-SIGWX-NO-FEATURE</c>).
    /// </summary>
    public sealed class PartialResultTests
    {
        private static readonly IwxxmParser Parser = new IwxxmParserBuilder().WithoutSchematron().Build();

        /// <summary>metar-NIL-collect.xml plus a second entry: the official metar-A3-1 METAR, optionally made unmappable.</summary>
        private static XDocument BulletinWithSecondReport(bool secondFails, bool firstFails = false)
        {
            XDocument bulletin = Load("metar-NIL-collect.xml");
            XElement second = new XElement(Load("metar-A3-1.xml").Root!);
            foreach (XElement e in second.DescendantsAndSelf().Where(e => e.Attribute(GmlNs + "id") != null && e != second))
                e.SetAttributeValue(GmlNs + "id", "b2-" + (string)e.Attribute(GmlNs + "id")!);
            foreach (XAttribute href in second.Descendants().Attributes(XlinkNs + "href").Where(a => a.Value.StartsWith("#")))
                href.Value = "#b2-" + href.Value.Substring(1);
            if (secondFails)
                BreakIssueTime(second);
            if (firstFails)
                BreakIssueTime(bulletin.Descendants(IwxxmNs + "METAR").Single());

            bulletin.Root!.Elements(CollectNs + "meteorologicalInformation").Last()
                .AddAfterSelf(new XElement(CollectNs + "meteorologicalInformation", second));
            return bulletin;
        }

        private static void BreakIssueTime(XElement report)
        {
            XElement issueTime = report.Element(IwxxmNs + "issueTime")!;
            issueTime.RemoveNodes();
            issueTime.SetAttributeValue(XlinkNs + "href", "#uuid.not-in-this-bulletin");
        }

        [Fact]
        public void Bulletin_WithOneUnmappableReport_KeepsTheOthers_AndSucceedsWithWarnings()
        {
            var result = Parser.ProcessBulletin(BulletinWithSecondReport(secondFails: true).ToString());

            Assert.True(result.IsSuccess, string.Join(" | ", result.Errors));
            MeteorologicalBulletin bulletin = result.Value;
            Assert.Single(bulletin.Reports);
            ItemMappingError itemError = Assert.Single(bulletin.ItemErrors);
            Assert.Equal(1, itemError.Index);
            Assert.Equal(Load("metar-A3-1.xml").Root!.Attribute(GmlNs + "id")!.Value, itemError.ItemId);
            Assert.Contains(itemError.Errors, e => e.Code == "ACL-ISSUE-TIME-MISSING");
            Assert.True(result.HasWarnings);
            Assert.All(result.Warnings, w => Assert.Equal(ValidationSeverity.Warning, w.Severity));
            Assert.Contains(result.Warnings, w => w.Code == "ACL-ISSUE-TIME-MISSING" && w.Message.Contains("#1"));
            Assert.Empty(result.Errors);
            Assert.Equal(2, bulletin.ReportResults.Count);
        }

        [Fact]
        public void Bulletin_WhoseReportsAllFail_IsAFailure()
        {
            var result = Parser.ProcessBulletin(BulletinWithSecondReport(secondFails: true, firstFails: true).ToString());

            Assert.True(result.IsFailure);
            Assert.Contains(result.Errors, e => e.Code == "DOMAIN-BULLETIN-EMPTY");
            Assert.Contains(result.Errors, e => e.Code == "ACL-ISSUE-TIME-MISSING");
        }

        [Fact]
        public void Bulletin_WithoutFailures_HasNoItemErrorsNorWarnings()
        {
            var result = Parser.ProcessBulletin(BulletinWithSecondReport(secondFails: false).ToString());

            Assert.True(result.IsSuccess, string.Join(" | ", result.Errors));
            Assert.Equal(2, result.Value.Reports.Count);
            Assert.Empty(result.Value.ItemErrors);
            Assert.False(result.HasWarnings);
        }

        private static XDocument WafsWithBrokenTurbulence(bool breakAll)
        {
            XDocument doc = Load("WAFS-Example.xml");
            foreach (XElement feature in doc.Descendants(IwxxmNs + "MeteorologicalFeature").ToList())
            {
                bool isTarget = (string?)feature.Attribute(GmlNs + "id") == "uuid.aaaf9f38-dc4c-410d-b056-c130a049bde9";
                if (breakAll || isTarget)
                {
                    foreach (XElement posList in feature.Descendants(GmlNs + "posList").Concat(feature.Descendants(GmlNs + "pos")))
                        posList.Value = "91.0 0.0 92.0 1.0 93.0 2.0 91.0 0.0";
                }
            }

            return doc;
        }

        [Fact]
        public void WafsChart_WithOneUnmappableFeature_KeepsTheOtherFeatures()
        {
            var official = IwxxmSigwxParser.ParseFile(PathOf("WAFS-Example.xml")).Value;

            var result = IwxxmSigwxParser.Parse(WafsWithBrokenTurbulence(breakAll: false).ToString());

            Assert.True(result.IsSuccess, string.Join(" | ", result.Errors));
            // The official example also references one remote feature, reported as a feature error (AUD-L4).
            ItemMappingError featureError = Assert.Single(result.Value.FeatureErrors, e => e.ItemId != null);
            Assert.Equal("uuid.aaaf9f38-dc4c-410d-b056-c130a049bde9", featureError.ItemId);
            Assert.Equal(official.Turbulences.Count - 1, result.Value.Turbulences.Count);
            Assert.Equal(official.JetStreams.Count, result.Value.JetStreams.Count);
            Assert.Equal(official.Clouds.Count, result.Value.Clouds.Count);
            Assert.True(result.HasWarnings);
            Assert.Contains(result.Warnings, w => w.Message.Contains("uuid.aaaf9f38-dc4c-410d-b056-c130a049bde9"));
        }

        [Fact]
        public void WafsChart_WhoseFeaturesAllFail_IsAFailure()
        {
            var result = IwxxmSigwxParser.Parse(WafsWithBrokenTurbulence(breakAll: true).ToString());

            Assert.True(result.IsFailure);
            Assert.Contains(result.Errors, e => e.Code == "DOMAIN-SIGWX-NO-FEATURE");
        }
    }
}
