using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Xml.Schema;
using Iwxxm.Net.Application.Ports;
using Iwxxm.Net.Domain.Common;
using Iwxxm.Net.Infrastructure.Resources;

namespace Iwxxm.Net.Infrastructure.Validation
{
    /// <summary>
    /// Structural XML validation engine backed by the official IWXXM 2025-2, OGC GML 3.2.1, and W3C XLink
    /// XSD schemas embedded in the assembly. Lazily compiles the <see cref="XmlSchemaSet"/> in a thread-safe manner.
    /// </summary>
    internal sealed class XsdValidator : IXmlSchemaValidator
    {
        private static readonly string[] SigwxEmbeddedSchemaFiles =
        {
            "Iwxxm.Net.Resources.Xsd.xlink.xsd",
            "Iwxxm.Net.Resources.Xsd.gml.xsd",
            "Iwxxm.Net.Resources.Xsd.aixm.xsd",
            "Iwxxm.Net.Resources.Xsd.iwxxm-sigwx.xsd"
        };

        private static readonly string[] FullSuiteEmbeddedSchemaFiles =
        {
            "Iwxxm.Net.Resources.Xsd.xlink.xsd",
            "Iwxxm.Net.Resources.Xsd.gml.xsd",
            "Iwxxm.Net.Resources.Xsd.aixm.xsd",
            "Iwxxm.Net.Resources.Xsd.metce.xsd",
            "Iwxxm.Net.Resources.Xsd.iwxxm-2025-2.xsd",
            "Iwxxm.Net.Resources.Xsd.collect.xsd"
        };

        private static readonly Lazy<XmlSchemaSet> CompiledSigwxSchemaSet =
            new Lazy<XmlSchemaSet>(() => BuildAndCompileSchemaSet(SigwxEmbeddedSchemaFiles), isThreadSafe: true);

        private static readonly Lazy<XmlSchemaSet> CompiledFullSuiteSchemaSet =
            new Lazy<XmlSchemaSet>(() => BuildAndCompileSchemaSet(FullSuiteEmbeddedSchemaFiles), isThreadSafe: true);

        private readonly bool _useFullSuite;
        private readonly IwxxmParserOptions _options;

        public XsdValidator(bool useFullSuite = false, IwxxmParserOptions? options = null)
        {
            _useFullSuite = useFullSuite;
            _options = options ?? IwxxmParserOptions.Default;
        }

        public static XsdValidator CreateFullSuiteValidator(IwxxmParserOptions? options = null) =>
            new XsdValidator(useFullSuite: true, options: options);

        private static XmlSchemaSet BuildAndCompileSchemaSet(string[] schemaResourceNames)
        {
            var resolver = new EmbeddedXmlUrlResolver();
            var schemaSet = new XmlSchemaSet
            {
                XmlResolver = resolver
            };

            var readerSettings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = resolver
            };

            const string resourcePrefix = "Iwxxm.Net.Resources.Xsd.";
            foreach (string resourceName in schemaResourceNames)
            {
                string fileName = resourceName.StartsWith(resourcePrefix, StringComparison.Ordinal)
                    ? resourceName.Substring(resourcePrefix.Length)
                    : Path.GetFileName(resourceName);
                using (Stream xsdStream = EmbeddedResourceLoader.GetRequiredResourceStream(resourceName))
                using (XmlReader schemaReader = XmlReader.Create(xsdStream, readerSettings, $"embedded://iwxxm-resources/{fileName}"))
                {
                    XmlSchema schema = XmlSchema.Read(schemaReader, (sender, args) =>
                    {
                        if (args.Severity == XmlSeverityType.Error)
                        {
                            throw new XmlSchemaException(
                                $"Error compiling embedded XSD schema '{resourceName}': {args.Message}",
                                args.Exception);
                        }
                    })!;

                    schemaSet.Add(schema);
                }
            }

            schemaSet.Compile();
            return schemaSet;
        }

        public IReadOnlyList<ValidationError> Validate(Stream xmlStream)
        {
            if (xmlStream == null)
                throw new ArgumentNullException(nameof(xmlStream));

            var errors = new List<ValidationError>();
            XmlSchemaSet activeSchemaSet = _useFullSuite ? CompiledFullSuiteSchemaSet.Value : CompiledSigwxSchemaSet.Value;

            var settings = new XmlReaderSettings
            {
                ValidationType = ValidationType.Schema,
                Schemas = activeSchemaSet,
                ValidationFlags =
                    XmlSchemaValidationFlags.ProcessIdentityConstraints |
                    XmlSchemaValidationFlags.ReportValidationWarnings,
                DtdProcessing = DtdProcessing.Prohibit,
                MaxCharactersInDocument = _options.MaxCharactersInDocument,
                MaxCharactersFromEntities = _options.MaxCharactersFromEntities,
                XmlResolver = null
            };

            settings.ValidationEventHandler += (sender, args) =>
            {
                bool isMissingRootSchema =
                    args.Severity == XmlSeverityType.Warning &&
                    args.Message != null &&
                    args.Message.IndexOf("Could not find schema information for the element", StringComparison.OrdinalIgnoreCase) >= 0;

                if (args.Severity == XmlSeverityType.Error || isMissingRootSchema)
                {
                    errors.Add(new ValidationError(
                        code: "XSD-SCHEMA-VIOLATION",
                        message: args.Message ?? "XSD validation error.",
                        category: ErrorCategory.XsdSchemaValidation,
                        severity: ValidationSeverity.Error,
                        location: args.Exception?.SourceUri,
                        lineNumber: args.Exception?.LineNumber,
                        linePosition: args.Exception?.LinePosition));
                }
            };

            try
            {
                using (XmlReader validatingReader = XmlReader.Create(xmlStream, settings))
                {
                    while (validatingReader.Read())
                    {
                        // Sequentially stream through the XML document to trigger all XSD validation rules
                    }
                }
            }
            catch (XmlException ex)
            {
                errors.Add(new ValidationError(
                    code: "XML-MALFORMED",
                    message: ex.Message,
                    category: ErrorCategory.XmlWellFormedness,
                    severity: ValidationSeverity.Error,
                    location: null,
                    lineNumber: ex.LineNumber,
                    linePosition: ex.LinePosition));
            }
            catch (XmlSchemaException ex)
            {
                errors.Add(new ValidationError(
                    code: "XSD-FATAL-ERROR",
                    message: ex.Message,
                    category: ErrorCategory.XsdSchemaValidation,
                    severity: ValidationSeverity.Error,
                    location: null,
                    lineNumber: ex.LineNumber,
                    linePosition: ex.LinePosition));
            }

            return errors;
        }
    }
}
