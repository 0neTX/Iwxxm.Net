namespace Iwxxm.Net.Reports
{
    /// <summary>
    /// Qualifier of a reported value that is a bound rather than an exact measurement (IWXXM
    /// <c>RelationalOperatorType</c>): TAC <c>P</c> ("above", e.g. <c>P49MPS</c>, <c>R27/P2000</c>, visibility <c>9999</c>
    /// reported as 10000 m and above) and <c>M</c> ("below", e.g. <c>R27/M0050</c>).
    /// </summary>
    public enum RelationalOperator
    {
        /// <summary>The actual value is above the reported value (<c>ABOVE</c>).</summary>
        Above,

        /// <summary>The actual value is below the reported value (<c>BELOW</c>).</summary>
        Below
    }
}
