namespace Iwxxm.Net.Reports
{
    /// <summary>Activity status of a volcano in a WAFS SIGWX forecast (<c>metFeature.xsd</c> <c>VolcanoActivityStatusType</c>).</summary>
    public enum VolcanoActivityStatus
    {
        /// <summary><c>NIL</c>.</summary>
        Nil,

        /// <summary><c>UNKNOWN</c>.</summary>
        Unknown,

        /// <summary><c>DECREASED_ACTIVITY</c>.</summary>
        DecreasedActivity,

        /// <summary><c>HEIGHTENED_UNREST</c>.</summary>
        HeightenedUnrest,

        /// <summary><c>ERUPTION_ONGOING</c>.</summary>
        EruptionOngoing,

        /// <summary><c>ERUPTION_OCCURRED</c>.</summary>
        EruptionOccurred
    }
}
