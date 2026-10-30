using System;

namespace Iwxxm.Net.Reports
{
    /// <summary>
    /// Units of speed allowed by the official IWXXM 2025-2 rules (UCUM codes <c>m/s</c>, <c>[kn_i]</c>, <c>km/h</c>).
    /// </summary>
    public enum SpeedUnit
    {
        /// <summary>Metres per second (UCUM <c>m/s</c>).</summary>
        MetersPerSecond = 0,
        /// <summary>International knots (UCUM <c>[kn_i]</c>); 1 kt = 1852/3600 m/s exactly.</summary>
        Knots = 1,
        /// <summary>Kilometres per hour (UCUM <c>km/h</c>); 1 km/h = 1/3.6 m/s exactly.</summary>
        KilometersPerHour = 2
    }
}
