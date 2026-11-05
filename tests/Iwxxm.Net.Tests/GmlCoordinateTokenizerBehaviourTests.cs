using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Iwxxm.Net.Infrastructure.Mapping;
using Xunit;

namespace Iwxxm.Net.Tests
{
    /// <summary>
    /// Tokenizer behaviour (AUD-L1): correctly rounded numbers (exact IEEE bit patterns on every runtime, including the sign of
    /// zero; on .NET Framework the tokens that fall back to the runtime parser get a one-ulp tolerance), XML whitespace only, and exact token counts for a single position.
    /// </summary>
    public sealed class GmlCoordinateTokenizerBehaviourTests
    {
        /// <summary>
        /// Tokens and the IEEE-754 bit pattern of their correctly rounded value. The table was generated with an independent
        /// correctly rounded conversion (.NET 10 <c>double.Parse</c> agrees, which <see cref="TheTable_AgreesWithDoubleParse_WhereTheRuntimeIsCorrectlyRounded"/>
        /// re-checks) and is asserted bit for bit on every runtime, so .NET Framework (whose <c>double.Parse</c> is not always
        /// correctly rounded and drops the sign of negative zero) is held to the same exact expectations.
        /// </summary>
        public static readonly object[][] KnownBitPatterns =
        {
            new object[] { "0", 0x0000000000000000UL },
            new object[] { "0.0", 0x0000000000000000UL },
            new object[] { "-0", 0x8000000000000000UL },
            new object[] { "-0.0", 0x8000000000000000UL },
            new object[] { "+0", 0x0000000000000000UL },
            new object[] { "+0.0", 0x0000000000000000UL },
            new object[] { "0e0", 0x0000000000000000UL },
            new object[] { "-0e0", 0x8000000000000000UL },
            new object[] { "1", 0x3FF0000000000000UL },
            new object[] { "-1", 0xBFF0000000000000UL },
            new object[] { "+1", 0x3FF0000000000000UL },
            new object[] { "7", 0x401C000000000000UL },
            new object[] { "45", 0x4046800000000000UL },
            new object[] { "-90", 0xC056800000000000UL },
            new object[] { "180", 0x4066800000000000UL },
            new object[] { "-180.0", 0xC066800000000000UL },
            new object[] { "0.1", 0x3FB999999999999AUL },
            new object[] { "-0.1", 0xBFB999999999999AUL },
            new object[] { "0.5", 0x3FE0000000000000UL },
            new object[] { ".5", 0x3FE0000000000000UL },
            new object[] { "-.5", 0xBFE0000000000000UL },
            new object[] { "1.", 0x3FF0000000000000UL },
            new object[] { "52.1", 0x404A0CCCCCCCCCCDUL },
            new object[] { "52.12", 0x404A0F5C28F5C28FUL },
            new object[] { "52.123", 0x404A0FBE76C8B439UL },
            new object[] { "52.1234", 0x404A0FCB923A29C7UL },
            new object[] { "52.12345", 0x404A0FCD35A85879UL },
            new object[] { "52.123456", 0x404A0FCD67FD3F5BUL },
            new object[] { "52.1234567", 0x404A0FCD6DDC7C6DUL },
            new object[] { "52.12345678", 0x404A0FCD6E8848E4UL },
            new object[] { "52.123456789", 0x404A0FCD6E9B9CB2UL },
            new object[] { "52.1234567890", 0x404A0FCD6E9B9CB2UL },
            new object[] { "52.12345678901", 0x404A0FCD6E9BA231UL },
            new object[] { "52.123456789012", 0x404A0FCD6E9BA34BUL },
            new object[] { "52.1234567890123", 0x404A0FCD6E9BA375UL },
            new object[] { "52.12345678901234", 0x404A0FCD6E9BA37AUL },
            new object[] { "52.123456789012345", 0x404A0FCD6E9BA37BUL },
            new object[] { "52.1234567890123456", 0x404A0FCD6E9BA37BUL },
            new object[] { "52.12345678901234567", 0x404A0FCD6E9BA37BUL },
            new object[] { "-179.99999999999", 0xC0667FFFFFFFFEA0UL },
            new object[] { "-179.999999999999999", 0xC066800000000000UL },
            new object[] { "89.9999999", 0x40567FFFFF94A036UL },
            new object[] { "0.30000000000000004", 0x3FD3333333333334UL },
            new object[] { "0.1000000000000000055511151231257827", 0x3FB999999999999AUL },
            new object[] { "123456789012345678", 0x437B69B4BA630F35UL },
            new object[] { "12345678901234567", 0x4345EE2A2EB5A5C4UL },
            new object[] { "1234567890123456", 0x43118B54F22AEB00UL },
            new object[] { "9007199254740993", 0x4340000000000000UL },
            new object[] { "9007199254740992", 0x4340000000000000UL },
            new object[] { "9007199254740991", 0x433FFFFFFFFFFFFFUL },
            new object[] { "0.000001", 0x3EB0C6F7A0B5ED8DUL },
            new object[] { "0.0000001234567", 0x3E8091F09E0EE5E7UL },
            new object[] { "1e2", 0x4059000000000000UL },
            new object[] { "1E2", 0x4059000000000000UL },
            new object[] { "1e+2", 0x4059000000000000UL },
            new object[] { "1e-2", 0x3F847AE147AE147BUL },
            new object[] { "1.5e1", 0x402E000000000000UL },
            new object[] { "-1.5E-1", 0xBFC3333333333333UL },
            new object[] { "2.5e-5", 0x3EFA36E2EB1C432DUL },
            new object[] { "1e22", 0x4480F0CF064DD592UL },
            new object[] { "1e23", 0x44B52D02C7E14AF6UL },
            new object[] { "1.7976931348623157e308", 0x7FEFFFFFFFFFFFFFUL },
            new object[] { "2.2250738585072014e-308", 0x0010000000000000UL },
            new object[] { "2.2250738585072011e-308", 0x000FFFFFFFFFFFFFUL },
            new object[] { "4.9e-324", 0x0000000000000001UL },
            new object[] { "5e-324", 0x0000000000000001UL },
            new object[] { "2e-323", 0x0000000000000004UL },
            new object[] { "1e-310", 0x000012688B70E62BUL },
            new object[] { "123456789e-30", 0x3B62A800D15B2FE8UL },
            new object[] { "0.00000000000000000001", 0x3BC79CA10C924223UL },
            new object[] { "100000000000000000000.5", 0x4415AF1D78B58C40UL },
            new object[] { "8.5e-1", 0x3FEB333333333333UL },
            new object[] { "359.99999999999994", 0x40767FFFFFFFFFFFUL },
            new object[] { "-0.00000000000000001", 0xBC670EF54646D497UL },
            new object[] { "3.141592653589793", 0x400921FB54442D18UL },
            new object[] { "2.718281828459045", 0x4005BF0A8B145769UL },
            new object[] { "6.02214076e23", 0x44DFE185CA57C517UL },
        };

        public static IEnumerable<object[]> KnownTokens() => KnownBitPatterns;

        [Theory]
        [MemberData(nameof(KnownTokens))]
        public void ReadNumber_ProducesTheExactIeeeBitPattern(string token, ulong expectedBits)
        {
            double actual = Read(token);
            long expected = unchecked((long)expectedBits);
            long actualBits = BitConverter.DoubleToInt64Bits(actual);
            string message = $"'{token}': expected 0x{expectedBits:X16}, got 0x{unchecked((ulong)actualBits):X16} ({actual:R})";

#if NETFRAMEWORK
            if (UsesTheRuntimeParser(token))
            {
                // Tokens outside the tokenizer's own fast path (exponent notation, more than 15 digits) go to the runtime's
                // double.TryParse, which on .NET Framework is not always correctly rounded and drops the sign of zero
                // (fixed in .NET Core 3.0): zeros compare by value and everything else may differ by one ulp.
                bool zeros = (expected & long.MaxValue) == 0 && actual == 0.0;
                bool oneUlp = ((expected >= 0) == (actualBits >= 0)) && (Math.Abs(expected - actualBits) <= 1);
                Assert.True(zeros || oneUlp, message);
                return;
            }
#endif
            Assert.True(expected == actualBits, message);
        }

        // The tokenizer's own arithmetic handles plain decimals of at most 15 digits; the rest is the runtime parser's job.
        private static bool UsesTheRuntimeParser(string token) =>
            token.IndexOfAny(new[] { 'e', 'E' }) >= 0 || token.Count(char.IsDigit) > 15;

        [Theory]
        [InlineData("0", false)]
        [InlineData("0.0", false)]
        [InlineData("+0", false)]
        [InlineData("+0.0", false)]
        [InlineData("-0", true)]
        [InlineData("-0.0", true)]
        [InlineData("-0.000000", true)]
        public void Zeros_KeepTheSignOfTheTokenText(string token, bool negative)
        {
            double actual = Read(token);

            Assert.Equal(0.0, actual);
            Assert.Equal(negative, BitConverter.DoubleToInt64Bits(actual) < 0);
        }

#if !NETFRAMEWORK
        [Fact]
        public void NegativeZeroInExponentNotation_KeepsItsSign_WhereTheRuntimeParserDoes()
        {
            Assert.True(BitConverter.DoubleToInt64Bits(Read("-0e0")) < 0);
        }
#endif

        [Fact]
        public void TheTable_AgreesWithDoubleParse_WhereTheRuntimeIsCorrectlyRounded()
        {
#if NETFRAMEWORK
            // .NET Framework's double.Parse is the very thing the table replaces as the oracle here.
            Assert.NotEmpty(KnownBitPatterns);
#else
            foreach (object[] row in KnownBitPatterns)
            {
                var token = (string)row[0];
                double expected = double.Parse(token, NumberStyles.Float, CultureInfo.InvariantCulture);
                Assert.True(BitConverter.DoubleToInt64Bits(expected) == unchecked((long)(ulong)row[1]), token);
            }
#endif
        }

        [Fact]
        public void RandomDecimals_UpToFifteenDigits_AreCorrectlyRounded()
        {
            // Oracle valid on every runtime (Clinger's fast path): a mantissa below 2^53 and a power of ten up to 1e22 are both
            // exact doubles, and one IEEE division of them is correctly rounded.
            var random = new Random(20261031);
            for (int i = 0; i < 200_000; i++)
            {
                int digits = random.Next(1, 16);
                int fractionDigits = random.Next(0, Math.Min(digits, 12) + 1);
                long mantissa = (long)(random.NextDouble() * ExactPowersOfTen[digits]);
                bool negative = random.Next(0, 2) == 1;

                string text = mantissa.ToString(CultureInfo.InvariantCulture).PadLeft(fractionDigits + 1, '0');
                string token = (negative ? "-" : string.Empty) + text.Substring(0, text.Length - fractionDigits) +
                               (fractionDigits > 0 ? "." + text.Substring(text.Length - fractionDigits) : string.Empty);
                double expected = (double)mantissa / ExactPowersOfTen[fractionDigits];
                if (negative)
                    expected = -expected;

                double actual = Read(token);
                Assert.True(
                    BitConverter.DoubleToInt64Bits(expected) == BitConverter.DoubleToInt64Bits(actual),
                    $"'{token}': expected {expected:R}, got {actual:R}");
            }
        }

        private static readonly double[] ExactPowersOfTen =
        {
            1e0, 1e1, 1e2, 1e3, 1e4, 1e5, 1e6, 1e7, 1e8, 1e9, 1e10, 1e11, 1e12, 1e13, 1e14, 1e15, 1e16, 1e17, 1e18, 1e19, 1e20, 1e21, 1e22
        };

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

        private static double Read(string token)
        {
            int index = 0;
            Assert.True(GmlCoordinateTokenizer.TryReadNextDouble(token, ref index, token.Length, out double value), token);
            Assert.Equal(token.Length, index);
            return value;
        }
    }
}
