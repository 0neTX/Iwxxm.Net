using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using Iwxxm.Net.Extensibility;
using Iwxxm.Net.Infrastructure.Xml;
using Iwxxm.Net.Validation.Saxon.Internal;
using Saxon.Api;

namespace Iwxxm.Net.Validation.Saxon
{
    /// <summary>
    /// <see cref="ISchematronValidator"/> adapter that executes the complete, unmodified official WMO IWXXM 2025-2
    /// Schematron (<c>iwxxm.sch</c>, <c>queryBinding="xslt2"</c>, 165 rule patterns) with SaxonCS-HE.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>The rule set is compiled once per process with the vendored SchXslt 1.10.1 compiler and cached as an
    /// immutable, thread-safe Saxon executable; instances of this class are lightweight and thread-safe.</item>
    /// <item>Runs air-gapped: <c>document()</c> code-list lookups resolve to the official WMO RDF files embedded in
    /// <c>Iwxxm.Net</c>; every other URI is refused.</item>
    /// <item>Input documents are read from <see cref="IwxxmDocumentContext.OpenRead"/> with DTD processing prohibited, no
    /// resolver and the character quotas of <see cref="IwxxmDocumentContext.Options"/>; the report limits
    /// (<see cref="IwxxmParserOptions.MaxRuleReportBytes"/>, <see cref="IwxxmParserOptions.MaxValidationErrors"/>) are those
    /// of the options given to the constructor.</item>
    /// <item>SVRL findings are mapped to <see cref="ValidationError"/> with <see cref="ErrorCategory.BusinessRuleViolation"/>
    /// and the official rule (pattern) identifier as <see cref="ValidationError.Code"/>. <see cref="ValidationError.Location"/>
    /// is an absolute, namespace-independent XPath 1.0 that selects the offending element, for example
    /// <c>/*[namespace-uri()='http://icao.int/iwxxm/2025-2' and local-name()='METAR'][1]/...</c> (the SchXslt
    /// <c>Q{namespace}local[n]</c> steps are rewritten because most XPath engines cannot parse them).</item>
    /// </list>
    /// Uses Saxon-HE capabilities only (<c>new Processor(false)</c>): no Saxonica license key is requested or required.
    /// </remarks>
    public sealed class OfficialSchematronValidator : ISchematronValidator
    {
        /// <summary>Error code reported when the input cannot be parsed.</summary>
        public const string MalformedInputCode = "XML-MALFORMED";

        /// <summary>Error code reported when the official Schematron cannot be compiled.</summary>
        public const string UnavailableCode = "SCHEMATRON-UNAVAILABLE";

        /// <summary>Error code reported when the compiled Schematron fails while evaluating the document.</summary>
        public const string ExecutionErrorCode = "SCHEMATRON-EXECUTION-ERROR";

        /// <summary>
        /// Error code reported when the SVRL report exceeds <see cref="IwxxmParserOptions.MaxRuleReportBytes"/>
        /// (a business-rule error: the input itself is well formed).
        /// </summary>
        public const string ReportSizeLimitCode = "SCHEMATRON-REPORT-SIZE-LIMIT";

        /// <summary>Error code appended once <see cref="IwxxmParserOptions.MaxValidationErrors"/> findings were reported.</summary>
        public const string TooManyErrorsCode = SvrlReportReader.TooManyErrorsCode;

        private static readonly Uri InputBaseUri = new Uri("http://input.iwxxm-net.invalid/document.xml");

        private readonly IwxxmParserOptions _options;

        /// <summary>Creates a validator applying the character quotas of <paramref name="options"/>.</summary>
        public OfficialSchematronValidator(IwxxmParserOptions? options = null)
        {
            _options = options ?? IwxxmParserOptions.Default;
        }

        /// <summary>
        /// Compiles the official Schematron now (instead of on the first validation), e.g. during application start-up.
        /// </summary>
        /// <returns>The time spent compiling, or <see cref="TimeSpan.Zero"/> if it was already compiled.</returns>
        public static TimeSpan Warmup()
        {
            bool alreadyCompiled = OfficialSchematronEngine.IsCompiled;
            OfficialSchematronEngine engine = OfficialSchematronEngine.Shared;
            return alreadyCompiled ? TimeSpan.Zero : engine.CompileTime;
        }

        /// <inheritdoc />
        public IReadOnlyList<ValidationError> Validate(IwxxmDocumentContext document)
        {
            if (document == null)
                throw new ArgumentNullException(nameof(document));

            OfficialSchematronEngine engine;
            try
            {
                engine = OfficialSchematronEngine.Shared;
            }
            catch (Exception ex)
            {
                return new[]
                {
                    new ValidationError(
                        UnavailableCode,
                        $"The official IWXXM Schematron could not be compiled: {ex.Message}",
                        ErrorCategory.BusinessRuleViolation)
                };
            }

            XdmNode input;
            ElementDepthLimitedReader? depthReader = null;
            try
            {
                DocumentBuilder builder = engine.Processor.NewDocumentBuilder();
                builder.BaseUri = InputBaseUri;
                using (Stream source = document.OpenRead())
                using (ElementDepthLimitedReader reader = depthReader = SecureXml.CreateReader(source, document.Options, InputBaseUri.AbsoluteUri))
                {
                    input = builder.Build(reader);
                }
            }
            catch (Exception ex)
            {
                // Saxon wraps the exception of its reader and keeps only the text: the depth-limit exception is recovered from the reader.
                if (depthReader?.Exceeded != null)
                    return new[] { SecureXml.Malformed(depthReader.Exceeded) };

                XmlException? xmlError = ex as XmlException ?? ex.InnerException as XmlException;

                return new[]
                {
                    new ValidationError(
                        MalformedInputCode,
                        xmlError?.Message ?? ex.Message,
                        ErrorCategory.MalformedInput,
                        ValidationSeverity.Error,
                        location: null,
                        lineNumber: xmlError?.LineNumber,
                        linePosition: xmlError?.LinePosition)
                };
            }

            // The SVRL report is serialised into a size-bounded buffer and streamed back (AUD-M14): it is never kept as a
            // Saxon tree, a runaway report stops the evaluation, and it is read with its own limits (not the input quotas).
            using (var svrl = new BoundedOutputStream(_options.MaxRuleReportBytes))
            {
                try
                {
                    Xslt30Transformer transformer = engine.CreateTransformer();
                    Serializer serializer = engine.Processor.NewSerializer(svrl);
                    transformer.ApplyTemplates(input, serializer);
                    serializer.Close();
                }
                catch (Exception) when (svrl.LimitExceeded)
                {
                    return new[]
                    {
                        new ValidationError(
                            ReportSizeLimitCode,
                            $"The official Schematron report exceeded the maximum allowed size of {_options.MaxRuleReportBytes} bytes; " +
                            "the document produces too many findings to be evaluated.",
                            ErrorCategory.BusinessRuleViolation)
                    };
                }
                catch (Exception ex)
                {
                    return new[]
                    {
                        new ValidationError(
                            ExecutionErrorCode,
                            $"Error executing the official IWXXM Schematron: {ex.Message}",
                            ErrorCategory.BusinessRuleViolation)
                    };
                }

                using (MemoryStream report = svrl.OpenRead())
                {
                    try
                    {
                        return SvrlReportReader.Read(report, _options.MaxValidationErrors, _options.MaxRuleReportBytes);
                    }
                    catch (XmlException ex)
                    {
                        return new[]
                        {
                            new ValidationError(
                                ExecutionErrorCode,
                                $"The official Schematron report could not be read: {ex.Message}",
                                ErrorCategory.BusinessRuleViolation)
                        };
                    }
                }
            }
        }
    }
}
