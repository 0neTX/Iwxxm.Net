namespace Iwxxm.Net.Reports
{
    /// <summary>
    /// Past tendency of the runway visual range over the 10-minute observation period (IWXXM <c>VisualRangeTendencyType</c>,
    /// TAC <c>U</c>/<c>D</c>/<c>N</c>).
    /// </summary>
    public enum RunwayVisualRangeTendency
    {
        /// <summary>Upward tendency (<c>UPWARD</c>, TAC <c>U</c>).</summary>
        Upward,

        /// <summary>Downward tendency (<c>DOWNWARD</c>, TAC <c>D</c>).</summary>
        Downward,

        /// <summary>No distinct tendency (<c>NO_CHANGE</c>, TAC <c>N</c>).</summary>
        NoChange
    }
}
