using System.Linq;
using System.Xml.Linq;
using Iwxxm.Net.Reports;
using Xunit;
using static Iwxxm.Net.Tests.DataFidelity.SampleDocuments;

namespace Iwxxm.Net.Tests.DataFidelity
{
    /// <summary>
    /// Geometry is never lost silently: an unreadable or degenerate interior ring is a mapping error, a surface with
    /// several patches is an explicit error where the Domain holds one surface, and every patch is read where it holds several.
    /// </summary>
    public sealed class GmlGeometryLossTests
    {
        private static readonly XNamespace Gml = GmlNs;

        private static XDocument SigmetWithPatchChange(System.Action<XElement> change)
        {
            XDocument doc = Load("sigmet-A6-1a-TS.xml");
            change(doc.Descendants(Gml + "PolygonPatch").First());
            return doc;
        }

        private static XElement Interior(string posList) =>
            new XElement(Gml + "interior", new XElement(Gml + "LinearRing", new XElement(Gml + "posList", posList)));

        [Fact]
        public void InteriorRing_WithTooFewPositions_IsAMappingError()
        {
            XDocument doc = SigmetWithPatchChange(patch => patch.Add(Interior("52.0 -10.0")));

            Result<IwxxmDocument> result = MapDirectly(doc);

            Assert.True(result.IsFailure);
            Assert.Contains(result.Errors, e => e.Code == "GEO-POLYGON-HOLE-DEGENERATE");
        }

        [Fact]
        public void InteriorRing_WithoutPositions_IsAMappingError()
        {
            XDocument doc = SigmetWithPatchChange(patch => patch.Add(new XElement(Gml + "interior")));

            Result<IwxxmDocument> result = MapDirectly(doc);

            Assert.True(result.IsFailure);
            Assert.Contains(result.Errors, e => e.Code == "ACL-GML-INTERIOR-RING-UNREADABLE");
        }

        [Fact]
        public void ValidInteriorRing_IsKept()
        {
            XDocument doc = SigmetWithPatchChange(patch => patch.Add(Interior("52.0 -11.0 52.0 -10.0 51.0 -10.0 52.0 -11.0")));

            Result<IwxxmDocument> result = MapDirectly(doc);

            Assert.True(result.IsSuccess, string.Join(" | ", result.Errors));
            GeoPolygon area = Assert.IsType<GeoPolygon>(Assert.IsType<Sigmet>(result.Value).EvolvingConditions[0].Area);
            Assert.Single(area.InteriorRings);
        }

        [Fact]
        public void EnRouteVolume_WithSeveralPatches_IsAnExplicitError_NotTheFirstPatch()
        {
            XDocument doc = Load("sigmet-A6-1a-TS.xml");
            XElement patch = doc.Descendants(Gml + "PolygonPatch").First();
            patch.AddAfterSelf(new XElement(patch));

            Result<IwxxmDocument> result = MapDirectly(doc);

            Assert.True(result.IsFailure);
            Assert.Contains(result.Errors, e => e.Code == "ACL-GML-MULTI-PATCH-UNSUPPORTED");
        }

        [Fact]
        public void SpaceWeatherRegion_WithSeveralPatches_KeepsEveryPatch()
        {
            Result<IwxxmDocument> result = MapDirectly(Load("spacewx-A7-4_alternate.xml"));

            Assert.True(result.IsSuccess, string.Join(" | ", result.Errors));
            var areaCounts = Assert.IsType<SpaceWx>(result.Value).Analyses
                .SelectMany(a => a.IntensityAndRegions).SelectMany(g => g.Regions).Select(r => r.Areas.Count).ToList();
            Assert.Contains(2, areaCounts);
        }
    }
}
