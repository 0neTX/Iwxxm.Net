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
    /// The scope is thread-bound and lives for one mapping call: the outermost <see cref="Begin"/> owns it and attaches the
    /// warnings to the result; a nested scope (a SIGWX report mapped inside a bulletin) shares it. Outside a scope the
    /// readers stay strict, so no alias is ever accepted without a warning being able to report it.
    /// </remarks>
    internal sealed class UomAliasScope : IDisposable
    {
        // legacy spelling -> canonical UCUM code
        private static readonly Dictionary<string, string> Aliases = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["kn_i"] = "[kn_i]",
        };

        [ThreadStatic]
        private static UomAliasScope? _current;

        private readonly bool _owner;
        private readonly Dictionary<(string Field, string Alias), Occurrence> _occurrences;

        private UomAliasScope(bool owner, Dictionary<(string Field, string Alias), Occurrence> occurrences)
        {
            _owner = owner;
            _occurrences = occurrences;
        }

        private sealed class Occurrence
        {
            public Occurrence(string firstLocation) => FirstLocation = firstLocation;

            public string FirstLocation { get; }

            public int Count { get; set; }
        }

        /// <summary>Starts the scope of a mapping call, or joins the one already active on this thread.</summary>
        public static UomAliasScope Begin()
        {
            if (_current != null)
                return new UomAliasScope(false, _current._occurrences);

            var scope = new UomAliasScope(true, new Dictionary<(string, string), Occurrence>());
            _current = scope;
            return scope;
        }

        /// <summary>Resolves a legacy spelling to its UCUM code; only while a scope is active (otherwise the readers stay strict).</summary>
        public static bool IsActiveAlias(string? uom, out string canonical)
        {
            canonical = string.Empty;
            return _current != null && uom != null && Aliases.TryGetValue(uom.Trim(), out canonical!);
        }

        /// <summary>Counts one accepted alias at <paramref name="location"/> and returns its UCUM code.</summary>
        public static string Accept(string alias, string location)
        {
            string canonical = Aliases[alias.Trim()];
            UomAliasScope? scope = _current;
            if (scope != null)
            {
                (string, string) key = (FieldOf(location), alias.Trim());
                if (!scope._occurrences.TryGetValue(key, out Occurrence? occurrence))
                {
                    occurrence = new Occurrence(location);
                    scope._occurrences.Add(key, occurrence);
                }

                occurrence.Count++;
            }

            return canonical;
        }

        /// <summary>
        /// Adds the aggregated alias warnings to a successful result when this scope owns the document; a nested scope and a
        /// failure return the result unchanged.
        /// </summary>
        public Result<T> Attach<T>(Result<T> result)
        {
            if (!_owner || result.IsFailure || _occurrences.Count == 0)
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

        public void Dispose()
        {
            if (_owner && ReferenceEquals(_current, this))
                _current = null;
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
