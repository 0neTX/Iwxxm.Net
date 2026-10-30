using System;
using System.Collections.Generic;
using Iwxxm20252.Generated;

namespace Iwxxm.Net.Infrastructure.Mapping.Wafs
{
    /// <summary>Reads the temporal properties of the SIGWX transport model as UTC instants.</summary>
    internal static class SigwxTimeReader
    {
        public static DateTimeOffset? ReadRequiredInstant(
            TimeInstantPropertyType? prop,
            string fieldName,
            List<ValidationError> errors)
        {
            if (prop?.TimeInstant == null)
            {
                errors.Add(new ValidationError(
                    "ACL-MISSING-TIME-INSTANT",
                    $"Required temporal element '{fieldName}' is missing.",
                    ErrorCategory.DomainMapping,
                    ValidationSeverity.Error,
                    fieldName));
                return null;
            }

            return NormalizeToUtc(prop.TimeInstant.TimePosition);
        }

        public static (DateTimeOffset? From, DateTimeOffset? Until) ReadRequiredPeriod(
            TimePeriodPropertyType? prop,
            string fieldName,
            List<ValidationError> errors)
        {
            if (prop?.TimePeriod == null)
            {
                errors.Add(new ValidationError(
                    "ACL-MISSING-TIME-PERIOD",
                    $"Required validity period '{fieldName}' is missing.",
                    ErrorCategory.DomainMapping,
                    ValidationSeverity.Error,
                    fieldName));
                return (null, null);
            }

            return (
                NormalizeToUtc(prop.TimePeriod.BeginPosition),
                NormalizeToUtc(prop.TimePeriod.EndPosition));
        }

        /// <summary>Converts a transport <see cref="DateTime"/> to UTC; an unspecified kind is taken as UTC.</summary>
        public static DateTimeOffset NormalizeToUtc(DateTime dt)
        {
            if (dt.Kind == DateTimeKind.Unspecified)
            {
                return new DateTimeOffset(DateTime.SpecifyKind(dt, DateTimeKind.Utc));
            }
            return new DateTimeOffset(dt.ToUniversalTime());
        }
    }
}
