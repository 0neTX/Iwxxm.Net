using System;

namespace Iwxxm.Net
{
    /// <summary>
    /// Identifies the technology-agnostic processing stage where a validation or processing error originated.
    /// The machine-readable contract of an error is its <see cref="ValidationError.Code"/> (for example
    /// <c>XSD-SCHEMA-VIOLATION</c> or an official Schematron rule identifier); categories only group codes.
    /// </summary>
    /// <remarks>
    /// Renamed in the Unreleased version (numeric values unchanged): <c>XmlWellFormedness</c> is now
    /// <see cref="MalformedInput"/>, <c>XsdSchemaValidation</c> is now <see cref="SchemaViolation"/> and
    /// <c>SchematronBusinessRule</c> is now <see cref="BusinessRuleViolation"/>.
    /// </remarks>
    public enum ErrorCategory
    {
        /// <summary>The input could not be read or is not a well-formed document.</summary>
        MalformedInput = 0,

        /// <summary>The document violates the structural schema of the format.</summary>
        SchemaViolation = 1,

        /// <summary>The document violates a business rule (for example an official IWXXM rule or a code-list constraint).</summary>
        BusinessRuleViolation = 2,

        /// <summary>The structurally valid document could not be decoded into the transport model.</summary>
        Deserialization = 3,

        /// <summary>The decoded content could not be mapped onto the domain model.</summary>
        DomainMapping = 4
    }

    /// <summary>
    /// Severity level associated with a validation finding.
    /// </summary>
    public enum ValidationSeverity
    {
        Warning,
        Error
    }

    /// <summary>
    /// Represents a structural, business-rule, or domain-invariant error detected during IWXXM processing.
    /// </summary>
    public sealed class ValidationError : IEquatable<ValidationError>
    {
        public string Code { get; }
        public string Message { get; }
        public ErrorCategory Category { get; }
        public ValidationSeverity Severity { get; }
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
