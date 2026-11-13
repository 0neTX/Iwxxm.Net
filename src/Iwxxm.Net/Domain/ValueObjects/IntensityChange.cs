namespace Iwxxm.Net.Reports
{
    /// <summary>
    /// Change in intensity of a phenomenon. Shared by the SIGMET and AIRMET <c>intensityChange</c> attribute
    /// (<c>SIGMETExpectedIntensityChangeType</c> / <c>AIRMETExpectedIntensityChangeType</c>, WMO BUFR code table 0 20 028
    /// "Expected change in intensity") and the tropical cyclone advisory <c>intensityChange</c> element
    /// (<c>TropicalCycloneIntensityChangeType</c>): the three schemas define the same three values.
    /// </summary>
    public enum IntensityChange
    {
        /// <summary><c>NO_CHANGE</c>: no change in intensity (SIGMET/AIRMET: "NC").</summary>
        NoChange,

        /// <summary><c>WEAKEN</c>: the phenomenon weakens or has weakened (SIGMET/AIRMET: forecast to weaken, "WKN").</summary>
        Weaken,

        /// <summary><c>INTENSIFY</c>: the phenomenon intensifies or has intensified (SIGMET/AIRMET: forecast to intensify, "INTSF").</summary>
        Intensify
    }
}
