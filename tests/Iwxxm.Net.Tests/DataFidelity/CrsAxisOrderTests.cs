using System.Globalization;
using System.Linq;
using System.Xml.Linq;
using Iwxxm.Net.Reports;
using Xunit;
using static Iwxxm.Net.Tests.DataFidelity.SampleDocuments;

namespace Iwxxm.Net.Tests.DataFidelity
{
    /// <summary>
    /// AUD-M10: <c>@srsName</c>, <c>@axisLabels</c> and <c>@srsDimension</c> are read: "Lat Long" (IWXXM default) and
    /// "Long Lat" (swapped) axis orders, 3-D positions (height ignored), and a mapping error for an unknown CRS or axis
    /// labels instead of a guessed axis order. Fixtures are synthetic variants of the official sigmet-A6-1a-TS.xml.
    /// </summary>
    public sealed class CrsAxisOrderTests
    {
        private static GeoPolygon OfficialArea()
        {
            var result = IwxxmParser.ParseFileAs<Sigmet>(PathOf("sigmet-A6-1a-TS.xml"));
            Assert.True(result.IsSuccess, string.Join(" | ", result.Errors));
            return Assert.IsType<GeoPolygon>(result.Value.EvolvingConditions[0].Area);
        }

        private static (XDocument Doc, XElement Surface, XElement PosList) Variant()
        {
            XDocument doc = Load("sigmet-A6-1a-TS.xml");
            XElement surface = doc.Descendants(AixmNs + "Surface").First();
            return (doc, surface, surface.Descendants(GmlNs + "posList").Single());
        }

        private static double[] Numbers(XElement posList) =>
            posList.Value.Split((char[]?)null, System.StringSplitOptions.RemoveEmptyEntries)
                .Select(t => double.Parse(t, CultureInfo.InvariantCulture)).ToArray();

        [Fact]
        public void LongLatAxisOrder_IsSwappedIntoTheSameDomainCoordinates()
        {
            var (doc, surface, posList) = Variant();
            double[] n = Numbers(posList);
            surface.SetAttributeValue("axisLabels", "Long Lat");
            posList.Value = string.Join(" ", Enumerable.Range(0, n.Length / 2)
                .SelectMany(i => new[] { n[(2 * i) + 1], n[2 * i] })
                .Select(v => v.ToString(CultureInfo.InvariantCulture)));

            var result = MapDirectly(doc);

            Assert.True(result.IsSuccess, string.Join(" | ", result.Errors));
            Assert.Equal(OfficialArea(), Assert.IsType<Sigmet>(result.Value).EvolvingConditions[0].Area);
        }

        [Fact]
        public void ThreeDimensionalPositions_KeepTheHorizontalCoordinates_AndIgnoreTheHeight()
        {
            var (doc, surface, posList) = Variant();
            double[] n = Numbers(posList);
            surface.SetAttributeValue("srsDimension", "3");
            surface.SetAttributeValue("axisLabels", "Lat Long h");
            surface.SetAttributeValue("srsName", "http://www.opengis.net/def/crs/EPSG/0/4979");
            posList.Value = string.Join(" ", Enumerable.Range(0, n.Length / 2)
                .SelectMany(i => new[] { n[2 * i], n[(2 * i) + 1], 1000.0 })
                .Select(v => v.ToString(CultureInfo.InvariantCulture)));

            var result = MapDirectly(doc);

            Assert.True(result.IsSuccess, string.Join(" | ", result.Errors));
            Assert.Equal(OfficialArea(), Assert.IsType<Sigmet>(result.Value).EvolvingConditions[0].Area);
        }

        [Fact]
        public void Crs84WithoutAxisLabels_IsLongitudeFirst()
        {
            var (doc, surface, posList) = Variant();
            double[] n = Numbers(posList);
            surface.SetAttributeValue("axisLabels", null);
            surface.SetAttributeValue("srsName", "http://www.opengis.net/def/crs/OGC/1.3/CRS84");
            posList.Value = string.Join(" ", Enumerable.Range(0, n.Length / 2)
                .SelectMany(i => new[] { n[(2 * i) + 1], n[2 * i] })
                .Select(v => v.ToString(CultureInfo.InvariantCulture)));

            var result = MapDirectly(doc);

            Assert.True(result.IsSuccess, string.Join(" | ", result.Errors));
            Assert.Equal(OfficialArea(), Assert.IsType<Sigmet>(result.Value).EvolvingConditions[0].Area);
        }

        [Theory]
        [InlineData("srsName", "http://www.opengis.net/def/crs/EPSG/0/3857", "ACL-CRS-UNSUPPORTED")]
        [InlineData("axisLabels", "x y", "ACL-CRS-AXIS-LABELS-UNSUPPORTED")]
        [InlineData("axisLabels", "Lat Long h", "ACL-CRS-AXIS-LABELS-UNSUPPORTED")]
        [InlineData("srsDimension", "4", "ACL-CRS-DIMENSION-UNSUPPORTED")]
        public void UnknownCrsMetadata_IsAMappingError(string attribute, string value, string expectedCode)
        {
            var (doc, surface, _) = Variant();
            surface.SetAttributeValue(attribute, value);

            var result = MapDirectly(doc);

            Assert.True(result.IsFailure);
            Assert.Contains(result.Errors, e => e.Code == expectedCode);
        }

        [Fact]
        public void Sigwx_LongLatElevatedVolume_IsSwapped()
        {
            XDocument doc = Load("WAFS-Example.xml");
            XElement volume = doc.Descendants(IwxxmNs + "ElevatedVolume")
                .First(v => (string?)v.Attribute(GmlNs + "id") == "uuid.138a9477-e1fd-43e2-8060-0eb6a093d0eb");
            foreach (XElement e in volume.DescendantsAndSelf().Where(e => e.Attribute("axisLabels") != null))
                e.SetAttributeValue("axisLabels", "Long Lat");
            XElement posList = volume.Descendants(GmlNs + "posList").Single();
            double[] n = Numbers(posList);
            posList.Value = string.Join(" ", Enumerable.Range(0, n.Length / 2)
                .SelectMany(i => new[] { n[(2 * i) + 1], n[2 * i] })
                .Select(v => v.ToString(CultureInfo.InvariantCulture)));

            var swapped = IwxxmSigwxParser.Parse(doc.ToString());
            var official = IwxxmSigwxParser.ParseFile(PathOf("WAFS-Example.xml"));

            Assert.True(swapped.IsSuccess, string.Join(" | ", swapped.Errors));
            Assert.Equal(official.Value.Turbulences.Select(t => t.Area), swapped.Value.Turbulences.Select(t => t.Area));
        }
    }
}
