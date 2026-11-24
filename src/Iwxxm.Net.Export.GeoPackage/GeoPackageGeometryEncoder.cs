using System;
using System.Collections.Generic;
using Iwxxm.Net.Export.GeoPackage.Model;

namespace Iwxxm.Net.Export.GeoPackage
{
    /// <summary>
    /// Encodes a <see cref="GeoPackageGeometry"/> as a GeoPackage <c>StandardGeoPackageBinary</c> blob (OGC 12-128r18,
    /// section 2.1.3): the header, the optional envelope and the ISO/OGC well-known binary of the geometry.
    /// </summary>
    /// <remarks>
    /// <para>Layout: <c>'G' 'P'</c> (0x47 0x50), version 0, one flags byte, the SRS id as an int32, the envelope when the flags
    /// announce one, then the WKB. Every number is little-endian. The flags byte is <c>0x01</c> (little-endian, no envelope,
    /// not empty, standard geometry) for a point and <c>0x03</c> (envelope indicator 1: the 32 bytes
    /// <c>[minx, maxx, miny, maxy]</c> as doubles) for a multi line string or a multi polygon. The WKB type codes are 1
    /// (<c>Point</c>), 2 (<c>LineString</c>), 3 (<c>Polygon</c>), 5 (<c>MultiLineString</c>) and 6 (<c>MultiPolygon</c>); the
    /// members repeat their own byte-order byte and type code.</para>
    /// <para>The output depends only on the input (no time, no culture, no platform byte order), so encoding the same
    /// geometry twice gives the same bytes.</para>
    /// </remarks>
    internal static class GeoPackageGeometryEncoder
    {
        private const byte MagicG = 0x47;
        private const byte MagicP = 0x50;
        private const byte Version = 0x00;
        private const byte LittleEndianWkb = 0x01;
        private const byte FlagsWithoutEnvelope = 0x01;
        private const byte FlagsWithEnvelope = 0x03;
        private const uint WkbPoint = 1;
        private const uint WkbLineString = 2;
        private const uint WkbPolygon = 3;
        private const uint WkbMultiLineString = 5;
        private const uint WkbMultiPolygon = 6;
        private const int HeaderLength = 8;
        private const int EnvelopeLength = 32;
        private const int WkbByteOrderLength = 1;
        private const int WkbTypeLength = 4;
        private const int WkbCountLength = 4;
        private const int WkbPositionLength = 16; // two doubles
        private const int WkbHeaderLength = WkbByteOrderLength + WkbTypeLength;
        private const int MinimumRingPositions = 4;
        private const int MinimumLinePositions = 2;

        /// <summary>Encodes <paramref name="geometry"/> with the SRS id <paramref name="srsId"/> (4326 for the tables of this package).</summary>
        /// <exception cref="ArgumentNullException"><paramref name="geometry"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentException">
        /// <paramref name="srsId"/> is outside the range of an int32, a coordinate is NaN or infinite, the geometry has no
        /// position, a line has fewer than two positions, or a ring is empty, has fewer than four positions or is not closed.
        /// </exception>
        public static byte[] Encode(GeoPackageGeometry geometry, long srsId)
        {
            if (geometry == null)
                throw new ArgumentNullException(nameof(geometry));
            if (srsId < int.MinValue || srsId > int.MaxValue)
                throw new ArgumentException($"The srs_id {srsId} is outside the range of a 32-bit integer.", nameof(srsId));

            Validate(geometry);

            bool isPoint = geometry.Kind == GeoPackageGeometryKind.Point;
            int envelopeLength = isPoint ? 0 : EnvelopeLength;
            var bytes = new byte[HeaderLength + envelopeLength + WkbLength(geometry)];
            int offset = 0;

            bytes[offset++] = MagicG;
            bytes[offset++] = MagicP;
            bytes[offset++] = Version;
            bytes[offset++] = isPoint ? FlagsWithoutEnvelope : FlagsWithEnvelope;
            WriteUInt32(bytes, ref offset, unchecked((uint)(int)srsId));

            if (!isPoint)
            {
                // Validate() guarantees at least one position, so the envelope exists.
                GeoPackageEnvelope envelope = geometry.Envelope()!.Value;
                WriteDouble(bytes, ref offset, envelope.MinX);
                WriteDouble(bytes, ref offset, envelope.MaxX);
                WriteDouble(bytes, ref offset, envelope.MinY);
                WriteDouble(bytes, ref offset, envelope.MaxY);
            }

            WriteGeometry(bytes, ref offset, geometry);
            return bytes;
        }

        private static void Validate(GeoPackageGeometry geometry)
        {
            switch (geometry.Kind)
            {
                case GeoPackageGeometryKind.Point:
                    if (!IsFinite(geometry.Point))
                        throw NotFinite("point");
                    break;

                case GeoPackageGeometryKind.MultiLineString:
                    if (geometry.Lines == null || geometry.Lines.Count == 0)
                        throw new ArgumentException("The multi line string has no line.", nameof(geometry));
                    for (int l = 0; l < geometry.Lines.Count; l++)
                        ValidateLine(geometry.Lines[l], l);
                    break;

                case GeoPackageGeometryKind.MultiPolygon:
                    if (geometry.Polygons == null || geometry.Polygons.Count == 0)
                        throw new ArgumentException("The multi polygon has no polygon.", nameof(geometry));
                    for (int p = 0; p < geometry.Polygons.Count; p++)
                    {
                        IReadOnlyList<IReadOnlyList<GeoPackagePosition>> rings = geometry.Polygons[p];
                        if (rings == null || rings.Count == 0)
                            throw new ArgumentException($"Polygon {p} of the multi polygon has no ring.", nameof(geometry));
                        for (int r = 0; r < rings.Count; r++)
                            ValidateRing(rings[r], p, r);
                    }

                    break;

                default:
                    throw new ArgumentException($"Unsupported geometry kind {geometry.Kind}.", nameof(geometry));
            }
        }

        private static void ValidateLine(IReadOnlyList<GeoPackagePosition> line, int index)
        {
            if (line == null || line.Count == 0)
                throw new ArgumentException($"Line {index} of the multi line string is empty.");
            if (line.Count < MinimumLinePositions)
                throw new ArgumentException($"Line {index} of the multi line string has {line.Count} position; a line needs at least {MinimumLinePositions}.");
            for (int i = 0; i < line.Count; i++)
            {
                if (!IsFinite(line[i]))
                    throw NotFinite($"line {index} position {i}");
            }
        }

        private static void ValidateRing(IReadOnlyList<GeoPackagePosition> ring, int polygon, int index)
        {
            // The messages are built only when a check fails, never per position on the success path.
            if (ring == null || ring.Count == 0)
                throw new ArgumentException($"The {RingName(polygon, index)} is empty.");
            if (ring.Count < MinimumRingPositions)
                throw new ArgumentException($"The {RingName(polygon, index)} has {ring.Count} positions; a ring needs at least {MinimumRingPositions} (including the closing position).");
            for (int i = 0; i < ring.Count; i++)
            {
                if (!IsFinite(ring[i]))
                    throw NotFinite($"{RingName(polygon, index)} position {i}");
            }

            if (ring[0].X != ring[ring.Count - 1].X || ring[0].Y != ring[ring.Count - 1].Y)
                throw new ArgumentException($"The {RingName(polygon, index)} is not closed: its last position differs from its first.");
        }

        private static string RingName(int polygon, int index) => $"ring {index} of polygon {polygon}";

        private static bool IsFinite(GeoPackagePosition position) =>
            !(double.IsNaN(position.X) || double.IsInfinity(position.X) || double.IsNaN(position.Y) || double.IsInfinity(position.Y));

        private static ArgumentException NotFinite(string where) =>
            new ArgumentException($"The {where} has a coordinate that is NaN or infinite.");

        private static int WkbLength(GeoPackageGeometry geometry)
        {
            switch (geometry.Kind)
            {
                case GeoPackageGeometryKind.Point:
                    return WkbHeaderLength + WkbPositionLength;

                case GeoPackageGeometryKind.MultiLineString:
                {
                    int length = WkbHeaderLength + WkbCountLength;
                    foreach (IReadOnlyList<GeoPackagePosition> line in geometry.Lines!)
                        length += WkbHeaderLength + WkbCountLength + (WkbPositionLength * line.Count);
                    return length;
                }

                case GeoPackageGeometryKind.MultiPolygon:
                {
                    int length = WkbHeaderLength + WkbCountLength;
                    foreach (IReadOnlyList<IReadOnlyList<GeoPackagePosition>> polygon in geometry.Polygons!)
                    {
                        length += WkbHeaderLength + WkbCountLength;
                        foreach (IReadOnlyList<GeoPackagePosition> ring in polygon)
                            length += WkbCountLength + (WkbPositionLength * ring.Count);
                    }

                    return length;
                }

                default:
                    throw UnsupportedKind(geometry.Kind);
            }
        }

        private static void WriteGeometry(byte[] bytes, ref int offset, GeoPackageGeometry geometry)
        {
            switch (geometry.Kind)
            {
                case GeoPackageGeometryKind.Point:
                    WriteWkbHeader(bytes, ref offset, WkbPoint);
                    WritePosition(bytes, ref offset, geometry.Point);
                    break;

                case GeoPackageGeometryKind.MultiLineString:
                    WriteWkbHeader(bytes, ref offset, WkbMultiLineString);
                    WriteUInt32(bytes, ref offset, (uint)geometry.Lines!.Count);
                    foreach (IReadOnlyList<GeoPackagePosition> line in geometry.Lines)
                    {
                        WriteWkbHeader(bytes, ref offset, WkbLineString);
                        WritePositions(bytes, ref offset, line);
                    }

                    break;

                case GeoPackageGeometryKind.MultiPolygon:
                    WriteWkbHeader(bytes, ref offset, WkbMultiPolygon);
                    WriteUInt32(bytes, ref offset, (uint)geometry.Polygons!.Count);
                    foreach (IReadOnlyList<IReadOnlyList<GeoPackagePosition>> polygon in geometry.Polygons)
                    {
                        WriteWkbHeader(bytes, ref offset, WkbPolygon);
                        WriteUInt32(bytes, ref offset, (uint)polygon.Count);
                        foreach (IReadOnlyList<GeoPackagePosition> ring in polygon)
                            WritePositions(bytes, ref offset, ring);
                    }

                    break;

                default:
                    throw UnsupportedKind(geometry.Kind);
            }
        }

        // Validate() rejects an unknown kind first, so this is only reached by a kind added without updating the encoder.
        private static InvalidOperationException UnsupportedKind(GeoPackageGeometryKind kind) =>
            new InvalidOperationException($"Unsupported geometry kind {kind}.");

        private static void WriteWkbHeader(byte[] bytes, ref int offset, uint type)
        {
            bytes[offset++] = LittleEndianWkb;
            WriteUInt32(bytes, ref offset, type);
        }

        private static void WritePositions(byte[] bytes, ref int offset, IReadOnlyList<GeoPackagePosition> positions)
        {
            WriteUInt32(bytes, ref offset, (uint)positions.Count);
            for (int i = 0; i < positions.Count; i++)
                WritePosition(bytes, ref offset, positions[i]);
        }

        private static void WritePosition(byte[] bytes, ref int offset, GeoPackagePosition position)
        {
            WriteDouble(bytes, ref offset, position.X);
            WriteDouble(bytes, ref offset, position.Y);
        }

        private static void WriteUInt32(byte[] bytes, ref int offset, uint value)
        {
            for (int i = 0; i < sizeof(uint); i++)
                bytes[offset++] = (byte)(value >> (8 * i));
        }

        // The IEEE 754 bit pattern, so that -0.0 and every other value are kept exactly, whatever the byte order of the machine.
        private static void WriteDouble(byte[] bytes, ref int offset, double value)
        {
            ulong bits = unchecked((ulong)BitConverter.DoubleToInt64Bits(value));
            for (int i = 0; i < sizeof(ulong); i++)
                bytes[offset++] = (byte)(bits >> (8 * i));
        }
    }
}
