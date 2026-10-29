using System.Collections.Generic;
using Iwxxm.Net.Reports;
using Iwxxm20252.Generated;

namespace Iwxxm.Net.Infrastructure.Mapping.Sigwx
{
    /// <summary>Reads vertical limits and wind speeds of the SIGWX transport model exactly as reported.</summary>
    internal static class SigwxMeasureReader
    {
        /// <summary>Reads an optional vertical measure exactly as reported (<c>null</c> when the element is absent).</summary>
        public static VerticalLimit? ReadVertical(MeasureType? measure, string? reference, string location, List<ValidationError> errors) =>
            measure == null
                ? (VerticalLimit?)null
                : VerticalLimitReader.Read(measure.RawValue, measure.Uom, measure.NilReason, false, reference, location, errors);

        /// <summary>Reads a mandatory vertical measure: an absent element is a mapping error (nil stays an explicit nil).</summary>
        public static VerticalLimit? ReadRequiredVertical(MeasureType? measure, string? reference, string location, List<ValidationError> errors)
        {
            if (measure == null)
            {
                errors.Add(new ValidationError(
                    "ACL-MISSING-FLIGHT-LEVEL",
                    $"Required vertical limit '{location}' is missing.",
                    ErrorCategory.DomainMapping,
                    ValidationSeverity.Error,
                    location));
                return null;
            }

            int before = errors.Count;
            VerticalLimit? limit = ReadVertical(measure, reference, location, errors);
            return errors.Count > before ? null : limit;
        }

        /// <summary>
        /// Reads a mandatory wind speed keeping its reported unit (<c>m/s</c>, <c>[kn_i]</c> or <c>km/h</c>).
        /// A nil/missing value, a missing <c>@uom</c> or an unknown unit is a mapping error (never assumed).
        /// </summary>
        public static Result<Speed> ReadRequiredSpeed(MeasureType? speedMeasure, string location)
        {
            if (speedMeasure == null || speedMeasure.IsNil)
            {
                return Result<Speed>.Failure(
                    "ACL-MISSING-WIND-SPEED",
                    $"Required wind speed at '{location}' is missing.",
                    ErrorCategory.DomainMapping,
                    location);
            }

            var errors = new List<ValidationError>();
            Speed? speed = UomMeasureReader.ReadSpeed(speedMeasure.RawValue, speedMeasure.Uom, location, errors);
            return speed.HasValue && errors.Count == 0
                ? Result<Speed>.Success(speed.Value)
                : Result<Speed>.Failure(errors);
        }
    }
}
