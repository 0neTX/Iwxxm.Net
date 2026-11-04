using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Xml.Xsl;
using Iwxxm.Net.Extensibility;
using Iwxxm.Net.Infrastructure.Caching;
using Iwxxm.Net.Infrastructure.Resources;
using Iwxxm.Net.Infrastructure.Xml;

namespace Iwxxm.Net.Infrastructure.Validation
{
    /// <summary>
    /// Business rules validation engine for ICAO/WMO IWXXM 2025-2 reports.
    /// Executes precompiled ISO Schematron XSLT stylesheets via <see cref="XslCompiledTransform"/>
    /// and parses standard SVRL (<c>svrl:failed-assert</c> and <c>svrl:successful-report</c>) output.
    /// </summary>
    internal sealed class XsltSchematronValidator : ISchematronValidator
    {
        private const string SvrlNamespace = "http://purl.oclc.org/dsdl/svrl";
        private const string EmbeddedXsltResource = "Schematron/iwxxm-2025-2-sigwx.xslt";

        internal const string UnavailableCode = "SCHEMATRON-UNAVAILABLE";
        internal const string ExecutionErrorCode = "SCHEMATRON-EXECUTION-ERROR";
        internal const string ReportSizeLimitCode = "SCHEMATRON-REPORT-SIZE-LIMIT";
        internal const string ReportInvalidCode = "SCHEMATRON-REPORT-INVALID";

        // XslCompiledTransform is thread-safe for concurrent Transform() executions once loaded. A compile failure is
        // not cached (AUD-L6): the next validation retries the compilation.
        private static readonly RetryOnFailureLazy<XslCompiledTransform> SharedCompiledSchematron =
            new RetryOnFailureLazy<XslCompiledTransform>(LoadAndCompileXslt);

        private static readonly EmbeddedXmlUrlResolver SharedEmbeddedResolver = new EmbeddedXmlUrlResolver();

        // document() targets (official code lists) parsed once per process and shared by every Transform (AUD-L5).
        internal static readonly CachedRuleDocumentResolver SharedRuleDocuments = new CachedRuleDocumentResolver();

        private readonly IwxxmParserOptions _options;
        private readonly RetryOnFailureLazy<XslCompiledTransform> _compiledSchematron;

        public XsltSchematronValidator(IwxxmParserOptions? options = null)
            : this(options, SharedCompiledSchematron)
        {
        }

        /// <summary>Test seam: uses <paramref name="compiledSchematron"/> instead of the process-wide compiled stylesheet.</summary>
        internal XsltSchematronValidator(IwxxmParserOptions? options, RetryOnFailureLazy<XslCompiledTransform> compiledSchematron)
        {
            _options = options ?? IwxxmParserOptions.Default;
            _compiledSchematron = compiledSchematron ?? throw new ArgumentNullException(nameof(compiledSchematron));
        }

        internal static XslCompiledTransform LoadAndCompileXslt()
        {
            var transform = new XslCompiledTransform(enableDebug: false);
            var xsltSettings = new XsltSettings(enableDocumentFunction: true, enableScript: false);
            var readerSettings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = SharedEmbeddedResolver
            };

            using (Stream xsltStream = EmbeddedResourceLoader.GetRequiredResourceStream(EmbeddedXsltResource))
            using (XmlReader xsltReader = XmlReader.Create(xsltStream, readerSettings, new Uri(EmbeddedXmlUrlResolver.EmbeddedRootUri, EmbeddedXsltResource).AbsoluteUri))
            {
                transform.Load(xsltReader, xsltSettings, SharedEmbeddedResolver);
            }

            return transform;
        }

        /// <summary>
        /// Runs the compiled rules over the raw bytes of <paramref name="document"/>. The SVRL report is written to a size-bounded buffer
        /// (<see cref="IwxxmParserOptions.MaxRuleReportBytes"/>) and read back as a stream with its own limits, never with the
        /// input-document quotas (AUD-M14); at most <see cref="IwxxmParserOptions.MaxValidationErrors"/> findings are returned.
        /// </summary>
        public IReadOnlyList<ValidationError> Validate(IwxxmDocumentContext document)
        {
            if (document == null)
                throw new ArgumentNullException(nameof(document));

            var errors = new List<ValidationError>();

            XslCompiledTransform compiled;
            try
            {
                compiled = _compiledSchematron.Value;
            }
            catch (Exception ex) when (!(ex is OutOfMemoryException))
            {
                errors.Add(new ValidationError(
                    code: UnavailableCode,
                    message: $"The business-rule stylesheet could not be compiled: {ex.Message}",
                    category: ErrorCategory.BusinessRuleViolation));
                return errors;
            }

            using (var svrlOutput = new BoundedOutputStream(_options.MaxRuleReportBytes))
            {
                try
                {
                    using (Stream input = document.OpenRead())
                    using (XmlReader inputReader = SecureXml.CreateReader(input, document.Options))
                    using (XmlWriter svrlWriter = XmlWriter.Create(svrlOutput, compiled.OutputSettings))
                    {
                        compiled.Transform(inputReader, null, svrlWriter, SharedRuleDocuments);
                    }
                }
                catch (Exception) when (svrlOutput.LimitExceeded)
                {
                    errors.Add(new ValidationError(
                        code: ReportSizeLimitCode,
                        message: $"The business-rule report exceeded the maximum allowed size of {_options.MaxRuleReportBytes} bytes; " +
                                 "the document produces too many findings to be evaluated.",
                        category: ErrorCategory.BusinessRuleViolation));
                    return errors;
                }
                catch (XmlException ex)
                {
                    errors.Add(SecureXml.Malformed(ex));
                    return errors;
                }
                catch (XsltException ex)
                {
                    errors.Add(new ValidationError(
                        code: ExecutionErrorCode,
                        message: $"Error executing Schematron XSLT transformation: {ex.Message}",
                        category: ErrorCategory.BusinessRuleViolation,
                        severity: ValidationSeverity.Error,
                        location: null,
                        lineNumber: ex.LineNumber,
                        linePosition: ex.LinePosition));
                    return errors;
                }

                using (MemoryStream svrl = svrlOutput.OpenRead())
                {
                    try
                    {
                        ParseSvrlReport(svrl, errors, _options);
                    }
                    catch (XmlException ex)
                    {
                        errors.Add(new ValidationError(
                            code: ReportInvalidCode,
                            message: $"The business-rule report could not be read: {ex.Message}",
                            category: ErrorCategory.BusinessRuleViolation));
                    }
                }
            }

            return errors;
        }

        /// <summary>
        /// Streams the SVRL report: <c>svrl:failed-assert</c> findings take the severity of their <c>@role</c>
        /// (<c>warn</c>/<c>warning</c>/<c>info</c>/<c>information</c> → warning, anything else → error);
        /// <c>svrl:successful-report</c> findings are warnings unless their role is explicitly an error
        /// (<c>error</c>/<c>fatal</c>), because a fired report is informative by Schematron convention (AUD-U5).
        /// </summary>
        private static void ParseSvrlReport(Stream svrlStream, List<ValidationError> errors, IwxxmParserOptions options)
        {
            var readerSettings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                MaxCharactersInDocument = options.MaxRuleReportBytes,
                MaxCharactersFromEntities = 0,
                XmlResolver = null
            };

            var limit = new ValidationFindingLimit(errors, options.MaxValidationErrors, ValidationFindingLimit.SchematronTooManyErrorsCode, ErrorCategory.BusinessRuleViolation);

            using (XmlReader svrlReader = XmlReader.Create(svrlStream, readerSettings))
            {
                while (svrlReader.Read())
                {
                    if (svrlReader.NodeType == XmlNodeType.Element &&
                        svrlReader.NamespaceURI == SvrlNamespace &&
                        (svrlReader.LocalName == "failed-assert" || svrlReader.LocalName == "successful-report"))
                    {
                        bool isReport = svrlReader.LocalName == "successful-report";
                        string ruleId = svrlReader.GetAttribute("id") ?? "SCHEMATRON-ASSERT-FAILED";
                        string? location = svrlReader.GetAttribute("location");
                        string? role = svrlReader.GetAttribute("role") ?? svrlReader.GetAttribute("flag");
                        string? testExpr = svrlReader.GetAttribute("test");

                        string messageText = ReadSvrlTextElement(svrlReader.ReadSubtree())
                                             ?? $"Schematron business rule assertion failed ({testExpr}).";

                        limit.TryAdd(new ValidationError(
                            code: ruleId,
                            message: messageText.Trim(),
                            category: ErrorCategory.BusinessRuleViolation,
                            severity: SvrlSeverity.Map(role, isReport),
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
