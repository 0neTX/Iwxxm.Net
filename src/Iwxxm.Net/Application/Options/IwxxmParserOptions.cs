using System;

namespace Iwxxm.Net
{
    /// <summary>
    /// Security and memory-bound configuration options for IWXXM XML processing pipelines.
    /// Protects against XML Denial-of-Service (DoS), unbounded stream buffering (LOH exhaustion),
    /// and entity expansion attacks.
    /// </summary>
    public sealed class IwxxmParserOptions
    {
        /// <summary>
        /// Default production-hardened parser options (50 MiB maximum stream payload, 52,428,800 maximum XML characters, 1024 entity character cap, 128 element depth).
        /// </summary>
        public static readonly IwxxmParserOptions Default = new IwxxmParserOptions();

        /// <summary>
        /// Maximum allowed input XML stream length in bytes before buffering is aborted with <c>XML-SIZE-LIMIT</c>.
        /// Defaults to 50 MiB (<c>52,428,800</c> bytes). The buffered payload costs up to about three times its size
        /// while it is read; the DOM built from it costs several times more (see <c>SECURITY.md</c>).
        /// </summary>
        public long MaxDocumentBytes { get; }

        /// <summary>
        /// Maximum allowed character count in an XML document enforced by <c>XmlReaderSettings.MaxCharactersInDocument</c>.
        /// Defaults to <c>52,428,800</c> characters, the same number as <see cref="MaxDocumentBytes"/>: every character
        /// takes at least one byte of UTF-8 (and two of UTF-16), so a document that passes the byte limit never exceeds
        /// this quota whatever its encoding.
        /// </summary>
        public long MaxCharactersInDocument { get; }

        /// <summary>
        /// Maximum allowed characters resulting from entity expansion enforced by <c>XmlReaderSettings.MaxCharactersFromEntities</c>.
        /// Defaults to <c>1024</c> characters.
        /// </summary>
        public long MaxCharactersFromEntities { get; }

        /// <summary>
        /// Maximum number of findings a validator reports for one document (applies to the XSD and the Schematron
        /// validators). When a document produces more, the first findings are kept and a final
        /// <c>XSD-TOO-MANY-ERRORS</c> / <c>SCHEMATRON-TOO-MANY-ERRORS</c> sentinel is appended. Defaults to <c>100</c>.
        /// </summary>
        public int MaxValidationErrors { get; }

        /// <summary>
        /// Maximum size in bytes of the SVRL report a Schematron validator may produce for one document. Larger reports
        /// stop the rule evaluation with <c>SCHEMATRON-REPORT-SIZE-LIMIT</c> (a business-rule error, not malformed input).
        /// Defaults to 4 MiB (<c>4,194,304</c> bytes).
        /// </summary>
        public long MaxRuleReportBytes { get; }

        /// <summary>
        /// Maximum element nesting depth of an input document; the root element has depth 1. A document that nests deeper is
        /// rejected with <c>XML-DEPTH-LIMIT</c> by a streaming check of the single read, before any tree is built or
        /// validated. Defaults to <c>128</c>: real IWXXM products nest at most about 20 levels (the largest sample, a 14 MB
        /// SIGWX product, reaches 16), so the default leaves a margin of more than six times, while the .NET XML tree
        /// validation overflows the stack (an uncatchable process crash) at about 25,000 levels (24,000 survived) of a schema-valid nested
        /// <c>collect:MeteorologicalBulletin</c>. Raise it consciously for legitimate deeper documents (see <c>SECURITY.md</c>).
        /// </summary>
        public int MaxElementDepth { get; }

        /// <summary>Initializes the parser limits. Every argument must be positive, except <paramref name="maxCharactersFromEntities"/>, which may be zero.</summary>
        /// <param name="maxDocumentBytes">The maximum input stream length in bytes (see <see cref="MaxDocumentBytes"/>); defaults to 50 MiB.</param>
        /// <param name="maxCharactersInDocument">The maximum number of characters of the XML document (see <see cref="MaxCharactersInDocument"/>); defaults to 52,428,800.</param>
        /// <param name="maxCharactersFromEntities">The maximum number of characters produced by entity expansion (see <see cref="MaxCharactersFromEntities"/>); defaults to 1024 and must not be negative.</param>
        /// <param name="maxValidationErrors">The maximum number of findings reported per document (see <see cref="MaxValidationErrors"/>); defaults to 100.</param>
        /// <param name="maxRuleReportBytes">The maximum size in bytes of a Schematron SVRL report (see <see cref="MaxRuleReportBytes"/>); defaults to 4 MiB.</param>
        /// <param name="maxElementDepth">The maximum element nesting depth (see <see cref="MaxElementDepth"/>); defaults to 128.</param>
        /// <exception cref="ArgumentOutOfRangeException">An argument is outside its allowed range.</exception>
        public IwxxmParserOptions(
            long maxDocumentBytes = 50L * 1024L * 1024L,
            long maxCharactersInDocument = 52_428_800L,
            long maxCharactersFromEntities = 1024L,
            int maxValidationErrors = 100,
            long maxRuleReportBytes = 4L * 1024L * 1024L,
            int maxElementDepth = 128)
        {
            if (maxDocumentBytes <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxDocumentBytes), "Maximum document byte limit must be positive.");
            if (maxCharactersInDocument <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxCharactersInDocument), "Maximum characters in document must be positive.");
            if (maxCharactersFromEntities < 0)
                throw new ArgumentOutOfRangeException(nameof(maxCharactersFromEntities), "Maximum characters from entities cannot be negative.");
            if (maxValidationErrors <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxValidationErrors), "Maximum validation errors must be positive.");
            if (maxRuleReportBytes <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxRuleReportBytes), "Maximum rule report size must be positive.");
            if (maxElementDepth <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxElementDepth), "Maximum element depth must be positive.");

            MaxDocumentBytes = maxDocumentBytes;
            MaxCharactersInDocument = maxCharactersInDocument;
            MaxCharactersFromEntities = maxCharactersFromEntities;
            MaxValidationErrors = maxValidationErrors;
            MaxRuleReportBytes = maxRuleReportBytes;
            MaxElementDepth = maxElementDepth;
        }
    }
}
