using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;
using Iwxxm.Net.Application.Ports.Out;
using Iwxxm.Net.Extensibility;
using Iwxxm.Net.Infrastructure.Caching;
using Iwxxm.Net.Infrastructure.Resources;
using Iwxxm.Net.Infrastructure.Xml;

namespace Iwxxm.Net.Infrastructure.Validation
{
    /// <summary>
    /// Structural XML validation engine.
    /// <list type="bullet">
    /// <item>Every standard root element is validated against the <b>unmodified official WMO IWXXM 2025-2
    /// schema tree</b> (<c>iwxxm-collect.xsd</c> → <c>iwxxm.xsd</c>, COLLECT 1.2, METCE 1.2, OPM 1.2, O&amp;M 2.0,
    /// GML 3.2.1, ISO 19139, AIXM 5.1.1, W3C XLink/XML) embedded byte-for-byte in the assembly.</item>
    /// <item>The compact <c>iwxxm:SIGWXReport</c> root is a NON-STANDARD extension of this library (it is not
    /// declared in any official schema) and is validated against a separate legacy schema subset.</item>
    /// </list>
    /// The schema set is chosen from the root element QName. Both compiled <see cref="XmlSchemaSet"/> instances are
    /// built lazily once per process (thread-safe); validation against one set is serialised (see <c>ValidateAgainst</c>).
    /// </summary>
    internal sealed class XsdValidator : ISchemaValidator
    {
        internal const string IwxxmNamespace = "http://icao.int/iwxxm/2025-2";
        internal const string LegacySigwxReportLocalName = "SIGWXReport";
        internal const string WafsForecastLocalName = "WAFSSignificantWeatherForecast";

        /// <summary>Official convenience root that imports both IWXXM 2025-2 and WMO COLLECT 1.2.</summary>
        internal const string OfficialRootSchemaUrl = "http://schemas.wmo.int/iwxxm/2025-2/iwxxm-collect.xsd";

        private static readonly string[] LegacySigwxSchemaPaths =
        {
            "Xsd/Legacy/xlink.xsd",
            "Xsd/Legacy/gml.xsd",
            "Xsd/Legacy/aixm.xsd",
            "Xsd/Legacy/iwxxm-sigwx.xsd"
        };

        private static readonly XmlQualifiedName LegacySigwxRoot = new XmlQualifiedName(LegacySigwxReportLocalName, IwxxmNamespace);
        private static readonly XmlQualifiedName WafsForecastRoot = new XmlQualifiedName(WafsForecastLocalName, IwxxmNamespace);

        private static long _officialCompileTicks = -1;

        // Compile failures are not cached (AUD-L6): a later validation retries the compilation.
        private static readonly RetryOnFailureLazy<XmlSchemaSet> CompiledOfficialSchemaSet =
            new RetryOnFailureLazy<XmlSchemaSet>(BuildOfficialSchemaSet);

        private static readonly RetryOnFailureLazy<XmlSchemaSet> CompiledLegacySigwxSchemaSet =
            new RetryOnFailureLazy<XmlSchemaSet>(BuildLegacySigwxSchemaSet);

        private readonly bool _useFullSuite;
        private readonly IwxxmParserOptions _options;

        /// <param name="useFullSuite">
        /// <c>true</c> accepts every global element declared by the official IWXXM 2025-2 / COLLECT schemas;
        /// <c>false</c> restricts the accepted roots to SIGWX documents
        /// (<c>iwxxm:WAFSSignificantWeatherForecast</c> and the non-standard <c>iwxxm:SIGWXReport</c>).
        /// </param>
        /// <param name="options">Parser security options.</param>
        public XsdValidator(bool useFullSuite = false, IwxxmParserOptions? options = null)
        {
            _useFullSuite = useFullSuite;
            _options = options ?? IwxxmParserOptions.Default;
        }

        public static XsdValidator CreateFullSuiteValidator(IwxxmParserOptions? options = null) =>
            new XsdValidator(useFullSuite: true, options: options);

        /// <summary>
        /// Wall-clock time spent compiling the official schema set, or <c>null</c> if it has not been compiled yet.
        /// </summary>
        internal static TimeSpan? OfficialSchemaSetCompileTime
        {
            get
            {
                long ticks = Interlocked.Read(ref _officialCompileTicks);
                return ticks < 0 ? (TimeSpan?)null : TimeSpan.FromTicks(ticks);
            }
        }

        /// <summary>Gets the compiled official IWXXM 2025-2 schema set (compiled on first access).</summary>
        internal static XmlSchemaSet OfficialSchemaSet => CompiledOfficialSchemaSet.Value;

        private static XmlSchemaSet BuildOfficialSchemaSet()
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            var resolver = new EmbeddedXmlUrlResolver();
            XmlSchemaSet schemaSet = CreateSchemaSet(resolver);

            using (var rootStream = (Stream)resolver.GetEntity(new Uri(OfficialRootSchemaUrl), null, typeof(Stream)))
            {
                AddSchema(schemaSet, rootStream, OfficialRootSchemaUrl, resolver);
            }

            schemaSet.Compile();
            stopwatch.Stop();
            Interlocked.Exchange(ref _officialCompileTicks, stopwatch.Elapsed.Ticks);
            return schemaSet;
        }

        private static XmlSchemaSet BuildLegacySigwxSchemaSet()
        {
            var resolver = new EmbeddedXmlUrlResolver();
            XmlSchemaSet schemaSet = CreateSchemaSet(resolver);

            foreach (string resourcePath in LegacySigwxSchemaPaths)
            {
                using (Stream xsdStream = EmbeddedResourceLoader.GetRequiredResourceStream(resourcePath))
                {
                    AddSchema(schemaSet, xsdStream, new Uri(EmbeddedXmlUrlResolver.EmbeddedRootUri, resourcePath).AbsoluteUri, resolver);
                }
            }

            schemaSet.Compile();
            return schemaSet;
        }

        private static XmlSchemaSet CreateSchemaSet(XmlResolver resolver)
        {
            var schemaSet = new XmlSchemaSet { XmlResolver = resolver };
            schemaSet.ValidationEventHandler += (sender, args) =>
            {
                if (args.Severity == XmlSeverityType.Error)
                {
                    throw new XmlSchemaException(
                        $"Error compiling embedded XSD schema set: {args.Message}",
                        args.Exception);
                }
            };
            return schemaSet;
        }

        private static void AddSchema(XmlSchemaSet schemaSet, Stream xsdStream, string baseUri, XmlResolver resolver)
        {
            var readerSettings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = resolver
            };

            using (XmlReader schemaReader = XmlReader.Create(xsdStream, readerSettings, baseUri))
            {
                XmlSchema schema = XmlSchema.Read(schemaReader, (sender, args) =>
                {
                    if (args.Severity == XmlSeverityType.Error)
                    {
                        throw new XmlSchemaException(
                            $"Error reading embedded XSD schema '{baseUri}': {args.Message}",
                            args.Exception);
                    }
                })!;

                schemaSet.Add(schema);
            }
        }

        /// <summary>
        /// Validates the parsed document of <paramref name="document"/> (parsed once per context and shared with the mapper,
        /// AUD-N3): the schema set is selected from the root element QName, then the tree is validated.
        /// </summary>
        public IReadOnlyList<ValidationError> Validate(IwxxmDocumentContext document)
        {
            if (document == null)
                throw new ArgumentNullException(nameof(document));

            var errors = new List<ValidationError>();

            ParsedXml parsed;
            try
            {
                parsed = SecureXml.Load(document);
            }
            catch (XmlException ex)
            {
                errors.Add(SecureXml.Malformed(ex));
                return errors;
            }
            catch (InvalidOperationException ex)
            {
                errors.Add(new ValidationError(IwxxmErrorCodes.XmlNoRootElement, ex.Message, ErrorCategory.MalformedInput));
                return errors;
            }

            XElement root = parsed.Root;
            var rootName = new XmlQualifiedName(root.Name.LocalName, root.Name.NamespaceName);

            XmlSchemaSet activeSchemaSet;
            try
            {
                activeSchemaSet = SelectSchemaSet(rootName, errors);
            }
            catch (Exception ex) when (!(ex is OutOfMemoryException))
            {
                errors.Add(new ValidationError(
                    code: "XSD-SCHEMA-SET-UNAVAILABLE",
                    message: $"The embedded XSD schema set could not be compiled: {ex.Message}",
                    category: ErrorCategory.SchemaViolation,
                    severity: ValidationSeverity.Error));
                return errors;
            }

            if (errors.Count > 0)
            {
                return errors;
            }

            // An embedded element (no own document) is validated as a document of its own.
            XDocument tree = parsed.Document ?? new XDocument(root);
            ValidateAgainst(activeSchemaSet, tree, errors);
            return errors;
        }

        private XmlSchemaSet SelectSchemaSet(XmlQualifiedName rootName, List<ValidationError> errors)
        {
            if (rootName == LegacySigwxRoot)
            {
                return CompiledLegacySigwxSchemaSet.Value;
            }

            if (!_useFullSuite && rootName != WafsForecastRoot)
            {
                errors.Add(new ValidationError(
                    code: IwxxmErrorCodes.XsdUnexpectedRootElement,
                    message: $"Root element '{{{rootName.Namespace}}}{rootName.Name}' is not a SIGWX document. Expected " +
                             $"'{{{IwxxmNamespace}}}{WafsForecastLocalName}' (official) or '{{{IwxxmNamespace}}}{LegacySigwxReportLocalName}' (non-standard extension).",
                    category: ErrorCategory.SchemaViolation,
                    severity: ValidationSeverity.Error,
                    location: "/" + rootName.Name));
                return CompiledOfficialSchemaSet.Value;
            }

            XmlSchemaSet official = CompiledOfficialSchemaSet.Value;
            // GlobalElements reads the shared set, which XmlSchemaSet does not guarantee to be thread-safe: read it under
            // the same per-set lock that serialises validation (see ValidateAgainst).
            bool declared;
            lock (official)
            {
                declared = official.GlobalElements.Contains(rootName);
            }

            if (!declared)
            {
                errors.Add(new ValidationError(
                    code: IwxxmErrorCodes.XsdUnknownRootElement,
                    message: $"Root element '{{{rootName.Namespace}}}{rootName.Name}' is not declared by the official WMO IWXXM 2025-2 / COLLECT schemas.",
                    category: ErrorCategory.SchemaViolation,
                    severity: ValidationSeverity.Error,
                    location: "/" + rootName.Name));
            }

            return official;
        }

        /// <summary>
        /// Validates through <c>XDocument.Validate</c> (System.Xml.Schema.Extensions)
        /// (the <see cref="XmlSchemaValidator"/> push API) instead of a validating <see cref="XmlReader"/>: the validating
        /// reader tries to materialize schema default attributes (e.g. the GML/XLink <c>xlink:type="simple"</c> default) and
        /// reports a spurious error when the instance does not declare a prefix for their namespace, which rejects valid
        /// official documents such as <c>metar-NIL-collect.xml</c>. With <c>addSchemaInfo: false</c> no default attribute is
        /// inserted and the structural validation is otherwise identical. The official schemas declare no identity constraints.
        /// </summary>
        private void ValidateAgainst(XmlSchemaSet schemaSet, XDocument document, List<ValidationError> errors)
        {
            try
            {
                // Concurrency (AUD-N2): XmlSchemaSet documents no thread-safety guarantee for instance members, and
                // XDocument.Validate builds an XmlSchemaValidator over the set's shared XmlNameTable, which it may write to
                // (NameTable.Add is not thread-safe). Validation against one shared compiled set is therefore serialised
                // per set; loading, mapping and Schematron execution of concurrent documents stay parallel. Per-thread
                // clones were rejected: compiling the official closure costs seconds and tens of megabytes per copy.
                lock (schemaSet)
                {
                    ValidateDocument(document, schemaSet, errors, _options.MaxValidationErrors);
                }
            }
            catch (TooManyFindingsException)
            {
                // The cap was reached: the sentinel finding is already in the list (AUD-M14).
            }
            catch (XmlSchemaException ex)
            {
                errors.Add(new ValidationError(
                    code: "XSD-FATAL-ERROR",
                    message: ex.Message,
                    category: ErrorCategory.SchemaViolation,
                    severity: ValidationSeverity.Error,
                    location: null,
                    lineNumber: ex.LineNumber,
                    linePosition: ex.LinePosition));
            }
        }

        private static void ValidateDocument(XDocument document, XmlSchemaSet schemaSet, List<ValidationError> errors, int maxErrors)
        {
            var limit = new ValidationFindingLimit(errors, maxErrors, ValidationFindingLimit.XsdTooManyErrorsCode, ErrorCategory.SchemaViolation);
            document.Validate(
                schemaSet,
                (sender, args) =>
                {
                    if (args.Severity == XmlSeverityType.Error)
                    {
                        bool added = limit.TryAdd(new ValidationError(
                            code: IwxxmErrorCodes.XsdSchemaViolation,
                            message: args.Message ?? "XSD validation error.",
                            category: ErrorCategory.SchemaViolation,
                            severity: ValidationSeverity.Error,
                            location: args.Exception?.SourceUri,
                            lineNumber: args.Exception?.LineNumber,
                            linePosition: args.Exception?.LinePosition));
                        if (!added)
                        {
                            // XDocument.Validate has no cancellation: leave it through the handler once the cap is reached.
                            throw new TooManyFindingsException();
                        }
                    }
                },
                addSchemaInfo: false);
        }

        /// <summary>Stops <c>XDocument.Validate</c> once <see cref="IwxxmParserOptions.MaxValidationErrors"/> is reached.</summary>
        private sealed class TooManyFindingsException : Exception
        {
        }
    }
}
