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
        private static readonly string[] EmbeddedSchemaFiles =
        {
            "Iwxxm.Net.Resources.Xsd.xlink.xsd",
            "Iwxxm.Net.Resources.Xsd.gml.xsd",
            "Iwxxm.Net.Resources.Xsd.iwxxm-sigwx.xsd"
        };

        private static readonly Lazy<XmlSchemaSet> CompiledSchemaSet =
            new Lazy<XmlSchemaSet>(BuildAndCompileSchemaSet, isThreadSafe: true);

        private static XmlSchemaSet BuildAndCompileSchemaSet()
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

            foreach (string resourceName in EmbeddedSchemaFiles)
            {
                using (Stream xsdStream = EmbeddedResourceLoader.GetRequiredResourceStream(resourceName))
                using (XmlReader schemaReader = XmlReader.Create(xsdStream, readerSettings))
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

            var settings = new XmlReaderSettings
            {
                ValidationType = ValidationType.Schema,
                Schemas = CompiledSchemaSet.Value,
                ValidationFlags =
                    XmlSchemaValidationFlags.ProcessIdentityConstraints |
                    XmlSchemaValidationFlags.ReportValidationWarnings,
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null
            };

            settings.ValidationEventHandler += (sender, args) =>
            {
                if (args.Severity == XmlSeverityType.Error)
                {
                    errors.Add(new ValidationError(
                        code: "XSD-SCHEMA-VIOLATION",
                        message: args.Message,
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
