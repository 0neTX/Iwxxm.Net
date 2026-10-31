using System;
using System.Xml;
using System.Xml.Linq;
using Iwxxm.Net.Extensibility;
using Iwxxm.Net.Infrastructure.Xml;
using Iwxxm20252.Generated;

namespace Iwxxm.Net.Infrastructure.Serialization
{
    /// <summary>
    /// Deserializes a validated SIGWX document (<c>iwxxm:WAFSSignificantWeatherForecast</c> or the non-standard
    /// compact <c>iwxxm:SIGWXReport</c>) into the internal XSD transport model through
    /// <see cref="AttributeDrivenXmlReader"/>, from the tree parsed by <see cref="SecureXml"/>.
    /// </summary>
    internal static class SigwxXmlDeserializer
    {
        /// <summary>
        /// Reads the transport model from the parsed root of <paramref name="document"/> (the tree parsed once by the use case,
        /// or the element of a report embedded in a bulletin; no re-serialization, AUD-L5).
        /// </summary>
        public static Result<SigwxDocumentEnvelope> Deserialize(IwxxmDocumentContext document)
        {
            if (document == null)
                throw new ArgumentNullException(nameof(document));

            try
            {
                XElement root = SecureXml.Load(document).Root;

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
                return Result<SigwxDocumentEnvelope>.Failure(SecureXml.Malformed(ex));
            }
        }
    }
}
