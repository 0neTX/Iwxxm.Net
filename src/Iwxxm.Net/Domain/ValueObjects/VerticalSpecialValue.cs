using System;

namespace Iwxxm.Net.Reports
{
    /// <summary>
    /// Non-numeric vertical values allowed by AIXM 5.1.1 <c>ValDistanceVerticalBaseType</c>.
    /// </summary>
    public enum VerticalSpecialValue
    {
        /// <summary>The limit is numeric (or nil).</summary>
        None = 0,

        /// <summary><c>GND</c> (also accepted as <c>SFC</c>): the ground / surface.</summary>
        Ground = 1,

        /// <summary><c>UNL</c>: unlimited.</summary>
        Unlimited = 2,

        /// <summary><c>FLOOR</c>: the floor of the associated airspace.</summary>
        Floor = 3,

        /// <summary><c>CEILING</c>: the ceiling of the associated airspace.</summary>
        Ceiling = 4
    }
}
