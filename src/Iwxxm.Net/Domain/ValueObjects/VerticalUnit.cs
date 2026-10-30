using System;

namespace Iwxxm.Net.Reports
{
    /// <summary>
    /// Unit of a vertical distance: AIXM 5.1.1 <c>UomDistanceVerticalType</c> (<c>FL</c>, <c>FT</c>, <c>M</c>, <c>SM</c>)
    /// plus the UCUM spellings tolerated by the reader (<c>[ft_i]</c>, <c>m</c>, <c>km</c>).
    /// </summary>
    public enum VerticalUnit
    {
        /// <summary><c>FL</c>: flight level (hundreds of feet of pressure altitude).</summary>
        FlightLevel = 1,

        /// <summary><c>FT</c> / <c>[ft_i]</c>: international feet.</summary>
        Feet = 2,

        /// <summary><c>M</c> / <c>m</c>: metres.</summary>
        Meters = 3,

        /// <summary><c>km</c>: kilometres.</summary>
        Kilometers = 4,

        /// <summary><c>SM</c>: standard metres (metric pressure altitude).</summary>
        StandardMeters = 5
    }
}
