using System;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using Iwxxm.Net.Infrastructure.GeoJson.Writing;
using Xunit;
using static Iwxxm.Net.Tests.DataFidelity.SampleDocuments;

namespace Iwxxm.Net.Tests.DataFidelity
{
    /// <summary>
    /// AUD-M3: non-finite numbers (<c>NaN</c>, <c>INF</c>, <c>-INF</c>, <c>Infinity</c>) are rejected at the mapping boundary
    /// for every numeric measure, and the JSON writer never emits a bare NaN/Infinity token.
    /// </summary>
    public sealed class NonFiniteNumberTests
    {
        [Theory]
        [InlineData("airTemperature", "NaN")]
        [InlineData("dewpointTemperature", "INF")]
        [InlineData("qnh", "-INF")]
        [InlineData("meanWindSpeed", "Infinity")]
        [InlineData("meanWindDirection", "NaN")]
        [InlineData("prevailingVisibility", "-Infinity")]
        [InlineData("meanRVR", "NaN")]
        public void MetarMeasure_NonFinite_IsAMappingError(string element, string value)
        {
            XDocument doc = Load("metar-A3-1.xml");
            XElement measure = doc.Descendants(IwxxmNs + element).First();
            measure.Value = value;

            var result = MapDirectly(doc);

            Assert.True(result.IsFailure, $"{element}={value} must not be accepted.");
            Assert.Contains(result.Errors, e => e.Code == "ACL-MEASURE-VALUE-INVALID");
        }

        [Theory]
        [InlineData("NaN")]
        [InlineData("INF")]
        public void SigmetVerticalLimit_NonFinite_IsAMappingError(string value)
        {
            XDocument doc = Load("sigmet-A6-1a-TS.xml");
            doc.Descendants(AixmNs + "upperLimit").First().Value = value;

            var result = MapDirectly(doc);

            Assert.True(result.IsFailure);
            Assert.Contains(result.Errors, e => e.Code == "ACL-MEASURE-VALUE-INVALID");
        }

        [Theory]
        [InlineData("NaN")]
        [InlineData("Infinity")]
        [InlineData("1e999")]
        public void Coordinate_NonFinite_IsAMappingError(string token)
        {
            XDocument doc = Load("sigmet-A6-1a-TS.xml");
            XElement posList = doc.Descendants(GmlNs + "posList").First();
            posList.Value = posList.Value.Trim().Replace("54.0 -12.0 50.0", token + " -12.0 50.0");

            var result = MapDirectly(doc);

            Assert.True(result.IsFailure);
            Assert.Contains(result.Errors, e => e.Code == "GML-INVALID-COORD-NUMBER");
        }

        [Fact]
        public void Writer_NonFiniteProperty_IsWrittenAsNull()
        {
            var sb = new StringBuilder();
            var writer = new Rfc8259JsonWriter(sb, indented: false);
            writer.BeginObject();
            writer.WriteProperty("a", double.NaN);
            writer.WriteProperty("b", double.PositiveInfinity);
            writer.WriteDynamicProperty("c", new[] { 1.0, double.NegativeInfinity });
            writer.EndObject();

            Assert.Equal("{\"a\":null,\"b\":null,\"c\":[1,null]}", sb.ToString());
            Assert.DoesNotContain("NaN", sb.ToString());
            Assert.DoesNotContain("Infinity", sb.ToString());
        }

        [Fact]
        public void Writer_NonFinitePosition_IsAnInternalErrorNotABareToken()
        {
            var writer = new Rfc8259JsonWriter(new StringBuilder(), indented: false);

            Assert.Throws<InvalidOperationException>(() => writer.WritePosition(new GeoJsonPosition(double.NaN, 0.0)));
            Assert.Throws<InvalidOperationException>(() => writer.WriteInlineNumber(double.PositiveInfinity));
        }
    }
}
