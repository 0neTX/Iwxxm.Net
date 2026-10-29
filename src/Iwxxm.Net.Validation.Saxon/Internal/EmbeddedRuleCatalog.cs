using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace Iwxxm.Net.Validation.Saxon.Internal
{
    /// <summary>
    /// Air-gapped catalog of every resource the official Schematron pipeline may dereference.
    /// <list type="bullet">
    /// <item>SchXslt 1.10.1 stylesheet modules (<c>xsl:include</c>/<c>xsl:import</c>) under <see cref="SchXsltBaseUri"/>.</item>
    /// <item>The official <c>iwxxm.sch</c> and its compiled stylesheet under <see cref="RuleBaseUri"/>; the relative
    /// <c>document('codes.wmo.int-*.rdf')</c> calls of the official rules resolve against that base to the official
    /// WMO RDF code lists embedded in the <c>Iwxxm.Net</c> assembly.</item>
    /// </list>
    /// The base URIs use the reserved <c>.invalid</c> top-level domain (RFC 2606), so they can never be dereferenced
    /// on a network. Any URI outside the catalog is refused; no network or file-system access is ever performed.
    /// </summary>
    internal static class EmbeddedRuleCatalog
    {
        internal static readonly Uri SchXsltBaseUri = new Uri("http://schxslt.iwxxm-net.invalid/1.10.1/");
        internal static readonly Uri RuleBaseUri = new Uri("http://rules.iwxxm-net.invalid/iwxxm/2025-2/rule/");

        internal static readonly Uri SchematronUri = new Uri(RuleBaseUri, "iwxxm.sch");
        internal static readonly Uri CompiledStylesheetUri = new Uri(RuleBaseUri, "iwxxm.sch.xsl");
        internal static readonly Uri PipelineUri = new Uri(SchXsltBaseUri, "2.0/pipeline-for-svrl.xsl");

        private const string OwnResourcePrefix = "Iwxxm.Net.Validation.Saxon/Resources/";
        private const string CoreCodelistPrefix = "Iwxxm.Net/Resources/Codelists/";

        private static readonly Assembly OwnAssembly = typeof(EmbeddedRuleCatalog).Assembly;
        private static readonly Assembly CoreAssembly = typeof(IwxxmParserOptions).Assembly;

        /// <summary>
        /// The 20 official WMO code lists published next to <c>iwxxm.sch</c> (<c>IWXXM/rule/*.rdf</c>, shipped
        /// byte-identical in <c>Iwxxm.Net</c>). The official rules dereference 17 of them through <c>document()</c>.
        /// </summary>
        internal static readonly IReadOnlyList<string> CodelistFileNames = new[]
        {
            "codes.wmo.int-49-2-AerodromePresentOrForecastWeather.rdf",
            "codes.wmo.int-49-2-AerodromeRecentWeather.rdf",
            "codes.wmo.int-49-2-AirWxPhenomena.rdf",
            "codes.wmo.int-49-2-AviationColourCode.rdf",
            "codes.wmo.int-49-2-CloudAmountReportedAtAerodrome.rdf",
            "codes.wmo.int-49-2-MeteorologicalFeature.rdf",
            "codes.wmo.int-49-2-SigConvectiveCloudType.rdf",
            "codes.wmo.int-49-2-SigWxPhenomena.rdf",
            "codes.wmo.int-49-2-SpaceWxLocation.rdf",
            "codes.wmo.int-49-2-SpaceWxPhenomena.rdf",
            "codes.wmo.int-49-2-WeatherCausingVisibilityReduction.rdf",
            "codes.wmo.int-bufr4-codeflag-0-11-030.rdf",
            "codes.wmo.int-bufr4-codeflag-0-20-008.rdf",
            "codes.wmo.int-bufr4-codeflag-0-20-012.rdf",
            "codes.wmo.int-bufr4-codeflag-0-20-041.rdf",
            "codes.wmo.int-bufr4-codeflag-0-22-061.rdf",
            "codes.wmo.int-common-nil.rdf",
            "codes.wmo.int-iwxxm-AviationColourCode.rdf",
            "codes.wmo.int-iwxxm-MeteorologicalFeature.rdf",
            "codes.wmo.int-iwxxm-nil.rdf"
        };

        /// <summary>
        /// Opens the embedded resource backing <paramref name="absoluteUri"/>, or returns <c>null</c> when the URI is
        /// not part of the catalog.
        /// </summary>
        internal static Stream? TryOpen(Uri absoluteUri)
        {
            string? manifestName = MapToManifestName(absoluteUri, out Assembly? assembly);
            return manifestName == null || assembly == null ? null : assembly.GetManifestResourceStream(manifestName);
        }

        /// <summary>Opens a resource of the catalog or throws <see cref="FileNotFoundException"/>.</summary>
        internal static Stream OpenRequired(Uri absoluteUri) =>
            TryOpen(absoluteUri)
            ?? throw new FileNotFoundException($"The URI '{absoluteUri}' is not available in the embedded air-gapped rule catalog.");

        internal static string? MapToManifestName(Uri absoluteUri, out Assembly? assembly)
        {
            assembly = null;
            if (absoluteUri == null || !absoluteUri.IsAbsoluteUri || !string.IsNullOrEmpty(absoluteUri.Query) || !string.IsNullOrEmpty(absoluteUri.Fragment))
                return null;

            string uri = absoluteUri.AbsoluteUri;

            if (uri.StartsWith(SchXsltBaseUri.AbsoluteUri, StringComparison.Ordinal))
            {
                string relative = uri.Substring(SchXsltBaseUri.AbsoluteUri.Length);
                if (!IsSafeRelativePath(relative) || !relative.StartsWith("2.0/", StringComparison.Ordinal) || !relative.EndsWith(".xsl", StringComparison.Ordinal))
                    return null;

                assembly = OwnAssembly;
                return OwnResourcePrefix + "SchXslt/" + relative;
            }

            if (uri.StartsWith(RuleBaseUri.AbsoluteUri, StringComparison.Ordinal))
            {
                string relative = uri.Substring(RuleBaseUri.AbsoluteUri.Length);
                if (relative == "iwxxm.sch")
                {
                    assembly = OwnAssembly;
                    return OwnResourcePrefix + "Schematron/iwxxm.sch";
                }

                foreach (string codelist in CodelistFileNames)
                {
                    if (string.Equals(relative, codelist, StringComparison.Ordinal))
                    {
                        assembly = CoreAssembly;
                        return CoreCodelistPrefix + codelist;
                    }
                }
            }

            return null;
        }

        private static bool IsSafeRelativePath(string path)
        {
            if (path.Length == 0 || path.IndexOf('\\') >= 0 || path.IndexOf('%') >= 0)
                return false;

            foreach (string segment in path.Split('/'))
            {
                if (segment.Length == 0 || segment == "." || segment == "..")
                    return false;
            }

            return true;
        }
    }
}
