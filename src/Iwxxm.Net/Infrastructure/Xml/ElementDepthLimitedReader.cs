using System;
using System.Xml;

namespace Iwxxm.Net.Infrastructure.Xml
{
    /// <summary>
    /// Thrown by <see cref="ElementDepthLimitedReader"/> at the first start tag that nests deeper than
    /// <see cref="IwxxmParserOptions.MaxElementDepth"/>. It is an <see cref="XmlException"/> so every reader of input
    /// documents already catches it; <see cref="SecureXml.Malformed"/> maps it to <c>XML-DEPTH-LIMIT</c>.
    /// </summary>
    internal sealed class ElementDepthLimitException : XmlException
    {
        public ElementDepthLimitException(int maxDepth, int lineNumber, int linePosition)
            : base(
                "The XML document nests elements deeper than the maximum allowed depth of " + maxDepth +
                " (IwxxmParserOptions.MaxElementDepth).",
                null,
                lineNumber,
                linePosition)
        {
            MaxDepth = maxDepth;
        }

        /// <summary>Gets the limit that was exceeded.</summary>
        public int MaxDepth { get; }
    }

    /// <summary>
    /// Forwarding <see cref="XmlReader"/> that stops the read at the first element start tag nested deeper than a limit.
    /// Handing it to <c>XDocument.Load</c> (or an XSLT / Schematron engine) bounds the nesting before any recursive
    /// processing sees the document, in the same single pass that builds the tree: the root element has depth 1.
    /// </summary>
    internal sealed class ElementDepthLimitedReader : XmlReader, IXmlLineInfo
    {
        private readonly XmlReader _inner;
        private readonly int _maxDepth;

        public ElementDepthLimitedReader(XmlReader inner, int maxDepth)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _maxDepth = maxDepth;
        }

        /// <summary>
        /// Gets the exception thrown when the limit was exceeded (<c>null</c> otherwise). Engines that wrap the exception of
        /// their input reader (for example Saxon) lose its type; the caller recovers it here.
        /// </summary>
        public ElementDepthLimitException? Exceeded { get; private set; }

        public override bool Read()
        {
            if (!_inner.Read())
                return false;

            // Reader depth is zero-based: the root element is at Depth 0, i.e. element depth 1.
            if (_inner.NodeType == XmlNodeType.Element && _inner.Depth >= _maxDepth)
            {
                var lineInfo = _inner as IXmlLineInfo;
                bool hasLineInfo = lineInfo != null && lineInfo.HasLineInfo();
                Exceeded = new ElementDepthLimitException(
                    _maxDepth,
                    hasLineInfo ? lineInfo!.LineNumber : 0,
                    hasLineInfo ? lineInfo!.LinePosition : 0);
                throw Exceeded;
            }

            return true;
        }

        public bool HasLineInfo() => _inner is IXmlLineInfo info && info.HasLineInfo();

        public int LineNumber => _inner is IXmlLineInfo info ? info.LineNumber : 0;

        public int LinePosition => _inner is IXmlLineInfo info ? info.LinePosition : 0;

        public override XmlNodeType NodeType => _inner.NodeType;

        public override string LocalName => _inner.LocalName;

        public override string NamespaceURI => _inner.NamespaceURI;

        public override string Prefix => _inner.Prefix;

        public override string Name => _inner.Name;

        public override bool HasValue => _inner.HasValue;

        public override string Value => _inner.Value;

        public override int Depth => _inner.Depth;

        public override string BaseURI => _inner.BaseURI;

        public override bool IsEmptyElement => _inner.IsEmptyElement;

        public override bool IsDefault => _inner.IsDefault;

        public override string XmlLang => _inner.XmlLang;

        public override XmlSpace XmlSpace => _inner.XmlSpace;

        public override int AttributeCount => _inner.AttributeCount;

        public override bool EOF => _inner.EOF;

        public override ReadState ReadState => _inner.ReadState;

        public override XmlNameTable NameTable => _inner.NameTable;

        public override string? GetAttribute(string name) => _inner.GetAttribute(name);

        public override string? GetAttribute(string name, string? namespaceURI) => _inner.GetAttribute(name, namespaceURI);

        public override string GetAttribute(int i) => _inner.GetAttribute(i);

        public override bool MoveToAttribute(string name) => _inner.MoveToAttribute(name);

        public override bool MoveToAttribute(string name, string? ns) => _inner.MoveToAttribute(name, ns);

        public override void MoveToAttribute(int i) => _inner.MoveToAttribute(i);

        public override bool MoveToFirstAttribute() => _inner.MoveToFirstAttribute();

        public override bool MoveToNextAttribute() => _inner.MoveToNextAttribute();

        public override bool MoveToElement() => _inner.MoveToElement();

        public override bool ReadAttributeValue() => _inner.ReadAttributeValue();

        public override string? LookupNamespace(string prefix) => _inner.LookupNamespace(prefix);

        public override void ResolveEntity() => _inner.ResolveEntity();

        public override void Close() => _inner.Close();
    }
}
