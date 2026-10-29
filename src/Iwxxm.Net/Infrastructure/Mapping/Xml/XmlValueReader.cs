using System;
using System.Collections.Generic;
using System.Xml.Linq;
using static Iwxxm.Net.Infrastructure.Mapping.Xml.IwxxmXmlNames;

namespace Iwxxm.Net.Infrastructure.Mapping.Xml
{
    /// <summary>Reads scalar values (identifiers, text, booleans, <c>xlink:href</c> codes) from LINQ to XML elements.</summary>
    internal static class XmlValueReader
    {
        public static string GetGmlId(XElement elem)
        {
            return elem.Attribute(GmlNs + "id")?.Value ?? string.Empty;
        }

        /// <summary>
        /// Returns the trimmed text of an element, or <c>null</c> when the element is absent, <c>xsi:nil</c>, carries a
        /// <c>nilReason</c> without content, or is empty (a nil string is never turned into an empty string).
        /// </summary>
        public static string? GetText(XElement? elem)
        {
            if (elem == null || IsXsiNil(elem))
                return null;

            string value = elem.Value.Trim();
            return value.Length == 0 ? null : value;
        }

        public static bool IsXsiNil(XElement elem)
        {
            string? nil = elem.Attribute(XsiNs + "nil")?.Value;
            return string.Equals(nil, "true", StringComparison.Ordinal) || string.Equals(nil, "1", StringComparison.Ordinal);
        }

        public static bool ParseBoolAttribute(XElement elem, string attrName)
        {
            string? val = elem.Attribute(attrName)?.Value;
            return string.Equals(val, "true", StringComparison.OrdinalIgnoreCase) || val == "1";
        }

        /// <summary>Returns the <c>xlink:href</c> of an element (a code-list reference), or <c>null</c>.</summary>
        public static string? GetHref(XElement? elem) => elem?.Attribute(XlinkNs + "href")?.Value;

        /// <summary>Collects the non-blank <c>xlink:href</c> references of the given elements, in document order.</summary>
        public static List<string> CollectHrefs(IEnumerable<XElement> elements)
        {
            var hrefs = new List<string>();
            foreach (XElement elem in elements)
            {
                string? href = GetHref(elem);
                if (!string.IsNullOrWhiteSpace(href))
                {
                    hrefs.Add(href!);
                }
            }

            return hrefs;
        }
    }
}
