using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Iwxxm.Net.Infrastructure.GeoJson.Writing;
using Iwxxm.Net.Reports;
using Xunit;

namespace Iwxxm.Net.Tests
{
    /// <summary>GeoJSON writer robustness (AUD-L9): round-trip numbers, null-safe string lists, JavaScript-safe escaping, valid holes.</summary>
    public sealed class GeoJsonWriterRobustnessTests
    {
        [Theory]
        [InlineData(0.1)]
        [InlineData(0.30000000000000004)]
        [InlineData(123.45678901234567)]
        [InlineData(-179.99999999999997)]
        [InlineData(1e-300)]
        [InlineData(double.MaxValue)]
        [InlineData(double.MinValue)]
        [InlineData(double.Epsilon)]
        public void Numbers_RoundTripExactly(double value)
        {
            string json = Write(w => w.WriteProperty("v", value));
            string number = json.Substring(json.IndexOf(':') + 1).TrimEnd('}');

            Assert.Equal(value, double.Parse(number, NumberStyles.Float, CultureInfo.InvariantCulture));
        }

        [Fact]
        public void RandomNumbers_RoundTripExactly()
        {
            var random = new Random(42);
            for (int i = 0; i < 20_000; i++)
            {
                double value = (random.NextDouble() - 0.5) * Math.Pow(10, random.Next(-10, 10));
                string json = Write(w => w.WriteProperty("v", value));
                Assert.Equal(value, double.Parse(json.Substring(5, json.Length - 6), NumberStyles.Float, CultureInfo.InvariantCulture));
            }
        }

        [Fact]
        public void StringListWithNull_WritesJsonNull()
        {
            string json = Write(w => w.WriteDynamicProperty("list", new List<string?> { "a", null, "b" }));

            Assert.Equal("{\"list\":[\"a\",null,\"b\"]}", json);
        }

        [Fact]
        public void LineAndParagraphSeparators_AreEscaped()
        {
            string json = Write(w => w.WriteProperty("s", "a\u2028b\u2029c"));

            Assert.Equal("{\"s\":\"a\\u2028b\\u2029c\"}", json);
        }

        [Fact]
        public void DegenerateHole_IsRejected()
        {
            var exterior = new[] { new GeoCoordinate(0, 0), new GeoCoordinate(0, 10), new GeoCoordinate(10, 10), new GeoCoordinate(0, 0) };
            var hole = new[] { new GeoCoordinate(1, 1), new GeoCoordinate(1, 2), new GeoCoordinate(1, 1) };

            Result<GeoPolygon> result = GeoPolygon.Create(exterior, new[] { hole });

            Assert.Equal("GEO-POLYGON-HOLE-DEGENERATE", Assert.Single(result.Errors).Code);
        }

        private static string Write(Action<Rfc8259JsonWriter> write)
        {
            var sb = new StringBuilder();
            var writer = new Rfc8259JsonWriter(sb, indented: false);
            writer.BeginObject();
            write(writer);
            writer.EndObject();
            return sb.ToString();
        }
    }
}
