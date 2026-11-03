using System;
using System.Globalization;
using Iwxxm.Net.Infrastructure.Mapping;
using Xunit;

namespace Iwxxm.Net.Tests
{
    /// <summary>
    /// Tokenizer behaviour (AUD-L1): correctly rounded numbers (bit-identical to <see cref="double.Parse(string, IFormatProvider)"/>),
    /// XML whitespace only, and exact token counts for a single position.
    /// </summary>
    public sealed class GmlCoordinateTokenizerBehaviourTests
    {
        [Theory]
        [InlineData("0.1")]
        [InlineData("52.123456789012")]
        [InlineData("-179.99999999999")]
        [InlineData("89.9999999")]
        [InlineData("0.30000000000000004")]
        [InlineData("1e2")]
        [InlineData("123456789012345678")]
        [InlineData("-0.0")]
        [InlineData("+45.5")]
        [InlineData("1.")]
        [InlineData(".5")]
        public void ReadNumber_MatchesDoubleParseBitForBit(string token)
        {
            AssertSameAsDoubleParse(token);
        }

        [Fact]
        public void RandomRoundTrips_MatchDoubleParseBitForBit()
        {
            var random = new Random(20261031);
            for (int i = 0; i < 200_000; i++)
            {
                double candidate = (random.NextDouble() * 360.0) - 180.0;
                string token = (i % 3) switch
                {
                    0 => candidate.ToString("R", CultureInfo.InvariantCulture),
                    1 => candidate.ToString("F" + random.Next(0, 13), CultureInfo.InvariantCulture),
                    _ => candidate.ToString("G" + random.Next(1, 18), CultureInfo.InvariantCulture)
                };

                AssertSameAsDoubleParse(token);
            }
        }

        [Fact]
        public void PlainDecimals_WithUpToFifteenDigits_AreCorrectlyRounded()
        {
            var random = new Random(7);
            for (int i = 0; i < 100_000; i++)
            {
                int fractionDigits = random.Next(1, 13);
                long integer = random.Next(0, 181);
                long fraction = (long)(random.NextDouble() * Math.Pow(10, fractionDigits));
                string token = integer.ToString(CultureInfo.InvariantCulture) + "." +
                               fraction.ToString(CultureInfo.InvariantCulture).PadLeft(fractionDigits, '0');
                AssertSameAsDoubleParse(token);
            }
        }

        [Theory]
        [InlineData("10\u00A020")]
        [InlineData("10\u200B20")]
        [InlineData("10\u2028 20")]
        [InlineData("10\u000B20")]
        public void NonXmlWhitespace_IsNotASeparator(string posList)
        {
            Assert.True(GmlCoordinateTokenizer.ParsePosList(posList, 2, null).IsFailure);
        }

        [Fact]
        public void XmlWhitespace_SeparatesTokens()
        {
            var result = GmlCoordinateTokenizer.ParsePosList(" 10\t20\r\n30 \n 40 ", 2, null);

            Assert.True(result.IsSuccess);
            Assert.Equal(2, result.Value.Count);
            Assert.Equal(30, result.Value[1].Lat);
        }

        [Theory]
        [InlineData("10 20 30")]
        [InlineData("10 20 abc")]
        [InlineData("10")]
        public void ParseSinglePoint_RejectsAnythingButTwoNumbers(string pos)
        {
            var result = GmlCoordinateTokenizer.ParseSinglePoint(pos, null);

            Assert.True(result.IsFailure);
            Assert.Equal("GML-INVALID-POINT", Assert.Single(result.Errors).Code);
        }

        [Fact]
        public void ParseSinglePoint_AcceptsTrailingWhitespace()
        {
            var result = GmlCoordinateTokenizer.ParseSinglePoint(" 10 20 \n", null);

            Assert.True(result.IsSuccess);
            Assert.Equal(20, result.Value.Lon);
        }

        private static void AssertSameAsDoubleParse(string token)
        {
            string text = token;
            int index = 0;
            Assert.True(GmlCoordinateTokenizer.TryReadNextDouble(text, ref index, text.Length, out double actual), token);
            double expected = double.Parse(token, NumberStyles.Float, CultureInfo.InvariantCulture);
#if NETFRAMEWORK
            // .NET Framework's double.Parse is not always correctly rounded and drops the sign of "-0.0" (both fixed in
            // .NET Core 3.0): compare zeros by value and allow one ulp otherwise. The tokenizer itself keeps the IEEE sign.
            long expectedBits = BitConverter.DoubleToInt64Bits(expected);
            long actualBits = BitConverter.DoubleToInt64Bits(actual);
            bool sameZero = expected == 0.0 && actual == 0.0;
            Assert.True(
                sameZero || (expectedBits >= 0) == (actualBits >= 0) && Math.Abs(expectedBits - actualBits) <= 1,
                $"'{token}': expected {expected:R}, got {actual:R}");
#else
            Assert.True(
                BitConverter.DoubleToInt64Bits(expected) == BitConverter.DoubleToInt64Bits(actual),
                $"'{token}': expected {expected:R}, got {actual:R}");
#endif
        }
    }
}
