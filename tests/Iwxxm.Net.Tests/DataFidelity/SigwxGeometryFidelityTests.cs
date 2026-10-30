using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Xml.Linq;
using Iwxxm.Net.Reports;
using Xunit;
using static Iwxxm.Net.Tests.DataFidelity.SampleDocuments;

namespace Iwxxm.Net.Tests.DataFidelity
{
    /// <summary>
    /// AUD-M13: SIGWX geometry fidelity on synthetic variants of the official WAFS-Example.xml: curve segments are
    /// concatenated in document order whatever their type, every <c>gml:PolygonPatch</c> is read, and interior rings
    /// (holes) are kept.
    /// </summary>
    public sealed class SigwxGeometryFidelityTests
    {
        private const string TurbulenceVolumeId = "uuid.138a9477-e1fd-43e2-8060-0eb6a093d0eb";
        private const string TurbulenceFeatureId = "uuid.aaaf9f38-dc4c-410d-b056-c130a049bde9";

        private static XElement TurbulencePatches(XDocument doc) =>
            doc.Descendants(IwxxmNs + "ElevatedVolume")
                .Single(v => (string?)v.Attribute(GmlNs + "id") == TurbulenceVolumeId)
                .Element(GmlNs + "patches")!;

        private static XElement LinearRing(string tag, string posList) =>
            new XElement(GmlNs + tag, new XElement(GmlNs + "LinearRing", new XElement(GmlNs + "posList", posList)));

        [Fact]
        public void MixedCurveSegments_KeepDocumentOrder()
        {
            XDocument doc = Load("WAFS-Example.xml");
            XElement segments = doc.Descendants(GmlNs + "Curve")
                .Single(c => (string?)c.Attribute(GmlNs + "id") == "uuid.7b5a197a-d9bf-4f3b-ba36-701fe019b6d2")
                .Element(GmlNs + "segments")!;
            segments.AddFirst(new XElement(GmlNs + "LineStringSegment", new XElement(GmlNs + "posList", "34.0 -110.0 35.4 -104.7")));
            segments.Add(new XElement(GmlNs + "LineStringSegment", new XElement(GmlNs + "posList", "41.9 -78.9 41.0 -70.0")));

            var result = IwxxmSigwxParser.Parse(doc.ToString());

            Assert.True(result.IsSuccess, string.Join(" | ", result.Errors));
            var axis = result.Value.JetStreams.Single(j => j.Id == "uuid.8c3d3467-e398-4627-ab23-f9e57c2215b5").AxisTrajectory;
            Assert.Equal(new GeoCoordinate(34.0, -110.0), axis[0]);
            Assert.Equal(new GeoCoordinate(35.4, -104.7), axis[1]);
            Assert.Equal(new GeoCoordinate(41.9, -78.9), axis[axis.Count - 2]);
            Assert.Equal(new GeoCoordinate(41.0, -70.0), axis[axis.Count - 1]);
            Assert.Equal(13, axis.Count);
        }

        [Fact]
        public void EveryPolygonPatch_IsRead_AndExportedAsAMultiPolygon()
        {
            XDocument doc = Load("WAFS-Example.xml");
            TurbulencePatches(doc).Add(new XElement(GmlNs + "PolygonPatch",
                LinearRing("exterior", "10.0 10.0 10.0 20.0 20.0 20.0 20.0 10.0 10.0 10.0")));

            var result = IwxxmSigwxParser.Parse(doc.ToString());

            Assert.True(result.IsSuccess, string.Join(" | ", result.Errors));
            Turbulence turbulence = result.Value.Turbulences.Single(t => t.Id == TurbulenceFeatureId);
            Assert.Equal(2, turbulence.Areas.Count);
            Assert.Same(turbulence.Areas[0], turbulence.Area);
            Assert.Equal(new GeoCoordinate(10.0, 10.0), Assert.IsType<GeoPolygon>(turbulence.Areas[1]).ExteriorRing[0]);

            using JsonDocument json = JsonDocument.Parse(IwxxmGeoJsonExporter.ToGeoJson(result.Value));
            JsonElement feature = json.RootElement.GetProperty("features").EnumerateArray()
                .Single(f => f.GetProperty("id").GetString() == TurbulenceFeatureId);
            Assert.Equal("MultiPolygon", feature.GetProperty("geometry").GetProperty("type").GetString());
            Assert.Equal(2, feature.GetProperty("geometry").GetProperty("coordinates").GetArrayLength());
            Assert.Equal(2, feature.GetProperty("properties").GetProperty("patchCount").GetInt32());
        }

        [Fact]
        public void InteriorRings_AreKept()
        {
            XDocument doc = Load("WAFS-Example.xml");
            TurbulencePatches(doc).Element(GmlNs + "PolygonPatch")!
                .Add(LinearRing("interior", "48.0 -80.0 50.0 -80.0 50.0 -70.0 48.0 -70.0 48.0 -80.0"));

            var result = IwxxmSigwxParser.Parse(doc.ToString());

            Assert.True(result.IsSuccess, string.Join(" | ", result.Errors));
            GeoPolygon area = Assert.IsType<GeoPolygon>(result.Value.Turbulences.Single(t => t.Id == TurbulenceFeatureId).Area);
            IReadOnlyList<GeoCoordinate> hole = Assert.Single(area.InteriorRings);
            Assert.Equal(new GeoCoordinate(48.0, -80.0), hole[0]);

            using JsonDocument json = JsonDocument.Parse(IwxxmGeoJsonExporter.ToGeoJson(result.Value));
            JsonElement rings = json.RootElement.GetProperty("features").EnumerateArray()
                .Single(f => f.GetProperty("id").GetString() == TurbulenceFeatureId).GetProperty("geometry").GetProperty("coordinates");
            Assert.Equal(2, rings.GetArrayLength());
            Assert.True(CircleGeometryTests.SignedArea(CircleGeometryTests.Ring(rings[1])) < 0);
        }
    }
}
