using System;

namespace Iwxxm.Net
{
    /// <summary>
    /// Severity level associated with a validation finding.
    /// </summary>
    public enum ValidationSeverity
    {
        /// <summary>A non-fatal finding: the document or item was still mapped, for example a bulletin entry that could not be mapped while the others were.</summary>
        Warning,
        /// <summary>A finding that makes the validated document, or the mapped item, invalid.</summary>
        Error
    }
}
