using System;

namespace Iwxxm.Net
{
    /// <summary>
    /// Identifies the technology-agnostic processing stage where a validation or processing error originated.
    /// The machine-readable contract of an error is its <see cref="ValidationError.Code"/> (for example
    /// <c>XSD-SCHEMA-VIOLATION</c> or an official Schematron rule identifier, see <see cref="IwxxmErrorCodes"/>); categories
    /// only group codes.
    /// </summary>
    /// <remarks>
    /// Renamed in the Unreleased version (numeric values unchanged): <c>XmlWellFormedness</c> is now
    /// <see cref="MalformedInput"/>, <c>XsdSchemaValidation</c> is now <see cref="SchemaViolation"/> and
    /// <c>SchematronBusinessRule</c> is now <see cref="BusinessRuleViolation"/>.
    /// </remarks>
    public enum ErrorCategory
    {
        /// <summary>The input is empty, over a size or depth limit, or not a well-formed document.</summary>
        MalformedInput = 0,

        /// <summary>The document violates the structural schema of the format.</summary>
        SchemaViolation = 1,

        /// <summary>The document violates a business rule (for example an official IWXXM rule or a code-list constraint).</summary>
        BusinessRuleViolation = 2,

        /// <summary>The structurally valid document could not be decoded into the transport model.</summary>
        Deserialization = 3,

        /// <summary>The decoded content could not be mapped onto the domain model.</summary>
        DomainMapping = 4,

        /// <summary>
        /// The input could not be read at all: the file is missing or inaccessible, or the stream or reader failed. The
        /// message was never seen, so the cause lies with the environment (path, permissions, transport), not with the content;
        /// a retry may succeed.
        /// </summary>
        Io = 5
    }
}
