using System;

namespace Iwxxm.Net.Reports
{
    /// <summary>
    /// WMO FM 94 BUFR Code Table 0 11 030 — Extended degree of turbulence
    /// (<c>http://codes.wmo.int/bufr4/codeflag/0-11-030</c>).
    /// Each member value equals the official code. Members without a location qualifier
    /// (<see cref="Nil"/>, <see cref="Light"/>, <see cref="Moderate"/>, <see cref="Severe"/>, <see cref="Extreme"/>)
    /// mean "cloud/clear air not specified"; <c>InCloud</c> and <c>ClearAir</c> members carry the official qualifier.
    /// </summary>
    public enum TurbulenceDegreeCode
    {
        /// <summary>The raw value is not a code of table 0 11 030.</summary>
        Unknown = -1,
        /// <summary>0 — Nil, in cloud.</summary>
        NilInCloud = 0,
        /// <summary>1 — Light, in cloud.</summary>
        LightInCloud = 1,
        /// <summary>2 — Moderate, in cloud.</summary>
        ModerateInCloud = 2,
        /// <summary>3 — Severe, in cloud.</summary>
        SevereInCloud = 3,
        /// <summary>4 — Nil, in clear air.</summary>
        NilClearAir = 4,
        /// <summary>5 — Light, in clear air.</summary>
        LightClearAir = 5,
        /// <summary>6 — Moderate, in clear air.</summary>
        ModerateClearAir = 6,
        /// <summary>7 — Severe, in clear air.</summary>
        SevereClearAir = 7,
        /// <summary>8 — Nil, cloud/clear air not specified.</summary>
        Nil = 8,
        /// <summary>9 — Light, cloud/clear air not specified.</summary>
        Light = 9,
        /// <summary>10 — Moderate, cloud/clear air not specified.</summary>
        Moderate = 10,
        /// <summary>11 — Severe, cloud/clear air not specified.</summary>
        Severe = 11,
        /// <summary>12 — Extreme, in clear air.</summary>
        ExtremeClearAir = 12,
        /// <summary>13 — Extreme, in cloud.</summary>
        ExtremeInCloud = 13,
        /// <summary>14 — Extreme, cloud/clear air not specified.</summary>
        Extreme = 14,
        /// <summary>15 — Light, isolated moderate.</summary>
        LightIsolatedModerate = 15,
        /// <summary>16 — Light, occasional moderate.</summary>
        LightOccasionalModerate = 16,
        /// <summary>17 — Light, frequently moderate.</summary>
        LightFrequentlyModerate = 17,
        /// <summary>18 — Moderate, isolated severe.</summary>
        ModerateIsolatedSevere = 18,
        /// <summary>19 — Moderate, occasional severe.</summary>
        ModerateOccasionalSevere = 19,
        /// <summary>20 — Moderate, frequently severe.</summary>
        ModerateFrequentlySevere = 20,
        /// <summary>21 — Severe, isolated extreme.</summary>
        SevereIsolatedExtreme = 21,
        /// <summary>22 — Severe, occasional extreme.</summary>
        SevereOccasionalExtreme = 22,
        /// <summary>23 — Severe, frequently extreme.</summary>
        SevereFrequentlyExtreme = 23,
        /// <summary>63 — Missing value.</summary>
        MissingValue = 63
    }
}
