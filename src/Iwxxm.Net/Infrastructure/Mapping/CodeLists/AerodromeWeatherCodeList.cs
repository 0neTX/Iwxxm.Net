using System;
using System.Collections.Generic;
using Iwxxm.Net.Reports;

namespace Iwxxm.Net.Infrastructure.Mapping.CodeLists
{
    /// <summary>
    /// Anti-Corruption Layer translation of the WMO code list <c>AerodromePresentOrForecastWeather</c> into
    /// <see cref="AerodromeWeather"/> Domain values, and back into references for Linked-Data outputs (GeoJSON).
    /// </summary>
    /// <remarks>
    /// The list has 402 combinatorial codes, so the codes are not duplicated in source: they are read once, lazily and
    /// thread-safely, from the embedded official RDF file (the source of truth), with the IWXXM 2025-2 version filter.
    /// </remarks>
    internal static class AerodromeWeatherCodeList
    {
        /// <summary>
        /// Register under which the official RDF (<c>codes.wmo.int-49-2-AerodromePresentOrForecastWeather.rdf</c>) publishes
        /// the codes: <c>reg:Register rdf:about</c> and every <c>skos:Concept rdf:about</c> use the WMO table 4678 register,
        /// so this is the reference form the official Schematron accepts.
        /// </summary>
        internal const string RegisterUri = "http://codes.wmo.int/306/4678";
        internal const string ResourcePath = "Codelists/codes.wmo.int-49-2-AerodromePresentOrForecastWeather.rdf";

        private static readonly Lazy<HashSet<string>> OfficialCodes =
            new Lazy<HashSet<string>>(() => OfficialCodeListReader.ReadCodes(ResourcePath, RegisterUri), isThreadSafe: true);

        /// <summary>Gets the official codes valid for IWXXM 2025-2.</summary>
        internal static IReadOnlyCollection<string> Codes => OfficialCodes.Value;

        /// <summary>Translates a non-blank reference; a non-official code is kept with <see cref="AerodromeWeather.IsKnown"/> <c>false</c>.</summary>
        internal static AerodromeWeather Translate(string href)
        {
            string? code = CodeListReference.TryGetCode(href, RegisterUri);
            return code == null
                ? new AerodromeWeather(href, isKnown: false)
                : new AerodromeWeather(code, OfficialCodes.Value.Contains(code));
        }

        /// <summary>Rebuilds the exact original reference of a translated value.</summary>
        internal static string ToReference(AerodromeWeather value) => CodeListReference.Rebuild(value.Notation, RegisterUri);
    }
}
