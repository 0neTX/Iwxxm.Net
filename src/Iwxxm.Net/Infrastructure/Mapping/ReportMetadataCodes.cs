using System.Collections.Generic;
using System.Xml.Linq;
using Iwxxm.Net.Reports;

namespace Iwxxm.Net.Infrastructure.Mapping
{
    /// <summary>
    /// Translates the IWXXM report metadata attributes (<c>common.xsd</c> ReportType) between their official codes and
    /// the domain enums. <c>reportStatus</c> and <c>permissibleUsage</c> are <c>use="required"</c>: a missing or unknown
    /// value is a mapping error, never a default.
    /// </summary>
    internal static class ReportMetadataCodes
    {
        internal const string ReportStatusMissingCode = "ACL-REPORT-STATUS-MISSING";
        internal const string ReportStatusInvalidCode = "ACL-REPORT-STATUS-INVALID";
        internal const string PermissibleUsageMissingCode = "ACL-PERMISSIBLE-USAGE-MISSING";
        internal const string PermissibleUsageInvalidCode = "ACL-PERMISSIBLE-USAGE-INVALID";
        internal const string PermissibleUsageReasonInvalidCode = "ACL-PERMISSIBLE-USAGE-REASON-INVALID";

        /// <summary>Report metadata read from a document.</summary>
        internal readonly struct Metadata
        {
            public Metadata(ReportStatus status, PermissibleUsage usage, PermissibleUsageReason? reason, string? supplementary)
            {
                Status = status;
                Usage = usage;
                Reason = reason;
                Supplementary = supplementary;
            }

            public ReportStatus Status { get; }
            public PermissibleUsage Usage { get; }
            public PermissibleUsageReason? Reason { get; }
            public string? Supplementary { get; }
        }

        /// <summary>Reads the metadata attributes of a report element.</summary>
        public static Metadata Read(XElement reportElement, string location, List<ValidationError> errors) =>
            Read(
                reportElement.Attribute("reportStatus")?.Value,
                reportElement.Attribute("permissibleUsage")?.Value,
                reportElement.Attribute("permissibleUsageReason")?.Value,
                reportElement.Attribute("permissibleUsageSupplementary")?.Value,
                location,
                errors);

        /// <summary>Reads raw metadata attribute values (also used for XmlSerializer transport DTOs).</summary>
        public static Metadata Read(
            string? reportStatus,
            string? permissibleUsage,
            string? permissibleUsageReason,
            string? permissibleUsageSupplementary,
            string location,
            List<ValidationError> errors)
        {
            ReportStatus status = default;
            if (string.IsNullOrWhiteSpace(reportStatus))
            {
                errors.Add(Error(ReportStatusMissingCode, "The required attribute 'reportStatus' is missing; no status is assumed.", location));
            }
            else if (!TryParseReportStatus(reportStatus!, out status))
            {
                errors.Add(Error(ReportStatusInvalidCode, $"Unknown reportStatus '{reportStatus}'. Expected NORMAL, AMENDMENT or CORRECTION.", location));
            }

            PermissibleUsage usage = default;
            if (string.IsNullOrWhiteSpace(permissibleUsage))
            {
                errors.Add(Error(PermissibleUsageMissingCode, "The required attribute 'permissibleUsage' is missing; the report is not assumed to be operational.", location));
            }
            else if (!TryParsePermissibleUsage(permissibleUsage!, out usage))
            {
                errors.Add(Error(PermissibleUsageInvalidCode, $"Unknown permissibleUsage '{permissibleUsage}'. Expected OPERATIONAL or NON-OPERATIONAL.", location));
            }

            PermissibleUsageReason? reason = null;
            if (!string.IsNullOrWhiteSpace(permissibleUsageReason))
            {
                if (TryParsePermissibleUsageReason(permissibleUsageReason!, out PermissibleUsageReason parsed))
                {
                    reason = parsed;
                }
                else
                {
                    errors.Add(Error(PermissibleUsageReasonInvalidCode, $"Unknown permissibleUsageReason '{permissibleUsageReason}'. Expected TEST or EXERCISE.", location));
                }
            }

            string? supplementary = string.IsNullOrWhiteSpace(permissibleUsageSupplementary) ? null : permissibleUsageSupplementary;
            return new Metadata(status, usage, reason, supplementary);
        }

        public static bool TryParseReportStatus(string code, out ReportStatus status)
        {
            switch (code.Trim())
            {
                case "NORMAL":
                    status = ReportStatus.Normal;
                    return true;
                case "AMENDMENT":
                    status = ReportStatus.Amendment;
                    return true;
                case "CORRECTION":
                    status = ReportStatus.Correction;
                    return true;
                default:
                    status = default;
                    return false;
            }
        }

        public static bool TryParsePermissibleUsage(string code, out PermissibleUsage usage)
        {
            switch (code.Trim())
            {
                case "OPERATIONAL":
                    usage = PermissibleUsage.Operational;
                    return true;
                case "NON-OPERATIONAL":
                    usage = PermissibleUsage.NonOperational;
                    return true;
                default:
                    usage = default;
                    return false;
            }
        }

        public static bool TryParsePermissibleUsageReason(string code, out PermissibleUsageReason reason)
        {
            switch (code.Trim())
            {
                case "TEST":
                    reason = PermissibleUsageReason.Test;
                    return true;
                case "EXERCISE":
                    reason = PermissibleUsageReason.Exercise;
                    return true;
                default:
                    reason = default;
                    return false;
            }
        }

        public static string ToCode(ReportStatus status) => status switch
        {
            ReportStatus.Normal => "NORMAL",
            ReportStatus.Amendment => "AMENDMENT",
            ReportStatus.Correction => "CORRECTION",
            _ => status.ToString()
        };

        public static string ToCode(PermissibleUsage usage) => usage switch
        {
            PermissibleUsage.Operational => "OPERATIONAL",
            PermissibleUsage.NonOperational => "NON-OPERATIONAL",
            _ => usage.ToString()
        };

        public static string ToCode(PermissibleUsageReason reason) => reason switch
        {
            PermissibleUsageReason.Test => "TEST",
            PermissibleUsageReason.Exercise => "EXERCISE",
            _ => reason.ToString()
        };

        private static ValidationError Error(string code, string message, string location) =>
            new ValidationError(code, message, ErrorCategory.DomainMapping, ValidationSeverity.Error, location);
    }
}
