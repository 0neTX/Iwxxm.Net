using System;
using System.IO;
using Iwxxm.Net.Extensibility;
using Iwxxm.Net.Infrastructure.Serialization;
using Iwxxm.Net.Reports;

namespace Iwxxm.Net.Infrastructure.Mapping
{
    /// <summary>
    /// Driven adapter implementing <see cref="ISigwxDocumentMapper"/>: deserializes the validated SIGWX document into the
    /// internal XSD transport model (<see cref="SigwxXmlDeserializer"/>) and maps it to the Domain through the
    /// Anti-Corruption Layer (<see cref="SigwxMapper"/>).
    /// </summary>
    internal sealed class SigwxDocumentMapper : ISigwxDocumentMapper
    {
        private readonly SigwxXmlDeserializer _deserializer;
        private readonly SigwxMapper _mapper = new SigwxMapper();

        public SigwxDocumentMapper(IwxxmParserOptions? options = null)
        {
            _deserializer = new SigwxXmlDeserializer(options);
        }

        public Result<SignificantWeatherReport> Map(Stream validatedDocument)
        {
            if (validatedDocument == null)
                throw new ArgumentNullException(nameof(validatedDocument));

            var transport = _deserializer.Deserialize(validatedDocument);
            return transport.IsSuccess
                ? _mapper.MapToDomain(transport.Value)
                : Result<SignificantWeatherReport>.Failure(transport.Errors);
        }
    }
}
