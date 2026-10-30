using System;

namespace Iwxxm.Net.Reports
{
    /// <summary>
    /// Vertical reference of a vertical distance (AIXM 5.1.1 <c>CodeVerticalReferenceBaseType</c>, used by
    /// <c>aixm:upperLimitReference</c>, <c>aixm:lowerLimitReference</c> and <c>iwxxm:*VerticalReference</c>).
    /// </summary>
    public enum VerticalReference
    {
        /// <summary><c>SFC</c>: height above the surface (ground or water).</summary>
        Sfc = 1,

        /// <summary><c>MSL</c>: altitude above mean sea level.</summary>
        Msl = 2,

        /// <summary><c>W84</c>: height above the WGS-84 ellipsoid.</summary>
        W84 = 3,

        /// <summary><c>STD</c>: pressure altitude referenced to the standard atmosphere (1013.25 hPa).</summary>
        Std = 4,

        /// <summary><c>OTHER</c> or <c>OTHER:...</c>: a reference outside the code list.</summary>
        Other = 5
    }
}
