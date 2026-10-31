using System;
using System.Text;
using System.Text.RegularExpressions;

namespace Iwxxm.Net.Validation.Saxon.Internal
{
    /// <summary>
    /// Rewrites the <c>@location</c> of an SVRL finding into an absolute XPath 1.0 expression that any XPath engine can
    /// evaluate without namespace bindings (AUD-L8).
    /// <para>
    /// The vendored SchXslt 1.10.1 compiler (kept byte-identical) writes locations with the XPath 3.0 braced URI literal
    /// syntax, e.g. <c>/Q{http://icao.int/iwxxm/2025-2}METAR[1]/Q{http://icao.int/iwxxm/2025-2}observation[1]</c>. That
    /// notation is not understood by <c>System.Xml.XPath</c> and most other XPath 1.0 / 2.0 engines. Each element step is
    /// therefore rewritten to the namespace-independent form
    /// <c>/*[namespace-uri()='ns' and local-name()='name'][n]</c> and each attribute step to
    /// <c>/@*[namespace-uri()='ns' and local-name()='name']</c>. The positional predicate keeps its meaning: SchXslt counts
    /// the preceding siblings with the same expanded name, which is exactly the position inside the name-filtered node
    /// set. <c>text()[n]</c>, <c>comment()[n]</c> and <c>processing-instruction("target")[n]</c> steps are already valid
    /// XPath 1.0 and are left untouched, as is a location that contains no <c>Q{...}</c> step.
    /// </para>
    /// <para>
    /// Namespace URIs come from the (untrusted) input document, so they are always emitted as properly quoted XPath string
    /// literals; a URI that contains both quote characters is emitted with <c>concat()</c>.
    /// </para>
    /// </summary>
    internal static class SvrlLocation
    {
        // [@]Q{namespace}local-name[position]; the namespace URI may contain '/', but never '}'.
        private static readonly Regex QualifiedStep = new Regex(
            @"(?<attribute>@)?Q\{(?<namespace>[^}]*)\}(?<local>[^/\[\]\s]+)(?:\[(?<position>\d+)\])?",
            RegexOptions.CultureInvariant | RegexOptions.Compiled,
            TimeSpan.FromSeconds(1));

        /// <summary>Converts a SchXslt location into the canonical namespace-independent XPath 1.0 form.</summary>
        /// <returns>The converted XPath, or <paramref name="location"/> itself when it has no braced-URI step.</returns>
        public static string? ToXPath(string? location)
        {
            if (string.IsNullOrEmpty(location) || location!.IndexOf("Q{", StringComparison.Ordinal) < 0)
                return location;

            try
            {
                return QualifiedStep.Replace(location, Rewrite);
            }
            catch (RegexMatchTimeoutException)
            {
                return location;
            }
        }

        private static string Rewrite(Match step)
        {
            string filter =
                "[namespace-uri()=" + Literal(step.Groups["namespace"].Value) +
                " and local-name()=" + Literal(step.Groups["local"].Value) + "]";

            if (step.Groups["attribute"].Success)
                return "@*" + filter;

            string position = step.Groups["position"].Success ? "[" + step.Groups["position"].Value + "]" : string.Empty;
            return "*" + filter + position;
        }

        /// <summary>Quotes <paramref name="value"/> as an XPath 1.0 string literal.</summary>
        internal static string Literal(string value)
        {
            if (value.IndexOf('\'') < 0)
                return "'" + value + "'";
            if (value.IndexOf('"') < 0)
                return "\"" + value + "\"";

            // Both quote kinds: XPath 1.0 literals have no escape sequence, so splice the apostrophes with concat().
            var builder = new StringBuilder("concat(");
            string[] parts = value.Split('\'');
            for (int i = 0; i < parts.Length; i++)
            {
                if (i > 0)
                    builder.Append(", \"'\", ");
                builder.Append('\'').Append(parts[i]).Append('\'');
            }

            return builder.Append(')').ToString();
        }
    }
}
