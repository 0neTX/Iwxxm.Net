namespace Iwxxm.Net.Infrastructure.Mapping.Xml
{
    /// <summary>
    /// Mapping error codes for mandatory content that is absent or cannot be interpreted (never defaulted), and the
    /// factory of the corresponding <see cref="ValidationError"/>.
    /// </summary>
    internal static class MappingErrors
    {
        internal const string IssueTimeMissingCode = "ACL-ISSUE-TIME-MISSING";
        internal const string ObservationTimeMissingCode = "ACL-OBSERVATION-TIME-MISSING";
        internal const string ValidPeriodMissingCode = "ACL-VALID-PERIOD-MISSING";
        internal const string TimeInvalidCode = "ACL-TIME-INVALID";
        internal const string AshDensityMissingCode = "ACL-ASH-DENSITY-MISSING";
        internal const string VolcanoNameMissingCode = "ACL-VOLCANO-NAME-MISSING";
        internal const string CircleRadiusMissingCode = "ACL-CIRCLE-RADIUS-MISSING";
        internal const string UnsupportedSegmentCode = "ACL-GEOMETRY-SEGMENT-UNSUPPORTED";
        internal const string MultiPatchUnsupportedCode = "ACL-GML-MULTI-PATCH-UNSUPPORTED";
        internal const string InteriorRingUnreadableCode = "ACL-GML-INTERIOR-RING-UNREADABLE";

        public static ValidationError Create(string code, string message, string location) =>
            new ValidationError(code, message, ErrorCategory.DomainMapping, ValidationSeverity.Error, string.IsNullOrEmpty(location) ? null : location);
    }
}
