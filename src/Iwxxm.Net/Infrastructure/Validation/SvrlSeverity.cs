namespace Iwxxm.Net.Infrastructure.Validation
{
    /// <summary>
    /// Maps the <c>@role</c> (or <c>@flag</c>) of an SVRL finding onto a <see cref="ValidationSeverity"/> (AUD-U5, AUD-L8).
    /// </summary>
    internal static class SvrlSeverity
    {
        /// <param name="role">The <c>@role</c> / <c>@flag</c> value, if any.</param>
        /// <param name="isSuccessfulReport"><c>true</c> for <c>svrl:successful-report</c>, <c>false</c> for <c>svrl:failed-assert</c>.</param>
        public static ValidationSeverity Map(string? role, bool isSuccessfulReport)
        {
            switch (role?.Trim().ToUpperInvariant())
            {
                case "WARN":
                case "WARNING":
                case "INFO":
                case "INFORMATION":
                    return ValidationSeverity.Warning;
                case "ERROR":
                case "FATAL":
                    return ValidationSeverity.Error;
                default:
                    return isSuccessfulReport ? ValidationSeverity.Warning : ValidationSeverity.Error;
            }
        }
    }
}
