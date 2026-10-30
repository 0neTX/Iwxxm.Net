namespace Iwxxm.Net.Reports
{
    /// <summary>
    /// AIRMET phenomenon (aeronautical area weather phenomena) (WMO code list <c>http://codes.wmo.int/49-2/AirWxPhenomena</c>, codes valid for IWXXM 2025-2).
    /// Members are listed with their official notation (<c>skos:notation</c>); the documentation quotes or summarizes the official
    /// description. <see cref="Unknown"/> (the default value) means the reported value is not an official code.
    /// </summary>
    /// <remarks>Used through <see cref="CodeListValue{TCode}"/>, which also keeps the code as reported.</remarks>
    public enum AirmetPhenomenon
    {
        /// <summary>The reported value is not a code of the official list valid for IWXXM 2025-2.</summary>
        Unknown = 0,
        /// <summary><c>SFC_WIND</c> — Widespread mean surface wind.</summary>
        SurfaceWind,
        /// <summary><c>SFC_VIS</c> — Surface visibility.</summary>
        SurfaceVisibility,
        /// <summary><c>ISOL_TS</c> — Isolated thunderstorms.</summary>
        IsolatedThunderstorms,
        /// <summary><c>ISOL_TSGR</c> — Isolated thunderstorms with hail.</summary>
        IsolatedThunderstormsWithHail,
        /// <summary><c>OCNL_TS</c> — Occasional thunderstorms.</summary>
        OccasionalThunderstorms,
        /// <summary><c>OCNL_TSGR</c> — Occasional thunderstorms with hail.</summary>
        OccasionalThunderstormsWithHail,
        /// <summary><c>MT_OBSC</c> — Mountain obscuration.</summary>
        MountainObscuration,
        /// <summary><c>BKN_CLD</c> — Broken cloud.</summary>
        BrokenCloud,
        /// <summary><c>OVC_CLD</c> — Overcast cloud.</summary>
        OvercastCloud,
        /// <summary><c>ISOL_CB</c> — Isolated cumulonimbus cloud.</summary>
        IsolatedCumulonimbus,
        /// <summary><c>OCNL_CB</c> — Occasional cumulonimbus cloud.</summary>
        OccasionalCumulonimbus,
        /// <summary><c>FRQ_CB</c> — Frequent cumulonimbus cloud.</summary>
        FrequentCumulonimbus,
        /// <summary><c>ISOL_TCU</c> — Isolated towering cumulus cloud.</summary>
        IsolatedToweringCumulus,
        /// <summary><c>OCNL_TCU</c> — Occasional towering cumulus cloud.</summary>
        OccasionalToweringCumulus,
        /// <summary><c>FRQ_TCU</c> — Frequent towering cumulus cloud.</summary>
        FrequentToweringCumulus,
        /// <summary><c>MOD_TURB</c> — Moderate turbulence.</summary>
        ModerateTurbulence,
        /// <summary><c>MOD_ICE</c> — Moderate icing.</summary>
        ModerateIcing,
        /// <summary><c>MOD_MTW</c> — Moderate mountain wave.</summary>
        ModerateMountainWave
    }
}
