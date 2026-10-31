using System.Collections.Generic;

namespace Iwxxm.Net.Infrastructure.Validation
{
    /// <summary>
    /// Caps the number of findings a validator reports for one document (AUD-M14). Once the cap is reached a single
    /// sentinel finding is appended and further findings are discarded, so a pathological document cannot produce an
    /// unbounded error list. The sentinel is an error when any discarded finding is an error, and a warning when every
    /// discarded finding seen so far is a warning (so a flood of warnings never turns into a failure on its own).
    /// </summary>
    internal sealed class ValidationFindingLimit
    {
        public const string XsdTooManyErrorsCode = "XSD-TOO-MANY-ERRORS";
        public const string SchematronTooManyErrorsCode = "SCHEMATRON-TOO-MANY-ERRORS";

        private readonly List<ValidationError> _findings;
        private readonly int _max;
        private readonly string _sentinelCode;
        private readonly ErrorCategory _category;
        private int _sentinelIndex = -1;

        public ValidationFindingLimit(List<ValidationError> findings, int max, string sentinelCode, ErrorCategory category)
        {
            _findings = findings;
            _max = max;
            _sentinelCode = sentinelCode;
            _category = category;
        }

        /// <summary>Gets a value indicating whether the cap has been reached (the sentinel was appended).</summary>
        public bool IsExhausted { get; private set; }

        /// <summary>Adds <paramref name="finding"/> unless the cap was reached; returns <c>false</c> once exhausted.</summary>
        public bool TryAdd(ValidationError finding)
        {
            if (IsExhausted)
            {
                if (finding.Severity == ValidationSeverity.Error && _findings[_sentinelIndex].Severity != ValidationSeverity.Error)
                {
                    _findings[_sentinelIndex] = CreateSentinel(ValidationSeverity.Error);
                }

                return false;
            }

            if (_findings.Count >= _max)
            {
                _sentinelIndex = _findings.Count;
                _findings.Add(CreateSentinel(finding.Severity == ValidationSeverity.Error ? ValidationSeverity.Error : ValidationSeverity.Warning));
                IsExhausted = true;
                return false;
            }

            _findings.Add(finding);
            return true;
        }

        private ValidationError CreateSentinel(ValidationSeverity severity) =>
            new ValidationError(
                _sentinelCode,
                $"Too many validation findings: only the first {_max} are reported.",
                _category,
                severity);
    }
}
