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
        /// Default production-hardened parser options (50 MiB maximum stream payload, 52,428,800 maximum XML characters, 1024 entity character cap).
        /// </summary>
        public static readonly IwxxmParserOptions Default = new IwxxmParserOptions();

        /// <summary>
        /// Maximum allowed input XML stream length in bytes before buffering is aborted with <c>XML-SIZE-LIMIT</c>.
        /// Defaults to 50 MiB (<c>52,428,800</c> bytes). The buffered payload costs up to about three times its size
        /// while it is read; the DOM built from it costs several times more (see <c>docs/SECURITY.md</c>).
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
        /// Defaults to 4 MB (<c>4,194,304</c> bytes).
        /// </summary>
        public long MaxRuleReportBytes { get; }

        public IwxxmParserOptions(
            long maxDocumentBytes = 50L * 1024L * 1024L,
            long maxCharactersInDocument = 52_428_800L,
            long maxCharactersFromEntities = 1024L,
            int maxValidationErrors = 100,
            long maxRuleReportBytes = 4L * 1024L * 1024L)
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

            MaxDocumentBytes = maxDocumentBytes;
            MaxCharactersInDocument = maxCharactersInDocument;
            MaxCharactersFromEntities = maxCharactersFromEntities;
            MaxValidationErrors = maxValidationErrors;
            MaxRuleReportBytes = maxRuleReportBytes;
        }
    }
}
