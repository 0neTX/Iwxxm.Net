namespace Iwxxm.Net.Reports
{
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
