using System;
using System.Globalization;

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

    /// <summary>
    /// Pure Domain resolver that translates WMO Code Registry URIs (<c>http://codes.wmo.int/bufr4/codeflag/...</c>),
    /// bare numeric BUFR codes, and aeronautical TAC abbreviations into strongly-typed Domain enumerations.
    /// Numeric codes are resolved only when they belong to the expected table: a URI of another table, or a number
    /// that is not defined in the official table, resolves to <c>Unknown</c>.
    /// </summary>
    public static class WmoCodeRegistry
    {
        public const string CloudDistributionTableUri = "http://codes.wmo.int/bufr4/codeflag/0-20-008";
        public const string CloudTypeTableUri = "http://codes.wmo.int/bufr4/codeflag/0-20-012";
        public const string TurbulenceDegreeTableUri = "http://codes.wmo.int/bufr4/codeflag/0-11-030";
        public const string IcingDegreeTableUri = "http://codes.wmo.int/bufr4/codeflag/0-20-041";

        public static CloudDistributionCode ResolveCloudDistribution(string? rawOrUri)
        {
            int? code = TryGetNumericCode(rawOrUri, CloudDistributionTableUri);
            if (code.HasValue)
            {
                return Enum.IsDefined(typeof(CloudDistributionCode), code.Value) && code.Value >= 0
                    ? (CloudDistributionCode)code.Value
                    : CloudDistributionCode.Unknown;
            }

            return ExtractTacToken(rawOrUri) switch
            {
                "SKC" => CloudDistributionCode.SkyClear,
                "FEW" => CloudDistributionCode.Few,
                "SCT" => CloudDistributionCode.Scattered,
                "BKN" => CloudDistributionCode.Broken,
                "OVC" => CloudDistributionCode.Overcast,
                "ISOL" => CloudDistributionCode.Isolated,
                "ISOL_EMBD" => CloudDistributionCode.IsolatedEmbedded,
                "OCNL" => CloudDistributionCode.Occasional,
                "OCNL_EMBD" => CloudDistributionCode.OccasionalEmbedded,
                "FRQ" => CloudDistributionCode.Frequent,
                "FRQ_EMBD" => CloudDistributionCode.FrequentEmbedded,
                "DNS" => CloudDistributionCode.Dense,
                "LYR" => CloudDistributionCode.Layers,
                "OBSC" => CloudDistributionCode.Obscured,
                "EMBD" => CloudDistributionCode.Embedded,
                _ => CloudDistributionCode.Unknown
            };
        }

        public static CloudTypeCode ResolveCloudType(string? rawOrUri)
        {
            int? code = TryGetNumericCode(rawOrUri, CloudTypeTableUri);
            if (code.HasValue)
            {
                return Enum.IsDefined(typeof(CloudTypeCode), code.Value) && code.Value >= 0
                    ? (CloudTypeCode)code.Value
                    : CloudTypeCode.Unknown;
            }

            return ExtractTacToken(rawOrUri) switch
            {
                "CI" => CloudTypeCode.Cirrus,
                "CC" => CloudTypeCode.Cirrocumulus,
                "CS" => CloudTypeCode.Cirrostratus,
                "AC" => CloudTypeCode.Altocumulus,
                "AS" => CloudTypeCode.Altostratus,
                "NS" => CloudTypeCode.Nimbostratus,
                "SC" => CloudTypeCode.Stratocumulus,
                "ST" => CloudTypeCode.Stratus,
                "CU" => CloudTypeCode.Cumulus,
                "TCU" => CloudTypeCode.CumulusMediocrisOrCongestus,
                "CB" => CloudTypeCode.Cumulonimbus,
                _ => CloudTypeCode.Unknown
            };
        }

        public static TurbulenceDegreeCode ResolveTurbulenceDegree(string? rawOrUri)
        {
            int? code = TryGetNumericCode(rawOrUri, TurbulenceDegreeTableUri);
            if (code.HasValue)
            {
                return Enum.IsDefined(typeof(TurbulenceDegreeCode), code.Value) && code.Value >= 0
                    ? (TurbulenceDegreeCode)code.Value
                    : TurbulenceDegreeCode.Unknown;
            }

            return ExtractTacToken(rawOrUri) switch
            {
                "NIL" => TurbulenceDegreeCode.Nil,
                "FBL" or "LIGHT" => TurbulenceDegreeCode.Light,
                "MOD" or "MODERATE" => TurbulenceDegreeCode.Moderate,
                "SEV" or "SEVERE" => TurbulenceDegreeCode.Severe,
                "EXTREME" => TurbulenceDegreeCode.Extreme,
                "MOD_CAT" => TurbulenceDegreeCode.ModerateClearAir,
                "SEV_CAT" => TurbulenceDegreeCode.SevereClearAir,
                "EXTREME_CAT" => TurbulenceDegreeCode.ExtremeClearAir,
                _ => TurbulenceDegreeCode.Unknown
            };
        }

        public static IcingDegreeCode ResolveIcingDegree(string? rawOrUri)
        {
            int? code = TryGetNumericCode(rawOrUri, IcingDegreeTableUri);
            if (code.HasValue)
            {
                return Enum.IsDefined(typeof(IcingDegreeCode), code.Value) && code.Value >= 0
                    ? (IcingDegreeCode)code.Value
                    : IcingDegreeCode.Unknown;
            }

            return ExtractTacToken(rawOrUri) switch
            {
                "NONE" or "NIL" => IcingDegreeCode.None,
                "FBL" or "LIGHT" => IcingDegreeCode.Light,
                "MOD" or "MODERATE" => IcingDegreeCode.Moderate,
                "SEV" or "SEVERE" => IcingDegreeCode.Severe,
                _ => IcingDegreeCode.Unknown
            };
        }

        /// <summary>
        /// Extracts the raw official numeric code from either a bare integer (e.g. <c>"10"</c>) or a code URI that belongs
        /// to <paramref name="tableUri"/> (e.g. <c>http://codes.wmo.int/bufr4/codeflag/0-11-030/10</c>).
        /// Returns <c>null</c> for TAC abbreviations, URIs of other tables, and malformed values.
        /// </summary>
        public static int? TryGetNumericCode(string? rawOrUri, string tableUri)
        {
            if (string.IsNullOrWhiteSpace(rawOrUri) || string.IsNullOrWhiteSpace(tableUri))
                return null;

            string value = rawOrUri!.Trim();
            string token;
            if (value.IndexOf('/') >= 0)
            {
                string prefix = tableUri.TrimEnd('/') + "/";
                if (!value.StartsWith(prefix, StringComparison.Ordinal))
                    return null;

                token = value.Substring(prefix.Length).TrimEnd('/');
            }
            else
            {
                token = value;
            }

            return token.Length > 0 &&
                   token.Length <= 3 &&
                   int.TryParse(token, NumberStyles.None, CultureInfo.InvariantCulture, out int code)
                ? code
                : (int?)null;
        }

        private static string ExtractTacToken(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            string trimmed = value!.Trim();
            return trimmed.IndexOf('/') >= 0 ? string.Empty : trimmed.ToUpperInvariant();
        }
    }
}
