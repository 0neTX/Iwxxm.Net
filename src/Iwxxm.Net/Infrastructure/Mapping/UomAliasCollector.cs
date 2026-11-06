using System;
using System.Collections.Generic;
using System.Globalization;
using Iwxxm.Net.Reports;

namespace Iwxxm.Net.Infrastructure.Mapping
{
    /// <summary>
    /// Per-document collector of the legacy unit aliases that the unit readers accept (AUD-R5). Operational producers
    /// write the speed unit <c>kn_i</c> where UCUM (and so IWXXM) requires <c>[kn_i]</c>; the XSD and the official
    /// Schematron do not check it. The readers accept the alias, but the non-conformance is never silent: every
    /// occurrence is counted here and reported as one <see cref="UomMeasureReader.NonUcumAliasCode"/> warning per alias
    /// and field for the whole document (not one warning per value).
    /// </summary>
    /// <remarks>
    /// A collector is a plain object created by the mapping call that owns the document and passed explicitly to every
    /// reader that interprets a speed unit; it holds no ambient state, so concurrent mapping calls never share one. Where
    /// aliases must not be accepted the readers receive <see cref="NoAliases"/>, which rejects every alias, so no alias is
    /// ever accepted without a warning being able to report it.
    /// </remarks>
    internal sealed class UomAliasCollector
    {
        // legacy spelling -> canonical UCUM code
        private static readonly Dictionary<string, string> Aliases = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["kn_i"] = "[kn_i]",
        };

        /// <summary>
        /// Shared strict instance: accepts no alias (a legacy spelling stays an unsupported unit, as before AUD-R5) and never
        /// records anything, so it is immutable and safe to share across threads.
        /// </summary>
        public static readonly UomAliasCollector NoAliases = new UomAliasCollector(acceptsAliases: false);

        private readonly bool _acceptsAliases;
        private readonly Dictionary<(string Field, string Alias), Occurrence> _occurrences =
            new Dictionary<(string Field, string Alias), Occurrence>();

        /// <summary>Creates a collector that accepts the known aliases and records each accepted occurrence.</summary>
        public UomAliasCollector()
            : this(acceptsAliases: true)
        {
        }

        private UomAliasCollector(bool acceptsAliases) => _acceptsAliases = acceptsAliases;

        private sealed class Occurrence
        {
            public Occurrence(string firstLocation) => FirstLocation = firstLocation;

            public string FirstLocation { get; }

            public int Count { get; set; }
        }

        /// <summary>Resolves a legacy spelling to its UCUM code; <c>false</c> for any other unit or for <see cref="NoAliases"/>.</summary>
        public bool IsAlias(string? uom, out string canonical)
        {
            canonical = string.Empty;
            return _acceptsAliases && uom != null && Aliases.TryGetValue(uom.Trim(), out canonical!);
        }

        /// <summary>Counts one accepted alias at <paramref name="location"/> and returns its UCUM code.</summary>
        public string Accept(string alias, string location)
        {
            if (!_acceptsAliases)
                throw new InvalidOperationException("This collector accepts no unit alias.");

            string canonical = Aliases[alias.Trim()];
            (string, string) key = (FieldOf(location), alias.Trim());
            if (!_occurrences.TryGetValue(key, out Occurrence? occurrence))
            {
                occurrence = new Occurrence(location);
                _occurrences.Add(key, occurrence);
            }

            occurrence.Count++;
            return canonical;
        }

        /// <summary>
        /// Adds the aggregated alias warnings to a successful result; a failure and a document without aliases return the
        /// result unchanged. Called once, by the mapping call that owns the document.
        /// </summary>
        public Result<T> Attach<T>(Result<T> result)
        {
            if (result.IsFailure || _occurrences.Count == 0)
                return result;

            var warnings = new List<ValidationError>(result.Warnings);
            foreach (KeyValuePair<(string Field, string Alias), Occurrence> entry in _occurrences)
            {
                string canonical = Aliases[entry.Key.Alias];
                warnings.Add(new ValidationError(
                    UomMeasureReader.NonUcumAliasCode,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "The non-UCUM unit of measure '{0}' was accepted as the UCUM code '{1}' on {2} '{3}' value(s) (first at '{4}'); IWXXM requires the UCUM code '{1}'.",
                        entry.Key.Alias,
                        canonical,
                        entry.Value.Count,
                        entry.Key.Field,
                        entry.Value.FirstLocation),
                    ErrorCategory.DomainMapping,
                    ValidationSeverity.Warning,
                    entry.Value.FirstLocation));
            }

            return Result<T>.Success(result.Value, warnings);
        }

        // Last path segment without its predicate: ".../WAFSJetStreamWindSymbol[@gml:id='x']/windSpeed" -> "windSpeed".
        private static string FieldOf(string location)
        {
            int slash = location.LastIndexOf('/');
            string segment = slash >= 0 ? location.Substring(slash + 1) : location;
            int bracket = segment.IndexOf('[');
            return bracket > 0 ? segment.Substring(0, bracket) : segment;
        }
    }
}
