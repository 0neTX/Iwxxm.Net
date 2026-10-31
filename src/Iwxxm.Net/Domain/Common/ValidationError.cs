using System;

namespace Iwxxm.Net
{
    /// <summary>
    /// Represents a structural, business-rule, or domain-invariant error detected during IWXXM processing.
    /// </summary>
    public sealed class ValidationError : IEquatable<ValidationError>
    {
        public string Code { get; }
        public string Message { get; }
        public ErrorCategory Category { get; }
        public ValidationSeverity Severity { get; }

        /// <summary>
        /// Where the finding is, when known. Schematron findings (built-in and official rules) carry an absolute XPath of the
        /// offending element; schema findings carry the source URI or the root element name. The exact forms are listed in
        /// the reference documentation ("Locations of findings").
        /// </summary>
        public string? Location { get; }
        public int? LineNumber { get; }
        public int? LinePosition { get; }

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

        public override string ToString()
        {
            string pos = LineNumber.HasValue ? $" (Line {LineNumber}, Pos {LinePosition ?? 0})" : string.Empty;
            string loc = !string.IsNullOrWhiteSpace(Location) ? $" [{Location}]" : string.Empty;
            return $"[{Category}:{Severity}] {Code}{loc}{pos}: {Message}";
        }

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

        public override bool Equals(object? obj) => obj is ValidationError other && Equals(other);

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
