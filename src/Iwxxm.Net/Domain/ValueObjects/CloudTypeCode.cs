using System;

namespace Iwxxm.Net.Reports
{
    /// <summary>
    /// WMO FM 94 BUFR Code Table 0 20 012 — Cloud type
    /// (<c>http://codes.wmo.int/bufr4/codeflag/0-20-012</c>).
    /// Each member value equals the official code; the member documentation quotes the official description.
    /// </summary>
    public enum CloudTypeCode
    {
        /// <summary>The raw value is not a code of table 0 20 012.</summary>
        Unknown = -1,
        /// <summary>0 — Cirrus (Ci).</summary>
        Cirrus = 0,
        /// <summary>1 — Cirrocumulus (Cc).</summary>
        Cirrocumulus = 1,
        /// <summary>2 — Cirrostratus (Cs).</summary>
        Cirrostratus = 2,
        /// <summary>3 — Altocumulus (Ac).</summary>
        Altocumulus = 3,
        /// <summary>4 — Altostratus (As).</summary>
        Altostratus = 4,
        /// <summary>5 — Nimbostratus (Ns).</summary>
        Nimbostratus = 5,
        /// <summary>6 — Stratocumulus (Sc).</summary>
        Stratocumulus = 6,
        /// <summary>7 — Stratus (St).</summary>
        Stratus = 7,
        /// <summary>8 — Cumulus (Cu).</summary>
        Cumulus = 8,
        /// <summary>9 — Cumulonimbus (Cb).</summary>
        Cumulonimbus = 9,
        /// <summary>10 — No CH clouds.</summary>
        NoHighClouds = 10,
        /// <summary>11 — Cirrus fibratus, sometimes uncinus, not progressively invading the sky.</summary>
        CirrusFibratusNotInvading = 11,
        /// <summary>12 — Cirrus spissatus, in patches or entangled sheaves, ...; or Cirrus castellanus or floccus.</summary>
        CirrusSpissatusCastellanusOrFloccus = 12,
        /// <summary>13 — Cirrus spissatus cumulonimbogenitus.</summary>
        CirrusSpissatusCumulonimbogenitus = 13,
        /// <summary>14 — Cirrus uncinus or fibratus, or both, progressively invading the sky; they generally thicken as a whole.</summary>
        CirrusUncinusOrFibratusInvading = 14,
        /// <summary>15 — Cirrus and Cirrostratus, or Cirrostratus alone, progressively invading the sky; the continuous veil does not reach 45 degrees above the horizon.</summary>
        CirrusAndCirrostratusInvadingBelow45Degrees = 15,
        /// <summary>16 — Cirrus and Cirrostratus, or Cirrostratus alone, progressively invading the sky; the continuous veil extends more than 45 degrees above the horizon, without the sky being totally covered.</summary>
        CirrusAndCirrostratusInvadingAbove45Degrees = 16,
        /// <summary>17 — Cirrostratus covering the whole sky.</summary>
        CirrostratusCoveringWholeSky = 17,
        /// <summary>18 — Cirrostratus not progressively invading the sky and not entirely covering it.</summary>
        CirrostratusNotInvadingNotCovering = 18,
        /// <summary>19 — Cirrocumulus alone, or Cirrocumulus predominant among the CH clouds.</summary>
        CirrocumulusAloneOrPredominant = 19,
        /// <summary>20 — No CM clouds.</summary>
        NoMiddleClouds = 20,
        /// <summary>21 — Altostratus translucidus.</summary>
        AltostratusTranslucidus = 21,
        /// <summary>22 — Altostratus opacus or Nimbostratus.</summary>
        AltostratusOpacusOrNimbostratus = 22,
        /// <summary>23 — Altocumulus translucidus at a single level.</summary>
        AltocumulusTranslucidusSingleLevel = 23,
        /// <summary>24 — Patches (often lenticular) of Altocumulus translucidus, continually changing and occurring at one or more levels.</summary>
        AltocumulusTranslucidusPatches = 24,
        /// <summary>25 — Altocumulus translucidus in bands, or one or more layers of Altocumulus translucidus or opacus, progressively invading the sky.</summary>
        AltocumulusInvading = 25,
        /// <summary>26 — Altocumulus cumulogenitus (or cumulonimbogenitus).</summary>
        AltocumulusCumulogenitus = 26,
        /// <summary>27 — Altocumulus translucidus or opacus in two or more layers, or Altocumulus opacus in a single layer, not progressively invading the sky, or Altocumulus with Altostratus or Nimbostratus.</summary>
        AltocumulusMultipleLayersOrWithAltostratusOrNimbostratus = 27,
        /// <summary>28 — Altocumulus castellanus or floccus.</summary>
        AltocumulusCastellanusOrFloccus = 28,
        /// <summary>29 — Altocumulus of a chaotic sky, generally at several levels.</summary>
        AltocumulusChaoticSky = 29,
        /// <summary>30 — No CL clouds.</summary>
        NoLowClouds = 30,
        /// <summary>31 — Cumulus humilis or Cumulus fractus other than of bad weather, or both.</summary>
        CumulusHumilisOrFractus = 31,
        /// <summary>32 — Cumulus mediocris or congestus, Towering cumulus (TCU), with or without Cumulus fractus or humilis or Stratocumulus, all having their bases at the same level.</summary>
        CumulusMediocrisOrCongestus = 32,
        /// <summary>33 — Cumulonimbus calvus, with or without Cumulus, Stratocumulus or Stratus.</summary>
        CumulonimbusCalvus = 33,
        /// <summary>34 — Stratocumulus cumulogenitus.</summary>
        StratocumulusCumulogenitus = 34,
        /// <summary>35 — Stratocumulus other than Stratocumulus cumulogenitus.</summary>
        StratocumulusNotCumulogenitus = 35,
        /// <summary>36 — Stratus nebulosus or Stratus fractus other than of bad weather, or both.</summary>
        StratusNebulosusOrFractus = 36,
        /// <summary>37 — Stratus fractus or Cumulus fractus of bad weather, or both (pannus), usually below Altostratus or Nimbostratus.</summary>
        FractusOfBadWeather = 37,
        /// <summary>38 — Cumulus and Stratocumulus other than Stratocumulus cumulogenitus, with bases at different levels.</summary>
        CumulusAndStratocumulusAtDifferentLevels = 38,
        /// <summary>39 — Cumulonimbus capillatus (often with an anvil), with or without Cumulonimbus calvus, Cumulus, Stratocumulus, Stratus or pannus.</summary>
        CumulonimbusCapillatus = 39,
        /// <summary>40 — CH.</summary>
        HighCloudClass = 40,
        /// <summary>41 — CM.</summary>
        MiddleCloudClass = 41,
        /// <summary>42 — CL.</summary>
        LowCloudClass = 42,
        /// <summary>59 — Cloud not visible owing to darkness, fog, duststorm, sandstorm, or other analogous phenomena.</summary>
        CloudNotVisible = 59,
        /// <summary>60 — CH clouds invisible owing to darkness, fog, blowing dust or sand, or other similar phenomena, or because of a continuous layer of lower clouds.</summary>
        HighCloudsInvisible = 60,
        /// <summary>61 — CM clouds invisible owing to darkness, fog, blowing dust or sand, or other similar phenomena, or because of continuous layer of lower clouds.</summary>
        MiddleCloudsInvisible = 61,
        /// <summary>62 — CL clouds invisible owing to darkness, fog, blowing dust or sand, or other similar phenomena.</summary>
        LowCloudsInvisible = 62,
        /// <summary>63 — Missing value.</summary>
        MissingValue = 63
    }
}
