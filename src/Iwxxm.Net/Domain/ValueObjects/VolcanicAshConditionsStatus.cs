namespace Iwxxm.Net.Reports
{
    /// <summary>
    /// <c>@status</c> of the Volcanic Ash Advisory observed/estimated conditions
    /// (<c>VolcanicAshObservedOrEstimatedStatusType</c>) and forecast conditions
    /// (<c>VolcanicAshCloudForecastStatusType</c>) in <c>volcanicAshAdvisory.xsd</c>.
    /// </summary>
    public enum VolcanicAshConditionsStatus
    {
        /// <summary><c>PROVIDED</c>: ash clouds are provided.</summary>
        Provided = 1,

        /// <summary><c>NOT_PROVIDED</c>.</summary>
        NotProvided = 2,

        /// <summary><c>NOT_AVAILABLE</c> (TAC <c>NOT AVBL</c>).</summary>
        NotAvailable = 3,

        /// <summary><c>NOT_IDENTIFIABLE</c> (observation only; TAC <c>VA NOT IDENTIFIABLE FM SATELLITE DATA</c>).</summary>
        NotIdentifiable = 4,

        /// <summary><c>NO_VOLCANIC_ASH_EXPECTED</c> (forecast only; TAC <c>NO VA EXP</c>).</summary>
        NoVolcanicAshExpected = 5
    }
}
