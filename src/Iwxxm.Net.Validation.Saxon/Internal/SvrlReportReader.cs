using System;
using System.Collections.Generic;
using Saxon.Api;

namespace Iwxxm.Net.Validation.Saxon.Internal
{
    /// <summary>
    /// Maps an SVRL report produced by SchXslt onto <see cref="ValidationError"/> instances.
    /// <list type="bullet">
    /// <item><b>Code</b>: <c>svrl:failed-assert/@id</c> or <c>svrl:successful-report/@id</c> when present, otherwise the
    /// <c>@id</c> of the preceding <c>svrl:active-pattern</c> (the official rule identifier, e.g.
    /// <c>METAR_SPECI.MeteorologicalAerodromeObservation-5</c>; the official rules declare ids on patterns only).</item>
    /// <item><b>Severity</b>: <c>@role</c> (or <c>@flag</c>) of <c>warn</c>, <c>warning</c>, <c>info</c> or
    /// <c>information</c> maps to <see cref="ValidationSeverity.Warning"/>; anything else (including no role, which is
    /// the case for every official assertion) maps to <see cref="ValidationSeverity.Error"/>.</item>
    /// <item><b>Location</b>: <c>@location</c> (an XPath with <c>Q{namespace}local</c> steps).</item>
    /// <item><b>Message</b>: <c>svrl:text</c>.</item>
    /// </list>
    /// </summary>
    internal static class SvrlReportReader
    {
        internal const string SvrlNamespace = "http://purl.oclc.org/dsdl/svrl";
        internal const string FallbackCode = "SCHEMATRON-ASSERT-FAILED";

        public static List<ValidationError> Read(XdmNode svrlDocument)
        {
            if (svrlDocument == null)
                throw new ArgumentNullException(nameof(svrlDocument));

            var findings = new List<ValidationError>();
            XdmNode? root = svrlDocument.NodeKind == XdmNodeKind.Document ? svrlDocument.OutermostElement : svrlDocument;
            if (root == null)
                return findings;

            string? currentPatternId = null;
            foreach (XdmNode child in root.Children())
            {
                QName? name = child.NodeName;
                if (name == null || name.Uri != SvrlNamespace)
                    continue;

                switch (name.LocalName)
                {
                    case "active-pattern":
                        currentPatternId = NullIfEmpty(child.GetAttributeValue("id")) ?? NullIfEmpty(child.GetAttributeValue("name"));
                        break;
                    case "failed-assert":
                    case "successful-report":
                        findings.Add(ToValidationError(child, currentPatternId));
                        break;
                }
            }

            return findings;
        }

        private static ValidationError ToValidationError(XdmNode finding, string? patternId)
        {
            string code = NullIfEmpty(finding.GetAttributeValue("id")) ?? patternId ?? FallbackCode;
            string? test = finding.GetAttributeValue("test");

            string? text = null;
            foreach (XdmNode textNode in finding.Children(SvrlNamespace, "text"))
            {
                text = NullIfEmpty(textNode.StringValue?.Trim());
                break;
            }

            return new ValidationError(
                code: code,
                message: text ?? $"Official Schematron assertion failed ({test}).",
                category: ErrorCategory.BusinessRuleViolation,
                severity: MapSeverity(finding.GetAttributeValue("role") ?? finding.GetAttributeValue("flag")),
                location: NullIfEmpty(finding.GetAttributeValue("location")));
        }

        private static ValidationSeverity MapSeverity(string? role)
        {
            switch (role?.Trim().ToLowerInvariant())
            {
                case "warn":
                case "warning":
                case "info":
                case "information":
                    return ValidationSeverity.Warning;
                default:
                    return ValidationSeverity.Error;
            }
        }

        private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
