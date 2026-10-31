using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Xml;
using System.Xml.XPath;
using Iwxxm.Net.Infrastructure.Caching;
using Iwxxm.Net.Infrastructure.Resources;

namespace Iwxxm.Net.Infrastructure.Validation
{
    /// <summary>
    /// Run-time resolver for the XSLT <c>document()</c> calls of the business-rule stylesheet (AUD-L5, AUD-M14).
    /// <para>
    /// Every embedded document (the official WMO RDF code lists) is parsed once per process into an immutable
    /// <see cref="XPathDocument"/> and handed to <see cref="System.Xml.Xsl.XslCompiledTransform"/> as an
    /// <see cref="IXPathNavigable"/>, which the transform uses directly instead of re-parsing a stream on every
    /// <c>Transform</c>. This also stops the input-document quotas (which the transform otherwise applies to every
    /// document it loads) from rejecting a code list larger than the report being validated. <see cref="XPathDocument"/>
    /// is read-only and safe for concurrent navigation. A failed load is not cached.
    /// </para>
    /// </summary>
    internal sealed class CachedRuleDocumentResolver : XmlResolver
    {
        private readonly EmbeddedXmlUrlResolver _inner = new EmbeddedXmlUrlResolver();

        private readonly ConcurrentDictionary<string, RetryOnFailureLazy<XPathDocument>> _documents =
            new ConcurrentDictionary<string, RetryOnFailureLazy<XPathDocument>>(StringComparer.Ordinal);

        private int _parseCount;

        /// <summary>Gets the number of documents parsed so far (diagnostics and tests).</summary>
        internal int ParseCount => _parseCount;

        public override ICredentials Credentials
        {
            set { /* Credentials are never used: the resolver performs no network access. */ }
        }

        public override Uri ResolveUri(Uri? baseUri, string? relativeUri) => _inner.ResolveUri(baseUri, relativeUri);

        public override object GetEntity(Uri absoluteUri, string? role, Type? ofObjectToReturn)
        {
            if (absoluteUri == null)
                throw new ArgumentNullException(nameof(absoluteUri));

            if (ofObjectToReturn == typeof(Stream))
            {
                return _inner.GetEntity(absoluteUri, role, ofObjectToReturn);
            }

            if (ofObjectToReturn != null && ofObjectToReturn != typeof(object) && !ofObjectToReturn.IsAssignableFrom(typeof(XPathDocument)))
            {
                throw new XmlException($"The rule document resolver cannot return '{ofObjectToReturn.FullName}' for '{absoluteUri}'.");
            }

            RetryOnFailureLazy<XPathDocument> document = _documents.GetOrAdd(
                absoluteUri.AbsoluteUri,
                _ => new RetryOnFailureLazy<XPathDocument>(() => Load(absoluteUri)));
            return document.Value;
        }

        private XPathDocument Load(Uri absoluteUri)
        {
            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null
            };

            using (var stream = (Stream)_inner.GetEntity(absoluteUri, null, typeof(Stream)))
            using (XmlReader reader = XmlReader.Create(stream, settings, absoluteUri.AbsoluteUri))
            {
                var document = new XPathDocument(reader, XmlSpace.Preserve);
                System.Threading.Interlocked.Increment(ref _parseCount);
                return document;
            }
        }
    }
}
