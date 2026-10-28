using System;
using System.IO;
using Iwxxm.Net.Application.Ports;
using Iwxxm.Net.Domain.Aggregates;
using Iwxxm.Net.Domain.Common;

namespace Iwxxm.Net.Application.UseCases
{
    /// <summary>
    /// Application Use Case that orchestrates the end-to-end validation and polymorphic dispatching
    /// pipeline for the full ICAO/WMO IWXXM 2025-2 suite and SWIM <c>collect:MeteorologicalBulletin</c> envelopes:
    /// 1) Bounded Buffer Stream -> 2) Validate XSD -> 3) Validate Schematron XSLT (with offline RDF codelists) -> 4) Dispatch &amp; Map to Domain.
    /// </summary>
    internal sealed class DispatchIwxxmReportUseCase : IIwxxmReportDispatcher
    {
        private readonly IXmlSchemaValidator _xsdValidator;
        private readonly ISchematronValidator _schematronValidator;
        private readonly IIwxxmSuiteDocumentMapper _documentMapper;
        private readonly IwxxmParserOptions _options;

        public DispatchIwxxmReportUseCase(
            IXmlSchemaValidator xsdValidator,
            ISchematronValidator schematronValidator,
            IIwxxmSuiteDocumentMapper documentMapper,
            IwxxmParserOptions? options = null)
        {
            _xsdValidator = xsdValidator ?? throw new ArgumentNullException(nameof(xsdValidator));
            _schematronValidator = schematronValidator ?? throw new ArgumentNullException(nameof(schematronValidator));
            _documentMapper = documentMapper ?? throw new ArgumentNullException(nameof(documentMapper));
            _options = options ?? IwxxmParserOptions.Default;
        }

        public Result<IIwxxmReport> Process(Stream xmlStream)
        {
            if (xmlStream == null)
                throw new ArgumentNullException(nameof(xmlStream));

            if (!xmlStream.CanRead)
            {
                return Result<IIwxxmReport>.Failure(
                    "STREAM-UNREADABLE",
                    "The input XML stream is not readable.",
                    ErrorCategory.XmlWellFormedness);
            }

            if (xmlStream.CanSeek && (xmlStream.Length - xmlStream.Position) > _options.MaxDocumentBytes)
            {
                return Result<IIwxxmReport>.Failure(
                    "XML-SIZE-LIMIT",
                    $"The input XML stream length ({xmlStream.Length - xmlStream.Position} bytes) exceeds the maximum allowed limit of {_options.MaxDocumentBytes} bytes.",
                    ErrorCategory.XmlWellFormedness);
            }

            using (var buffer = new MemoryStream())
            {
                try
                {
                    var chunk = new byte[8192];
                    long totalBytesRead = 0;
                    int bytesRead;
                    while ((bytesRead = xmlStream.Read(chunk, 0, chunk.Length)) > 0)
                    {
                        totalBytesRead += bytesRead;
                        if (totalBytesRead > _options.MaxDocumentBytes)
                        {
                            return Result<IIwxxmReport>.Failure(
                                "XML-SIZE-LIMIT",
                                $"The input XML stream exceeds the maximum allowed limit of {_options.MaxDocumentBytes} bytes.",
                                ErrorCategory.XmlWellFormedness);
                        }
                        buffer.Write(chunk, 0, bytesRead);
                    }
                }
                catch (Exception ex)
                {
                    return Result<IIwxxmReport>.Failure(
                        "STREAM-READ-ERROR",
                        $"Failed to read the input XML stream: {ex.Message}",
                        ErrorCategory.XmlWellFormedness);
                }

                if (buffer.Length == 0)
                {
                    return Result<IIwxxmReport>.Failure(
                        "STREAM-EMPTY",
                        "The input XML stream is empty (0 bytes).",
                        ErrorCategory.XmlWellFormedness);
                }

                // Step 1: Structural validation against the full IWXXM 2025-2, AIXM 5.1.1, METCE, Collect, and GML 3.2.1 XSDs
                buffer.Position = 0;
                var xsdErrors = _xsdValidator.Validate(buffer);
                if (xsdErrors.Count > 0)
                {
                    return Result<IIwxxmReport>.Failure(xsdErrors);
                }

                // Step 2: Business rule & offline RDF/SKOS code-list validation via precompiled ISO Schematron XSLT
                buffer.Position = 0;
                var schematronErrors = _schematronValidator.Validate(buffer);
                if (schematronErrors.Count > 0)
                {
                    return Result<IIwxxmReport>.Failure(schematronErrors);
                }

                // Step 3: Polymorphic root element dispatch and Anti-Corruption Layer (ACL) Domain mapping
                buffer.Position = 0;
                return _documentMapper.MapValidatedStream(buffer);
            }
        }

        public Result<MeteorologicalBulletin> ProcessBulletin(Stream xmlStream)
        {
            Result<IIwxxmReport> dispatchResult = Process(xmlStream);
            if (dispatchResult.IsFailure)
            {
                return Result<MeteorologicalBulletin>.Failure(dispatchResult.Errors);
            }

            if (dispatchResult.Value is MeteorologicalBulletin bulletin)
            {
                return Result<MeteorologicalBulletin>.Success(bulletin);
            }

            return Result<MeteorologicalBulletin>.Failure(
                "DISPATCH-NOT-A-BULLETIN",
                $"Expected a 'collect:MeteorologicalBulletin' document, but parsed '{dispatchResult.Value.ReportType}' ('{dispatchResult.Value.Id}').",
                ErrorCategory.Deserialization,
                dispatchResult.Value.Id);
        }
    }
}
