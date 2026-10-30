using System;

namespace Iwxxm.Net.Reports
{
    /// <summary>
    /// WMO FM 94 BUFR Code Table 0 20 041 — Airframe icing
    /// (<c>http://codes.wmo.int/bufr4/codeflag/0-20-041</c>).
    /// Each member value equals the official code; the member documentation quotes the official description.
    /// </summary>
    public enum IcingDegreeCode
    {
        /// <summary>The raw value is not a code of table 0 20 041.</summary>
        Unknown = -1,
        /// <summary>0 — No icing.</summary>
        None = 0,
        /// <summary>1 — Light icing.</summary>
        Light = 1,
        /// <summary>2 — Light icing in cloud.</summary>
        LightInCloud = 2,
        /// <summary>3 — Light icing in precipitation.</summary>
        LightInPrecipitation = 3,
        /// <summary>4 — Moderate icing.</summary>
        Moderate = 4,
        /// <summary>5 — Moderate icing in cloud.</summary>
        ModerateInCloud = 5,
        /// <summary>6 — Moderate icing in precipitation.</summary>
        ModerateInPrecipitation = 6,
        /// <summary>7 — Severe icing.</summary>
        Severe = 7,
        /// <summary>8 — Severe icing in cloud.</summary>
        SevereInCloud = 8,
        /// <summary>9 — Severe icing in precipitation.</summary>
        SevereInPrecipitation = 9,
        /// <summary>10 — Trace of icing.</summary>
        Trace = 10,
        /// <summary>11 — Trace of icing in cloud.</summary>
        TraceInCloud = 11,
        /// <summary>12 — Trace of icing in precipitation.</summary>
        TraceInPrecipitation = 12,
        /// <summary>15 — Missing value.</summary>
        MissingValue = 15
    }
}
