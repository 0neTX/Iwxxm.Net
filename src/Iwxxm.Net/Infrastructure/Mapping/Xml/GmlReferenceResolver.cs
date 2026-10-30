using System;
using System.Collections.Generic;
using System.Xml.Linq;
using static Iwxxm.Net.Infrastructure.Mapping.Xml.IwxxmXmlNames;

namespace Iwxxm.Net.Infrastructure.Mapping.Xml
{
    /// <summary>
    /// Resolves local GML references (<c>xlink:href="#id"</c>) against the objects of the same document.
    /// </summary>
    /// <remarks>
    /// <para>A <c>gml:id</c> index (<c>gml:id → XElement</c>) is built once per document, on the first lookup, and cached as
    /// an annotation of the <see cref="XDocument"/> (or of the outermost element when the tree has no document). A
    /// document is mapped by one thread at a time, so the lazily built index needs no synchronisation.</para>
    /// <para>Only local references (<c>#id</c>) are resolved. Remote references (<c>http://...</c>,
    /// <c>other.xml#id</c>) and local references to an identifier absent from the document stay unresolved: callers map
    /// them to <c>null</c>, never to an invented value. A reference to the referring element itself or to one of its
    /// ancestors is a cycle and is not resolved; chains of references (a referenced element that is itself only a
    /// reference) are followed up to <see cref="MaxHops"/> hops with a visited set.</para>
    /// </remarks>
    internal static class GmlReferenceResolver
    {
        /// <summary>Maximum number of reference hops followed for one lookup.</summary>
        internal const int MaxHops = 8;

        /// <summary>
        /// Returns the inline child named <paramref name="objectName"/> of <paramref name="property"/>, or, when the
        /// property carries a local <c>xlink:href</c>, the referenced element if it has that name; otherwise <c>null</c>.
        /// </summary>
        public static XElement? Content(XElement? property, XName objectName)
        {
            if (property == null)
                return null;

            XElement? inline = property.Element(objectName);
            if (inline != null)
                return inline;

            XElement? referenced = Resolve(property);
            return referenced != null && referenced.Name == objectName ? referenced : null;
        }

        /// <summary>
        /// Resolves the local <c>xlink:href</c> of <paramref name="property"/> to the element with that <c>gml:id</c>, or
        /// returns <c>null</c> when there is no reference, the reference is remote or unknown, or it forms a cycle.
        /// </summary>
        public static XElement? Resolve(XElement property)
        {
            var visited = new HashSet<XElement>();
            XElement current = property;
            for (int hop = 0; hop < MaxHops; hop++)
            {
                string? id = LocalId(current.Attribute(XlinkNs + "href")?.Value);
                if (id == null || !visited.Add(current))
                    return null;

                if (!IndexOf(current).TryGetValue(id, out XElement? target) || IsAncestorOrSelf(target, property))
                    return null;

                // A target that is itself a bare reference (no content) is followed; otherwise it is the referenced object.
                if (target.Attribute(XlinkNs + "href") == null || target.HasElements)
                    return target;

                current = target;
            }

            return null;
        }

        private static string? LocalId(string? href)
        {
            if (string.IsNullOrWhiteSpace(href))
                return null;

            string trimmed = href!.Trim();
            return trimmed.Length > 1 && trimmed[0] == '#' ? trimmed.Substring(1) : null;
        }

        private static bool IsAncestorOrSelf(XElement candidate, XElement element)
        {
            for (XElement? node = element; node != null; node = node.Parent)
            {
                if (ReferenceEquals(node, candidate))
                    return true;
            }

            return false;
        }

        private static Dictionary<string, XElement> IndexOf(XElement element)
        {
            XContainer owner = (XContainer?)element.Document ?? Outermost(element);
            GmlIdIndex? index = owner.Annotation<GmlIdIndex>();
            if (index == null)
            {
                index = new GmlIdIndex(owner);
                owner.AddAnnotation(index);
            }

            return index.Elements;
        }

        private static XElement Outermost(XElement element)
        {
            XElement node = element;
            while (node.Parent != null)
                node = node.Parent;

            return node;
        }

        /// <summary>Per-document <c>gml:id</c> index (first occurrence wins; identifiers are unique in a valid document).</summary>
        private sealed class GmlIdIndex
        {
            public GmlIdIndex(XContainer owner)
            {
                Elements = new Dictionary<string, XElement>(StringComparer.Ordinal);
                IEnumerable<XElement> elements = owner is XElement root ? root.DescendantsAndSelf() : owner.Descendants();
                foreach (XElement element in elements)
                {
                    string? id = element.Attribute(GmlNs + "id")?.Value;
                    if (!string.IsNullOrEmpty(id) && !Elements.ContainsKey(id!))
                        Elements.Add(id!, element);
                }
            }

            public Dictionary<string, XElement> Elements { get; }
        }
    }
}
