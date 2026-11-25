using System;
using System.Collections.Generic;
using System.Linq;
using Iwxxm.Net.Export.GeoPackage.Model;
using Iwxxm.Net.Reports;
using Xunit;
using static Iwxxm.Net.Export.GeoPackage.Tests.GeoPackageGeometryTests;

namespace Iwxxm.Net.Export.GeoPackage.Tests
{
    /// <summary>
    /// The StandardGeoPackageBinary blobs: hand-written byte vectors, a decoder written for the tests (independent of the
    /// encoder) that reads back every geometry of the samples, and the rejection of invalid input.
    /// </summary>
    public sealed class GeoPackageGeometryEncoderTests
    {
        public static IEnumerable<object[]> Samples() => SampleFiles.All();

        public static IEnumerable<object[]> ExtraSamples() => SampleFiles.Extra();

        /// <summary>A list that reports a huge count without holding any element.</summary>
        private sealed class Phantom<T> : IReadOnlyList<T>
        {
            public Phantom(int count) => Count = count;

            public int Count { get; }

            public T this[int index] => throw new NotSupportedException();

            public IEnumerator<T> GetEnumerator() => throw new NotSupportedException();

            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
        }

        [Fact]
        public void WkbLength_IsComputedIn64Bits_SoAHugeGeometryDoesNotWrapAround()
        {
            // 200 million positions of 16 bytes are 3.2 GB: more than an int can count, and the int sum would have wrapped.
            GeoPackageGeometry huge = GeoPackageGeometry.ForMultiLineString(new IReadOnlyList<GeoPackagePosition>[] { new Phantom<GeoPackagePosition>(200_000_000) });

            long length = GeoPackageGeometryEncoder.WkbLength(huge);

            Assert.Equal(9 + (9 + 16L * 200_000_000), length);
            Assert.True(length > int.MaxValue);
        }

        [Fact]
        public void WkbLength_OfSmallGeometries_IsTheExactWkbSize()
        {
            Assert.Equal(21, GeoPackageGeometryEncoder.WkbLength(GeoPackageGeometry.ForPoint(new GeoPackagePosition(1, 2))));
            Assert.Equal(8 + 21, GeoPackageGeometryEncoder.Encode(GeoPackageGeometry.ForPoint(new GeoPackagePosition(1, 2)), 4326).Length);
        }

        // ---- hand-written vectors ------------------------------------------------------------------------------------------
        // Doubles, little-endian: 0 = 00..00, 1 = 000000000000F03F, 2 = 0000000000000040, 3 = 0000000000000840,
        // 4 = 0000000000001040, 5 = 0000000000001440, 10 = 0000000000002440, 10.5 = 0000000000002540, 45.25 = 0000000000A04640.

        [Fact]
        public void Point_IsTheEightByteHeaderAndThePointWkb_WithoutEnvelope()
        {
            byte[] blob = GeoPackageGeometryEncoder.Encode(GeoPackageGeometry.ForPoint(new GeoPackagePosition(10.5, 45.25)), 4326);

            Assert.Equal(
                Hex("47 50 00 01 E6100000" + " 01 01000000 0000000000002540 0000000000A04640"),
                blob);
        }

        [Fact]
        public void MultiLineString_HasTheEnvelopeAndOneWkbLineStringPerLine()
        {
            GeoPackageGeometry geometry = GeoPackageGeometry.ForMultiLineString(new[]
            {
                Line((0, 0), (1, 1)),
                Line((2, 3), (4, 5))
            });

            byte[] blob = GeoPackageGeometryEncoder.Encode(geometry, 4326);

            Assert.Equal(
                Hex(
                    "47 50 00 03 E6100000" +
                    // envelope: minx 0, maxx 4, miny 0, maxy 5
                    " 0000000000000000 0000000000001040 0000000000000000 0000000000001440" +
                    // MultiLineString, 2 members
                    " 01 05000000 02000000" +
                    " 01 02000000 02000000 0000000000000000 0000000000000000 000000000000F03F 000000000000F03F" +
                    " 01 02000000 02000000 0000000000000040 0000000000000840 0000000000001040 0000000000001440"),
                blob);
        }

        [Fact]
        public void MultiPolygonWithAHole_HasTheEnvelopeAndOneWkbPolygonWithTwoRings()
        {
            GeoPackageGeometry geometry = GeoPackageGeometry.ForMultiPolygon(new[]
            {
                Polygon(
                    Ring((0, 0), (10, 0), (10, 10), (0, 10), (0, 0)),
                    Ring((2, 2), (2, 4), (4, 4), (4, 2), (2, 2)))
            });

            byte[] blob = GeoPackageGeometryEncoder.Encode(geometry, 4326);

            Assert.Equal(
                Hex(
                    "47 50 00 03 E6100000" +
                    // envelope: minx 0, maxx 10, miny 0, maxy 10
                    " 0000000000000000 0000000000002440 0000000000000000 0000000000002440" +
                    // MultiPolygon, 1 member, Polygon with 2 rings
                    " 01 06000000 01000000" +
                    " 01 03000000 02000000" +
                    // exterior ring: (0 0) (10 0) (10 10) (0 10) (0 0)
                    " 05000000 0000000000000000 0000000000000000 0000000000002440 0000000000000000" +
                    " 0000000000002440 0000000000002440 0000000000000000 0000000000002440 0000000000000000 0000000000000000" +
                    // hole: (2 2) (2 4) (4 4) (4 2) (2 2)
                    " 05000000 0000000000000040 0000000000000040 0000000000000040 0000000000001040" +
                    " 0000000000001040 0000000000001040 0000000000001040 0000000000000040 0000000000000040 0000000000000040"),
                blob);
        }

        [Fact]
        public void SrsId_IsAnInt32LittleEndian_AnyInt32Value()
        {
            GeoPackageGeometry point = GeoPackageGeometry.ForPoint(new GeoPackagePosition(1, 2));

            Assert.Equal(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF }, GeoPackageGeometryEncoder.Encode(point, -1).Skip(4).Take(4).ToArray());
            Assert.Equal(new byte[] { 0, 0, 0, 0 }, GeoPackageGeometryEncoder.Encode(point, 0).Skip(4).Take(4).ToArray());
            Assert.Equal(new byte[] { 0xFF, 0xFF, 0xFF, 0x7F }, GeoPackageGeometryEncoder.Encode(point, int.MaxValue).Skip(4).Take(4).ToArray());
            Assert.Equal(new byte[] { 0, 0, 0, 0x80 }, GeoPackageGeometryEncoder.Encode(point, int.MinValue).Skip(4).Take(4).ToArray());
        }

        // ---- the samples, read back by the independent decoder -------------------------------------------------------------

        [Theory]
        [MemberData(nameof(Samples))]
        public void EveryGeometryOfTheWmoExamples_IsReadBackByTheIndependentDecoder(string sample) =>
            AssertRoundTrip(SampleFiles.PathOf(sample));

        [Theory]
        [MemberData(nameof(ExtraSamples))]
        public void EveryGeometryOfTheSigwxSamples_IsReadBackByTheIndependentDecoder(string sample) =>
            AssertRoundTrip(SampleFiles.ExtraPathOf(sample));

        [Fact]
        public void EveryFeatureOfTheSamples_EncodesTwiceToTheSameBytes()
        {
            foreach (string path in SampleFiles.AllPaths())
            {
                foreach (GeoPackageFeature feature in ReadFeatures(path).Where(f => f.Geometry != null))
                {
                    byte[] first = GeoPackageGeometryEncoder.Encode(feature.Geometry!, 4326);
                    byte[] second = GeoPackageGeometryEncoder.Encode(feature.Geometry!, 4326);
                    Assert.Equal(first, second);
                }
            }
        }

        [Fact]
        public void AntimeridianSplitAndPolarCapGeometries_KeepTheirNumericEnvelope()
        {
            // A split polygon: the envelope is the plain min/max of what is written, so it spans -180..180 here.
            var split = GeoPackageGeometry.ForMultiPolygon(new[]
            {
                Polygon(Ring((170, 10), (180, 10), (180, 20), (170, 20), (170, 10))),
                Polygon(Ring((-180, 10), (-175, 10), (-175, 20), (-180, 20), (-180, 10)))
            });

            GeoPackageGeometry decoded = Decode(GeoPackageGeometryEncoder.Encode(split, 4326), out GeoPackageEnvelope? envelope);

            AssertSameGeometry(split, decoded, "split");
            Assert.Equal(-180.0, envelope!.Value.MinX);
            Assert.Equal(180.0, envelope.Value.MaxX);
            Assert.Equal(10.0, envelope.Value.MinY);
            Assert.Equal(20.0, envelope.Value.MaxY);
        }

        // ---- exactness -----------------------------------------------------------------------------------------------------

        [Fact]
        public void NegativeZeroAndExtremeCoordinates_ArePreservedBitForBit()
        {
            double[] values =
            {
                -0.0, 0.0, double.Epsilon, -double.Epsilon, 1e-300, -1e-300, 1e300, -1e300, double.MaxValue, double.MinValue,
                0.1, 1.0 / 3.0, 179.99999999999997, -179.99999999999997, 90.0, -90.0
            };
            var line = values.Select((v, i) => new GeoPackagePosition(v, values[(i + 1) % values.Length])).ToArray();
            var ring = line.Concat(new[] { line[0] }).ToArray();

            foreach (GeoPackageGeometry geometry in new[]
            {
                GeoPackageGeometry.ForPoint(new GeoPackagePosition(-0.0, 0.0)),
                GeoPackageGeometry.ForPoint(new GeoPackagePosition(double.Epsilon, double.MaxValue)),
                GeoPackageGeometry.ForMultiLineString(new[] { (IReadOnlyList<GeoPackagePosition>)line }),
                GeoPackageGeometry.ForMultiPolygon(new[] { Polygon(ring) })
            })
            {
                byte[] blob = GeoPackageGeometryEncoder.Encode(geometry, 4326);
                AssertSameGeometry(geometry, Decode(blob, out _), geometry.Kind.ToString());
            }

            // The sign of zero is visible in the bytes: -0.0 is 0x80 in the last byte of the double.
            byte[] point = GeoPackageGeometryEncoder.Encode(GeoPackageGeometry.ForPoint(new GeoPackagePosition(-0.0, 0.0)), 4326);
            Assert.Equal(Hex("0000000000000080 0000000000000000"), point.Skip(8 + 5).ToArray());
        }

        [Fact]
        public void Envelope_OfTheBlob_IsTheMinimumAndMaximumOfTheCoordinates()
        {
            GeoPackageGeometry geometry = GeoPackageGeometry.ForMultiLineString(new[] { Line((1.5, -2.5), (-7.25, 8), (3, 0.125)) });

            Decode(GeoPackageGeometryEncoder.Encode(geometry, 4326), out GeoPackageEnvelope? envelope);

            Assert.Equal(-7.25, envelope!.Value.MinX);
            Assert.Equal(3.0, envelope.Value.MaxX);
            Assert.Equal(-2.5, envelope.Value.MinY);
            Assert.Equal(8.0, envelope.Value.MaxY);
        }

        // ---- invalid input -------------------------------------------------------------------------------------------------

        [Fact]
        public void Encode_NullGeometry_Throws() =>
            Assert.Throws<ArgumentNullException>(() => GeoPackageGeometryEncoder.Encode(null!, 4326));

        [Theory]
        [InlineData((long)int.MaxValue + 1)]
        [InlineData((long)int.MinValue - 1)]
        [InlineData(long.MaxValue)]
        [InlineData(long.MinValue)]
        public void Encode_SrsIdOutOfInt32Range_Throws(long srsId)
        {
            var ex = Assert.Throws<ArgumentException>(() => GeoPackageGeometryEncoder.Encode(GeoPackageGeometry.ForPoint(new GeoPackagePosition(1, 2)), srsId));
            Assert.Contains("srs_id", ex.Message);
        }

        [Theory]
        [InlineData(double.NaN, 1.0)]
        [InlineData(1.0, double.NaN)]
        [InlineData(double.PositiveInfinity, 1.0)]
        [InlineData(1.0, double.NegativeInfinity)]
        public void Encode_NonFiniteCoordinate_Throws(double x, double y)
        {
            var ex = Assert.Throws<ArgumentException>(() => GeoPackageGeometryEncoder.Encode(GeoPackageGeometry.ForPoint(new GeoPackagePosition(x, y)), 4326));
            Assert.Contains("NaN or infinite", ex.Message);

            var line = GeoPackageGeometry.ForMultiLineString(new[] { Line((0, 0), (x, y)) });
            Assert.Contains("NaN or infinite", Assert.Throws<ArgumentException>(() => GeoPackageGeometryEncoder.Encode(line, 4326)).Message);

            var polygon = GeoPackageGeometry.ForMultiPolygon(new[] { Polygon(Ring((0, 0), (x, y), (1, 1), (0, 0))) });
            Assert.Contains("NaN or infinite", Assert.Throws<ArgumentException>(() => GeoPackageGeometryEncoder.Encode(polygon, 4326)).Message);
        }

        [Fact]
        public void Encode_EmptyGeometries_Throw()
        {
            Assert.Contains("no line", Assert.Throws<ArgumentException>(() =>
                GeoPackageGeometryEncoder.Encode(GeoPackageGeometry.ForMultiLineString(Array.Empty<IReadOnlyList<GeoPackagePosition>>()), 4326)).Message);
            Assert.Contains("is empty", Assert.Throws<ArgumentException>(() =>
                GeoPackageGeometryEncoder.Encode(GeoPackageGeometry.ForMultiLineString(new[] { Line() }), 4326)).Message);
            Assert.Contains("no polygon", Assert.Throws<ArgumentException>(() =>
                GeoPackageGeometryEncoder.Encode(GeoPackageGeometry.ForMultiPolygon(Array.Empty<IReadOnlyList<IReadOnlyList<GeoPackagePosition>>>()), 4326)).Message);
            Assert.Contains("no ring", Assert.Throws<ArgumentException>(() =>
                GeoPackageGeometryEncoder.Encode(GeoPackageGeometry.ForMultiPolygon(new[] { Polygon() }), 4326)).Message);
        }

        [Fact]
        public void Encode_LineWithASinglePosition_Throws()
        {
            var ex = Assert.Throws<ArgumentException>(() =>
                GeoPackageGeometryEncoder.Encode(GeoPackageGeometry.ForMultiLineString(new[] { Line((1, 2)) }), 4326));
            Assert.Contains("at least 2", ex.Message);
        }

        [Fact]
        public void Encode_EmptyRing_Throws()
        {
            var ex = Assert.Throws<ArgumentException>(() =>
                GeoPackageGeometryEncoder.Encode(GeoPackageGeometry.ForMultiPolygon(new[] { Polygon(Ring()) }), 4326));
            Assert.Contains("ring 0 of polygon 0 is empty", ex.Message);
        }

        [Fact]
        public void Encode_RingWithFewerThanFourPositions_Throws()
        {
            var ex = Assert.Throws<ArgumentException>(() =>
                GeoPackageGeometryEncoder.Encode(
                    GeoPackageGeometry.ForMultiPolygon(new[] { Polygon(Ring((0, 0), (1, 0), (1, 1), (0, 0)), Ring((0, 0), (1, 0), (0, 0))) }), 4326));
            Assert.Contains("ring 1 of polygon 0 has 3 positions", ex.Message);
        }

        [Fact]
        public void Encode_RingThatIsNotClosed_Throws()
        {
            var ex = Assert.Throws<ArgumentException>(() =>
                GeoPackageGeometryEncoder.Encode(
                    GeoPackageGeometry.ForMultiPolygon(new[] { Polygon(Ring((0, 0), (1, 0), (1, 1), (0, 1))) }), 4326));
            Assert.Contains("not closed", ex.Message);
        }

        // ---- the independent reader ----------------------------------------------------------------------------------------

        internal static IReadOnlyList<GeoPackageFeature> ReadFeatures(string path)
        {
            var result = IwxxmParser.ParseFile(path);
            Assert.True(result.IsSuccess, string.Join(" | ", result.Errors));
            return GeoPackageFeatureSource.Read(result.Value).Features;
        }

        private static void AssertRoundTrip(string path)
        {
            int index = 0;
            foreach (GeoPackageFeature feature in ReadFeatures(path))
            {
                if (feature.Geometry != null)
                {
                    byte[] blob = GeoPackageGeometryEncoder.Encode(feature.Geometry, 4326);
                    GeoPackageGeometry decoded = Decode(blob, out GeoPackageEnvelope? envelope);
                    AssertSameGeometry(feature.Geometry, decoded, path + " feature " + index);

                    GeoPackageEnvelope? expected = feature.Geometry.Envelope();
                    Assert.True(feature.Geometry.Kind == GeoPackageGeometryKind.Point ? envelope == null : envelope.HasValue, "envelope presence");
                    if (envelope.HasValue)
                    {
                        Assert.Equal(expected!.Value.MinX, envelope.Value.MinX);
                        Assert.Equal(expected.Value.MaxX, envelope.Value.MaxX);
                        Assert.Equal(expected.Value.MinY, envelope.Value.MinY);
                        Assert.Equal(expected.Value.MaxY, envelope.Value.MaxY);
                    }
                }

                index++;
            }
        }

        /// <summary>
        /// A reader of StandardGeoPackageBinary written from the specification, not from the encoder: it checks the magic,
        /// the version, the flags, the SRS id (4326), the envelope layout and the WKB byte order and type codes.
        /// </summary>
        internal static GeoPackageGeometry Decode(byte[] blob, out GeoPackageEnvelope? envelope)
        {
            int p = 0;
            Assert.Equal(0x47, blob[p++]);
            Assert.Equal(0x50, blob[p++]);
            Assert.Equal(0, blob[p++]);
            byte flags = blob[p++];
            Assert.Equal(0, flags & 0xC0);          // reserved bits
            Assert.Equal(0, (flags >> 5) & 1);      // StandardGeoPackageBinary
            Assert.Equal(0, (flags >> 4) & 1);      // not empty
            Assert.Equal(1, flags & 1);             // little-endian
            int indicator = (flags >> 1) & 7;
            Assert.True(indicator == 0 || indicator == 1, "envelope indicator " + indicator);
            // srs_id: an int32, little-endian, read byte by byte (not with BitConverter, which follows the machine).
            int srsId = blob[p] | (blob[p + 1] << 8) | (blob[p + 2] << 16) | (blob[p + 3] << 24);
            Assert.Equal(4326, srsId);
            p += 4;

            envelope = null;
            if (indicator == 1)
            {
                double minX = ReadDouble(blob, ref p), maxX = ReadDouble(blob, ref p), minY = ReadDouble(blob, ref p), maxY = ReadDouble(blob, ref p);
                envelope = new GeoPackageEnvelope(minX, maxX, minY, maxY);
            }

            GeoPackageGeometry geometry = ReadWkb(blob, ref p);
            Assert.Equal(blob.Length, p);
            return geometry;
        }

        private static GeoPackageGeometry ReadWkb(byte[] b, ref int p)
        {
            Assert.Equal(1, b[p++]);                // byte order: 1 = little-endian (NDR)
            uint type = ReadUInt32(b, ref p);
            switch (type)
            {
                case 1:                             // Point
                    return GeoPackageGeometry.ForPoint(ReadPosition(b, ref p));

                case 5:                             // MultiLineString, members are LineStrings (2)
                {
                    uint count = ReadUInt32(b, ref p);
                    var lines = new List<IReadOnlyList<GeoPackagePosition>>();
                    for (uint i = 0; i < count; i++)
                    {
                        Assert.Equal(1, b[p++]);
                        Assert.Equal(2u, ReadUInt32(b, ref p));
                        lines.Add(ReadPositions(b, ref p));
                    }

                    return GeoPackageGeometry.ForMultiLineString(lines);
                }

                case 6:                             // MultiPolygon, members are Polygons (3)
                {
                    uint count = ReadUInt32(b, ref p);
                    var polygons = new List<IReadOnlyList<IReadOnlyList<GeoPackagePosition>>>();
                    for (uint i = 0; i < count; i++)
                    {
                        Assert.Equal(1, b[p++]);
                        Assert.Equal(3u, ReadUInt32(b, ref p));
                        uint rings = ReadUInt32(b, ref p);
                        var list = new List<IReadOnlyList<GeoPackagePosition>>();
                        for (uint r = 0; r < rings; r++)
                            list.Add(ReadPositions(b, ref p));
                        polygons.Add(list);
                    }

                    return GeoPackageGeometry.ForMultiPolygon(polygons);
                }

                default:
                    throw new InvalidOperationException("Unexpected WKB type " + type);
            }
        }

        private static IReadOnlyList<GeoPackagePosition> ReadPositions(byte[] b, ref int p)
        {
            uint count = ReadUInt32(b, ref p);
            var list = new List<GeoPackagePosition>();
            for (uint i = 0; i < count; i++)
                list.Add(ReadPosition(b, ref p));
            return list;
        }

        private static GeoPackagePosition ReadPosition(byte[] b, ref int p)
        {
            double x = ReadDouble(b, ref p);
            double y = ReadDouble(b, ref p);
            return new GeoPackagePosition(x, y);
        }

        private static uint ReadUInt32(byte[] b, ref int p)
        {
            uint value = (uint)(b[p] | (b[p + 1] << 8) | (b[p + 2] << 16) | (b[p + 3] << 24));
            p += 4;
            return value;
        }

        private static double ReadDouble(byte[] b, ref int p)
        {
            long bits = 0;
            for (int i = 7; i >= 0; i--)
                bits = (bits << 8) | b[p + i];
            p += 8;
            return BitConverter.Int64BitsToDouble(bits);
        }

        internal static void AssertSameGeometry(GeoPackageGeometry expected, GeoPackageGeometry actual, string where)
        {
            Assert.Equal(expected.Kind, actual.Kind);
            switch (expected.Kind)
            {
                case GeoPackageGeometryKind.Point:
                    AssertSamePosition(expected.Point, actual.Point, where);
                    break;

                case GeoPackageGeometryKind.MultiLineString:
                    Assert.Equal(expected.Lines!.Count, actual.Lines!.Count);
                    for (int l = 0; l < expected.Lines.Count; l++)
                        AssertSamePositions(expected.Lines[l], actual.Lines[l], where + " line " + l);
                    break;

                default:
                    Assert.Equal(expected.Polygons!.Count, actual.Polygons!.Count);
                    for (int g = 0; g < expected.Polygons.Count; g++)
                    {
                        Assert.Equal(expected.Polygons[g].Count, actual.Polygons[g].Count);
                        for (int r = 0; r < expected.Polygons[g].Count; r++)
                            AssertSamePositions(expected.Polygons[g][r], actual.Polygons[g][r], where + " polygon " + g + " ring " + r);
                    }

                    break;
            }
        }

        private static void AssertSamePositions(IReadOnlyList<GeoPackagePosition> expected, IReadOnlyList<GeoPackagePosition> actual, string where)
        {
            Assert.Equal(expected.Count, actual.Count);
            for (int i = 0; i < expected.Count; i++)
                AssertSamePosition(expected[i], actual[i], where + " position " + i);
        }

        private static void AssertSamePosition(GeoPackagePosition expected, GeoPackagePosition actual, string where)
        {
            Assert.True(BitConverter.DoubleToInt64Bits(expected.X) == BitConverter.DoubleToInt64Bits(actual.X), where + ": x");
            Assert.True(BitConverter.DoubleToInt64Bits(expected.Y) == BitConverter.DoubleToInt64Bits(actual.Y), where + ": y");
        }

        internal static byte[] Hex(string text)
        {
            string digits = text.Replace(" ", string.Empty);
            var bytes = new byte[digits.Length / 2];
            for (int i = 0; i < bytes.Length; i++)
                bytes[i] = Convert.ToByte(digits.Substring(2 * i, 2), 16);
            return bytes;
        }
    }
}
