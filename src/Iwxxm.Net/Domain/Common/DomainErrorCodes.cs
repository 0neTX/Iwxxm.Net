using System;

namespace Iwxxm.Net.Reports
{
    /// <summary>
    /// Error codes of the physical / structural invariants shared by every aggregate <c>Create</c> factory.
    /// </summary>
    internal static class DomainErrorCodes
    {
        internal const string TimeNotUtc = "DOMAIN-TIME-NOT-UTC";
        internal const string IssueTimeMissing = "DOMAIN-ISSUE-TIME-MISSING";
        internal const string ReportStatusInvalid = "DOMAIN-REPORT-STATUS-INVALID";
        internal const string PermissibleUsageInvalid = "DOMAIN-PERMISSIBLE-USAGE-INVALID";
        internal const string PermissibleUsageReasonInvalid = "DOMAIN-PERMISSIBLE-USAGE-REASON-INVALID";
        internal const string DirectionOutOfRange = "DOMAIN-DIRECTION-OUT-OF-RANGE";
        internal const string NegativeSpeed = "DOMAIN-NEGATIVE-SPEED";
        internal const string NegativeVisibility = "DOMAIN-NEGATIVE-VISIBILITY";
        internal const string NegativeRvr = "DOMAIN-NEGATIVE-RVR";
        internal const string ForecastGroupPeriod = "DOMAIN-FORECAST-GROUP-PERIOD";
    }
}
