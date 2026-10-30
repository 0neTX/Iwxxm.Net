namespace Iwxxm.Net.Reports
{
    /// <summary>
    /// IWXXM <c>TimeIndicatorType</c> (<c>common.xsd</c>): whether a phenomenon is observed or forecast at its
    /// <c>phenomenonTime</c> (SIGMET/AIRMET condition collections, space weather analyses).
    /// </summary>
    public enum TimeIndicator
    {
        /// <summary><c>OBSERVATION</c>: observed (OBS) at the phenomenon time.</summary>
        Observation = 1,

        /// <summary><c>FORECAST</c>: forecast (FCST) for the phenomenon time.</summary>
        Forecast = 2
    }
}
