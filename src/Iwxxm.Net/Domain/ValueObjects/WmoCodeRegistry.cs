using System;
using System.Collections.Generic;
using System.Globalization;

namespace Iwxxm.Net.Reports
{
    /// <summary>
    /// Pure Domain resolver that translates WMO Code Registry URIs (<c>http://codes.wmo.int/bufr4/codeflag/...</c>),
    /// bare numeric BUFR codes, and aeronautical TAC abbreviations into strongly-typed Domain enumerations.
    /// Numeric codes are resolved only when they belong to the expected table: a URI of another table, or a number
    /// that is not defined in the official table, resolves to <c>Unknown</c>.
    /// </summary>
    /// <remarks>
    /// Every supported enumeration follows the same convention: each member value equals its official BUFR code and
    /// the extra member <c>Unknown</c> has the value <see cref="UnknownCodeValue"/>. All four <c>Resolve*</c> methods share
    /// one algorithm (<see cref="Resolve{TCode}"/>): numeric code first, then the TAC abbreviation table of the code list.
    /// </remarks>
    public static class WmoCodeRegistry
    {
        public const string CloudDistributionTableUri = "http://codes.wmo.int/bufr4/codeflag/0-20-008";
        public const string CloudTypeTableUri = "http://codes.wmo.int/bufr4/codeflag/0-20-012";
        public const string TurbulenceDegreeTableUri = "http://codes.wmo.int/bufr4/codeflag/0-11-030";
        public const string IcingDegreeTableUri = "http://codes.wmo.int/bufr4/codeflag/0-20-041";

        /// <summary>
        /// Value of the <c>Unknown</c> member of every BUFR code enumeration. BUFR code-table entries are unsigned, so a
        /// negative value can never collide with an official code; this is why a parsed code must be <c>&gt;= 0</c> before
        /// it is accepted, even though <c>Unknown</c> is a defined enum member.
        /// </summary>
        private const int UnknownCodeValue = -1;

        /// <summary>
        /// Longest numeric token accepted as a code. A BUFR code-table entry is bounded by the bit width of its element:
        /// the tables resolved here are at most 6 bits wide (codes 0-63). Three digits cover any code of a table up to
        /// 9 bits wide (0-511) and reject longer tokens before parsing, so a malformed reference cannot overflow
        /// <see cref="int"/> or match a code by accident.
        /// </summary>
        private const int MaxCodeDigits = 3;

        private static readonly Dictionary<string, CloudDistributionCode> CloudDistributionAbbreviations =
            new Dictionary<string, CloudDistributionCode>(StringComparer.Ordinal)
            {
                ["SKC"] = CloudDistributionCode.SkyClear,
                ["FEW"] = CloudDistributionCode.Few,
                ["SCT"] = CloudDistributionCode.Scattered,
                ["BKN"] = CloudDistributionCode.Broken,
                ["OVC"] = CloudDistributionCode.Overcast,
                ["ISOL"] = CloudDistributionCode.Isolated,
                ["ISOL_EMBD"] = CloudDistributionCode.IsolatedEmbedded,
                ["OCNL"] = CloudDistributionCode.Occasional,
                ["OCNL_EMBD"] = CloudDistributionCode.OccasionalEmbedded,
                ["FRQ"] = CloudDistributionCode.Frequent,
                ["FRQ_EMBD"] = CloudDistributionCode.FrequentEmbedded,
                ["DNS"] = CloudDistributionCode.Dense,
                ["LYR"] = CloudDistributionCode.Layers,
                ["OBSC"] = CloudDistributionCode.Obscured,
                ["EMBD"] = CloudDistributionCode.Embedded
            };

        private static readonly Dictionary<string, CloudTypeCode> CloudTypeAbbreviations =
            new Dictionary<string, CloudTypeCode>(StringComparer.Ordinal)
            {
                ["CI"] = CloudTypeCode.Cirrus,
                ["CC"] = CloudTypeCode.Cirrocumulus,
                ["CS"] = CloudTypeCode.Cirrostratus,
                ["AC"] = CloudTypeCode.Altocumulus,
                ["AS"] = CloudTypeCode.Altostratus,
                ["NS"] = CloudTypeCode.Nimbostratus,
                ["SC"] = CloudTypeCode.Stratocumulus,
                ["ST"] = CloudTypeCode.Stratus,
                ["CU"] = CloudTypeCode.Cumulus,
                ["TCU"] = CloudTypeCode.CumulusMediocrisOrCongestus,
                ["CB"] = CloudTypeCode.Cumulonimbus
            };

        private static readonly Dictionary<string, TurbulenceDegreeCode> TurbulenceDegreeAbbreviations =
            new Dictionary<string, TurbulenceDegreeCode>(StringComparer.Ordinal)
            {
                ["NIL"] = TurbulenceDegreeCode.Nil,
                ["FBL"] = TurbulenceDegreeCode.Light,
                ["LIGHT"] = TurbulenceDegreeCode.Light,
                ["MOD"] = TurbulenceDegreeCode.Moderate,
                ["MODERATE"] = TurbulenceDegreeCode.Moderate,
                ["SEV"] = TurbulenceDegreeCode.Severe,
                ["SEVERE"] = TurbulenceDegreeCode.Severe,
                ["EXTREME"] = TurbulenceDegreeCode.Extreme,
                ["MOD_CAT"] = TurbulenceDegreeCode.ModerateClearAir,
                ["SEV_CAT"] = TurbulenceDegreeCode.SevereClearAir,
                ["EXTREME_CAT"] = TurbulenceDegreeCode.ExtremeClearAir
            };

        private static readonly Dictionary<string, IcingDegreeCode> IcingDegreeAbbreviations =
            new Dictionary<string, IcingDegreeCode>(StringComparer.Ordinal)
            {
                ["NONE"] = IcingDegreeCode.None,
                ["NIL"] = IcingDegreeCode.None,
                ["FBL"] = IcingDegreeCode.Light,
                ["LIGHT"] = IcingDegreeCode.Light,
                ["MOD"] = IcingDegreeCode.Moderate,
                ["MODERATE"] = IcingDegreeCode.Moderate,
                ["SEV"] = IcingDegreeCode.Severe,
                ["SEVERE"] = IcingDegreeCode.Severe
            };

        public static CloudDistributionCode ResolveCloudDistribution(string? rawOrUri) =>
            Resolve(rawOrUri, CloudDistributionTableUri, CloudDistributionAbbreviations);

        public static CloudTypeCode ResolveCloudType(string? rawOrUri) =>
            Resolve(rawOrUri, CloudTypeTableUri, CloudTypeAbbreviations);

        public static TurbulenceDegreeCode ResolveTurbulenceDegree(string? rawOrUri) =>
            Resolve(rawOrUri, TurbulenceDegreeTableUri, TurbulenceDegreeAbbreviations);

        public static IcingDegreeCode ResolveIcingDegree(string? rawOrUri) =>
            Resolve(rawOrUri, IcingDegreeTableUri, IcingDegreeAbbreviations);

        /// <summary>
        /// Extracts the raw official numeric code from either a bare integer (e.g. <c>"10"</c>) or a code URI that belongs
        /// to <paramref name="tableUri"/> (e.g. <c>http://codes.wmo.int/bufr4/codeflag/0-11-030/10</c>).
        /// Returns <c>null</c> for TAC abbreviations, URIs of other tables, signed numbers, tokens longer than three
        /// digits, and malformed values.
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
                   token.Length <= MaxCodeDigits &&
                   int.TryParse(token, NumberStyles.None, CultureInfo.InvariantCulture, out int code)
                ? code
                : (int?)null;
        }

        /// <summary>
        /// Shared resolution algorithm: a numeric code (bare or URI of <paramref name="tableUri"/>) maps to the enum
        /// member with that value when the official table defines it, otherwise to <c>Unknown</c>; a value that is not
        /// numeric is looked up as an upper-case TAC abbreviation in <paramref name="abbreviations"/>.
        /// </summary>
        private static TCode Resolve<TCode>(
            string? rawOrUri,
            string tableUri,
            IReadOnlyDictionary<string, TCode> abbreviations)
            where TCode : struct, Enum
        {
            TCode unknown = (TCode)Enum.ToObject(typeof(TCode), UnknownCodeValue);

            int? code = TryGetNumericCode(rawOrUri, tableUri);
            if (code.HasValue)
            {
                return code.Value >= 0 && Enum.IsDefined(typeof(TCode), code.Value)
                    ? (TCode)Enum.ToObject(typeof(TCode), code.Value)
                    : unknown;
            }

            return abbreviations.TryGetValue(ExtractTacToken(rawOrUri), out TCode member) ? member : unknown;
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
