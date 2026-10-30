namespace Iwxxm.Net.Reports
{
    /// <summary>
    /// METAR/SPECI trend time indicator (<c>metarSpeci.xsd</c> <c>TrendForecastTimeIndicatorType</c>; TAC <c>AT</c>,
    /// <c>TL</c>, <c>FM</c>). The trend time itself is the <c>phenomenonTime</c> of the trend
    /// (<see cref="AerodromeForecastGroup.ValidFromUtc"/> / <see cref="AerodromeForecastGroup.ValidUntilUtc"/>).
    /// </summary>
    public enum TrendTimeIndicator
    {
        /// <summary><c>AT</c>: the change occurs at the trend time.</summary>
        At = 1,

        /// <summary><c>UNTIL</c> (TAC <c>TL</c>): the condition lasts until the trend time.</summary>
        Until = 2,

        /// <summary><c>FROM</c> (TAC <c>FM</c>): the condition starts from the trend time.</summary>
        From = 3
    }
}
