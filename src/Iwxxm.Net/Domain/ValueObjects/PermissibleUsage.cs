namespace Iwxxm.Net.Reports
{
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
}
