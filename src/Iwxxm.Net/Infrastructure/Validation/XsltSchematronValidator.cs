using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Xml.Xsl;
using Iwxxm.Net.Application.Ports;
using Iwxxm.Net.Domain.Common;
using Iwxxm.Net.Infrastructure.Resources;

namespace Iwxxm.Net.Infrastructure.Validation
{
    /// <summary>
    /// Business rules validation engine for ICAO/WMO IWXXM 2025-2 SIGWX reports.
    /// Executes precompiled ISO Schematron XSLT stylesheets via <see cref="XslCompiledTransform"/>
    /// and parses standard SVRL (<c>svrl:failed-assert</c> and <c>svrl:successful-report</c>) output.
    /// </summary>
    internal sealed class XsltSchematronValidator : ISchematronValidator
    {
        private const string SvrlNamespace = "http://purl.oclc.org/dsdl/svrl";
        private const string EmbeddedXsltResource = "Iwxxm.Net.Resources.Schematron.iwxxm-2025-2-sigwx.xslt";

        // XslCompiledTransform is thread-safe for concurrent Transform() executions once loaded.
        private static readonly Lazy<XslCompiledTransform> CompiledSchematron =
            new Lazy<XslCompiledTransform>(LoadAndCompileXslt, isThreadSafe: true);

        private static readonly EmbeddedXmlUrlResolver SharedEmbeddedResolver = new EmbeddedXmlUrlResolver();

        private static XslCompiledTransform LoadAndCompileXslt()
        {
            var transform = new XslCompiledTransform(enableDebug: false);
            var xsltSettings = new XsltSettings(enableDocumentFunction: true, enableScript: false);
            var readerSettings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = SharedEmbeddedResolver
            };

            using (Stream xsltStream = EmbeddedResourceLoader.GetRequiredResourceStream(EmbeddedXsltResource))
            using (XmlReader xsltReader = XmlReader.Create(xsltStream, readerSettings, "embedded://iwxxm-resources/iwxxm-2025-2-sigwx.xslt"))
            {
                transform.Load(xsltReader, xsltSettings, SharedEmbeddedResolver);
            }

            return transform;
        }

        public IReadOnlyList<ValidationError> Validate(Stream xmlStream)
        {
            if (xmlStream == null)
                throw new ArgumentNullException(nameof(xmlStream));

            var errors = new List<ValidationError>();

            var inputReaderSettings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null
            };

            try
            {
                using (var svrlOutputStream = new MemoryStream())
                {
                    using (XmlReader inputReader = XmlReader.Create(xmlStream, inputReaderSettings))
                    using (XmlWriter svrlWriter = XmlWriter.Create(svrlOutputStream, CompiledSchematron.Value.OutputSettings))
                    {
                        CompiledSchematron.Value.Transform(inputReader, null, svrlWriter, SharedEmbeddedResolver);
                    }

                    svrlOutputStream.Position = 0;
                    ParseSvrlReport(svrlOutputStream, errors);
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
            catch (XsltException ex)
            {
                errors.Add(new ValidationError(
                    code: "SCHEMATRON-EXECUTION-ERROR",
                    message: $"Error executing Schematron XSLT transformation: {ex.Message}",
                    category: ErrorCategory.SchematronBusinessRule,
                    severity: ValidationSeverity.Error,
                    location: null,
                    lineNumber: ex.LineNumber,
                    linePosition: ex.LinePosition));
            }

            return errors;
        }

        private static void ParseSvrlReport(Stream svrlStream, List<ValidationError> errors)
        {
            var readerSettings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null
            };

            using (XmlReader svrlReader = XmlReader.Create(svrlStream, readerSettings))
            {
                while (svrlReader.Read())
                {
                    if (svrlReader.NodeType == XmlNodeType.Element &&
                        svrlReader.NamespaceURI == SvrlNamespace &&
                        (svrlReader.LocalName == "failed-assert" || svrlReader.LocalName == "successful-report"))
                    {
                        string ruleId = svrlReader.GetAttribute("id") ?? "SCHEMATRON-ASSERT-FAILED";
                        string? location = svrlReader.GetAttribute("location");
                        string? role = svrlReader.GetAttribute("role");
                        string? testExpr = svrlReader.GetAttribute("test");

                        ValidationSeverity severity = string.Equals(role, "WARN", StringComparison.OrdinalIgnoreCase) ||
                                                      string.Equals(role, "WARNING", StringComparison.OrdinalIgnoreCase)
                            ? ValidationSeverity.Warning
                            : ValidationSeverity.Error;

                        string messageText = ReadSvrlTextElement(svrlReader.ReadSubtree())
                                             ?? $"Schematron business rule assertion failed ({testExpr}).";

                        errors.Add(new ValidationError(
                            code: ruleId,
                            message: messageText.Trim(),
                            category: ErrorCategory.SchematronBusinessRule,
                            severity: severity,
                            location: location));
                    }
                }
            }
        }

        private static string? ReadSvrlTextElement(XmlReader subtreeReader)
        {
            using (subtreeReader)
            {
                while (subtreeReader.Read())
                {
                    if (subtreeReader.NodeType == XmlNodeType.Element &&
                        subtreeReader.NamespaceURI == SvrlNamespace &&
                        subtreeReader.LocalName == "text")
                    {
                        return subtreeReader.ReadElementContentAsString();
                    }
                }
            }
            return null;
        }
    }
}
