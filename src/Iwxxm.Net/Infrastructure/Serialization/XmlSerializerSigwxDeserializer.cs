using System;
using System.IO;
using System.Xml;
using System.Xml.Serialization;
using Iwxxm20252.Generated;
using Iwxxm.Net.Application.Ports;
using Iwxxm.Net.Domain.Common;

namespace Iwxxm.Net.Infrastructure.Serialization
{
    /// <summary>
    /// Infrastructure adapter implementing <see cref="ISigwxXmlDeserializer{SigwxDocumentEnvelope}"/>
    /// using <see cref="XmlSerializer"/> from the .NET Standard 2.0 Base Class Library.
    /// Caches <see cref="XmlSerializer"/> instances statically to prevent dynamic assembly memory leaks.
    /// </summary>
    internal sealed class XmlSerializerSigwxDeserializer : ISigwxXmlDeserializer<SigwxDocumentEnvelope>
    {
        private static readonly XmlSerializer WafsForecastSerializer =
            new XmlSerializer(typeof(WAFSSignificantWeatherForecastType));

        private static readonly XmlSerializer CompactReportSerializer =
            new XmlSerializer(typeof(SIGWXReportType));

        public Result<SigwxDocumentEnvelope> Deserialize(Stream xmlStream)
        {
            if (xmlStream == null)
                throw new ArgumentNullException(nameof(xmlStream));

            var readerSettings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null
            };

            try
            {
                using (XmlReader xmlReader = XmlReader.Create(xmlStream, readerSettings))
                {
                    if ( xmlReader.MoveToContent() != XmlNodeType.Element)
                    {
                        return Result<SigwxDocumentEnvelope>.Failure(
                            "XML-NO-ROOT-ELEMENT",
                            "The XML document does not contain a valid root element.",
                            ErrorCategory.Deserialization);
                    }

                    if (xmlReader.NamespaceURI == XmlNamespaces.Iwxxm &&
                        xmlReader.LocalName == "WAFSSignificantWeatherForecast")
                    {
                        var wafsDto = WafsForecastSerializer.Deserialize(xmlReader) as WAFSSignificantWeatherForecastType;
                        return wafsDto != null
                            ? Result<SigwxDocumentEnvelope>.Success(new SigwxDocumentEnvelope(wafsDto))
                            : Result<SigwxDocumentEnvelope>.Failure(
                                "XML-NULL-ROOT",
                                "Failed to deserialize WAFSSignificantWeatherForecast root element.",
                                ErrorCategory.Deserialization);
                    }

                    if (xmlReader.NamespaceURI == XmlNamespaces.Iwxxm &&
                        xmlReader.LocalName == "SIGWXReport")
                    {
                        var compactDto = CompactReportSerializer.Deserialize(xmlReader) as SIGWXReportType;
                        return compactDto != null
                            ? Result<SigwxDocumentEnvelope>.Success(new SigwxDocumentEnvelope(compactDto))
                            : Result<SigwxDocumentEnvelope>.Failure(
                                "XML-NULL-ROOT",
                                "Failed to deserialize SIGWXReport root element.",
                                ErrorCategory.Deserialization);
                    }

                    return Result<SigwxDocumentEnvelope>.Failure(
                        "XML-UNSUPPORTED-ROOT",
                        $"Unsupported root element '<{xmlReader.LocalName} xmlns=\"{xmlReader.NamespaceURI}\">'. Expected 'WAFSSignificantWeatherForecast' or 'SIGWXReport'.",
                        ErrorCategory.Deserialization);
                }
            }
            catch (InvalidOperationException ex)
            {
                return Result<SigwxDocumentEnvelope>.Failure(
                    "XML-DESERIALIZATION-ERROR",
                    $"Error deserializing IWXXM 2025-2 SIGWX document: {ex.InnerException?.Message ?? ex.Message}",
                    ErrorCategory.Deserialization);
            }
            catch (XmlException ex)
            {
                return Result<SigwxDocumentEnvelope>.Failure(
                    new ValidationError(
                        "XML-MALFORMED",
                        ex.Message,
                        ErrorCategory.XmlWellFormedness,
                        ValidationSeverity.Error,
                        null,
                        ex.LineNumber,
                        ex.LinePosition));
            }
        }
    }
}
