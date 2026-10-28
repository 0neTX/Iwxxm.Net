using System;

namespace Iwxxm.Net.Domain.Common
{
    /// <summary>
    /// Security and memory-bound configuration options for IWXXM XML processing pipelines.
    /// Protects against XML Denial-of-Service (DoS), unbounded stream buffering (LOH exhaustion),
    /// and entity expansion attacks.
    /// </summary>
    public sealed class IwxxmParserOptions
    {
        /// <summary>
        /// Default production-hardened parser options (10 MB maximum stream payload, 10M maximum XML characters, 1024 entity character cap).
        /// </summary>
        public static readonly IwxxmParserOptions Default = new IwxxmParserOptions();

        /// <summary>
        /// Maximum allowed input XML stream length in bytes before buffering is aborted with <c>XML-SIZE-LIMIT</c>.
        /// Defaults to 10 MB (<c>10,485,760</c> bytes).
        /// </summary>
        public long MaxDocumentBytes { get; }

        /// <summary>
        /// Maximum allowed character count in an XML document enforced by <c>XmlReaderSettings.MaxCharactersInDocument</c>.
        /// Defaults to <c>10,485,760</c> characters.
        /// </summary>
        public long MaxCharactersInDocument { get; }

        /// <summary>
        /// Maximum allowed characters resulting from entity expansion enforced by <c>XmlReaderSettings.MaxCharactersFromEntities</c>.
        /// Defaults to <c>1024</c> characters.
        /// </summary>
        public long MaxCharactersFromEntities { get; }

        public IwxxmParserOptions(
            long maxDocumentBytes = 10L * 1024L * 1024L,
            long maxCharactersInDocument = 10_485_760L,
            long maxCharactersFromEntities = 1024L)
        {
            if (maxDocumentBytes <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxDocumentBytes), "Maximum document byte limit must be positive.");
            if (maxCharactersInDocument <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxCharactersInDocument), "Maximum characters in document must be positive.");
            if (maxCharactersFromEntities < 0)
                throw new ArgumentOutOfRangeException(nameof(maxCharactersFromEntities), "Maximum characters from entities cannot be negative.");

            MaxDocumentBytes = maxDocumentBytes;
            MaxCharactersInDocument = maxCharactersInDocument;
            MaxCharactersFromEntities = maxCharactersFromEntities;
        }
    }
}
