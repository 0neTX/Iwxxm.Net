using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Iwxxm.Net.Tests.Scale
{
    /// <summary>
    /// Builds large but valid IWXXM documents for the scale tests by templating real WMO examples: the horizontal
    /// projection of the SIGMET example is replaced by a generated ring, and the examples are repeated inside a
    /// <c>collect:MeteorologicalBulletin</c>. The geometry shapes are generated as (latitude, unwrapped longitude) pairs and
    /// wrapped into [-180, 180] the way a source would write them.
    /// </summary>
    internal static class ScaleDocuments
    {
        private const string PosListStart = "<gml:posList>";
        private const string PosListEnd = "</gml:posList>";

        private static readonly Regex Uuid = new Regex("uuid\\.([0-9a-fA-F]{4})([0-9a-fA-F]{4}-)", RegexOptions.Compiled);

        private static readonly Lazy<string> SigmetTemplate = new Lazy<string>(() => ReadExample("sigmet-A6-1a-TS.xml"));
        private static readonly Lazy<string> MetarTemplate = new Lazy<string>(() => ReadExample("metar-A3-1.xml"));

        private static string ReadExample(string fileName) => File.ReadAllText(Path.Combine("Samples", "wmo-examples", fileName));

        /// <summary>The SIGMET example with its square replaced by <paramref name="ring"/> (latitude, unwrapped longitude; closed here).</summary>
        public static string SigmetWithRing(IReadOnlyList<(double Lat, double X)> ring)
        {
            string template = SigmetTemplate.Value;
            int start = template.IndexOf(PosListStart, StringComparison.Ordinal) + PosListStart.Length;
            int end = template.IndexOf(PosListEnd, StringComparison.Ordinal);

            var sb = new StringBuilder(template.Length + (ring.Count * 18));
            sb.Append(template, 0, start);
            foreach ((double lat, double x) in ring)
                AppendPair(sb, lat, Wrap(x));

            // GML rings are closed: repeat the first vertex unless the generator already did.
            if (ring[0].Lat != ring[ring.Count - 1].Lat || Wrap(ring[0].X) != Wrap(ring[ring.Count - 1].X))
                AppendPair(sb, ring[0].Lat, Wrap(ring[0].X));

            sb.Append(template, end, template.Length - end);
            return sb.ToString();
        }

        /// <summary>A <c>collect:MeteorologicalBulletin</c> of <paramref name="reportCount"/> reports, alternating the METAR and SIGMET examples.</summary>
        public static string Bulletin(int reportCount)
        {
            var sb = new StringBuilder(reportCount * 6500);
            sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n");
            sb.Append("<collect:MeteorologicalBulletin xmlns:collect=\"http://def.wmo.int/collect/2014\" xmlns:gml=\"http://www.opengis.net/gml/3.2\" ");
            sb.Append("xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" ");
            sb.Append("xsi:schemaLocation=\"http://def.wmo.int/collect/2014 http://schemas.wmo.int/collect/1.2/collect.xsd\" ");
            sb.Append("gml:id=\"uuid.6f353602-12a1-40a7-b6b5-3edb14c6241e\">\n");
            for (int i = 0; i < reportCount; i++)
            {
                string report = i % 2 == 0 ? MetarTemplate.Value : SigmetTemplate.Value;
                sb.Append("<collect:meteorologicalInformation>\n");
                sb.Append(MakeIdsUnique(StripXmlDeclaration(report), i));
                sb.Append("\n</collect:meteorologicalInformation>\n");
            }

            sb.Append("<collect:bulletinIdentifier>A_LAYU31YUDO221630_C_YUDO_20120822163000.xml</collect:bulletinIdentifier>\n");
            sb.Append("</collect:MeteorologicalBulletin>\n");
            return sb.ToString();
        }

        /// <summary>
        /// A convex ring of <paramref name="vertices"/> vertices (an ellipse 80 degrees wide and 100 degrees long) that stays far from the
        /// antimeridian.
        /// </summary>
        public static List<(double Lat, double X)> Ellipse(int vertices)
        {
            var ring = new List<(double, double)>(vertices + 1);
            for (int k = 0; k < vertices; k++)
            {
                double t = 2.0 * Math.PI * k / vertices;
                ring.Add((Round(40.0 * Math.Sin(t)), Round(50.0 * Math.Cos(t))));
            }

            ring.Add(ring[0]);
            return ring;
        }

        /// <summary>
        /// A comb along the antimeridian: a spine west of 180 with <paramref name="teeth"/> teeth that reach across it to longitude
        /// -170. The ring crosses the antimeridian twice per tooth and exports as one western piece plus one piece per tooth tip.
        /// </summary>
        public static List<(double Lat, double X)> Comb(int teeth)
        {
            double step = 160.0 / teeth;
            double height = step / 2.0;
            var ring = new List<(double, double)>(4 * teeth + 2) { (Round(-80.0), 170.0) };
            for (int i = 0; i < teeth; i++)
            {
                double y = Round(-80.0 + (i * step));
                double top = Round(y + height);
                if (i > 0)
                    ring.Add((y, 175.0));

                ring.Add((y, 190.0));
                ring.Add((top, 190.0));
                ring.Add((top, i == teeth - 1 ? 170.0 : 175.0));
            }

            ring.Add(ring[0]);
            return ring;
        }

        /// <summary>A ring around the north pole (latitude 75 to 81) with <paramref name="vertices"/> vertices and a single wrap of the longitude.</summary>
        public static List<(double Lat, double X)> NorthPolarRing(int vertices)
        {
            var ring = new List<(double, double)>(vertices + 1);
            for (int k = 0; k < vertices; k++)
                ring.Add((Round(78.0 + (3.0 * Math.Sin(k * 0.01))), Round(-180.0 + (360.0 * k / vertices))));

            ring.Add(ring[0]);
            return ring;
        }

        /// <summary>
        /// The south polar ring with a tongue across the antimeridian that the data-fidelity tests use (it leaves the strip and returns
        /// before the ring goes on), with every edge densified so that the ring has about <paramref name="vertices"/> vertices.
        /// </summary>
        public static List<(double Lat, double X)> SouthPolarRingWithLobe(int vertices)
        {
            (double Lat, double X)[] corners = { (-70, 0), (-70, 90), (-70, 170), (-75, 200), (-80, 170), (-80, 230), (-70, 270), (-70, 360) };
            int perEdge = Math.Max(1, vertices / (corners.Length - 1));
            var ring = new List<(double, double)>(vertices + 1) { corners[0] };
            for (int e = 1; e < corners.Length; e++)
            {
                for (int s = 1; s <= perEdge; s++)
                {
                    double f = (double)s / perEdge;
                    ring.Add((Round(corners[e - 1].Lat + (f * (corners[e].Lat - corners[e - 1].Lat))), Round(corners[e - 1].X + (f * (corners[e].X - corners[e - 1].X)))));
                }
            }

            return ring;
        }

        private static string StripXmlDeclaration(string xml)
        {
            int end = xml.IndexOf("?>", StringComparison.Ordinal);
            return xml.StartsWith("<?xml", StringComparison.Ordinal) ? xml.Substring(end + 2).TrimStart() : xml;
        }

        // Every gml:id and every local reference of one report is rewritten the same way, so references stay valid and ids stay UUID-shaped.
        private static string MakeIdsUnique(string report, int index) =>
            Uuid.Replace(report, m => "uuid." + index.ToString("x4", CultureInfo.InvariantCulture) + m.Groups[2].Value);

        private static void AppendPair(StringBuilder sb, double lat, double lon)
        {
            sb.Append(lat.ToString("0.######", CultureInfo.InvariantCulture)).Append(' ')
              .Append(lon.ToString("0.######", CultureInfo.InvariantCulture)).Append(' ');
        }

        private static double Round(double value) => Math.Round(value, 6);

        private static double Wrap(double x)
        {
            double lon = ((((x + 180.0) % 360.0) + 360.0) % 360.0) - 180.0;
            return lon == -180.0 && x > 0 ? 180.0 : lon;
        }

        /// <summary>Counts the positions of every ring of a GeoJSON geometry (used to check that no vertex is lost).</summary>
        public static int CountPositions(System.Text.Json.JsonElement coordinates)
        {
            if (coordinates.GetArrayLength() > 0 && coordinates[0].ValueKind == System.Text.Json.JsonValueKind.Number)
                return 1;

            return coordinates.EnumerateArray().Sum(CountPositions);
        }
    }
}
