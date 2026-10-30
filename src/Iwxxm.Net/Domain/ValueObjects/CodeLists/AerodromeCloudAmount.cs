namespace Iwxxm.Net.Reports
{
    /// <summary>
    /// Cloud amount reported at aerodrome (WMO code list <c>http://codes.wmo.int/49-2/CloudAmountReportedAtAerodrome</c>, codes valid for IWXXM 2025-2).
    /// Members are listed with their official notation (<c>skos:notation</c>); the documentation quotes or summarizes the official
    /// description. <see cref="Unknown"/> (the default value) means the reported value is not an official code.
    /// </summary>
    /// <remarks>Used through <see cref="CodeListValue{TCode}"/>, which also keeps the code as reported.</remarks>
    public enum AerodromeCloudAmount
    {
        /// <summary>The reported value is not a code of the official list valid for IWXXM 2025-2.</summary>
        Unknown = 0,
        /// <summary><c>SKC</c> — Sky clear (0 oktas).</summary>
        SkyClear,
        /// <summary><c>FEW</c> — Few (1 - 2 oktas).</summary>
        Few,
        /// <summary><c>SCT</c> — Scattered (3 - 4 oktas).</summary>
        Scattered,
        /// <summary><c>BKN</c> — Broken (5 - 7 oktas).</summary>
        Broken,
        /// <summary><c>OVC</c> — Overcast (8 oktas).</summary>
        Overcast,
        /// <summary><c>ISOL</c> — Isolated. Applicable only to cumulonimbus (CB).</summary>
        Isolated,
        /// <summary><c>OCNL</c> — Occasional. Applicable only to cumulonimbus (CB).</summary>
        Occasional,
        /// <summary><c>FRQ</c> — Frequent. Applicable only to cumulonimbus (CB).</summary>
        Frequent,
        /// <summary><c>EMBD</c> — Embedded. Applicable only to cumulonimbus (CB).</summary>
        Embedded,
        /// <summary><c>LYR</c> — Layers. Applicable only to cumulonimbus (CB).</summary>
        Layers
    }
}
