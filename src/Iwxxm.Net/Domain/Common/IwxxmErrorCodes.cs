namespace Iwxxm.Net
{
    /// <summary>
    /// The stable <see cref="ValidationError.Code"/> values that a consumer realistically branches on, defined once.
    /// The library itself emits exactly these strings, so a comparison such as
    /// <c>error.Code == IwxxmErrorCodes.FileNotFound</c> cannot drift from the code.
    /// </summary>
    /// <remarks>
    /// <para>This is a deliberately short list. The several hundred mapping and domain codes (<c>ACL-*</c>, <c>DOMAIN-*</c>,
    /// <c>GML-*</c>, <c>GEO-*</c>) and the official Schematron rule identifiers describe the content of a document, are not
    /// constants and may gain new members; match them by prefix or read the message. All of them, including the codes in
    /// this class, are listed in <c>docs/REFERENCE.md</c> (a test keeps the list and the code in sync).</para>
    /// <para>The values are compile-time constants: they can be used in <c>switch</c> statements and attribute arguments.</para>
    /// </remarks>
    public static class IwxxmErrorCodes
    {
        // ---- Files, streams and text (ErrorCategory.Io, ErrorCategory.MalformedInput) ----

        /// <summary>The file or its directory does not exist (<see cref="ErrorCategory.Io"/>).</summary>
        public const string FileNotFound = "FILE-NOT-FOUND";

        /// <summary>Permission denied, or the path is a directory (<see cref="ErrorCategory.Io"/>).</summary>
        public const string AccessDenied = "ACCESS-DENIED";

        /// <summary>Sharing violation, path too long, malformed path or another I/O error on a file (<see cref="ErrorCategory.Io"/>).</summary>
        public const string FileReadError = "FILE-READ-ERROR";

        /// <summary>The stream does not support reading (<see cref="ErrorCategory.Io"/>).</summary>
        public const string StreamUnreadable = "STREAM-UNREADABLE";

        /// <summary>Reading the stream or text reader threw an I/O error (<see cref="ErrorCategory.Io"/>).</summary>
        public const string StreamReadError = "STREAM-READ-ERROR";

        /// <summary>The input stream has zero bytes (<see cref="ErrorCategory.MalformedInput"/>).</summary>
        public const string StreamEmpty = "STREAM-EMPTY";

        /// <summary>The XML string, reader or file content is null, empty or whitespace (<see cref="ErrorCategory.MalformedInput"/>).</summary>
        public const string XmlEmptyString = "XML-EMPTY-STRING";

        // ---- Limits (ErrorCategory.MalformedInput) ----

        /// <summary>The input exceeds <see cref="IwxxmParserOptions.MaxDocumentBytes"/>.</summary>
        public const string XmlSizeLimit = "XML-SIZE-LIMIT";

        /// <summary>An element is nested deeper than <see cref="IwxxmParserOptions.MaxElementDepth"/>.</summary>
        public const string XmlDepthLimit = "XML-DEPTH-LIMIT";

        // ---- XML well-formedness (ErrorCategory.MalformedInput) ----

        /// <summary>Not well-formed XML, or a DTD was found (DTDs are always refused).</summary>
        public const string XmlMalformed = "XML-MALFORMED";

        /// <summary>The XML has no root element.</summary>
        public const string XmlNoRootElement = "XML-NO-ROOT-ELEMENT";

        // ---- Schema validation (ErrorCategory.SchemaViolation) ----

        /// <summary>The document violates the official IWXXM / COLLECT XSD (one finding per violation).</summary>
        public const string XsdSchemaViolation = "XSD-SCHEMA-VIOLATION";

        /// <summary>The root element is not declared by the official IWXXM / COLLECT schemas.</summary>
        public const string XsdUnknownRootElement = "XSD-UNKNOWN-ROOT-ELEMENT";

        /// <summary>The SIGWX entry point (<see cref="IwxxmSigwxParser"/>) received a root element that is not a SIGWX document.</summary>
        public const string XsdUnexpectedRootElement = "XSD-UNEXPECTED-ROOT-ELEMENT";

        // ---- Validator adapters (ErrorCategory.SchemaViolation, ErrorCategory.BusinessRuleViolation) ----

        /// <summary>A schema validator threw or returned nothing.</summary>
        public const string SchemaValidatorError = "SCHEMA-VALIDATOR-ERROR";

        /// <summary>A business-rule (Schematron) validator threw or returned nothing.</summary>
        public const string RuleValidatorError = "RULE-VALIDATOR-ERROR";

        // ---- Routing and mapping (ErrorCategory.Deserialization, ErrorCategory.DomainMapping) ----

        /// <summary><c>ProcessBulletin</c> received a document that is not a <c>collect:MeteorologicalBulletin</c>.</summary>
        public const string NotABulletin = "DISPATCH-NOT-A-BULLETIN";

        /// <summary><c>ProcessAs&lt;T&gt;</c> parsed a document of a type other than <c>T</c>.</summary>
        public const string UnexpectedReportType = "IWXXM-UNEXPECTED-REPORT-TYPE";

        /// <summary>The root element is not in the IWXXM 2025-2 namespace (for example IWXXM 3.0 or 2023-1).</summary>
        public const string XmlUnsupportedNamespace = "XML-UNSUPPORTED-NAMESPACE";

        /// <summary>The IWXXM root element is not a supported 2025-2 report.</summary>
        public const string XmlUnsupportedReport = "XML-UNSUPPORTED-REPORT";

        /// <summary>A bulletin entry is itself a bulletin (reported as an item error).</summary>
        public const string CollectNestedBulletin = "COLLECT-NESTED-BULLETIN";

        /// <summary>A document or report mapper threw or returned nothing (for a bulletin, the item error of that one entry).</summary>
        public const string MapperError = "MAPPER-ERROR";

        /// <summary>A bulletin contains no successfully mapped report.</summary>
        public const string BulletinEmpty = "DOMAIN-BULLETIN-EMPTY";

        // ---- GeoJSON export (ErrorCategory.DomainMapping) ----

        /// <summary>The GeoJSON exporter threw or returned nothing.</summary>
        public const string GeoJsonExportError = "GEOJSON-EXPORT-ERROR";
    }
}
