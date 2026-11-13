using System.Linq;
using Iwxxm.Net.Reports;
using Xunit;

namespace Iwxxm.Net.Tests.DataFidelity
{
    /// <summary>
    /// Every nil reason of the Domain is a <c>CodeListValue&lt;NilReason&gt;</c> of the WMO register
    /// <c>http://codes.wmo.int/common/nil</c>: a known code maps to its member, a reason that is not in the register is
    /// kept (<c>Unknown</c> + raw value) and the GeoJSON text of the reason is the one that was reported.
    /// </summary>
    public sealed class NilReasonTypingTests
    {
        private const string Missing = "http://codes.wmo.int/common/nil/missing";
        private const string Foreign = "http://example.org/nil/lost";

        [Fact]
        public void TafBaseForecast_KnownReason_IsTranslated()
        {
            Taf taf = ParseTaf(SampleDocuments.Text("taf-NIL-collect.xml"));

            Assert.True(taf.IsNilForecast);
            Assert.Equal(NilReason.Missing, taf.BaseForecastNilReason!.Code);
            Assert.Equal("missing", taf.BaseForecastNilReason.Notation);
        }

        [Fact]
        public void TafBaseForecast_UnknownReason_IsKeptVerbatim()
        {
            Taf taf = ParseTaf(SampleDocuments.Text("taf-NIL-collect.xml").Replace(Missing, Foreign));

            Assert.True(taf.IsNilForecast);
            Assert.Equal(NilReason.Unknown, taf.BaseForecastNilReason!.Code);
            Assert.Equal(Foreign, taf.BaseForecastNilReason.Notation);
        }

        [Fact]
        public void MetarObservation_KnownReason_IsTranslated()
        {
            Metar metar = ParseMetar(SampleDocuments.Text("metar-NIL-collect.xml"));

            Assert.True(metar.IsNilObservation);
            Assert.Equal(NilReason.Missing, metar.ObservationNilReason!.Code);
        }

        [Fact]
        public void MetarObservation_UnknownReason_IsKeptVerbatim()
        {
            Metar metar = ParseMetar(SampleDocuments.Text("metar-NIL-collect.xml").Replace(Missing, Foreign));

            Assert.True(metar.IsNilObservation);
            Assert.Equal(NilReason.Unknown, metar.ObservationNilReason!.Code);
            Assert.Equal(Foreign, metar.ObservationNilReason.Notation);
        }

        [Fact]
        public void SpaceWeatherAnalysis_KnownReason_IsTranslated_AndGeoJsonIsUnchanged()
        {
            string xml = SampleDocuments.Text("spacewx-A7-5.xml");
            SpaceWx swx = Assert.IsType<SpaceWx>(Parse(xml));

            SpaceWeatherAnalysis nil = swx.Analyses.First(a => a.Regions.Count == 0);
            Assert.True(nil.IsNothingOfOperationalSignificance);
            Assert.Equal(NilReason.NothingOfOperationalSignificance, nil.NilReason!.Code);

            Assert.Contains(
                "\"nilReason\":\"http://codes.wmo.int/common/nil/nothingOfOperationalSignificance\"",
                IwxxmParser.ParseToGeoJson(xml).Value);
        }

        [Fact]
        public void SpaceWeatherAnalysis_UnknownReason_IsKept_AndGeoJsonKeepsTheRawValue()
        {
            string xml = SampleDocuments.Text("spacewx-A7-5.xml")
                .Replace("http://codes.wmo.int/common/nil/nothingOfOperationalSignificance", Foreign);
            SpaceWx swx = Assert.IsType<SpaceWx>(Parse(xml));

            SpaceWeatherAnalysis nil = swx.Analyses.First(a => a.Regions.Count == 0);
            Assert.False(nil.IsNothingOfOperationalSignificance);
            Assert.Equal(NilReason.Unknown, nil.NilReason!.Code);
            Assert.Equal(Foreign, nil.NilReason.Notation);
            Assert.Contains("\"nilReason\":\"" + Foreign + "\"", IwxxmParser.ParseToGeoJson(xml).Value);
        }

        [Fact]
        public void VerticalLimit_OfficialExample_KeepsForeignUri_AndGeoJsonIsUnchanged()
        {
            const string sigwx = "Samples/wmo-examples/WAFS-Example.xml";
            var result = IwxxmSigwxParser.ParseFile(sigwx);
            Assert.True(result.IsSuccess, string.Join(" | ", result.Errors));

            VerticalLimit nilBase = result.Value.Clouds[0].Base!.Value;
            Assert.Equal(NilReason.Unknown, nilBase.NilReason!.Code);
            Assert.Equal("http://codes.wmo.int/iwxxm/nil/inapplicable", nilBase.NilReason.Notation);
            Assert.Equal("NIL(http://codes.wmo.int/iwxxm/nil/inapplicable)", nilBase.ToString());

            string geoJson = IwxxmParser.ParseFileToGeoJson(sigwx).Value;
            Assert.Contains("\"baseNilReason\":\"http://codes.wmo.int/iwxxm/nil/inapplicable\"", geoJson);
        }

        [Fact]
        public void VerticalLimit_RegisterReason_MapsToTheCode_AndEqualityHolds()
        {
            var inapplicable = new CodeListValue<NilReason>(NilReason.Inapplicable, "inapplicable");
            var same = new CodeListValue<NilReason>(NilReason.Inapplicable, "inapplicable");

            VerticalLimit a = VerticalLimit.Nil(inapplicable);
            VerticalLimit b = VerticalLimit.Nil(same);

            Assert.Equal(a, b);
            Assert.Equal(a.GetHashCode(), b.GetHashCode());
            Assert.NotEqual(a, VerticalLimit.Nil(null));
            Assert.Equal("NIL(inapplicable)", a.ToString());
            Assert.Equal("NIL", VerticalLimit.Nil(null).ToString());
        }

        private static IwxxmDocument Parse(string xml)
        {
            Result<IwxxmDocument> result = IwxxmParser.Parse(xml);
            Assert.True(result.IsSuccess, string.Join(" | ", result.Errors));
            return result.Value;
        }

        private static Taf ParseTaf(string xml) => Assert.IsType<Taf>(Assert.Single(ParseBulletin(xml)));

        private static Metar ParseMetar(string xml) => Assert.IsType<Metar>(Assert.Single(ParseBulletin(xml)));

        private static IwxxmReport[] ParseBulletin(string xml)
        {
            var result = IwxxmParser.ParseBulletin(xml);
            Assert.True(result.IsSuccess, string.Join(" | ", result.Errors));
            return result.Value.Reports.ToArray();
        }
    }
}
