using System.Collections.Generic;
using Iwxxm.Net.Reports;

namespace Iwxxm.Net.Infrastructure.Mapping
{
    /// <summary>
    /// Reads the <c>iwxxm:Volcano/iwxxm:activityStatus</c> code (<c>VolcanoActivityStatusType</c>, <c>metFeature.xsd</c>),
    /// shared by the WAFS SIGWX, VONA and QVACI volcano readers.
    /// </summary>
    internal static class VolcanoActivityStatusReader
    {
        internal const string InvalidCode = "ACL-VOLCANO-ACTIVITY-INVALID";

        /// <summary>Returns the status, <c>null</c> when absent or blank; an unsupported value is recorded as <see cref="InvalidCode"/> and yields <c>null</c>.</summary>
        public static VolcanoActivityStatus? Read(string? value, string id, List<ValidationError> errors)
        {
            switch (value?.Trim())
            {
                case null:
                case "":
                    return null;
                case "NIL":
                    return VolcanoActivityStatus.Nil;
                case "UNKNOWN":
                    return VolcanoActivityStatus.Unknown;
                case "DECREASED_ACTIVITY":
                    return VolcanoActivityStatus.DecreasedActivity;
                case "HEIGHTENED_UNREST":
                    return VolcanoActivityStatus.HeightenedUnrest;
                case "ERUPTION_ONGOING":
                    return VolcanoActivityStatus.EruptionOngoing;
                case "ERUPTION_OCCURRED":
                    return VolcanoActivityStatus.EruptionOccurred;
                default:
                    errors.Add(new ValidationError(
                        InvalidCode,
                        $"Unsupported volcano activityStatus '{value}'.",
                        ErrorCategory.DomainMapping,
                        ValidationSeverity.Error,
                        id));
                    return null;
            }
        }
    }
}
