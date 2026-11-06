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
            if (validatedDocument == null)
                throw new ArgumentNullException(nameof(validatedDocument));

            var transport = SigwxXmlDeserializer.Deserialize(validatedDocument);
            if (!transport.IsSuccess)
                return Result<Sigwx>.Failure(transport.Errors);

            // Legacy unit aliases are reported once per document: the outermost mapping call owns the scope.
            using (UomAliasScope aliases = UomAliasScope.Begin())
            {
                return aliases.Attach(_mapper.MapToDomain(transport.Value));
            }
        }
    }
}
