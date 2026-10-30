namespace Iwxxm.Net.Reports
{
    /// <summary>
    /// Weather causing a reduction of surface visibility (WMO code list
    /// <c>http://codes.wmo.int/49-2/WeatherCausingVisibilityReduction</c>, codes valid for IWXXM 2025-2), used by the AIRMET
    /// <c>surfaceVisibilityCause</c>. Members are listed with their official notation (<c>skos:notation</c>).
    /// <see cref="Unknown"/> (the default value) means the reported value is not an official code.
    /// </summary>
    /// <remarks>Used through <see cref="CodeListValue{TCode}"/>, which also keeps the code as reported.</remarks>
    public enum VisibilityReductionCause
    {
        /// <summary>The reported value is not a code of the official list valid for IWXXM 2025-2.</summary>
        Unknown = 0,
        /// <summary><c>BR</c> — Mist.</summary>
        Mist,
        /// <summary><c>DS</c> — Duststorm.</summary>
        Duststorm,
        /// <summary><c>DU</c> — Dust.</summary>
        Dust,
        /// <summary><c>DZ</c> — Drizzle.</summary>
        Drizzle,
        /// <summary><c>FC</c> — Funnel cloud.</summary>
        FunnelCloud,
        /// <summary><c>FG</c> — Fog.</summary>
        Fog,
        /// <summary><c>FU</c> — Smoke.</summary>
        Smoke,
        /// <summary><c>GR</c> — Hail.</summary>
        Hail,
        /// <summary><c>GS</c> — Small hail.</summary>
        SmallHail,
        /// <summary><c>HZ</c> — Haze.</summary>
        Haze,
        /// <summary><c>PL</c> — Ice pellets.</summary>
        IcePellets,
        /// <summary><c>PO</c> — Dust/sand whirls.</summary>
        DustSandWhirls,
        /// <summary><c>RA</c> — Rain.</summary>
        Rain,
        /// <summary><c>SA</c> — Sand.</summary>
        Sand,
        /// <summary><c>SG</c> — Snow grains.</summary>
        SnowGrains,
        /// <summary><c>SN</c> — Snow.</summary>
        Snow,
        /// <summary><c>SQ</c> — Squall.</summary>
        Squall,
        /// <summary><c>SS</c> — Sandstorm.</summary>
        Sandstorm,
        /// <summary><c>VA</c> — Volcanic ash.</summary>
        VolcanicAsh
    }
}
