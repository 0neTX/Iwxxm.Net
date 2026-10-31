using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;

namespace Iwxxm.Net.Validation.Saxon.Internal
{
    /// <summary>
    /// Streams an SVRL report produced by SchXslt onto <see cref="ValidationError"/> instances (forward-only; the report is
    /// never materialised as a tree).
    /// <list type="bullet">
    /// <item><b>Code</b>: <c>svrl:failed-assert/@id</c> or <c>svrl:successful-report/@id</c> when present, otherwise the
    /// <c>@id</c> of the preceding <c>svrl:active-pattern</c> (the official rule identifier, e.g.
    /// <c>METAR_SPECI.MeteorologicalAerodromeObservation-5</c>; the official rules declare ids on patterns only).</item>
    /// <item><b>Severity</b>: <c>@role</c> (or <c>@flag</c>) of <c>warn</c>, <c>warning</c>, <c>info</c> or
    /// <c>information</c> maps to <see cref="ValidationSeverity.Warning"/>, <c>error</c>/<c>fatal</c> to
    /// <see cref="ValidationSeverity.Error"/>; without a role a failed assertion is an error (the case for every official
    /// assertion) and a successful report is a warning.</item>
    /// <item><b>Location</b>: <c>@location</c>, rewritten by <see cref="SvrlLocation"/> from the SchXslt <c>Q{namespace}local[n]</c>
    /// steps into an absolute, namespace-independent XPath 1.0 (<c>/*[namespace-uri()=...][n]</c>).</item>
    /// <item><b>Message</b>: <c>svrl:text</c>.</item>
    /// <item><b>Cap</b>: at most <c>maxFindings</c> findings; then a single <see cref="TooManyErrorsCode"/> sentinel.</item>
    /// </list>
    /// </summary>
    internal static class SvrlReportReader
    {
        internal const string SvrlNamespace = "http://purl.oclc.org/dsdl/svrl";
        internal const string FallbackCode = "SCHEMATRON-ASSERT-FAILED";
        internal const string TooManyErrorsCode = "SCHEMATRON-TOO-MANY-ERRORS";

        public static List<ValidationError> Read(Stream svrl, int maxFindings, long maxCharacters)
        {
            if (svrl == null)
                throw new ArgumentNullException(nameof(svrl));

            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                MaxCharactersInDocument = maxCharacters,
                MaxCharactersFromEntities = 0,
                CloseInput = false
            };

            var findings = new List<ValidationError>();
            int sentinelIndex = -1;
            string? currentPatternId = null;

            using (XmlReader reader = XmlReader.Create(svrl, settings))
            {
                while (reader.Read())
                {
                    if (reader.NodeType != XmlNodeType.Element || reader.NamespaceURI != SvrlNamespace)
                        continue;

                    switch (reader.LocalName)
                    {
                        case "active-pattern":
                            currentPatternId = NullIfEmpty(reader.GetAttribute("id")) ?? NullIfEmpty(reader.GetAttribute("name"));
                            break;
                        case "failed-assert":
                        case "successful-report":
                            ValidationError finding = ToValidationError(reader, currentPatternId);
                            if (sentinelIndex >= 0)
                            {
                                if (finding.Severity == ValidationSeverity.Error && findings[sentinelIndex].Severity != ValidationSeverity.Error)
                                    findings[sentinelIndex] = Sentinel(maxFindings, ValidationSeverity.Error);
                            }
                            else if (findings.Count >= maxFindings)
                            {
                                sentinelIndex = findings.Count;
                                findings.Add(Sentinel(maxFindings, finding.Severity));
                            }
                            else
                            {
                                findings.Add(finding);
                            }

                            break;
                    }
                }
            }

            return findings;
        }

        private static ValidationError Sentinel(int maxFindings, ValidationSeverity severity) =>
            new ValidationError(
                TooManyErrorsCode,
                $"Too many validation findings: only the first {maxFindings} are reported.",
                ErrorCategory.BusinessRuleViolation,
                severity);

        private static ValidationError ToValidationError(XmlReader reader, string? patternId)
        {
            bool isReport = reader.LocalName == "successful-report";
            string code = NullIfEmpty(reader.GetAttribute("id")) ?? patternId ?? FallbackCode;
            string? test = reader.GetAttribute("test");
            string? role = reader.GetAttribute("role") ?? reader.GetAttribute("flag");
            string? location = SvrlLocation.ToXPath(NullIfEmpty(reader.GetAttribute("location")));

            string? text = null;
            using (XmlReader subtree = reader.ReadSubtree())
            {
                while (subtree.Read())
                {
                    if (text == null && subtree.NodeType == XmlNodeType.Element && subtree.NamespaceURI == SvrlNamespace && subtree.LocalName == "text")
                    {
                        text = NullIfEmpty(subtree.ReadElementContentAsString().Trim());
                    }
                }
            }

            return new ValidationError(
                code: code,
                message: text ?? $"Official Schematron assertion failed ({test}).",
                category: ErrorCategory.BusinessRuleViolation,
                severity: MapSeverity(role, isReport),
                location: location);
        }

        internal static ValidationSeverity MapSeverity(string? role, bool isSuccessfulReport)
        {
            switch (role?.Trim().ToLowerInvariant())
            {
                case "warn":
                case "warning":
                case "info":
                case "information":
                    return ValidationSeverity.Warning;
                case "error":
                case "fatal":
                    return ValidationSeverity.Error;
                default:
                    return isSuccessfulReport ? ValidationSeverity.Warning : ValidationSeverity.Error;
            }
        }

        private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
