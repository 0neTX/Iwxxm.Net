using System;

namespace Iwxxm.Net
{
    /// <summary>
    /// Represents a structural, business-rule, or domain-invariant error detected during IWXXM processing.
    /// </summary>
    public sealed class ValidationError : IEquatable<ValidationError>
    {
        /// <summary>Gets the stable machine-readable code of the finding (for example <c>XSD-INVALID</c> or <c>DOMAIN-TAF-ID</c>); never blank.</summary>
        public string Code { get; }
        /// <summary>Gets the human-readable description of the finding; never blank.</summary>
        public string Message { get; }
        /// <summary>Gets the processing stage that produced the finding.</summary>
        public ErrorCategory Category { get; }
        /// <summary>Gets whether the finding is an <see cref="ValidationSeverity.Error"/> or a non-fatal <see cref="ValidationSeverity.Warning"/>.</summary>
        public ValidationSeverity Severity { get; }

        /// <summary>
        /// Where the finding is, when known. Schematron findings (built-in and official rules) carry an absolute XPath of the
        /// offending element; schema findings carry the source URI or the root element name. The exact forms are listed in
        /// the reference documentation ("Locations of findings").
        /// </summary>
        public string? Location { get; }
        /// <summary>Gets the 1-based line in the XML source where the finding was detected, or <c>null</c> when the finding has no source position.</summary>
        public int? LineNumber { get; }
        /// <summary>Gets the 1-based column in the XML source where the finding was detected, or <c>null</c> when the finding has no source position.</summary>
        public int? LinePosition { get; }

        /// <summary>Creates a finding.</summary>
        /// <param name="code">The stable machine-readable code; must not be blank.</param>
        /// <param name="message">The human-readable description; must not be blank.</param>
        /// <param name="category">The processing stage that produced the finding.</param>
        /// <param name="severity">Whether the finding is an error (default) or a warning.</param>
        /// <param name="location">The XPath, source URI or element name where the finding is; <c>null</c> when unknown.</param>
        /// <param name="lineNumber">The 1-based source line; <c>null</c> when unknown.</param>
        /// <param name="linePosition">The 1-based source column; <c>null</c> when unknown.</param>
        /// <exception cref="ArgumentException"><paramref name="code"/> or <paramref name="message"/> is <c>null</c>, empty or whitespace.</exception>
        public ValidationError(
            string code,
            string message,
            ErrorCategory category,
            ValidationSeverity severity = ValidationSeverity.Error,
            string? location = null,
            int? lineNumber = null,
            int? linePosition = null)
        {
            if (string.IsNullOrWhiteSpace(code))
                throw new ArgumentException("Error code cannot be null or whitespace.", nameof(code));
            if (string.IsNullOrWhiteSpace(message))
                throw new ArgumentException("Error message cannot be null or whitespace.", nameof(message));

            Code = code;
            Message = message;
            Category = category;
            Severity = severity;
            Location = location;
            LineNumber = lineNumber;
            LinePosition = linePosition;
        }

        /// <inheritdoc />
        public override string ToString()
        {
            string pos = LineNumber.HasValue ? $" (Line {LineNumber}, Pos {LinePosition ?? 0})" : string.Empty;
            string loc = !string.IsNullOrWhiteSpace(Location) ? $" [{Location}]" : string.Empty;
            return $"[{Category}:{Severity}] {Code}{loc}{pos}: {Message}";
        }

        /// <inheritdoc />
        public bool Equals(ValidationError? other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Code == other.Code &&
                   Message == other.Message &&
                   Category == other.Category &&
                   Severity == other.Severity &&
                   Location == other.Location &&
                   LineNumber == other.LineNumber &&
                   LinePosition == other.LinePosition;
        }

        /// <inheritdoc />
        public override bool Equals(object? obj) => obj is ValidationError other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = (hash * 23) + Code.GetHashCode();
                hash = (hash * 23) + Message.GetHashCode();
                hash = (hash * 23) + Category.GetHashCode();
                hash = (hash * 23) + Severity.GetHashCode();
                return hash;
            }
        }
    }
}
