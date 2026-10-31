using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Iwxxm.Net.Infrastructure.Mapping;
using Iwxxm.Net.Reports;
using Xunit;

namespace Iwxxm.Net.Tests.DataFidelity
{
    /// <summary>
    /// WAFS features are never skipped silently (AUD-L4): by-reference features and unknown phenomena are reported as
    /// feature errors (partial results), and tropopause contour holes are kept.
    /// </summary>
    public sealed class SigwxFeatureReportingTests
    {
        private const string VolcanoPhenomenon = "http://codes.wmo.int/iwxxm/MeteorologicalFeature/VOLCANO";

        [Fact]
        public void FeatureGivenOnlyByReference_IsAFeatureError_AndTheOthersAreKept()
        {
            string wafs = SampleDocuments.Text("WAFS-Example.xml");
            int start = wafs.LastIndexOf("<iwxxm:feature>", wafs.IndexOf("<iwxxm:phenomenon xlink:href=\"" + VolcanoPhenomenon, StringComparison.Ordinal), StringComparison.Ordinal);
            int end = wafs.IndexOf("</iwxxm:feature>", start, StringComparison.Ordinal) + "</iwxxm:feature>".Length;
            string xml = wafs.Substring(0, start) + "<iwxxm:feature xlink:href=\"http://example.invalid/features/1\"/>" + wafs.Substring(end);

            Result<Sigwx> result = Map(xml);

            Assert.True(result.IsSuccess, string.Join(" | ", result.Errors));
            ItemMappingError error = Assert.Single(result.Value.FeatureErrors, e => e.Errors[0].Message.Contains("example.invalid"));
            Assert.Null(error.ItemId);
            Assert.Equal("ACL-SIGWX-FEATURE-BY-REFERENCE", Assert.Single(error.Errors).Code);
            Assert.Contains("http://example.invalid/features/1", error.Errors[0].Message, StringComparison.Ordinal);
            Assert.Empty(result.Value.Volcanoes);
            Assert.NotEmpty(result.Value.JetStreams);
            Assert.Contains(result.Warnings, w => w.Code == "ACL-SIGWX-FEATURE-BY-REFERENCE");
        }

        [Fact]
        public void OfficialWafsExample_RemoteFeatureReference_IsReportedNotSkipped()
        {
            Result<Sigwx> result = IwxxmSigwxParser.ParseFile(SampleDocuments.PathOf("WAFS-Example.xml"));

            Assert.True(result.IsSuccess, string.Join(" | ", result.Errors));
            ItemMappingError error = Assert.Single(result.Value.FeatureErrors);
            Assert.Equal(9, error.Index);
            Assert.Equal("ACL-SIGWX-FEATURE-BY-REFERENCE", Assert.Single(error.Errors).Code);
            Assert.Contains(result.Warnings, w => w.Code == "ACL-SIGWX-FEATURE-BY-REFERENCE");
        }

        [Fact]
        public void FeatureWithAnUnsupportedPhenomenon_IsAFeatureError()
        {
            string xml = SampleDocuments.Text("WAFS-Example.xml").Replace(
                "<iwxxm:phenomenon xlink:href=\"" + VolcanoPhenomenon + "\"/>",
                "<iwxxm:phenomenon xlink:href=\"http://codes.wmo.int/iwxxm/MeteorologicalFeature/HAIL_SWATH\"/>");

            Result<Sigwx> result = Map(xml);

            Assert.True(result.IsSuccess, string.Join(" | ", result.Errors));
            ItemMappingError error = Assert.Single(result.Value.FeatureErrors, e => e.ItemId != null);
            ValidationError detail = Assert.Single(error.Errors);
            Assert.Equal("ACL-SIGWX-PHENOMENON-UNSUPPORTED", detail.Code);
            Assert.Contains("HAIL_SWATH", detail.Message, StringComparison.Ordinal);
            Assert.Empty(result.Value.Volcanoes);
        }

        [Fact]
        public void TropopauseContour_KeepsItsHoles()
        {
            string wafs = SampleDocuments.Text("WAFS-Example.xml");
            int tropopause = wafs.IndexOf("<iwxxm:phenomenon xlink:href=\"http://codes.wmo.int/iwxxm/MeteorologicalFeature/TROPOPAUSE\"", StringComparison.Ordinal);
            int exteriorEnd = wafs.IndexOf("</gml:exterior>", tropopause, StringComparison.Ordinal) + "</gml:exterior>".Length;
            string xml = wafs.Insert(exteriorEnd,
                "<gml:interior><gml:LinearRing><gml:posList>25.0 -80.0 24.0 -78.0 26.0 -77.0 25.0 -80.0</gml:posList></gml:LinearRing></gml:interior>");

            Result<Sigwx> result = IwxxmSigwxParser.Parse(xml);

            Assert.True(result.IsSuccess, string.Join(" | ", result.Errors));
            Assert.True(result.Value.FeatureErrors.All(e => e.ItemId == null), string.Join(" | ", result.Value.FeatureErrors.SelectMany(e => e.Errors)));
            GeoPolygon contour = Assert.Single(result.Value.Tropopauses).ContourPolygon!;
            IReadOnlyList<GeoCoordinate> hole = Assert.Single(contour.InteriorRings);
            Assert.Equal(new GeoCoordinate(25.0, -80.0), hole[0]);
            Assert.Equal(4, hole.Count);
        }

        private static Result<Sigwx> Map(string xml)
        {
            return new SigwxDocumentMapper().Map(new Extensibility.IwxxmDocumentContext(Encoding.UTF8.GetBytes(xml)));
        }
    }
}
