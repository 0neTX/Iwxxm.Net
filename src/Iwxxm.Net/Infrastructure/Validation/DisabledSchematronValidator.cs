using System;
using System.Collections.Generic;
using System.IO;
using Iwxxm.Net.Extensibility;

namespace Iwxxm.Net.Infrastructure.Validation
{
    /// <summary>
    /// No-op <see cref="ISchematronValidator"/> used when business-rule validation is explicitly disabled
    /// through <c>IwxxmParserBuilder.WithoutSchematron()</c>. Always reports no finding.
    /// </summary>
    internal sealed class DisabledSchematronValidator : ISchematronValidator
    {
        internal static readonly DisabledSchematronValidator Instance = new DisabledSchematronValidator();

        private static readonly IReadOnlyList<ValidationError> NoFindings = Array.Empty<ValidationError>();

        private DisabledSchematronValidator()
        {
        }

        public IReadOnlyList<ValidationError> Validate(Stream document)
        {
            if (document == null)
                throw new ArgumentNullException(nameof(document));

            return NoFindings;
        }
    }
}
