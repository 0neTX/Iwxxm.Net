using System;
using System.IO;
using System.Xml;
using System.Xml.Linq;
using Iwxxm20252.Generated;

namespace Iwxxm.Net.Infrastructure.Serialization
{
    /// <summary>
    /// Deserializes a validated SIGWX document (<c>iwxxm:WAFSSignificantWeatherForecast</c> or the non-standard
    /// compact <c>iwxxm:SIGWXReport</c>) into the internal XSD transport model through
    /// <see cref="AttributeDrivenXmlReader"/>. Input readers are hardened (DTD prohibited, no resolver, size quotas).
    /// </summary>
    internal sealed class SigwxXmlDeserializer
    {
        private readonly IwxxmParserOptions _options;

        public SigwxXmlDeserializer(IwxxmParserOptions? options = null)
        {
            _options = options ?? IwxxmParserOptions.Default;
        }

        public Result<SigwxDocumentEnvelope> Deserialize(Stream xmlStream)
        {
            if (xmlStream == null)
                throw new ArgumentNullException(nameof(xmlStream));

            var readerSettings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                MaxCharactersInDocument = _options.MaxCharactersInDocument,
                MaxCharactersFromEntities = _options.MaxCharactersFromEntities,
                XmlResolver = null
            };

            try
            {
                XDocument document;
                using (XmlReader xmlReader = XmlReader.Create(xmlStream, readerSettings))
                {
                    document = XDocument.Load(xmlReader);
                }

                XElement? root = document.Root;
                if (root == null)
                {
                    return Result<SigwxDocumentEnvelope>.Failure(
                        "XML-NO-ROOT-ELEMENT",
                        "The XML document does not contain a valid root element.",
                        ErrorCategory.Deserialization);
                }

                if (root.Name.NamespaceName == XmlNamespaces.Iwxxm && root.Name.LocalName == "WAFSSignificantWeatherForecast")
                {
                    return Result<SigwxDocumentEnvelope>.Success(
                        new SigwxDocumentEnvelope(AttributeDrivenXmlReader.Read<WAFSSignificantWeatherForecastType>(root)));
                }

                if (root.Name.NamespaceName == XmlNamespaces.Iwxxm && root.Name.LocalName == "SIGWXReport")
                {
                    return Result<SigwxDocumentEnvelope>.Success(
                        new SigwxDocumentEnvelope(AttributeDrivenXmlReader.Read<SIGWXReportType>(root)));
                }

                return Result<SigwxDocumentEnvelope>.Failure(
                    "XML-UNSUPPORTED-ROOT",
                    $"Unsupported root element '<{root.Name.LocalName} xmlns=\"{root.Name.NamespaceName}\">'. Expected 'WAFSSignificantWeatherForecast' or 'SIGWXReport'.",
                    ErrorCategory.Deserialization);
            }
            catch (InvalidOperationException ex)
            {
                return Result<SigwxDocumentEnvelope>.Failure(
                    "XML-DESERIALIZATION-ERROR",
                    $"Error deserializing IWXXM 2025-2 SIGWX document: {ex.Message}",
                    ErrorCategory.Deserialization);
            }
            catch (XmlException ex)
            {
                return Result<SigwxDocumentEnvelope>.Failure(
                    new ValidationError(
                        "XML-MALFORMED",
                        ex.Message,
                        ErrorCategory.MalformedInput,
                        ValidationSeverity.Error,
                        null,
                        ex.LineNumber,
                        ex.LinePosition));
            }
        }
    }
}
