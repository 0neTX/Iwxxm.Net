using System;
using System.Collections.Generic;
using System.Globalization;
using Iwxxm.Net.Domain.Common;
using Iwxxm.Net.Domain.ValueObjects;

namespace Iwxxm.Net.Infrastructure.Mapping
{
    /// <summary>
    /// High-performance, zero-array-allocation scanner for OGC GML 3.2.1 <c>gml:posList</c> and <c>gml:pos</c>
    /// whitespace-separated coordinate sequences (<c>lat1 lon1 lat2 lon2 ...</c>).
    /// Eliminates intermediate <c>string.Split</c> array and substring allocations on the Gen0 managed heap
    /// while maintaining 100% compatibility with <c>.NET Standard 2.0</c>.
    /// </summary>
    internal static class GmlCoordinateTokenizer
    {
        private static readonly double[] NegativePowersOfTen =
        {
            1.0,
            1e-1, 1e-2, 1e-3, 1e-4, 1e-5, 1e-6, 1e-7, 1e-8, 1e-9,
            1e-10, 1e-11, 1e-12, 1e-13, 1e-14, 1e-15
        };

        /// <summary>
        /// Parses a whitespace-delimited GML coordinate list into a list of <see cref="GeoCoordinate"/> instances
        /// without allocating intermediate <c>string[]</c> arrays.
        /// </summary>
        public static Result<IReadOnlyList<GeoCoordinate>> ParsePosList(
            string? posListText,
            int srsDimension,
            string? contextLocation)
        {
            if (string.IsNullOrWhiteSpace(posListText))
            {
                return Result<IReadOnlyList<GeoCoordinate>>.Failure(
                    "GML-EMPTY-POSLIST",
                    "The GML posList or pos element contains no coordinate values.",
                    ErrorCategory.DomainMapping,
                    contextLocation);
            }

            if (srsDimension != 2 && srsDimension != 3)
            {
                return Result<IReadOnlyList<GeoCoordinate>>.Failure(
                    "GML-UNSUPPORTED-SRSDIM",
                    $"Unsupported srsDimension '{srsDimension}'. Expected 2 (lat/lon) or 3 (lat/lon/alt).",
                    ErrorCategory.DomainMapping,
                    contextLocation);
            }

            string text = posListText!;
            int estimatedPairs = Math.Max(4, text.Length / 14);
            var coordinates = new List<GeoCoordinate>(estimatedPairs);

            int index = 0;
            int length = text.Length;
            int tokenIndex = 0;

            while (SkipWhitespace(text, ref index, length))
            {
                if (!TryReadNextDouble(text, ref index, length, out double lat))
                {
                    return Result<IReadOnlyList<GeoCoordinate>>.Failure(
                        "GML-INVALID-COORD-NUMBER",
                        $"Invalid numeric latitude token at index {tokenIndex} in GML coordinate sequence.",
                        ErrorCategory.DomainMapping,
                        contextLocation);
                }
                tokenIndex++;

                if (!SkipWhitespace(text, ref index, length))
                {
                    return Result<IReadOnlyList<GeoCoordinate>>.Failure(
                        "GML-UNBALANCED-POSLIST",
                        $"The GML coordinate sequence has {tokenIndex} numbers, which is not a multiple of srsDimension ({srsDimension}).",
                        ErrorCategory.DomainMapping,
                        contextLocation);
                }

                if (!TryReadNextDouble(text, ref index, length, out double lon))
                {
                    return Result<IReadOnlyList<GeoCoordinate>>.Failure(
                        "GML-INVALID-COORD-NUMBER",
                        $"Invalid numeric longitude token at index {tokenIndex} in GML coordinate sequence.",
                        ErrorCategory.DomainMapping,
                        contextLocation);
                }
                tokenIndex++;

                if (srsDimension == 3)
                {
                    if (!SkipWhitespace(text, ref index, length))
                    {
                        return Result<IReadOnlyList<GeoCoordinate>>.Failure(
                            "GML-UNBALANCED-POSLIST",
                            $"The GML coordinate sequence has {tokenIndex} numbers, which is not a multiple of srsDimension ({srsDimension}).",
                            ErrorCategory.DomainMapping,
                            contextLocation);
                    }

                    if (!TryReadNextDouble(text, ref index, length, out _))
                    {
                        return Result<IReadOnlyList<GeoCoordinate>>.Failure(
                            "GML-INVALID-COORD-NUMBER",
                            $"Invalid numeric elevation token at index {tokenIndex} in GML coordinate sequence.",
                            ErrorCategory.DomainMapping,
                            contextLocation);
                    }
                    tokenIndex++;
                }

                Result<GeoCoordinate> coordResult = GeoCoordinate.Create(lat, lon, contextLocation);
                if (coordResult.IsFailure)
                {
                    return Result<IReadOnlyList<GeoCoordinate>>.Failure(coordResult.Errors);
                }

                coordinates.Add(coordResult.Value);
            }

            if (coordinates.Count == 0)
            {
                return Result<IReadOnlyList<GeoCoordinate>>.Failure(
                    "GML-EMPTY-POSLIST",
                    "The GML posList or pos element contains no coordinate values.",
                    ErrorCategory.DomainMapping,
                    contextLocation);
            }

            return Result<IReadOnlyList<GeoCoordinate>>.Success(coordinates);
        }

        /// <summary>
        /// Parses a single <c>gml:pos</c> string (<c>lat lon</c>) into a <see cref="GeoCoordinate"/> without array allocation.
        /// </summary>
        public static Result<GeoCoordinate> ParseSinglePoint(string? posText, string? contextLocation)
        {
            if (string.IsNullOrWhiteSpace(posText))
            {
                return Result<GeoCoordinate>.Failure(
                    "GML-EMPTY-POS",
                    "The GML pos element is empty.",
                    ErrorCategory.DomainMapping,
                    contextLocation);
            }

            string text = posText!;
            int index = 0;
            int length = text.Length;

            if (!SkipWhitespace(text, ref index, length) ||
                !TryReadNextDouble(text, ref index, length, out double lat) ||
                !SkipWhitespace(text, ref index, length) ||
                !TryReadNextDouble(text, ref index, length, out double lon))
            {
                return Result<GeoCoordinate>.Failure(
                    "GML-INVALID-POINT",
                    $"Invalid GML Point coordinate string '{posText}'. Expected 'lat lon'.",
                    ErrorCategory.DomainMapping,
                    contextLocation);
            }

            return GeoCoordinate.Create(lat, lon, contextLocation);
        }

        private static bool SkipWhitespace(string text, ref int index, int length)
        {
            while (index < length && char.IsWhiteSpace(text[index]))
            {
                index++;
            }
            return index < length;
        }

        /// <summary>
        /// Reads the next whitespace-delimited IEEE-754 double from <paramref name="text"/> starting at <paramref name="index"/>.
        /// Uses zero-allocation character arithmetic for standard decimal numbers (<c>[+-]?\d+(\.\d+)?</c>)
        /// and falls back to <see cref="double.TryParse(string, NumberStyles, IFormatProvider, out double)"/> only for scientific notation.
        /// </summary>
        internal static bool TryReadNextDouble(string text, ref int index, int length, out double value)
        {
            value = 0.0;
            if (!SkipWhitespace(text, ref index, length))
            {
                return false;
            }

            int tokenStart = index;
            bool isNegative = false;
            char c = text[index];
            if (c == '-')
            {
                isNegative = true;
                index++;
            }
            else if (c == '+')
            {
                index++;
            }

            long integerPart = 0;
            int intDigits = 0;
            while (index < length)
            {
                c = text[index];
                if (c >= '0' && c <= '9')
                {
                    integerPart = (integerPart * 10L) + (c - '0');
                    intDigits++;
                    index++;
                }
                else
                {
                    break;
                }
            }

            long fractionPart = 0;
            int fractionDigits = 0;
            if (index < length && text[index] == '.')
            {
                index++;
                while (index < length)
                {
                    c = text[index];
                    if (c >= '0' && c <= '9')
                    {
                        if (fractionDigits < 15)
                        {
                            fractionPart = (fractionPart * 10L) + (c - '0');
                            fractionDigits++;
                        }
                        index++;
                    }
                    else
                    {
                        break;
                    }
                }
            }

            // If scientific notation or oversized integer part is encountered, advance to token end and use invariant TryParse
            if ((index < length && !char.IsWhiteSpace(text[index])) || intDigits > 15 || (intDigits == 0 && fractionDigits == 0))
            {
                while (index < length && !char.IsWhiteSpace(text[index]))
                {
                    index++;
                }
#if NETSTANDARD2_1_OR_GREATER
                return double.TryParse(
                    text.AsSpan(tokenStart, index - tokenStart),
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out value);
#else
                string fallbackToken = text.Substring(tokenStart, index - tokenStart);
                return double.TryParse(fallbackToken, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
#endif
            }

            double result = integerPart;
            if (fractionDigits > 0)
            {
                result += fractionPart * NegativePowersOfTen[fractionDigits];
            }

            value = isNegative ? -result : result;
            return true;
        }
    }
}
