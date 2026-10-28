using System;

namespace Iwxxm.Net.Domain.ValueObjects
{
    /// <summary>
    /// WMO FM 94 BUFR Code Table 0 20 008 — Cloud Distribution for Aviation
    /// (<c>http://codes.wmo.int/bufr4/codeflag/0-20-008</c>).
    /// </summary>
    public enum CloudDistributionCode
    {
        Unknown = 0,
        Isolated = 8,
        IsolatedEmbedded = 9,
        Occasional = 10,
        OccasionalEmbedded = 11,
        Frequent = 12,
        FrequentEmbedded = 13,
        Dense = 14,
        DenseEmbedded = 15,
        Layers = 16
    }

    /// <summary>
    /// WMO FM 94 BUFR Code Table 0 20 012 — Cloud Type
    /// (<c>http://codes.wmo.int/bufr4/codeflag/0-20-012</c>).
    /// </summary>
    public enum CloudTypeCode
    {
        Unknown = -1,
        Cirrus = 0,
        Cirrocumulus = 1,
        Cirrostratus = 2,
        Altocumulus = 3,
        Altostratus = 4,
        Nimbostratus = 5,
        Stratocumulus = 6,
        Stratus = 7,
        Cumulus = 8,
        Cumulonimbus = 9
    }

    /// <summary>
    /// WMO FM 94 BUFR Code Table 0 11 030 — Extended Degree of Turbulence
    /// (<c>http://codes.wmo.int/bufr4/codeflag/0-11-030</c>).
    /// </summary>
    public enum TurbulenceDegreeCode
    {
        Unknown = -1,
        Nil = 0,
        Light = 1,
        Moderate = 2,
        Severe = 3,
        Extreme = 19,
        LightClearAir = 8,
        ModerateClearAir = 10,
        SevereClearAir = 11,
        ExtremeClearAir = 20
    }

    /// <summary>
    /// WMO FM 94 BUFR Code Table 0 20 041 — Airframe Icing
    /// (<c>http://codes.wmo.int/bufr4/codeflag/0-20-041</c>).
    /// </summary>
    public enum IcingDegreeCode
    {
        Unknown = -1,
        None = 0,
        Light = 1,
        Moderate = 3,
        Severe = 7
    }

    /// <summary>
    /// Pure Domain resolver that translates WMO/ICAO Code Registry URIs (<c>http://codes.wmo.int/...</c>)
    /// and aeronautical TAC abbreviations into strongly-typed Domain enumerations.
    /// </summary>
    public static class WmoCodeRegistry
    {
        public static CloudDistributionCode ResolveCloudDistribution(string? rawOrUri)
        {
            if (string.IsNullOrWhiteSpace(rawOrUri))
                return CloudDistributionCode.Unknown;

            string token = ExtractTrailingCodeToken(rawOrUri!);
            return token.ToUpperInvariant() switch
            {
                "8" or "ISOL" => CloudDistributionCode.Isolated,
                "9" or "ISOL_EMBD" => CloudDistributionCode.IsolatedEmbedded,
                "10" or "OCNL" => CloudDistributionCode.Occasional,
                "11" or "OCNL_EMBD" => CloudDistributionCode.OccasionalEmbedded,
                "12" or "FRQ" => CloudDistributionCode.Frequent,
                "13" or "FRQ_EMBD" => CloudDistributionCode.FrequentEmbedded,
                "14" or "DNS" => CloudDistributionCode.Dense,
                "15" or "DNS_EMBD" => CloudDistributionCode.DenseEmbedded,
                "16" or "LYR" => CloudDistributionCode.Layers,
                _ => CloudDistributionCode.Unknown
            };
        }

        public static CloudTypeCode ResolveCloudType(string? rawOrUri)
        {
            if (string.IsNullOrWhiteSpace(rawOrUri))
                return CloudTypeCode.Unknown;

            string token = ExtractTrailingCodeToken(rawOrUri!);
            return token.ToUpperInvariant() switch
            {
                "0" or "CI" => CloudTypeCode.Cirrus,
                "1" or "CC" => CloudTypeCode.Cirrocumulus,
                "2" or "CS" => CloudTypeCode.Cirrostratus,
                "3" or "AC" => CloudTypeCode.Altocumulus,
                "4" or "AS" => CloudTypeCode.Altostratus,
                "5" or "NS" => CloudTypeCode.Nimbostratus,
                "6" or "SC" => CloudTypeCode.Stratocumulus,
                "7" or "ST" => CloudTypeCode.Stratus,
                "8" or "CU" or "TCU" => CloudTypeCode.Cumulus,
                "9" or "CB" => CloudTypeCode.Cumulonimbus,
                _ => CloudTypeCode.Unknown
            };
        }

        public static TurbulenceDegreeCode ResolveTurbulenceDegree(string? rawOrUri)
        {
            if (string.IsNullOrWhiteSpace(rawOrUri))
                return TurbulenceDegreeCode.Unknown;

            string token = ExtractTrailingCodeToken(rawOrUri!);
            return token.ToUpperInvariant() switch
            {
                "0" or "NIL" => TurbulenceDegreeCode.Nil,
                "1" or "FBL" or "LIGHT" => TurbulenceDegreeCode.Light,
                "2" or "MOD" or "MODERATE" => TurbulenceDegreeCode.Moderate,
                "3" or "SEV" or "SEVERE" => TurbulenceDegreeCode.Severe,
                "8" => TurbulenceDegreeCode.LightClearAir,
                "10" or "MOD_CAT" => TurbulenceDegreeCode.ModerateClearAir,
                "11" or "SEV_CAT" => TurbulenceDegreeCode.SevereClearAir,
                "19" or "EXTREME" => TurbulenceDegreeCode.Extreme,
                "20" or "EXTREME_CAT" => TurbulenceDegreeCode.ExtremeClearAir,
                _ => TurbulenceDegreeCode.Unknown
            };
        }

        public static IcingDegreeCode ResolveIcingDegree(string? rawOrUri)
        {
            if (string.IsNullOrWhiteSpace(rawOrUri))
                return IcingDegreeCode.Unknown;

            string token = ExtractTrailingCodeToken(rawOrUri!);
            return token.ToUpperInvariant() switch
            {
                "0" or "NONE" or "NIL" => IcingDegreeCode.None,
                "1" or "2" or "FBL" or "LIGHT" => IcingDegreeCode.Light,
                "3" or "4" or "5" or "6" or "MOD" or "MODERATE" => IcingDegreeCode.Moderate,
                "7" or "8" or "9" or "SEV" or "SEVERE" => IcingDegreeCode.Severe,
                _ => IcingDegreeCode.Unknown
            };
        }

        private static string ExtractTrailingCodeToken(string value)
        {
            string trimmed = value.Trim().TrimEnd('/');
            int lastSlash = trimmed.LastIndexOf('/');
            return lastSlash >= 0 && lastSlash < trimmed.Length - 1
                ? trimmed.Substring(lastSlash + 1)
                : trimmed;
        }
    }
}
