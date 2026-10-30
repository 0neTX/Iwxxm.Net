using System;

namespace Iwxxm.Net.Reports
{
    /// <summary>
    /// Units of length/distance used by IWXXM 2025-2 measures (UCUM codes <c>m</c>, <c>[ft_i]</c>, <c>km</c>,
    /// <c>[nmi_i]</c>, <c>[mi_i]</c>).
    /// </summary>
    public enum LengthUnit
    {
        /// <summary>Metres (UCUM <c>m</c>).</summary>
        Meters = 0,
        /// <summary>International feet (UCUM <c>[ft_i]</c>); 1 ft = 0.3048 m exactly.</summary>
        Feet = 1,
        /// <summary>Kilometres (UCUM <c>km</c>).</summary>
        Kilometers = 2,
        /// <summary>International nautical miles (UCUM <c>[nmi_i]</c>); 1 NM = 1852 m exactly.</summary>
        NauticalMiles = 3,
        /// <summary>International statute miles (UCUM <c>[mi_i]</c>); 1 mi = 1609.344 m exactly.</summary>
        StatuteMiles = 4
    }
}
