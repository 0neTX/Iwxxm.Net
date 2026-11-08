using System;
using System.Collections.Generic;
using CsCheck;
using Iwxxm.Net.Infrastructure.GeoJson.Writing;
using Iwxxm.Net.Tests.Properties;
using Xunit;

namespace Iwxxm.Net.Tests.DataFidelity
{
    /// <summary>
    /// TD-SCALE-1: <see cref="StripClipper"/> picks the successor of an arc with a sorted index instead of scanning every arc. The
    /// previous implementation is kept below as a test-only reference (<see cref="ReferenceStripClipper"/>, a verbatim copy of the
    /// quadratic scan) and both are run on generated chains: the rings must be identical, vertex for vertex, including ties
    /// (arcs with exactly equal entry positions, entry positions that differ by less than the rounding of the perimeter distance),
    /// wrap-around, a single arc, border-exact vertices and clamped latitudes.
    /// </summary>
    public sealed class StripClipperDifferentialTests
    {
        private enum LatitudeMode
        {
            Continuous,
            FewValues,
            NearTies,
            WithNaN,
        }

        private static readonly (double West, double East, double Shift)[] Strips =
        {
            (-180.0, 180.0, 0.0),
            (180.0, 540.0, -360.0),
            (-10.0, 30.0, 0.0),
            (-540.0, -180.0, 360.0),
        };

        public sealed class ChainCase
        {
            public ChainCase(int seed, int arcs, int latitudeMode, int strip)
            {
                Seed = seed;
                Arcs = arcs;
                LatitudeMode = latitudeMode;
                Strip = strip;
            }

            public int Seed { get; }

            public int Arcs { get; }

            public int LatitudeMode { get; }

            public int Strip { get; }

            public override string ToString() => $"seed={Seed} arcs={Arcs} latitudeMode={LatitudeMode} strip={Strip}";
        }

        private static readonly Gen<ChainCase> Cases = Gen.Select(
            Gen.Int[0, int.MaxValue],
            Gen.OneOfConst(0, 1, 1, 2, 3, 5, 8, 20, 60, 200, 700),
            Gen.Int[0, 3],
            Gen.Int[0, Strips.Length - 1],
            (seed, arcs, mode, strip) => new ChainCase(seed, arcs, mode, strip));

        [Fact]
        public void Clip_GeneratedChains_MatchesTheScanningReference()
        {
            PropertySettings.Run(Cases, PropertySettings.StripClipperSeed, c => AssertSameRings(c.Seed, c.Arcs, (LatitudeMode)c.LatitudeMode, c.Strip), c => c.ToString());
        }

        [Theory]
        [InlineData(1, 1, 0)]
        [InlineData(2, 1, 1)]
        [InlineData(3, 2, 1)]
        [InlineData(4, 40, 1)]      // many arcs on five latitudes: runs of exactly equal entry positions
        [InlineData(5, 300, 2)]     // entry positions a few ulp apart
        [InlineData(6, 1000, 2)]
        [InlineData(7, 3000, 1)]
        [InlineData(8, 3000, 0)]
        [InlineData(9, 4000, 2)]
        [InlineData(10, 200, 3)]    // some latitudes are NaN: such an arc is never a successor
        [InlineData(11, 30, 3)]
        public void Clip_FixedScenarios_MatchesTheScanningReference(int seed, int arcs, int mode)
        {
            for (int strip = 0; strip < Strips.Length; strip++)
                AssertSameRings(seed, arcs, (LatitudeMode)mode, strip);
        }

        [Fact]
        public void Clip_ChainWithoutArcs_IsEmpty()
        {
            var chain = new List<StripClipper.Node> { new StripClipper.Node(-200.0, 0.0, 0), new StripClipper.Node(-190.0, 5.0, 1) };
            var source = new List<GeoJsonPosition> { new GeoJsonPosition(-200.0, 0.0), new GeoJsonPosition(-190.0, 5.0) };
            Assert.Empty(StripClipper.Clip(chain, source, -180.0, 180.0, 0.0));
        }

        private static void AssertSameRings(int seed, int arcs, LatitudeMode mode, int stripIndex)
        {
            (double west, double east, double shift) = Strips[stripIndex];
            BuildChain(new Random(seed), arcs, mode, west, east, shift, out List<StripClipper.Node> chain, out List<GeoJsonPosition> source);

            List<List<GeoJsonPosition>> expected = ReferenceStripClipper.Clip(chain, source, west, east, shift);
            List<List<GeoJsonPosition>> actual = StripClipper.Clip(chain, source, west, east, shift);

            string context = $"seed={seed} arcs={arcs} mode={mode} strip={stripIndex}";
            Assert.True(expected.Count == actual.Count, $"{context}: ring count {actual.Count}, expected {expected.Count}");
            for (int r = 0; r < expected.Count; r++)
            {
                Assert.True(expected[r].Count == actual[r].Count, $"{context}: ring {r} has {actual[r].Count} vertices, expected {expected[r].Count}");
                for (int v = 0; v < expected[r].Count; v++)
                    Assert.True(expected[r][v].Equals(actual[r][v]), $"{context}: ring {r} vertex {v} is ({actual[r][v].Lon}, {actual[r][v].Lat}), expected ({expected[r][v].Lon}, {expected[r][v].Lat})");
            }
        }

        // A chain that starts and ends outside the strip and alternates outside runs with arcs inside it. An arc enters and leaves either
        // exactly on a border (the latitude then comes straight from the pool, so entry positions can tie exactly or differ by a few ulp)
        // or through an interpolated crossing (arbitrary positions). Latitudes beyond +-90 exercise the clamping of the perimeter position.
        private static void BuildChain(Random random, int arcs, LatitudeMode mode, double west, double east, double shift, out List<StripClipper.Node> chain, out List<GeoJsonPosition> source)
        {
            double[] pool = { -90.0, -45.5, 0.0, 12.25, 90.0 };
            double[] fewValues = { pool[random.Next(pool.Length)], pool[random.Next(pool.Length)], pool[random.Next(pool.Length)], pool[random.Next(pool.Length)] };

            double Latitude()
            {
                switch (mode)
                {
                    case LatitudeMode.FewValues:
                        return fewValues[random.Next(fewValues.Length)];
                    case LatitudeMode.WithNaN:
                        return random.Next(20) == 0 ? double.NaN : (random.NextDouble() * 200.0) - 100.0;
                    case LatitudeMode.NearTies:
                        // A few base latitudes plus noise of a few ulp at the scale of the perimeter positions (about 1e-13).
                        return fewValues[random.Next(fewValues.Length)] + (random.Next(-3, 4) * 1e-14);
                    default:
                        return (random.NextDouble() * 200.0) - 100.0;
                }
            }

            var nodes = new List<StripClipper.Node>();
            var positions = new List<GeoJsonPosition>();

            void Add(double x, double y, bool fromSource)
            {
                int index = -1;
                if (fromSource)
                {
                    index = positions.Count;
                    positions.Add(new GeoJsonPosition(x + shift, y));
                }

                nodes.Add(new StripClipper.Node(x, y, index));
            }

            double Outside() => random.Next(2) == 0 ? west - 1.0 - (random.NextDouble() * 20.0) : east + 1.0 + (random.NextDouble() * 20.0);

            Add(Outside(), Latitude(), true);
            for (int a = 0; a < arcs; a++)
            {
                bool exact = random.Next(3) != 0;
                double entry = random.Next(2) == 0 ? west : east;
                double exit = random.Next(2) == 0 ? west : east;
                if (exact)
                {
                    Add(entry, Latitude(), true);
                    int inner = random.Next(1, 3);
                    for (int i = 0; i < inner; i++)
                        Add(west + (random.NextDouble() * (east - west)), Latitude(), true);
                    Add(exit, Latitude(), true);
                    Add(Outside(), Latitude(), true);
                }
                else
                {
                    // Interpolated entry and exit: the first and last inside vertices are strictly inside the strip.
                    Add(west + ((east - west) * (0.05 + (random.NextDouble() * 0.9))), Latitude(), true);
                    int inner = random.Next(0, 3);
                    for (int i = 0; i < inner; i++)
                        Add(west + (random.NextDouble() * (east - west)), Latitude(), true);
                    Add(west + ((east - west) * (0.05 + (random.NextDouble() * 0.9))), Latitude(), true);
                    Add(Outside(), Latitude(), true);
                }

                // Sometimes several outside vertices between two arcs.
                if (random.Next(4) == 0)
                    Add(Outside(), Latitude(), true);

                // Between arcs the chain must be outside the strip, so the next arc enters again from there.
                if (a < arcs - 1 && random.Next(3) == 0)
                    Add(Outside(), Latitude(), true);
            }

            chain = nodes;
            source = positions;
        }

        /// <summary>
        /// Verbatim copy of <see cref="StripClipper"/> before TD-SCALE-1: for every arc appended to a ring, the successor is found by
        /// scanning all arcs (smallest counterclockwise perimeter distance from the exit position to the entry position; on equal
        /// distances the first arc in the list; the start arc wins a tie). Test-only; do not optimize it.
        /// </summary>
        private static class ReferenceStripClipper
        {
            private const double MinLat = -90.0;
            private const double MaxLat = 90.0;

            private sealed class Arc
            {
                public List<StripClipper.Node> Nodes { get; } = new List<StripClipper.Node>();

                public double EntryT { get; set; }

                public double ExitT { get; set; }

                public bool Used { get; set; }
            }

            public static List<List<GeoJsonPosition>> Clip(IReadOnlyList<StripClipper.Node> chain, IReadOnlyList<GeoJsonPosition> source, double west, double east, double shift)
            {
                List<Arc> arcs = CollectArcs(chain, west, east);
                var rings = new List<List<GeoJsonPosition>>();
                if (arcs.Count == 0)
                    return rings;

                double width = east - west;
                double height = MaxLat - MinLat;
                double perimeter = (2.0 * width) + (2.0 * height);
                foreach (Arc arc in arcs)
                {
                    arc.EntryT = Perimeter(arc.Nodes[0], west, width, height);
                    arc.ExitT = Perimeter(arc.Nodes[arc.Nodes.Count - 1], west, width, height);
                }

                var corners = new[]
                {
                    (T: 0.0, X: west, Y: MinLat),
                    (T: width, X: east, Y: MinLat),
                    (T: width + height, X: east, Y: MaxLat),
                    (T: (2.0 * width) + height, X: west, Y: MaxLat),
                };

                foreach (Arc start in arcs)
                {
                    if (start.Used)
                        continue;

                    var ring = new List<GeoJsonPosition>();
                    Arc current = start;
                    for (int guard = 0; guard <= arcs.Count; guard++)
                    {
                        current.Used = true;
                        foreach (StripClipper.Node node in current.Nodes)
                            Append(ring, ToPosition(node, source, shift));

                        Arc next = start;
                        double best = CounterclockwiseDistance(current.ExitT, start.EntryT, perimeter);
                        foreach (Arc candidate in arcs)
                        {
                            if (candidate.Used)
                                continue;

                            double distance = CounterclockwiseDistance(current.ExitT, candidate.EntryT, perimeter);
                            if (distance < best)
                            {
                                best = distance;
                                next = candidate;
                            }
                        }

                        var passed = new List<(double Distance, double X, double Y)>();
                        foreach (var corner in corners)
                        {
                            double distance = CounterclockwiseDistance(current.ExitT, corner.T, perimeter);
                            if (distance > 0.0 && distance < best)
                                passed.Add((distance, corner.X, corner.Y));
                        }

                        passed.Sort((a, b) => a.Distance.CompareTo(b.Distance));
                        foreach (var corner in passed)
                            Append(ring, new GeoJsonPosition(corner.X + shift, corner.Y));

                        if (ReferenceEquals(next, start))
                            break;

                        current = next;
                    }

                    if (ring.Count > 1 && ring[ring.Count - 1].Equals(ring[0]))
                        ring.RemoveAt(ring.Count - 1);

                    if (ring.Count >= 3)
                    {
                        ring.Add(ring[0]);
                        if (ring.Count >= 4)
                            rings.Add(ring);
                    }
                }

                return rings;
            }

            private static List<Arc> CollectArcs(IReadOnlyList<StripClipper.Node> chain, double west, double east)
            {
                var arcs = new List<Arc>();
                Arc? current = null;
                for (int i = 0; i < chain.Count; i++)
                {
                    StripClipper.Node p = chain[i];
                    bool inside = p.X >= west && p.X <= east;
                    if (i > 0)
                    {
                        StripClipper.Node q = chain[i - 1];
                        bool previousInside = q.X >= west && q.X <= east;
                        if (!previousInside && inside)
                        {
                            double side = q.X < west ? west : east;
                            current = new Arc();
                            if (p.X != side)
                                current.Nodes.Add(new StripClipper.Node(side, Interpolate(q, p, side), -1));
                        }
                        else if (previousInside && !inside && current != null)
                        {
                            double side = p.X < west ? west : east;
                            if (q.X != side)
                                current.Nodes.Add(new StripClipper.Node(side, Interpolate(q, p, side), -1));

                            Complete(arcs, current, west, east);
                            current = null;
                        }
                    }

                    if (inside && current != null)
                        current.Nodes.Add(p);
                }

                return arcs;
            }

            private static void Complete(List<Arc> arcs, Arc arc, double west, double east)
            {
                if (arc.Nodes.Count < 2)
                    return;

                bool onWest = true;
                bool onEast = true;
                foreach (StripClipper.Node node in arc.Nodes)
                {
                    onWest &= node.X == west;
                    onEast &= node.X == east;
                }

                if (!onWest && !onEast)
                    arcs.Add(arc);
            }

            private static double Interpolate(StripClipper.Node a, StripClipper.Node b, double x) =>
                b.X == a.X ? a.Y : a.Y + ((b.Y - a.Y) * (x - a.X) / (b.X - a.X));

            private static double Perimeter(StripClipper.Node node, double west, double width, double height)
            {
                double y = Math.Max(MinLat, Math.Min(MaxLat, node.Y));
                return node.X == west
                    ? (2.0 * width) + height + (MaxLat - y)
                    : width + (y - MinLat);
            }

            private static double CounterclockwiseDistance(double from, double to, double perimeter)
            {
                double distance = to - from;
                while (distance < 0.0)
                    distance += perimeter;
                return distance >= perimeter ? 0.0 : distance;
            }

            private static GeoJsonPosition ToPosition(StripClipper.Node node, IReadOnlyList<GeoJsonPosition> source, double shift)
            {
                double x = node.X + shift;
                if (node.Index >= 0 && Math.Abs(source[node.Index].Lon - x) <= 1e-9)
                    return source[node.Index];

                return GeoJsonPosition.Computed(x, node.Y);
            }

            private static void Append(List<GeoJsonPosition> ring, GeoJsonPosition position)
            {
                if (ring.Count == 0 || !ring[ring.Count - 1].Equals(position))
                    ring.Add(position);
            }
        }
    }
}
