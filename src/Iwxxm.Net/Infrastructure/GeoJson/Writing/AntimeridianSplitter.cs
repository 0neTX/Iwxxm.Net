using System;
using Iwxxm.Net.Reports;
using System.Collections.Generic;
using System.Linq;

namespace Iwxxm.Net.Infrastructure.GeoJson.Writing
{
    /// <summary>
    /// RFC 7946 §3.1.9 antimeridian handling for output geometries. Two consecutive positions whose longitudes differ by
    /// more than 180 degrees are joined by the short way, across the antimeridian. Such lines are cut at ±180 into a
    /// <c>MultiLineString</c>; such polygons are unwrapped and clipped into the [-180, 180] longitude strips, giving a
    /// <c>MultiPolygon</c>. A ring that wraps the whole globe (its unwrapped longitudes advance by a full turn: it
    /// encloses a pole) is written as the region between the ring and the pole on the side of its mean latitude: the
    /// ring is repeated every 360 degrees, clipped to the [-180, 180] strip and closed along the antimeridian and the
    /// pole, so a ring that leaves the strip and comes back (a lobe across the antimeridian) loses no vertex and may give
    /// several polygons (the one exception is a lobe with no area at all, such as a ring that runs along a pole out across the
    /// antimeridian and back: the pole is a single point, so no polygon can hold the spur, which is replaced by the cut, or stays a
    /// spike of a larger ring when the pieces are joined). A piece that has no area on the grid the coordinates are written on (see
    /// <see cref="HasNoArea"/>) is never written next to a piece that has one. A ring that crosses a strip border more than twice is clipped the same way, so its pieces are
    /// never joined by a zero-width bridge. Interior rings that lie entirely inside a cap are kept as its holes; an
    /// interior ring that cannot be represented that way (it crosses the antimeridian, or is not inside the cap) is
    /// dropped and counted, so the caller can report it explicitly.
    /// </summary>
    internal static class AntimeridianSplitter
    {
        /// <summary>Gets a value indicating whether a sequence crosses the antimeridian (an edge spans more than 180 degrees).</summary>
        public static bool Crosses(IReadOnlyList<GeoJsonPosition> positions)
        {
            for (int i = 1; i < positions.Count; i++)
            {
                if (IsCrossing(positions[i - 1], positions[i]))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// An edge crosses the antimeridian when its longitudes differ by more than 180 degrees, unless both ends lie on
        /// the antimeridian itself (an edge from -180 to +180 along a parallel is a full-globe band edge, e.g. the space
        /// weather latitude bands "W180-E180", and is kept as reported).
        /// </summary>
        private static bool IsCrossing(GeoJsonPosition a, GeoJsonPosition b) =>
            Math.Abs(b.Lon - a.Lon) > 180.0 && !(Math.Abs(a.Lon) == 180.0 && Math.Abs(b.Lon) == 180.0);

        /// <summary>
        /// Cuts a line at every antimeridian crossing. A vertex that lies exactly on ±180 is the cut position itself: it is not
        /// repeated, and a part that would have fewer than two distinct positions (the line only starts or ends on the
        /// antimeridian) is left out, so no part is a stub. The total length is unchanged.
        /// </summary>
        public static List<IReadOnlyList<GeoJsonPosition>> SplitLine(IReadOnlyList<GeoJsonPosition> line)
        {
            var parts = new List<IReadOnlyList<GeoJsonPosition>>();
            var current = new List<GeoJsonPosition> { line[0] };
            for (int i = 1; i < line.Count; i++)
            {
                GeoJsonPosition a = line[i - 1];
                GeoJsonPosition b = line[i];
                double delta = b.Lon - a.Lon;
                if (IsCrossing(a, b))
                {
                    double boundary = delta < 0 ? 180.0 : -180.0;            // a is east of the antimeridian when b wraps to the west
                    double unwrappedB = b.Lon + (delta < 0 ? 360.0 : -360.0);
                    double lat;
                    if (a.Lon == boundary)
                    {
                        lat = a.Lat;                                         // the line leaves from the antimeridian itself
                    }
                    else if (unwrappedB == boundary)
                    {
                        lat = b.Lat;                                         // the line ends on the antimeridian
                    }
                    else
                    {
                        double t = (boundary - a.Lon) / (unwrappedB - a.Lon);
                        lat = a.Lat + (t * (b.Lat - a.Lat));
                    }

                    AppendDistinct(current, GeoJsonPosition.Computed(boundary, lat));
                    if (HasTwoDistinctPositions(current))
                        parts.Add(current);

                    current = new List<GeoJsonPosition> { GeoJsonPosition.Computed(-boundary, lat) };
                    AppendDistinct(current, b);
                    continue;
                }

                current.Add(b);
            }

            if (HasTwoDistinctPositions(current) || parts.Count == 0)
                parts.Add(current);

            return parts;
        }

        private static void AppendDistinct(List<GeoJsonPosition> positions, GeoJsonPosition position)
        {
            if (positions.Count == 0 || !positions[positions.Count - 1].Equals(position))
                positions.Add(position);
        }

        private static bool HasTwoDistinctPositions(List<GeoJsonPosition> positions)
        {
            for (int i = 1; i < positions.Count; i++)
            {
                if (!positions[i].Equals(positions[0]))
                    return true;
            }

            return false;
        }

        /// <summary>Splits a polygon (exterior plus holes) that crosses the antimeridian into polygons inside [-180, 180].</summary>
        public static List<List<List<GeoJsonPosition>>> SplitPolygon(List<GeoJsonPosition> exterior, List<List<GeoJsonPosition>> holes, out bool isPolarCap, out int droppedInteriorRings)
        {
            isPolarCap = false;
            droppedInteriorRings = 0;
            List<double> unwrapped = Unwrap(exterior);
            double wrap = unwrapped[unwrapped.Count - 1] - unwrapped[0];
            if (Math.Abs(wrap) > 180.0)
            {
                // The ring goes once around the globe: it encloses a pole.
                isPolarCap = true;
                double meanLat = exterior.Average(p => p.Lat);
                List<List<GeoJsonPosition>> caps = PolarCaps(exterior, unwrapped, meanLat >= 0 ? 90.0 : -90.0);
                var polygons = new List<List<List<GeoJsonPosition>>>(caps.Count);
                foreach (List<GeoJsonPosition> cap in caps)
                    polygons.Add(new List<List<GeoJsonPosition>> { cap });

                foreach (List<GeoJsonPosition> hole in holes)
                {
                    List<List<GeoJsonPosition>>? owner = null;
                    if (hole.Count >= 4 && !Crosses(hole))
                        owner = polygons.FirstOrDefault(p => hole.All(v => IsInside(p[0], v)));

                    if (owner != null)
                        owner.Add(hole);
                    else
                        droppedInteriorRings++;
                }

                return WithoutPiecesOfNoArea(polygons);
            }

            double min = unwrapped.Min();
            double max = unwrapped.Max();
            var pieces = new List<List<List<GeoJsonPosition>>>();
            List<StripClipper.Node> ringNodes = RingNodes(exterior, unwrapped);
            var shell = new Shell(ringNodes);
            var shapes = new List<HoleShape>(holes.Count);
            foreach (List<GeoJsonPosition> hole in holes)
                shapes.Add(new HoleShape(hole, Unwrap(hole), (min + max) / 2.0));

            for (int k = (int)Math.Floor((min + 180.0) / 360.0); k <= (int)Math.Floor((max + 180.0) / 360.0); k++)
            {
                double west = -180.0 + (360.0 * k);
                double east = west + 360.0;
                double shift = -360.0 * k;

                // Interior rings that reach the strip border (they leave the strip, or run along its border) are cut out of the
                // exterior with the exterior's own arcs (a notch); the others are holes of the piece that contains them.
                var cuts = new List<(IReadOnlyList<StripClipper.Node> Chain, IReadOnlyList<GeoJsonPosition> Source)>();
                var bordering = new List<HoleShape>();
                var inside = new List<List<GeoJsonPosition>>();
                foreach (HoleShape shape in shapes)
                {
                    if (shape.Dropped)
                        continue;

                    List<List<StripClipper.Node>>? chains = shape.BorderChains(west, east);
                    if (chains != null)
                    {
                        var shapeCuts = new List<List<StripClipper.Node>>();
                        foreach (List<StripClipper.Node> chain in chains)
                        {
                            if (StripClipper.HasArcs(chain, west, east))
                                shapeCuts.Add(chain);
                        }

                        if (shapeCuts.Count == 0)
                            continue;

                        // A notch is cut with the arcs of the exterior: a ring that is not inside the exterior has no arcs to be cut
                        // from and would be closed through the corners of the strip (a world-sized ring), so it is left out and counted
                        // once (the verdict is kept for the other strips it crosses).
                        if (!shell.Contains(shape.Nodes))
                        {
                            shape.Dropped = true;
                            droppedInteriorRings++;
                            continue;
                        }

                        foreach (List<StripClipper.Node> chain in shapeCuts)
                            cuts.Add((chain, shape.Positions));

                        bordering.Add(shape);
                        continue;
                    }

                    List<GeoJsonPosition>? clippedHole = Clip(shape.Positions, shape.Lons, west, east, shift);
                    if (clippedHole != null)
                        inside.Add(clippedHole);
                }

                // More than one piece inside the strip, or a notch to cut: assemble them properly (Sutherland-Hodgman would join
                // pieces with zero-width bridges along the strip border and cannot cut a notch).
                var orphans = new List<IReadOnlyList<GeoJsonPosition>>();
                List<List<GeoJsonPosition>> separate = ClipSeparatePieces(ringNodes, exterior, west, east, shift, cuts, orphans);
                foreach (IReadOnlyList<GeoJsonPosition> orphan in orphans)
                {
                    // A notch that no arc of the exterior surrounds (cannot happen for a hole inside the exterior): counted, not written.
                    HoleShape orphaned = shapes.First(h => ReferenceEquals(h.Positions, orphan));
                    if (!orphaned.Dropped)
                    {
                        orphaned.Dropped = true;
                        droppedInteriorRings++;
                    }
                }

                if (separate.Count > 1 || (separate.Count == 1 && cuts.Count > 0))
                {
                    var multi = separate.Select(r => new List<List<GeoJsonPosition>> { r }).ToList();
                    PieceIndex? index = inside.Count > 0 && multi.Count > 1 ? new PieceIndex(multi) : null;
                    foreach (List<GeoJsonPosition> clippedHole in inside)
                    {
                        List<List<GeoJsonPosition>>? owner = index == null ? multi[0] : index.OwnerOf(clippedHole);
                        if (owner != null)
                            owner.Add(clippedHole);
                        else
                            droppedInteriorRings++;
                    }

                    pieces.AddRange(multi);
                    continue;
                }

                List<GeoJsonPosition>? clipped = Clip(exterior, unwrapped, west, east, shift);
                if (clipped == null)
                    continue;

                var rings = new List<List<GeoJsonPosition>> { clipped };
                rings.AddRange(inside);

                // Cannot happen for a valid surface (an interior ring that reaches the border lies inside the exterior, which then has
                // arcs in the strip); kept so that such a ring is never lost: it is written as a plain hole.
                foreach (HoleShape shape in bordering)
                {
                    if (shape.Dropped)
                        continue;

                    List<GeoJsonPosition>? clippedHole = Clip(shape.Positions, shape.Lons, west, east, shift);
                    if (clippedHole != null)
                        rings.Add(clippedHole);
                }

                pieces.Add(rings);
            }

            return WithoutPiecesOfNoArea(pieces);
        }

        /// <summary>
        /// An interior ring at its unwrapped longitudes (shifted by whole turns to lie next to the exterior), oriented clockwise
        /// (the polygon is on the left of its direction of travel), able to describe how it meets a strip.
        /// </summary>
        private sealed class HoleShape
        {
            public HoleShape(List<GeoJsonPosition> positions, List<double> lons, double exteriorCentre)
            {
                Positions = positions;
                double turns = 360.0 * Math.Round((exteriorCentre - lons.Average()) / 360.0);
                Lons = lons.Select(l => l + turns).ToList();
                List<StripClipper.Node> nodes = RingNodes(positions, Lons);
                double area = 0.0;
                for (int i = 0, j = nodes.Count - 1; i < nodes.Count; j = i++)
                    area += (nodes[j].X * nodes[i].Y) - (nodes[i].X * nodes[j].Y);

                if (area > 0.0)
                    nodes.Reverse();

                Nodes = nodes;
            }

            public List<GeoJsonPosition> Positions { get; }

            /// <summary>Gets or sets a value indicating whether the ring was left out (and counted) because it is not inside the exterior.</summary>
            public bool Dropped { get; set; }

            public List<double> Lons { get; }

            public List<StripClipper.Node> Nodes { get; }

            /// <summary>
            /// The chains (each starts and ends outside the strip, see <see cref="StripClipper"/>) that describe the part of the ring
            /// inside the strip [west, east] when the ring leaves the strip or has an edge on its border; <c>null</c> when it is
            /// entirely inside the closed strip without an edge on the border (a plain hole; touching the border at single
            /// vertices is valid for a hole). A ring with an edge on the border is cut at its border edges: every stretch
            /// between two of them is a chain, bracketed by two computed vertices outside the strip.
            /// </summary>
            public List<List<StripClipper.Node>>? BorderChains(double west, double east)
            {
                int n = Nodes.Count;
                int outside = Nodes.FindIndex(v => v.X < west || v.X > east);
                if (outside >= 0)
                {
                    var chain = new List<StripClipper.Node>(n + 1);
                    for (int i = 0; i <= n; i++)
                        chain.Add(Nodes[(outside + i) % n]);

                    return new List<List<StripClipper.Node>> { chain };
                }

                bool OnBorderEdge(int edge)
                {
                    StripClipper.Node a = Nodes[edge];
                    StripClipper.Node b = Nodes[(edge + 1) % n];
                    return a.X == b.X && (a.X == west || a.X == east);
                }

                int firstBorderEdge = -1;
                for (int e = 0; e < n && firstBorderEdge < 0; e++)
                {
                    if (OnBorderEdge(e))
                        firstBorderEdge = e;
                }

                if (firstBorderEdge < 0)
                    return null;

                var chains = new List<List<StripClipper.Node>>();
                var run = new List<StripClipper.Node> { Nodes[(firstBorderEdge + 1) % n] };
                for (int step = 1; step < n; step++)
                {
                    int edge = (firstBorderEdge + step) % n;
                    StripClipper.Node end = Nodes[(edge + 1) % n];
                    if (!OnBorderEdge(edge))
                    {
                        run.Add(end);
                        continue;
                    }

                    chains.Add(Bracket(run, west));
                    run = new List<StripClipper.Node> { end };
                }

                chains.Add(Bracket(run, west));
                return chains;
            }

            // Adds a vertex outside the strip, beside the border the end belongs to, before and after a stretch of the ring.
            private static List<StripClipper.Node> Bracket(List<StripClipper.Node> run, double west)
            {
                StripClipper.Node first = run[0];
                StripClipper.Node last = run[run.Count - 1];
                var chain = new List<StripClipper.Node>(run.Count + 2)
                {
                    new StripClipper.Node(first.X == west ? west - 1.0 : first.X + 1.0, first.Y, -1),
                };
                chain.AddRange(run);
                chain.Add(new StripClipper.Node(last.X == west ? west - 1.0 : last.X + 1.0, last.Y, -1));
                return chain;
            }
        }

        /// <summary>
        /// The exterior ring at its unwrapped longitudes, able to tell whether an interior ring is inside it. The test is made once
        /// per interior ring, in the unwrapped plane and against the whole exterior (not against the part in a strip): the answer does
        /// not depend on the strip, so a ring that crosses several strips gets one verdict.
        /// <para>
        /// An interior ring is inside when none of its vertices is strictly outside: a vertex on the exterior boundary is allowed (a
        /// hole may touch its shell at a point), any other vertex must satisfy the even-odd rule. The bounding box of the exterior
        /// rejects the common case (a ring beside or beyond the shell) in O(m) for the m vertices of the ring. The remaining vertices
        /// cost O(n) each against the n exterior edges; above <see cref="VertexBudget"/> edge tests the vertices are spread evenly
        /// along the ring and only a sample is tested (at least 16), which keeps a very large ring linear in the document size.
        /// A ring whose vertices are all inside but whose edges leave a concave exterior between two vertices is not detected.
        /// </para>
        /// </summary>
        private sealed class Shell
        {
            private const long VertexBudget = 4_000_000;
            private const int MinimumSamples = 16;
            private readonly List<StripClipper.Node> _nodes;
            private readonly double _minX = double.PositiveInfinity;
            private readonly double _maxX = double.NegativeInfinity;
            private readonly double _minY = double.PositiveInfinity;
            private readonly double _maxY = double.NegativeInfinity;

            public Shell(List<StripClipper.Node> nodes)
            {
                _nodes = nodes;
                foreach (StripClipper.Node node in nodes)
                {
                    _minX = Math.Min(_minX, node.X);
                    _maxX = Math.Max(_maxX, node.X);
                    _minY = Math.Min(_minY, node.Y);
                    _maxY = Math.Max(_maxY, node.Y);
                }
            }

            public bool Contains(IReadOnlyList<StripClipper.Node> ring)
            {
                foreach (StripClipper.Node node in ring)
                {
                    if (node.X < _minX || node.X > _maxX || node.Y < _minY || node.Y > _maxY)
                        return false;
                }

                long samples = Math.Min(ring.Count, Math.Max(MinimumSamples, VertexBudget / Math.Max(1, _nodes.Count)));
                for (long s = 0; s < samples; s++)
                {
                    if (!IsInsideOrOnBoundary(ring[(int)(s * ring.Count / samples)]))
                        return false;
                }

                return true;
            }

            private bool IsInsideOrOnBoundary(StripClipper.Node point)
            {
                bool inside = false;
                for (int i = 0, j = _nodes.Count - 1; i < _nodes.Count; j = i++)
                {
                    StripClipper.Node a = _nodes[i];
                    StripClipper.Node b = _nodes[j];
                    double cross = ((b.X - a.X) * (point.Y - a.Y)) - ((b.Y - a.Y) * (point.X - a.X));
                    if (cross == 0.0 &&
                        point.X >= Math.Min(a.X, b.X) && point.X <= Math.Max(a.X, b.X) &&
                        point.Y >= Math.Min(a.Y, b.Y) && point.Y <= Math.Max(a.Y, b.Y))
                    {
                        return true;
                    }

                    if ((a.Y > point.Y) != (b.Y > point.Y) &&
                        point.X < ((b.X - a.X) * (point.Y - a.Y) / (b.Y - a.Y)) + a.X)
                    {
                        inside = !inside;
                    }
                }

                return inside;
            }
        }

        private readonly struct Box
        {
            public Box(IReadOnlyList<GeoJsonPosition> ring)
            {
                MinLon = double.PositiveInfinity;
                MaxLon = double.NegativeInfinity;
                MinLat = double.PositiveInfinity;
                MaxLat = double.NegativeInfinity;
                foreach (GeoJsonPosition p in ring)
                {
                    MinLon = Math.Min(MinLon, p.Lon);
                    MaxLon = Math.Max(MaxLon, p.Lon);
                    MinLat = Math.Min(MinLat, p.Lat);
                    MaxLat = Math.Max(MaxLat, p.Lat);
                }
            }

            public double MinLon { get; }

            public double MaxLon { get; }

            public double MinLat { get; }

            public double MaxLat { get; }

            public bool Intersects(Box other) =>
                MinLon <= other.MaxLon && other.MinLon <= MaxLon && MinLat <= other.MaxLat && other.MinLat <= MaxLat;

            public bool Contains(GeoJsonPosition p) => p.Lon >= MinLon && p.Lon <= MaxLon && p.Lat >= MinLat && p.Lat <= MaxLat;
        }

        /// <summary>
        /// The pieces of a strip with their bounding boxes, sorted by minimum latitude with a running maximum of the maximum latitude,
        /// so that the pieces whose box can meet a given box are found without looking at the ones that lie entirely to the north or
        /// south (the pieces of a comb or a serpentine are stacked in latitude). The answer is exact: it only discards pieces whose box
        /// does not meet the box of the hole, which cannot contain any of its vertices.
        /// </summary>
        private sealed class PieceIndex
        {
            private readonly List<List<List<GeoJsonPosition>>> _pieces;
            private readonly Box[] _boxes;
            private readonly int[] _byMinLat;
            private readonly double[] _runningMaxLat;

            public PieceIndex(List<List<List<GeoJsonPosition>>> pieces)
            {
                _pieces = pieces;
                _boxes = pieces.Select(p => new Box(p[0])).ToArray();
                _byMinLat = Enumerable.Range(0, pieces.Count).ToArray();
                Array.Sort(_byMinLat, (x, y) =>
                {
                    int byLat = _boxes[x].MinLat.CompareTo(_boxes[y].MinLat);
                    return byLat != 0 ? byLat : x.CompareTo(y);
                });

                _runningMaxLat = new double[pieces.Count];
                double max = double.NegativeInfinity;
                for (int i = 0; i < _byMinLat.Length; i++)
                {
                    max = Math.Max(max, _boxes[_byMinLat[i]].MaxLat);
                    _runningMaxLat[i] = max;
                }
            }

            /// <summary>
            /// The piece that holds a plain hole: a few vertices of the hole spread along its ring, skipping the ones on the strip
            /// border (±180; a vertex on it has no reliable even-odd answer), are tested against the exterior of each piece whose
            /// box meets the box of the hole, and the piece that contains most of them wins, the first in list order on a tie.
            /// <c>null</c> when none contains one.
            /// </summary>
            public List<List<GeoJsonPosition>>? OwnerOf(List<GeoJsonPosition> hole)
            {
                const int Samples = 5;
                var samples = new List<GeoJsonPosition>();
                for (int i = 0; i < hole.Count - 1; i++)
                {
                    if (Math.Abs(hole[i].Lon) < 180.0)
                        samples.Add(hole[i]);
                }

                int stride = Math.Max(1, samples.Count / Samples);
                var picked = new List<GeoJsonPosition>(Samples);
                for (int s = 0; s < samples.Count && picked.Count < Samples; s += stride)
                    picked.Add(samples[s]);

                var holeBox = new Box(hole);

                // Pieces that can meet the box of the hole: those with minimum latitude up to its top, scanned from the north while
                // some piece to the south still reaches its bottom.
                int low = 0;
                int high = _byMinLat.Length;
                while (low < high)
                {
                    int middle = low + ((high - low) / 2);
                    if (_boxes[_byMinLat[middle]].MinLat <= holeBox.MaxLat)
                        low = middle + 1;
                    else
                        high = middle;
                }

                var candidates = new List<int>();
                for (int i = low - 1; i >= 0 && _runningMaxLat[i] >= holeBox.MinLat; i--)
                {
                    int piece = _byMinLat[i];
                    if (_boxes[piece].Intersects(holeBox))
                        candidates.Add(piece);
                }

                candidates.Sort();
                List<List<GeoJsonPosition>>? owner = null;
                int bestVotes = 0;
                foreach (int piece in candidates)
                {
                    int votes = 0;
                    foreach (GeoJsonPosition v in picked)
                    {
                        if (_boxes[piece].Contains(v) && IsInside(_pieces[piece][0], v))
                            votes++;
                    }

                    if (votes > bestVotes)
                    {
                        bestVotes = votes;
                        owner = _pieces[piece];
                    }
                }

                return owner;
            }
        }

        /// <summary>Nodes of a closed ring at their unwrapped longitudes (the closing vertex is not repeated).</summary>
        private static List<StripClipper.Node> RingNodes(IReadOnlyList<GeoJsonPosition> ring, IReadOnlyList<double> unwrapped)
        {
            int count = ring.Count > 1 && ring[0].Equals(ring[ring.Count - 1]) ? ring.Count - 1 : ring.Count;
            var nodes = new List<StripClipper.Node>(count);
            for (int i = 0; i < count; i++)
                nodes.Add(new StripClipper.Node(unwrapped[i], ring[i].Lat, i));

            return nodes;
        }

        /// <summary>
        /// The pieces of an unwrapped closed ring inside the strip [west, east], assembled so that the region stays on the left
        /// of the boundary (a clockwise ring is traversed backwards).
        /// </summary>
        private static List<List<GeoJsonPosition>> ClipSeparatePieces(
            List<StripClipper.Node> nodes,
            IReadOnlyList<GeoJsonPosition> source,
            double west,
            double east,
            double shift,
            IReadOnlyList<(IReadOnlyList<StripClipper.Node> Chain, IReadOnlyList<GeoJsonPosition> Source)> cuts,
            List<IReadOnlyList<GeoJsonPosition>> orphans)
        {
            double area = 0.0;
            for (int i = 0, j = nodes.Count - 1; i < nodes.Count; j = i++)
                area += (nodes[j].X * nodes[i].Y) - (nodes[i].X * nodes[j].Y);

            var ordered = new List<StripClipper.Node>(nodes);
            if (area < 0.0)
                ordered.Reverse();

            // Start at a vertex outside the strip so that every piece is a complete arc.
            int start = ordered.FindIndex(n => n.X < west || n.X > east);
            if (start < 0)
            {
                // No arc of the exterior in this strip: nothing for a notch to be cut from.
                foreach ((IReadOnlyList<StripClipper.Node> _, IReadOnlyList<GeoJsonPosition> cutSource) in cuts)
                {
                    if (!orphans.Contains(cutSource))
                        orphans.Add(cutSource);
                }

                return new List<List<GeoJsonPosition>>();
            }

            var chain = new List<StripClipper.Node>(ordered.Count + 1);
            for (int i = 0; i <= ordered.Count; i++)
                chain.Add(ordered[(start + i) % ordered.Count]);

            if (cuts.Count == 0)
                return StripClipper.Clip(chain, source, west, east, shift);

            var all = new List<(IReadOnlyList<StripClipper.Node> Chain, IReadOnlyList<GeoJsonPosition> Source)>(cuts.Count + 1) { (chain, source) };
            all.AddRange(cuts);
            return StripClipper.Clip(all, west, east, shift, orphans);
        }

        /// <summary>
        /// The polygons of the region between a pole-enclosing ring and the pole: the unwrapped ring is repeated every 360
        /// degrees, clipped to [-180, 180] and closed along the antimeridian and the pole. Every vertex of the ring is kept.
        /// </summary>
        private static List<List<GeoJsonPosition>> PolarCaps(IReadOnlyList<GeoJsonPosition> ring, IReadOnlyList<double> unwrapped, double poleLat)
        {
            List<StripClipper.Node> nodes = RingNodes(ring, unwrapped);
            double turn = unwrapped[unwrapped.Count - 1] - unwrapped[0];
            double period = turn > 0 ? 360.0 : -360.0;
            double minLon = nodes.Min(n => n.X);
            double maxLon = nodes.Max(n => n.X);

            // Repetitions (shift = j * period) that reach the strip, plus one on each side so that the chain starts and ends outside it.
            int low = (int)Math.Ceiling((-180.0 - maxLon) / 360.0) - 1;
            int high = (int)Math.Floor((180.0 - minLon) / 360.0) + 1;
            var chain = new List<StripClipper.Node>(nodes.Count * (high - low + 1));
            for (int j = turn > 0 ? low : -high; j <= (turn > 0 ? high : -low); j++)
            {
                double shift = j * period;
                foreach (StripClipper.Node node in nodes)
                    chain.Add(new StripClipper.Node(node.X + shift, node.Y, node.Index));
            }

            // The region (towards the pole) must be on the left of the direction of travel: east with the north pole, west with the south pole.
            if ((poleLat > 0) != (turn > 0))
                chain.Reverse();

            List<List<GeoJsonPosition>> caps = StripClipper.Clip(chain, ring, -180.0, 180.0, 0.0);
            if (caps.Count == 0)
            {
                // Degenerate input (for example every vertex on the antimeridian): fall back to the single-turn cap.
                caps.Add(PolarCap(unwrapped, ring.Select(p => p.Lat).ToList(), poleLat));
            }

            return caps;
        }

        /// <summary>Builds the ring of a pole-enclosing boundary from its unwrapped longitudes.</summary>
        internal static List<GeoJsonPosition> PolarCap(IReadOnlyList<double> unwrappedLons, IReadOnlyList<double> lats, double poleLat)
        {
            int n = unwrappedLons.Count;
            var lons = unwrappedLons.ToArray();
            var latitudes = lats.ToArray();
            if (lons[n - 1] < lons[0])
            {
                Array.Reverse(lons);
                Array.Reverse(latitudes);
            }

            double turn = lons[n - 1] - lons[0];
            double shift = -360.0 * Math.Ceiling((lons[0] + 180.0) / 360.0);
            var extendedLon = new List<double>(2 * n);
            var extendedLat = new List<double>(2 * n);
            for (int copy = 0; copy < 2; copy++)
            {
                for (int k = copy == 0 ? 0 : 1; k < n; k++)
                {
                    extendedLon.Add(lons[k] + shift + (copy * turn));
                    extendedLat.Add(latitudes[k]);
                }
            }

            var ring = new List<GeoJsonPosition>(n + 6);
            for (int k = 1; k < extendedLon.Count; k++)
            {
                double a = extendedLon[k - 1];
                double b = extendedLon[k];
                if (a <= -180.0 && b > -180.0)
                    ring.Add(GeoJsonPosition.Computed(-180.0, Interpolate(a, extendedLat[k - 1], b, extendedLat[k], -180.0)));

                if (b > -180.0 && b < 180.0)
                    ring.Add(GeoJsonPosition.Computed(b, extendedLat[k]));

                if (a < 180.0 && b >= 180.0)
                {
                    ring.Add(GeoJsonPosition.Computed(180.0, Interpolate(a, extendedLat[k - 1], b, extendedLat[k], 180.0)));
                    break;
                }
            }

            ring.Add(new GeoJsonPosition(180.0, poleLat));
            ring.Add(new GeoJsonPosition(-180.0, poleLat));
            ring.Add(ring[0]);
            return ring;
        }

        /// <summary>Even-odd point-in-ring test in the longitude/latitude plane.</summary>
        private static bool IsInside(IReadOnlyList<GeoJsonPosition> ring, GeoJsonPosition point)
        {
            bool inside = false;
            for (int i = 0, j = ring.Count - 1; i < ring.Count; j = i++)
            {
                GeoJsonPosition a = ring[i];
                GeoJsonPosition b = ring[j];
                if ((a.Lat > point.Lat) != (b.Lat > point.Lat) &&
                    point.Lon < ((b.Lon - a.Lon) * (point.Lat - a.Lat) / (b.Lat - a.Lat)) + a.Lon)
                {
                    inside = !inside;
                }
            }

            return inside;
        }

        private static List<double> Unwrap(IReadOnlyList<GeoJsonPosition> ring)
        {
            // Longitude plus a whole number of turns, so a position on the antimeridian stays exactly at ±180 (+ k * 360).
            var lons = new List<double>(ring.Count) { ring[0].Lon };
            int turns = 0;
            for (int i = 1; i < ring.Count; i++)
            {
                if (IsCrossing(ring[i - 1], ring[i]))
                {
                    double delta = ring[i].Lon - ring[i - 1].Lon;
                    while (delta > 180.0) { delta -= 360.0; turns--; }
                    while (delta < -180.0) { delta += 360.0; turns++; }
                }

                lons.Add(ring[i].Lon + (360.0 * turns));
            }

            return lons;
        }

        /// <summary>Sutherland–Hodgman clipping of an unwrapped ring against the strip [west, east], shifted back by <paramref name="shift"/>.</summary>
        private static List<GeoJsonPosition>? Clip(IReadOnlyList<GeoJsonPosition> ring, IReadOnlyList<double> lons, double west, double east, double shift)
        {
            var points = new List<(double Lon, double Lat)>(ring.Count);
            for (int i = 0; i < ring.Count; i++)
                points.Add((lons[i], ring[i].Lat));

            if (points.Count > 1 && points[0].Lon == points[points.Count - 1].Lon && points[0].Lat == points[points.Count - 1].Lat)
                points.RemoveAt(points.Count - 1);

            points = ClipHalfPlane(points, west, keepGreater: true);
            points = ClipHalfPlane(points, east, keepGreater: false);
            if (points.Count < 3)
                return null;

            var result = points.Select(p => GeoJsonPosition.Computed(p.Lon + shift, p.Lat)).ToList();
            result.Add(result[0]);
            return HasNoArea(result) ? null : result;
        }

        private static List<(double Lon, double Lat)> ClipHalfPlane(List<(double Lon, double Lat)> input, double x, bool keepGreater)
        {
            var output = new List<(double Lon, double Lat)>(input.Count + 4);
            if (input.Count == 0)
                return output;

            bool Inside((double Lon, double Lat) p) => keepGreater ? p.Lon >= x : p.Lon <= x;
            (double Lon, double Lat) previous = input[input.Count - 1];
            foreach ((double Lon, double Lat) current in input)
            {
                bool currentInside = Inside(current);
                bool previousInside = Inside(previous);
                if (currentInside != previousInside)
                    output.Add((x, Interpolate(previous.Lon, previous.Lat, current.Lon, current.Lat, x)));
                if (currentInside)
                    output.Add(current);
                previous = current;
            }

            return output;
        }

        /// <summary>
        /// Gets a value indicating whether a closed ring has no area at the precision it is written at: on the grid of
        /// <see cref="GeoCoordinate.Precision"/> degrees all its positions lie on one line (a spur along a pole, a ring along a strip
        /// border, a lobe cut down to a line). The test is made on the grid positions themselves, in exact arithmetic, so it has no
        /// tolerance to choose and no rounding error to be mistaken for an area: a ring that has any area on the grid, however small,
        /// is a polygon; a ring that has none is not (a polygon of zero area is not valid, RFC 7946 section 3.1.6).
        /// </summary>
        internal static bool HasNoArea(IReadOnlyList<GeoJsonPosition> ring)
        {
            decimal twiceArea = 0m;
            long x0 = Cell(ring[0].Lon);
            long y0 = Cell(ring[0].Lat);
            for (int i = 1; i < ring.Count - 1; i++)
            {
                decimal ax = Cell(ring[i].Lon) - x0;
                decimal ay = Cell(ring[i].Lat) - y0;
                decimal bx = Cell(ring[i + 1].Lon) - x0;
                decimal by = Cell(ring[i + 1].Lat) - y0;
                twiceArea += (ax * by) - (bx * ay);
            }

            return twiceArea == 0m;
        }

        private static long Cell(double degrees) => (long)Math.Round(degrees / GeoCoordinate.Precision);

        /// <summary>
        /// Leaves out the pieces that have no area on the output grid (and the interior rings that have none), which every way of
        /// assembling pieces can produce from a spur of zero width. When no piece has an area the source ring itself has none: it is
        /// returned as it is.
        /// </summary>
        private static List<List<List<GeoJsonPosition>>> WithoutPiecesOfNoArea(List<List<List<GeoJsonPosition>>> pieces)
        {
            if (pieces.All(p => HasNoArea(p[0])))
                return pieces;

            var kept = new List<List<List<GeoJsonPosition>>>(pieces.Count);
            foreach (List<List<GeoJsonPosition>> piece in pieces)
            {
                if (HasNoArea(piece[0]))
                    continue;

                List<GeoJsonPosition> exterior = piece[0];
                piece.RemoveAll(ring => !ReferenceEquals(ring, exterior) && HasNoArea(ring));
                kept.Add(piece);
            }

            return kept;
        }

        private static double Interpolate(double lonA, double latA, double lonB, double latB, double lon) =>
            lonB == lonA ? latA : latA + ((latB - latA) * (lon - lonA) / (lonB - lonA));
    }
}
