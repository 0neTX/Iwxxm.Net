using System.Xml.Linq;

namespace Iwxxm.Net.Infrastructure.Mapping.Xml
{
    /// <summary>XML namespaces of the ICAO/WMO IWXXM 2025-2 suite and of the standards it imports.</summary>
    internal static class IwxxmXmlNames
    {
        public static readonly XNamespace IwxxmNs = "http://icao.int/iwxxm/2025-2";
        public static readonly XNamespace GmlNs = "http://www.opengis.net/gml/3.2";
        public static readonly XNamespace AixmNs = "http://www.aixm.aero/schema/5.1.1";
        public static readonly XNamespace MetceNs = "http://def.wmo.int/metce/2013";
        public static readonly XNamespace CollectNs = "http://def.wmo.int/collect/2014";
        public static readonly XNamespace XlinkNs = "http://www.w3.org/1999/xlink";
        public static readonly XNamespace XsiNs = "http://www.w3.org/2001/XMLSchema-instance";
    }
}
