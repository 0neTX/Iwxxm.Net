using System;
using System.Collections.Generic;

namespace Iwxxm.Net.Export.GeoPackage.Model
{
    /// <summary>The geometry kinds a GeoPackage feature table of this package stores (OGC simple features, 2D).</summary>
    internal enum GeoPackageGeometryKind
    {
        /// <summary>A single position (table <c>iwxxm_points</c>).</summary>
        Point,

        /// <summary>One or more lines (table <c>iwxxm_lines</c>); a line string is stored as a one-member multi line string.</summary>
        MultiLineString,

        /// <summary>One or more polygons (table <c>iwxxm_polygons</c>); a polygon is stored as a one-member multi polygon.</summary>
        MultiPolygon
    }

    /// <summary>A 2D position in the coordinate order of the SRS 4326 GeoPackage: <c>X</c> is the longitude, <c>Y</c> the latitude (degrees).</summary>
    internal readonly struct GeoPackagePosition
    {
        public GeoPackagePosition(double x, double y)
        {
            X = x;
            Y = y;
        }

        public double X { get; }

        public double Y { get; }
    }

    /// <summary>The plain minimum and maximum of the x and y values of a geometry (the layout of the GeoPackage envelope).</summary>
    internal readonly struct GeoPackageEnvelope
    {
        public GeoPackageEnvelope(double minX, double maxX, double minY, double maxY)
        {
            MinX = minX;
            MaxX = maxX;
            MinY = minY;
            MaxY = maxY;
        }

        public double MinX { get; }

        public double MaxX { get; }

        public double MinY { get; }

        public double MaxY { get; }
    }

    /// <summary>
    /// A geometry as a GeoPackage stores it, independent of the core types so that the binary encoder works on it
    /// directly. The coordinates are exactly those the GeoJSON writer of the core produced: the right-hand-rule ring
    /// orientation, the antimeridian split and the polar caps are already applied and nothing is re-oriented or
    /// re-split here.
    /// </summary>
    /// <remarks>
    /// The model does not validate its content (the encoder does), so that invalid shapes can be built to test it.
    /// </remarks>
    internal sealed class GeoPackageGeometry
    {
        private GeoPackageGeometry(
            GeoPackageGeometryKind kind,
            GeoPackagePosition point,
            IReadOnlyList<IReadOnlyList<GeoPackagePosition>>? lines,
            IReadOnlyList<IReadOnlyList<IReadOnlyList<GeoPackagePosition>>>? polygons)
        {
            Kind = kind;
            Point = point;
            Lines = lines;
            Polygons = polygons;
        }

        public GeoPackageGeometryKind Kind { get; }

        /// <summary>The position of a <see cref="GeoPackageGeometryKind.Point"/>; <c>default</c> for the other kinds.</summary>
        public GeoPackagePosition Point { get; }

        /// <summary>The lines of a <see cref="GeoPackageGeometryKind.MultiLineString"/>; <c>null</c> for the other kinds.</summary>
        public IReadOnlyList<IReadOnlyList<GeoPackagePosition>>? Lines { get; }

        /// <summary>
        /// The polygons of a <see cref="GeoPackageGeometryKind.MultiPolygon"/>, each as its rings (the exterior ring first,
        /// then the holes), every ring closed; <c>null</c> for the other kinds.
        /// </summary>
        public IReadOnlyList<IReadOnlyList<IReadOnlyList<GeoPackagePosition>>>? Polygons { get; }

        public static GeoPackageGeometry ForPoint(GeoPackagePosition position) =>
            new GeoPackageGeometry(GeoPackageGeometryKind.Point, position, null, null);

        public static GeoPackageGeometry ForMultiLineString(IReadOnlyList<IReadOnlyList<GeoPackagePosition>> lines) =>
            new GeoPackageGeometry(GeoPackageGeometryKind.MultiLineString, default, lines ?? throw new ArgumentNullException(nameof(lines)), null);

        public static GeoPackageGeometry ForMultiPolygon(IReadOnlyList<IReadOnlyList<IReadOnlyList<GeoPackagePosition>>> polygons) =>
            new GeoPackageGeometry(GeoPackageGeometryKind.MultiPolygon, default, null, polygons ?? throw new ArgumentNullException(nameof(polygons)));

        /// <summary>
        /// The plain minimum and maximum of x and y over every position of the geometry, or <c>null</c> for a geometry
        /// without any position. It is deliberately not the GeoJSON bounding box: a geometry split at the antimeridian
        /// has no "west greater than east" box here, only the numeric extremes of the written coordinates.
        /// </summary>
        public GeoPackageEnvelope? Envelope()
        {
            bool any = false;
            double minX = 0.0, maxX = 0.0, minY = 0.0, maxY = 0.0;

            void Include(GeoPackagePosition p)
            {
                if (!any)
                {
                    minX = maxX = p.X;
                    minY = maxY = p.Y;
                    any = true;
                    return;
                }

                if (p.X < minX) minX = p.X;
                if (p.X > maxX) maxX = p.X;
                if (p.Y < minY) minY = p.Y;
                if (p.Y > maxY) maxY = p.Y;
            }

            switch (Kind)
            {
                case GeoPackageGeometryKind.Point:
                    Include(Point);
                    break;

                case GeoPackageGeometryKind.MultiLineString:
                    foreach (IReadOnlyList<GeoPackagePosition> line in Lines!)
                        foreach (GeoPackagePosition p in line)
                            Include(p);
                    break;

                case GeoPackageGeometryKind.MultiPolygon:
                    foreach (IReadOnlyList<IReadOnlyList<GeoPackagePosition>> polygon in Polygons!)
                        foreach (IReadOnlyList<GeoPackagePosition> ring in polygon)
                            foreach (GeoPackagePosition p in ring)
                                Include(p);
                    break;

                default:
                    throw new InvalidOperationException($"Unsupported geometry kind {Kind}.");
            }

            return any ? new GeoPackageEnvelope(minX, maxX, minY, maxY) : (GeoPackageEnvelope?)null;
        }
    }
}
