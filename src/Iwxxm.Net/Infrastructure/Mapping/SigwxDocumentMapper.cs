using System;
using Iwxxm.Net.Application.Ports.Out;
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
        private readonly SigwxMapper _mapper = new SigwxMapper();

        public Result<Sigwx> Map(IwxxmDocumentContext validatedDocument)
        {
            // Legacy unit aliases are reported once per document: this call owns the collector and attaches its warning.
            var aliases = new UomAliasCollector();
            return aliases.Attach(Map(validatedDocument, aliases));
        }

        /// <summary>
        /// Maps the document recording accepted unit aliases in <paramref name="aliases"/>, which belongs to the caller (the
        /// composite mapper shares one collector between a bulletin and its SIGWX reports): the caller attaches the warning.
        /// </summary>
        internal Result<Sigwx> Map(IwxxmDocumentContext validatedDocument, UomAliasCollector aliases)
        {
            if (validatedDocument == null)
                throw new ArgumentNullException(nameof(validatedDocument));

            var transport = SigwxXmlDeserializer.Deserialize(validatedDocument);
            if (!transport.IsSuccess)
                return Result<Sigwx>.Failure(transport.Errors);

            return _mapper.MapToDomain(transport.Value, aliases);
        }
    }
}
