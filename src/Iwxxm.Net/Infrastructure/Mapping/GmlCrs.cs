using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;
using Iwxxm.Net.Reports;

namespace Iwxxm.Net.Infrastructure.Mapping
{
    /// <summary>
    /// Coordinate reference system metadata of a GML geometry (<c>@srsName</c>, <c>@axisLabels</c>, <c>@srsDimension</c>)
    /// reduced to what the coordinate reader needs: the axis order of the horizontal coordinates and the dimension.
    /// </summary>
    /// <remarks>
    /// <para>Supported CRSs: WGS 84 geographic 2-D (<c>EPSG:4326</c>, default axis order latitude/longitude) and 3-D
    /// (<c>EPSG:4979</c>), and <c>OGC CRS84</c> (longitude/latitude). <c>@axisLabels</c>, when present, decides the axis
    /// order: <c>"Lat Long"</c> (IWXXM default) or <c>"Long Lat"</c> (swapped on read). A 3-D position keeps its
    /// horizontal coordinates; the third coordinate (height) is explicitly ignored because the Domain coordinates are
    /// 2-D.</para>
    /// <para>The identifier <c>http://www.opengis.net/def/ers/EPSG/0/4326</c> ("ers" instead of "crs") appears verbatim
    /// in several official WMO examples (<c>WAFS-Example.xml</c>, <c>qvaci-Example.xml</c>, <c>vona-A7-1.xml</c>) and is
    /// accepted as an alias of <c>EPSG:4326</c>. Any other CRS, axis label or dimension is a mapping error: coordinates
    /// are never read with a guessed axis order.</para>
    /// <para>When no ancestor-or-self of a geometry carries <c>@srsName</c>, the IWXXM default (<c>EPSG:4326</c>, "Lat
    /// Long", 2-D) applies.</para>
    /// </remarks>
    internal readonly struct GmlCrs
    {
        internal const string UnsupportedCrsCode = "ACL-CRS-UNSUPPORTED";
        internal const string UnsupportedAxisLabelsCode = "ACL-CRS-AXIS-LABELS-UNSUPPORTED";
        internal const string UnsupportedDimensionCode = "ACL-CRS-DIMENSION-UNSUPPORTED";

        private static readonly string[] LatitudeFirstCrs =
        {
            "http://www.opengis.net/def/crs/EPSG/0/4326",
            "https://www.opengis.net/def/crs/EPSG/0/4326",
            "urn:ogc:def:crs:EPSG::4326",
            "urn:ogc:def:crs:EPSG:6.6:4326",
            "EPSG:4326",
            "http://www.opengis.net/def/ers/EPSG/0/4326"
        };

        private static readonly string[] LatitudeFirst3DCrs =
        {
            "http://www.opengis.net/def/crs/EPSG/0/4979",
            "urn:ogc:def:crs:EPSG::4979",
            "EPSG:4979"
        };

        private static readonly string[] LongitudeFirstCrs =
        {
            "http://www.opengis.net/def/crs/OGC/1.3/CRS84",
            "urn:ogc:def:crs:OGC:1.3:CRS84",
            "urn:ogc:def:crs:OGC::CRS84"
        };

        /// <summary>The IWXXM default: <c>EPSG:4326</c>, latitude/longitude, 2-D.</summary>
        public static readonly GmlCrs Default = new GmlCrs(false, 2);

        private GmlCrs(bool isLongitudeFirst, int dimension)
        {
            IsLongitudeFirst = isLongitudeFirst;
            Dimension = dimension;
        }

        /// <summary>Gets a value indicating whether positions are written longitude first (<c>"Long Lat"</c>).</summary>
        public bool IsLongitudeFirst { get; }

        /// <summary>Number of coordinates per position (2 or 3).</summary>
        public int Dimension { get; }

        /// <summary>
        /// Resolves the CRS metadata of a geometry element: each attribute is taken from the nearest ancestor-or-self that
        /// carries it (the inheritance used by the IWXXM Schematron rules).
        /// </summary>
        public static GmlCrs? For(XElement geometry, string location, List<ValidationError> errors)
        {
            string? srsName = Inherited(geometry, "srsName");
            string? axisLabels = Inherited(geometry, "axisLabels");
            string? dimension = Inherited(geometry, "srsDimension");
            return Resolve(srsName, axisLabels, dimension, location, errors);
        }

        /// <summary>Resolves explicit attribute values (<c>null</c> = not reported).</summary>
        public static GmlCrs? Resolve(string? srsName, string? axisLabels, string? srsDimension, string location, List<ValidationError> errors)
        {
            bool defaultLongitudeFirst = false;
            int defaultDimension = 2;
            string? name = srsName?.Trim();
            if (!string.IsNullOrEmpty(name))
            {
                if (LatitudeFirstCrs.Contains(name, StringComparer.Ordinal))
                {
                    defaultDimension = 2;
                }
                else if (LatitudeFirst3DCrs.Contains(name, StringComparer.Ordinal))
                {
                    defaultDimension = 3;
                }
                else if (LongitudeFirstCrs.Contains(name, StringComparer.Ordinal))
                {
                    defaultLongitudeFirst = true;
                }
                else
                {
                    errors.Add(Error(UnsupportedCrsCode,
                        $"Unsupported coordinate reference system srsName='{name}' at '{location}'. Expected WGS 84 (EPSG:4326, EPSG:4979 or OGC CRS84); coordinates are not read with an assumed axis order.",
                        location));
                    return null;
                }
            }

            string[] labels = string.IsNullOrWhiteSpace(axisLabels)
                ? Array.Empty<string>()
                : axisLabels!.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

            int dimension = defaultDimension;
            if (!string.IsNullOrWhiteSpace(srsDimension))
            {
                if (!int.TryParse(srsDimension!.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out dimension) || (dimension != 2 && dimension != 3))
                {
                    errors.Add(Error(UnsupportedDimensionCode,
                        $"Unsupported srsDimension '{srsDimension.Trim()}' at '{location}'. Expected 2 (latitude/longitude) or 3 (latitude/longitude/height).",
                        location));
                    return null;
                }
            }
            else if (labels.Length == 2 || labels.Length == 3)
            {
                dimension = labels.Length;
            }

            bool longitudeFirst = defaultLongitudeFirst;
            if (labels.Length > 0)
            {
                if (labels.Length != dimension || !TryReadHorizontalOrder(labels, out longitudeFirst))
                {
                    errors.Add(Error(UnsupportedAxisLabelsCode,
                        $"Unsupported axisLabels '{axisLabels!.Trim()}' for srsDimension {dimension} at '{location}'. Expected \"Lat Long\" or \"Long Lat\" (plus a height axis in 3-D).",
                        location));
                    return null;
                }
            }

            return new GmlCrs(longitudeFirst, dimension);
        }

        private static bool TryReadHorizontalOrder(string[] labels, out bool longitudeFirst)
        {
            longitudeFirst = false;
            bool firstIsLat = IsLatitude(labels[0]);
            bool firstIsLon = IsLongitude(labels[0]);
            bool secondIsLat = IsLatitude(labels[1]);
            bool secondIsLon = IsLongitude(labels[1]);
            if (firstIsLat && secondIsLon)
                return true;

            if (firstIsLon && secondIsLat)
            {
                longitudeFirst = true;
                return true;
            }

            return false;
        }

        private static bool IsLatitude(string label) =>
            label.Equals("Lat", StringComparison.OrdinalIgnoreCase) || label.Equals("Latitude", StringComparison.OrdinalIgnoreCase);

        private static bool IsLongitude(string label) =>
            label.Equals("Long", StringComparison.OrdinalIgnoreCase) ||
            label.Equals("Lon", StringComparison.OrdinalIgnoreCase) ||
            label.Equals("Longitude", StringComparison.OrdinalIgnoreCase);

        private static string? Inherited(XElement element, string attributeName)
        {
            for (XElement? node = element; node != null; node = node.Parent)
            {
                XAttribute? attribute = node.Attribute(attributeName);
                if (attribute != null)
                    return attribute.Value;
            }

            return null;
        }

        private static ValidationError Error(string code, string message, string location) =>
            new ValidationError(code, message, ErrorCategory.DomainMapping, ValidationSeverity.Error, location);
    }
}
