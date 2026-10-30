using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;
using Iwxxm.Net.Extensibility;
using Iwxxm.Net.Infrastructure.Resources;

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

        private static readonly Lazy<XmlSchemaSet> CompiledOfficialSchemaSet =
            new Lazy<XmlSchemaSet>(BuildOfficialSchemaSet, LazyThreadSafetyMode.ExecutionAndPublication);

        private static readonly Lazy<XmlSchemaSet> CompiledLegacySigwxSchemaSet =
            new Lazy<XmlSchemaSet>(BuildLegacySigwxSchemaSet, LazyThreadSafetyMode.ExecutionAndPublication);

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

        public IReadOnlyList<ValidationError> Validate(Stream xmlStream)
        {
            if (xmlStream == null)
                throw new ArgumentNullException(nameof(xmlStream));

            var errors = new List<ValidationError>();

            Stream source = xmlStream.CanSeek ? xmlStream : BufferStream(xmlStream);
            long startPosition = source.Position;

            XmlQualifiedName? rootName = ReadRootElementName(source, errors);
            if (rootName == null)
            {
                return errors;
            }

            source.Position = startPosition;

            XmlSchemaSet activeSchemaSet;
            try
            {
                activeSchemaSet = SelectSchemaSet(rootName, errors);
            }
            catch (Exception ex) when (ex is XmlSchemaException || ex is XmlException || ex is FileNotFoundException)
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

            ValidateAgainst(activeSchemaSet, source, errors);
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
                    code: "XSD-UNEXPECTED-ROOT-ELEMENT",
                    message: $"Root element '{{{rootName.Namespace}}}{rootName.Name}' is not a SIGWX document. Expected " +
                             $"'{{{IwxxmNamespace}}}{WafsForecastLocalName}' (official) or '{{{IwxxmNamespace}}}{LegacySigwxReportLocalName}' (non-standard extension).",
                    category: ErrorCategory.SchemaViolation,
                    severity: ValidationSeverity.Error,
                    location: "/" + rootName.Name));
                return CompiledOfficialSchemaSet.Value;
            }

            XmlSchemaSet official = CompiledOfficialSchemaSet.Value;
            if (!official.GlobalElements.Contains(rootName))
            {
                errors.Add(new ValidationError(
                    code: "XSD-UNKNOWN-ROOT-ELEMENT",
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
        private void ValidateAgainst(XmlSchemaSet schemaSet, Stream source, List<ValidationError> errors)
        {
            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                MaxCharactersInDocument = _options.MaxCharactersInDocument,
                MaxCharactersFromEntities = _options.MaxCharactersFromEntities,
                XmlResolver = null,
                CloseInput = false
            };

            XDocument document;
            try
            {
                using (XmlReader reader = XmlReader.Create(source, settings))
                {
                    document = XDocument.Load(reader, LoadOptions.SetLineInfo);
                }
            }
            catch (XmlException ex)
            {
                errors.Add(CreateMalformedError(ex));
                return;
            }

            try
            {
                // Concurrency (AUD-N2): XmlSchemaSet documents no thread-safety guarantee for instance members, and
                // XDocument.Validate builds an XmlSchemaValidator over the set's shared XmlNameTable, which it may write to
                // (NameTable.Add is not thread-safe). Validation against one shared compiled set is therefore serialised
                // per set; loading, mapping and Schematron execution of concurrent documents stay parallel. Per-thread
                // clones were rejected: compiling the official closure costs seconds and tens of megabytes per copy.
                lock (schemaSet)
                {
                    ValidateDocument(document, schemaSet, errors);
                }
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

        private static void ValidateDocument(XDocument document, XmlSchemaSet schemaSet, List<ValidationError> errors)
        {
            document.Validate(
                schemaSet,
                (sender, args) =>
                {
                    if (args.Severity == XmlSeverityType.Error)
                    {
                        errors.Add(new ValidationError(
                            code: "XSD-SCHEMA-VIOLATION",
                            message: args.Message ?? "XSD validation error.",
                            category: ErrorCategory.SchemaViolation,
                            severity: ValidationSeverity.Error,
                            location: args.Exception?.SourceUri,
                            lineNumber: args.Exception?.LineNumber,
                            linePosition: args.Exception?.LinePosition));
                    }
                },
                addSchemaInfo: false);
        }

        private XmlQualifiedName? ReadRootElementName(Stream source, List<ValidationError> errors)
        {
            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                MaxCharactersInDocument = _options.MaxCharactersInDocument,
                MaxCharactersFromEntities = _options.MaxCharactersFromEntities,
                XmlResolver = null,
                CloseInput = false
            };

            try
            {
                using (XmlReader reader = XmlReader.Create(source, settings))
                {
                    if (reader.MoveToContent() == XmlNodeType.Element)
                    {
                        return new XmlQualifiedName(reader.LocalName, reader.NamespaceURI);
                    }
                }

                errors.Add(new ValidationError(
                    code: "XML-NO-ROOT-ELEMENT",
                    message: "The XML document does not contain a root element.",
                    category: ErrorCategory.MalformedInput,
                    severity: ValidationSeverity.Error));
            }
            catch (XmlException ex)
            {
                errors.Add(CreateMalformedError(ex));
            }

            return null;
        }

        private static ValidationError CreateMalformedError(XmlException ex) =>
            new ValidationError(
                code: "XML-MALFORMED",
                message: ex.Message,
                category: ErrorCategory.MalformedInput,
                severity: ValidationSeverity.Error,
                location: null,
                lineNumber: ex.LineNumber,
                linePosition: ex.LinePosition);

        private static MemoryStream BufferStream(Stream input)
        {
            var buffer = new MemoryStream();
            input.CopyTo(buffer);
            buffer.Position = 0;
            return buffer;
        }
    }
}
