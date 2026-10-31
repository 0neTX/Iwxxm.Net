using System.Collections.Generic;
using Iwxxm.Net.Extensibility;

namespace Iwxxm.Net.Application.Ports
{
    /// <summary>
    /// Internal outbound port through which the use cases parse an input document once, with the hardened reader settings
    /// of the context options, before any validator or mapper runs (HEX-01). The parsed tree is cached in the
    /// <see cref="IwxxmDocumentContext"/> and shared by the built-in adapters.
    /// </summary>
    internal interface IDocumentParser
    {
        /// <summary>Parses <paramref name="document"/>; returns the well-formedness errors (empty when the document is well formed).</summary>
        IReadOnlyList<ValidationError> Parse(IwxxmDocumentContext document);
    }
}
