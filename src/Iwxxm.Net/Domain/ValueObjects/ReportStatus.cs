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
}
