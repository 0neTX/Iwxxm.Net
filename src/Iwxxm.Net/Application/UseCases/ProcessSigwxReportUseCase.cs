using System;
using System.IO;
using Iwxxm.Net.Application.Ports;
using Iwxxm.Net.Domain.Aggregates;
using Iwxxm.Net.Domain.Common;

namespace Iwxxm.Net.Application.UseCases
{
    /// <summary>
    /// Application Use Case that orchestrates the 5-step SIGWX processing pipeline:
    /// 1) Read Stream into a seekable in-memory buffer (supporting network or non-seekable streams).
    /// 2) Validate XML structure against embedded XSD schemas.
    /// 3) Validate business rules against embedded Schematron XSLT.
    /// 4) Deserialize using XmlSerializer (via <see cref="ISigwxXmlDeserializer{TDto}"/>).
    /// 5) Map to pure Domain objects via the Anti-Corruption Layer (<see cref="ISigwxAclMapper{TDto}"/>).
    /// Strictly adheres to Clean Architecture by depending exclusively on Domain and Application ports.
    /// </summary>
    /// <typeparam name="TDto">Type of the intermediate XSD-generated transport object.</typeparam>
    internal sealed class ProcessSigwxReportUseCase<TDto> : IIwxxmSigwxProcessor
    {
        private readonly IXmlSchemaValidator _xsdValidator;
        private readonly ISchematronValidator _schematronValidator;
        private readonly ISigwxXmlDeserializer<TDto> _xmlDeserializer;
        private readonly ISigwxAclMapper<TDto> _aclMapper;

        public ProcessSigwxReportUseCase(
            IXmlSchemaValidator xsdValidator,
            ISchematronValidator schematronValidator,
            ISigwxXmlDeserializer<TDto> xmlDeserializer,
            ISigwxAclMapper<TDto> aclMapper)
        {
            _xsdValidator = xsdValidator ?? throw new ArgumentNullException(nameof(xsdValidator));
            _schematronValidator = schematronValidator ?? throw new ArgumentNullException(nameof(schematronValidator));
            _xmlDeserializer = xmlDeserializer ?? throw new ArgumentNullException(nameof(xmlDeserializer));
            _aclMapper = aclMapper ?? throw new ArgumentNullException(nameof(aclMapper));
        }

        public Result<SignificantWeatherReport> Process(Stream xmlStream)
        {
            if (xmlStream == null)
                throw new ArgumentNullException(nameof(xmlStream));

            if (!xmlStream.CanRead)
            {
                return Result<SignificantWeatherReport>.Failure(
                    "STREAM-NOT-READABLE",
                    "The provided input XML stream is not readable.",
                    ErrorCategory.XmlWellFormedness);
            }

            // Step 1: Read the input stream into a reusable byte array buffer so that all three XML passes
            // (XSD Validation -> XSLT Schematron Validation -> Deserialization) can read from position 0.
            byte[] rawPayload;
            try
            {
                using (var buffer = new MemoryStream())
                {
                    xmlStream.CopyTo(buffer);
                    rawPayload = buffer.ToArray();
                }
            }
            catch (Exception ex)
            {
                return Result<SignificantWeatherReport>.Failure(
                    "STREAM-READ-ERROR",
                    $"Failed to read the input XML stream: {ex.Message}",
                    ErrorCategory.XmlWellFormedness);
            }

            if (rawPayload.Length == 0)
            {
                return Result<SignificantWeatherReport>.Failure(
                    "XML-EMPTY-STREAM",
                    "The input XML stream is empty (0 bytes).",
                    ErrorCategory.XmlWellFormedness);
            }

            // Step 2: Validate structure against embedded XSD schemas (IWXXM 2025-2 + GML 3.2.1 + XLink)
            using (var xsdStream = new MemoryStream(rawPayload, writable: false))
            {
                var xsdErrors = _xsdValidator.Validate(xsdStream);
                if (xsdErrors.Count > 0)
                {
                    return Result<SignificantWeatherReport>.Failure(xsdErrors);
                }
            }

            // Step 3: Validate aeronautical business rules via Schematron XSLT (XslCompiledTransform)
            using (var xsltStream = new MemoryStream(rawPayload, writable: false))
            {
                var schematronErrors = _schematronValidator.Validate(xsltStream);
                if (schematronErrors.Count > 0)
                {
                    return Result<SignificantWeatherReport>.Failure(schematronErrors);
                }
            }

            // Step 4: Deserialize XML using XmlSerializer into XSD-generated classes
            Result<TDto> deserializationResult;
            using (var deserializeStream = new MemoryStream(rawPayload, writable: false))
            {
                deserializationResult = _xmlDeserializer.Deserialize(deserializeStream);
            }

            if (deserializationResult.IsFailure)
            {
                return Result<SignificantWeatherReport>.Failure(deserializationResult.Errors);
            }

            // Step 5: Map XSD-generated DTO to pure Domain objects via the Anti-Corruption Layer (ACL)
            return _aclMapper.MapToDomain(deserializationResult.Value);
        }
    }
}
