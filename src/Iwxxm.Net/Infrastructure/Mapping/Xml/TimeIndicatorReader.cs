using System.Collections.Generic;
using System.Xml.Linq;
using Iwxxm.Net.Reports;

namespace Iwxxm.Net.Infrastructure.Mapping.Xml
{
    /// <summary>Reads the IWXXM <c>@timeIndicator</c> attribute (<c>TimeIndicatorType</c>: <c>OBSERVATION</c> / <c>FORECAST</c>).</summary>
    internal static class TimeIndicatorReader
    {
        internal const string InvalidCode = "ACL-TIME-INDICATOR-INVALID";

        /// <summary>Returns the indicator, <c>null</c> when absent, or records a mapping error for any other value.</summary>
        public static TimeIndicator? Read(XElement element, string context, List<ValidationError> errors)
        {
            string? value = element.Attribute("timeIndicator")?.Value?.Trim();
            switch (value)
            {
                case null:
                case "":
                    return null;
                case "OBSERVATION":
                    return TimeIndicator.Observation;
                case "FORECAST":
                    return TimeIndicator.Forecast;
                default:
                    errors.Add(MappingErrors.Create(InvalidCode, $"Unsupported timeIndicator '{value}' (expected OBSERVATION or FORECAST).", context));
                    return null;
            }
        }
    }
}
