using System;
using System.Collections.Generic;
using System.Globalization;

namespace Iwxxm.Net.Reports
{
    /// <summary>
    /// Homogeneous minimal invariants (DDD-05) applied by the aggregate factories. They cover only physical and
    /// structural facts (UTC timestamps, ordered periods, directions within 0-360 degrees, non-negative magnitudes,
    /// defined metadata values); business rules of the official Schematron are deliberately not duplicated.
    /// Every violation is collected so that a factory reports all of them at once.
    /// </summary>
    internal sealed class DomainInvariants
    {
        private readonly List<ValidationError> _errors = new List<ValidationError>();
        private readonly string _location;

        internal DomainInvariants(string? location)
        {
            _location = string.IsNullOrWhiteSpace(location) ? string.Empty : location!;
        }

        internal bool HasErrors => _errors.Count > 0;

        internal IReadOnlyList<ValidationError> Errors => _errors;

        internal void Add(string code, string message)
        {
            _errors.Add(new ValidationError(
                code,
                message,
                ErrorCategory.DomainMapping,
                ValidationSeverity.Error,
                _location.Length == 0 ? null : _location));
        }

        internal void RequireText(string? value, string code, string message)
        {
            if (string.IsNullOrWhiteSpace(value))
                Add(code, message);
        }

        internal void RequireMetadata(
            ReportStatus reportStatus,
            PermissibleUsage permissibleUsage,
            PermissibleUsageReason? permissibleUsageReason)
        {
            if (!Enum.IsDefined(typeof(ReportStatus), reportStatus))
                Add(DomainErrorCodes.ReportStatusInvalid, $"Report status value '{(int)reportStatus}' is not a defined ReportStatus.");

            if (!Enum.IsDefined(typeof(PermissibleUsage), permissibleUsage))
                Add(DomainErrorCodes.PermissibleUsageInvalid, $"Permissible usage value '{(int)permissibleUsage}' is not a defined PermissibleUsage.");

            if (permissibleUsageReason.HasValue && !Enum.IsDefined(typeof(PermissibleUsageReason), permissibleUsageReason.Value))
                Add(DomainErrorCodes.PermissibleUsageReasonInvalid, $"Permissible usage reason value '{(int)permissibleUsageReason.Value}' is not a defined PermissibleUsageReason.");
        }

        /// <summary>The issue time is mandatory in every IWXXM report: <c>default(DateTimeOffset)</c> is rejected.</summary>
        internal void RequireIssueTime(DateTimeOffset issueTimeUtc)
        {
            if (issueTimeUtc == default)
            {
                Add(DomainErrorCodes.IssueTimeMissing, "The report issue time is required (DateTimeOffset.MinValue is not a valid issue time).");
                return;
            }

            RequireUtc(issueTimeUtc, "issue time");
        }

        internal void RequireUtc(DateTimeOffset? value, string field)
        {
            if (value.HasValue && value.Value.Offset != TimeSpan.Zero)
            {
                Add(DomainErrorCodes.TimeNotUtc,
                    $"The {field} ({value.Value.ToString("O", CultureInfo.InvariantCulture)}) must be expressed in UTC (offset +00:00).");
            }
        }

        /// <summary>
        /// When both bounds are present the end must be after the start; with <paramref name="allowInstant"/> an end equal
        /// to the start (a <c>gml:TimePeriod</c> that describes a single instant, which the schema allows) is accepted too.
        /// An end before the start is always an error.
        /// </summary>
        internal void RequireOrderedPeriod(DateTimeOffset? from, DateTimeOffset? until, string code, string field, bool allowInstant = false)
        {
            RequireUtc(from, field + " start");
            RequireUtc(until, field + " end");

            if (from.HasValue && until.HasValue && (allowInstant ? until.Value < from.Value : until.Value <= from.Value))
            {
                string relation = allowInstant ? "must not be before" : "must be strictly after";
                Add(code,
                    $"The {field} end ({until.Value.ToString("O", CultureInfo.InvariantCulture)}) {relation} its start ({from.Value.ToString("O", CultureInfo.InvariantCulture)}).");
            }
        }

        internal void RequireDirection(double? degrees, string field)
        {
            if (degrees.HasValue && (double.IsNaN(degrees.Value) || degrees.Value < 0.0 || degrees.Value > 360.0))
            {
                Add(DomainErrorCodes.DirectionOutOfRange,
                    string.Format(CultureInfo.InvariantCulture, "The {0} ({1} deg) must be within [0, 360] degrees.", field, degrees.Value));
            }
        }

        internal void RequireNonNegative(Speed? speed, string field)
        {
            if (speed.HasValue && speed.Value.Amount < 0.0)
                Add(DomainErrorCodes.NegativeSpeed, $"The {field} ({speed.Value}) cannot be negative.");
        }

        internal void RequireNonNegative(double? value, string code, string field)
        {
            if (value.HasValue && (double.IsNaN(value.Value) || value.Value < 0.0))
            {
                Add(code, string.Format(CultureInfo.InvariantCulture, "The {0} ({1}) cannot be negative.", field, value.Value));
            }
        }

        internal void Check(SurfaceWind? wind, string field)
        {
            if (wind == null)
                return;

            RequireDirection(wind.MeanDirectionDegrees, field + " direction");
            RequireDirection(wind.ExtremeClockwiseDirectionDegrees, field + " extreme clockwise direction");
            RequireDirection(wind.ExtremeCounterClockwiseDirectionDegrees, field + " extreme counter-clockwise direction");
            RequireNonNegative(wind.MeanSpeed, field + " mean speed");
            RequireNonNegative(wind.GustSpeed, field + " gust speed");
        }

        internal void Check(AerodromeForecastGroup? group)
        {
            if (group == null)
                return;

            RequireOrderedPeriod(group.ValidFromUtc, group.ValidUntilUtc, DomainErrorCodes.ForecastGroupPeriod, $"forecast group '{group.Id}' period", allowInstant: true);
            RequireNonNegative(group.PrevailingVisibilityMeters, DomainErrorCodes.NegativeVisibility, $"forecast group '{group.Id}' prevailing visibility");
            Check(group.SurfaceWind, $"forecast group '{group.Id}' surface wind");
            Check(group.Cloud, $"forecast group '{group.Id}' cloud");
            foreach (AerodromeTemperatureForecast temperature in group.Temperatures)
            {
                RequireUtc(temperature?.MaximumTimeUtc, $"forecast group '{group.Id}' maximum temperature time");
                RequireUtc(temperature?.MinimumTimeUtc, $"forecast group '{group.Id}' minimum temperature time");
            }
        }

        internal void Check(AerodromeHorizontalVisibility? visibility)
        {
            if (visibility == null)
                return;

            RequireNonNegative(visibility.PrevailingMeters, DomainErrorCodes.NegativeVisibility, "prevailing visibility");
            RequireNonNegative(visibility.MinimumMeters, DomainErrorCodes.NegativeVisibility, "minimum visibility");
            RequireDirection(visibility.MinimumDirectionDegrees, "minimum visibility direction");
        }

        internal void Check(AerodromeCloud? cloud, string field)
        {
            if (cloud?.VerticalVisibility.HasValue == true && cloud.VerticalVisibility.Value.Amount < 0.0)
            {
                Add(DomainErrorCodes.NegativeVisibility, $"The {field} vertical visibility ({cloud.VerticalVisibility.Value}) cannot be negative.");
            }
        }

        internal void Check(EnRoutePhenomenonVolume? volume)
        {
            if (volume == null)
                return;

            RequireDirection(volume.DirectionOfMotionDegrees, $"direction of motion of '{volume.Id}'");
            RequireNonNegative(volume.SpeedOfMotion, $"speed of motion of '{volume.Id}'");
        }

        internal void Check(AirmetEvolvingCondition condition)
        {
            Check(condition.Volume);
            RequireDirection(condition.SurfaceWindDirectionDegrees, $"surface wind direction of '{condition.Id}'");
            RequireNonNegative(condition.SurfaceWindSpeed, $"surface wind speed of '{condition.Id}'");
            if (condition.SurfaceVisibility.HasValue && condition.SurfaceVisibility.Value.Amount < 0.0)
            {
                Add(DomainErrorCodes.NegativeVisibility, $"The surface visibility of '{condition.Id}' cannot be negative.");
            }
        }

        internal void Check(TropicalCycloneTrackPoint? point, string field)
        {
            if (point == null)
                return;

            RequireUtc(point.PhenomenonTimeUtc, field + " time");
            RequireNonNegative(point.MaxSurfaceWindSpeed, field + " maximum surface wind speed");
        }

        internal Result<T> ToResult<T>(Func<T> factory)
        {
            return HasErrors ? Result<T>.Failure(_errors) : Result<T>.Success(factory());
        }
    }
}
