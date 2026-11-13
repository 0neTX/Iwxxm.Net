using System;
using System.Collections.Generic;
using Iwxxm.Net.Reports;

namespace Iwxxm.Net.Infrastructure.Mapping.Xml
{
    /// <summary>
    /// Reads the <c>intensityChange</c> token shared by SIGMET, AIRMET and tropical cyclone advisories. The three
    /// schemas define the same values (<c>NO_CHANGE</c>, <c>WEAKEN</c>, <c>INTENSIFY</c>).
    /// </summary>
    internal static class IntensityChangeReader
    {
        /// <summary>Mapping error code of an unsupported SIGMET/AIRMET <c>intensityChange</c> value.</summary>
        internal const string EnRouteInvalidCode = "ACL-ENROUTE-INTENSITY-CHANGE-INVALID";

        /// <summary>
        /// Returns the matching value, or <c>null</c> when <paramref name="value"/> is <c>null</c> or blank (not reported).
        /// Any other token adds a mapping error <paramref name="errorCode"/> and yields <c>null</c>: it is never passed through.
        /// </summary>
        public static IntensityChange? Read(string? value, string id, List<ValidationError> errors, string errorCode)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            string token = value!.Trim();
            switch (token)
            {
                case "NO_CHANGE": return IntensityChange.NoChange;
                case "WEAKEN": return IntensityChange.Weaken;
                case "INTENSIFY": return IntensityChange.Intensify;
            }

            errors.Add(MappingErrors.Create(errorCode, $"Unsupported intensityChange value '{token}'.", id));
            return null;
        }
    }
}
