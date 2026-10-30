namespace Iwxxm.Net.Reports
{
    /// <summary>
    /// SIGMET phenomenon (significant weather phenomena) (WMO code list <c>http://codes.wmo.int/49-2/SigWxPhenomena</c>, codes valid for IWXXM 2025-2).
    /// Members are listed with their official notation (<c>skos:notation</c>); the documentation quotes or summarizes the official
    /// description. <see cref="Unknown"/> (the default value) means the reported value is not an official code.
    /// </summary>
    /// <remarks>Used through <see cref="CodeListValue{TCode}"/>, which also keeps the code as reported.</remarks>
    public enum SigmetPhenomenon
    {
        /// <summary>The reported value is not a code of the official list valid for IWXXM 2025-2.</summary>
        Unknown = 0,
        /// <summary><c>OBSC_TS</c> — Obscured thunderstorm.</summary>
        ObscuredThunderstorm,
        /// <summary><c>OBSC_TSGR</c> — Obscured thunderstorm with hail.</summary>
        ObscuredThunderstormWithHail,
        /// <summary><c>EMBD_TS</c> — Embedded thunderstorm.</summary>
        EmbeddedThunderstorm,
        /// <summary><c>EMBD_TSGR</c> — Embedded thunderstorm with hail.</summary>
        EmbeddedThunderstormWithHail,
        /// <summary><c>FRQ_TS</c> — Frequent thunderstorm.</summary>
        FrequentThunderstorm,
        /// <summary><c>FRQ_TSGR</c> — Frequent thunderstorm with hail.</summary>
        FrequentThunderstormWithHail,
        /// <summary><c>SQL_TS</c> — Squall line.</summary>
        SquallLine,
        /// <summary><c>SQL_TSGR</c> — Squall line with hail.</summary>
        SquallLineWithHail,
        /// <summary><c>TC</c> — Tropical cyclone.</summary>
        TropicalCyclone,
        /// <summary><c>SEV_TURB</c> — Severe turbulence.</summary>
        SevereTurbulence,
        /// <summary><c>SEV_ICE</c> — Severe airframe icing.</summary>
        SevereIcing,
        /// <summary><c>SEV_ICE_FZRA</c> — Severe airframe icing from freezing rain.</summary>
        SevereIcingFreezingRain,
        /// <summary><c>SEV_MTW</c> — Severe mountain wave.</summary>
        SevereMountainWave,
        /// <summary><c>HVY_DS</c> — Heavy dust storm.</summary>
        HeavyDustStorm,
        /// <summary><c>HVY_SS</c> — Heavy sand storm.</summary>
        HeavySandStorm,
        /// <summary><c>VA</c> — Volcanic ash.</summary>
        VolcanicAsh,
        /// <summary><c>RDOACT_CLD</c> — Radioactive cloud.</summary>
        RadioactiveCloud
    }
}
