using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Iwxxm.Net.Application.Ports;
using Iwxxm.Net.Extensibility;

namespace Iwxxm.Net.Infrastructure.Xml
{
    /// <summary>
    /// Single place where input documents are read (HEX-01): hardened <see cref="XmlReaderSettings"/> (DTD prohibited, no
    /// resolver, character quotas of <see cref="IwxxmParserOptions"/>) and the parse-once cache of an
    /// <see cref="IwxxmDocumentContext"/>. Every built-in adapter reads its input through this class.
    /// </summary>
    internal sealed class SecureXml : IDocumentParser
    {
        public static readonly SecureXml Parser = new SecureXml();

        /// <summary>Hardened reader settings for input documents.</summary>
        public static XmlReaderSettings InputSettings(IwxxmParserOptions options) =>
            new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                MaxCharactersInDocument = options.MaxCharactersInDocument,
                MaxCharactersFromEntities = options.MaxCharactersFromEntities,
                CloseInput = false
            };

        /// <summary>
        /// Creates the reader every consumer of an untrusted input stream must use: hardened settings
        /// (<see cref="InputSettings"/>) plus the <see cref="IwxxmParserOptions.MaxElementDepth"/> check, which throws an
        /// <see cref="XmlException"/> (<see cref="ElementDepthLimitException"/>) at the first element nested too deep.
        /// </summary>
        public static ElementDepthLimitedReader CreateReader(Stream stream, IwxxmParserOptions options, string? baseUri = null)
        {
            XmlReader reader = baseUri == null
                ? XmlReader.Create(stream, InputSettings(options))
                : XmlReader.Create(stream, InputSettings(options), baseUri);
            return new ElementDepthLimitedReader(reader, options.MaxElementDepth);
        }

        /// <summary>
        /// Gets the parsed document of <paramref name="document"/> (parsed once, with line information, and cached in the
        /// context). Throws <see cref="XmlException"/> for a malformed document.
        /// </summary>
        public static ParsedXml Load(IwxxmDocumentContext document) =>
            (document ?? throw new ArgumentNullException(nameof(document))).GetOrAddParsed(ParseDocument);

        /// <summary>Creates a context for an element of an already parsed document (no serialization unless requested).</summary>
        public static IwxxmDocumentContext ForElement(XElement element, IwxxmParserOptions options) =>
            IwxxmDocumentContext.FromParsed(new ParsedXml(null, element), () => Serialize(element), options);

        public IReadOnlyList<ValidationError> Parse(IwxxmDocumentContext document)
        {
            try
            {
                Load(document);
                return Array.Empty<ValidationError>();
            }
            catch (XmlException ex)
            {
                return new[] { Malformed(ex) };
            }
            catch (InvalidOperationException ex)
            {
                return new[] { new ValidationError(IwxxmErrorCodes.XmlNoRootElement, ex.Message, ErrorCategory.MalformedInput) };
            }
        }

        public static ValidationError Malformed(XmlException ex) =>
            new ValidationError(
                ex is ElementDepthLimitException ? IwxxmErrorCodes.XmlDepthLimit : IwxxmErrorCodes.XmlMalformed,
                ex.Message,
                ErrorCategory.MalformedInput,
                ValidationSeverity.Error,
                location: null,
                lineNumber: ex.LineNumber,
                linePosition: ex.LinePosition);

        private static ParsedXml ParseDocument(IwxxmDocumentContext document)
        {
            XDocument parsed;
            using (Stream stream = document.OpenRead())
            using (XmlReader reader = CreateReader(stream, document.Options))
            {
                parsed = XDocument.Load(reader, LoadOptions.SetLineInfo);
            }

            XElement root = parsed.Root ?? throw new InvalidOperationException("The XML document does not contain a root element.");
            return new ParsedXml(parsed, root);
        }

        private static byte[] Serialize(XElement element)
        {
            using (var buffer = new MemoryStream())
            {
                using (XmlWriter writer = XmlWriter.Create(buffer, new XmlWriterSettings { Encoding = new UTF8Encoding(false) }))
                {
                    element.Save(writer);
                }

                return buffer.ToArray();
            }
        }
    }

    /// <summary>Parsed form of an input document: the whole document, or only the root element of an embedded report.</summary>
    internal sealed class ParsedXml
    {
        public ParsedXml(XDocument? document, XElement root)
        {
            Document = document;
            Root = root;
        }

        /// <summary>Gets the whole document; <c>null</c> for an element embedded in another document.</summary>
        public XDocument? Document { get; }

        /// <summary>Gets the root element of the (embedded) document.</summary>
        public XElement Root { get; }
    }
}
