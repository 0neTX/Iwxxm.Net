namespace Iwxxm.Net.Reports
{
    /// <summary>
    /// Issuance status of a report (official <c>iwxxm:ReportStatusType</c>, <c>common.xsd</c>; the attribute
    /// <c>reportStatus</c> is <c>use="required"</c>). Members start at 1 so that <c>default(ReportStatus)</c> is not a
    /// valid status and cannot be used as an accidental default.
    /// </summary>
    public enum ReportStatus
    {
        /// <summary><c>NORMAL</c>: the report is issued for the first time.</summary>
        Normal = 1,

        /// <summary><c>AMENDMENT</c>: the report is an amendment to a previously issued report.</summary>
        Amendment = 2,

        /// <summary><c>CORRECTION</c>: the report is a correction to a previously issued report.</summary>
        Correction = 3
    }

    /// <summary>
    /// Permitted usage of the report data (official <c>iwxxm:PermissibleUsageType</c>, <c>common.xsd</c>; the attribute
    /// <c>permissibleUsage</c> is <c>use="required"</c>). Members start at 1 so that <c>default(PermissibleUsage)</c> is
    /// not a valid value: a document without the attribute is never presented as operational.
    /// </summary>
    public enum PermissibleUsage
    {
        /// <summary><c>OPERATIONAL</c>: data may be used for operational purposes.</summary>
        Operational = 1,

        /// <summary><c>NON-OPERATIONAL</c>: data should not be used for operational purposes.</summary>
        NonOperational = 2
    }

    /// <summary>
    /// Reason for non-operational data (official <c>iwxxm:PermissibleUsageReasonType</c>, <c>common.xsd</c>; the
    /// attribute <c>permissibleUsageReason</c> is optional and only populated for <c>NON-OPERATIONAL</c> reports).
    /// </summary>
    public enum PermissibleUsageReason
    {
        /// <summary><c>TEST</c>: the data is part of a test activity.</summary>
        Test = 1,

        /// <summary><c>EXERCISE</c>: the data is part of an exercise.</summary>
        Exercise = 2
    }
}
