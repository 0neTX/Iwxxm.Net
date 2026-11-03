using System;
using System.Collections.Generic;
using Iwxxm.Net.Reports;

namespace Iwxxm.Net.Application.UseCases
{
    /// <summary>
    /// Runs outbound-port calls (schema validator, business-rule validator, mapper) so that an exception thrown by an
    /// adapter, or a <c>null</c> answer, is reported as a typed <see cref="ValidationError"/> instead of escaping the
    /// <see cref="Result{T}"/> contract of the use cases (CLN-04). Only <see cref="OutOfMemoryException"/> is rethrown.
    /// </summary>
    internal static class PipelineGuard
    {
        internal const string SchemaValidatorErrorCode = "SCHEMA-VALIDATOR-ERROR";
        internal const string RuleValidatorErrorCode = "RULE-VALIDATOR-ERROR";

        public static IReadOnlyList<ValidationError> Validate(
            Func<IReadOnlyList<ValidationError>> validate,
            string errorCode,
            ErrorCategory category,
            string stage)
        {
            try
            {
                IReadOnlyList<ValidationError>? errors = validate();
                return errors ?? new[] { Failure(errorCode, category, $"The {stage} returned no result.") };
            }
            catch (Exception ex) when (!(ex is OutOfMemoryException))
            {
                return new[] { Failure(errorCode, category, $"The {stage} failed: {ex.Message}") };
            }
        }

        public static Result<T> Map<T>(Func<Result<T>> map)
        {
            try
            {
                return map() ?? Result<T>.Failure(Failure(SharedErrorCodes.MapperError, ErrorCategory.DomainMapping, "The document mapper returned no result."));
            }
            catch (Exception ex) when (!(ex is OutOfMemoryException))
            {
                return Result<T>.Failure(Failure(SharedErrorCodes.MapperError, ErrorCategory.DomainMapping, $"The document mapper failed: {ex.Message}"));
            }
        }

        private static ValidationError Failure(string code, ErrorCategory category, string message) =>
            new ValidationError(code, message, category);
    }
}
