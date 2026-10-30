using System;

namespace Iwxxm.Net.Reports
{
    /// <summary>
    /// WMO FM 94 BUFR Code Table 0 20 008 — Cloud distribution for aviation
    /// (<c>http://codes.wmo.int/bufr4/codeflag/0-20-008</c>).
    /// Each member value equals the official code (<c>skos:notation</c>); the member documentation quotes the
    /// official <c>dcterms:description</c>. <see cref="Unknown"/> (-1) means the value could not be resolved.
    /// </summary>
    public enum CloudDistributionCode
    {
        /// <summary>The raw value is not a code of table 0 20 008 (not an official code).</summary>
        Unknown = -1,
        /// <summary>0 — Sky clear.</summary>
        SkyClear = 0,
        /// <summary>1 — Few.</summary>
        Few = 1,
        /// <summary>2 — Scattered.</summary>
        Scattered = 2,
        /// <summary>3 — Broken.</summary>
        Broken = 3,
        /// <summary>4 — Overcast.</summary>
        Overcast = 4,
        /// <summary>6 — Scattered/broken.</summary>
        ScatteredBroken = 6,
        /// <summary>7 — Broken/overcast.</summary>
        BrokenOvercast = 7,
        /// <summary>8 — Isolated (used on aviation charts to describe the cloud type Cb).</summary>
        Isolated = 8,
        /// <summary>9 — Isolated embedded.</summary>
        IsolatedEmbedded = 9,
        /// <summary>10 — Occasional.</summary>
        Occasional = 10,
        /// <summary>11 — Occasional embedded.</summary>
        OccasionalEmbedded = 11,
        /// <summary>12 — Frequent.</summary>
        Frequent = 12,
        /// <summary>13 — Dense (cloud that would cause sudden changes in visibility, less than 1 000 m).</summary>
        Dense = 13,
        /// <summary>14 — Layers.</summary>
        Layers = 14,
        /// <summary>15 — Obscured (OBSC).</summary>
        Obscured = 15,
        /// <summary>16 — Embedded (EMBD).</summary>
        Embedded = 16,
        /// <summary>17 — Frequent embedded.</summary>
        FrequentEmbedded = 17,
        /// <summary>31 — Missing value.</summary>
        MissingValue = 31
    }
}
