using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using Iwxxm.Net.Infrastructure.Resources;

namespace Iwxxm.Net.Infrastructure.Mapping.CodeLists
{
    /// <summary>
    /// Reads the codes of an official WMO RDF/SKOS code list embedded in the core assembly
    /// (<c>Resources/Codelists/*.rdf</c>, byte-identical copies of <c>IWXXM/rule/*.rdf</c>), the single source of truth
    /// for the code-list translation tables.
    /// </summary>
    /// <remarks>
    /// A code is valid for IWXXM 2025-2 when its <c>skos:Concept</c> declares
    /// <c>owl:versionInfo rdf:resource="http://icao.int/iwxxm/2025-2"</c>, the same filter used by the official Schematron
    /// (<c>@xlink:href = ...skos:Concept/owl:versionInfo[@rdf:resource=$iwxxmVersion]/../@rdf:about</c>). The code is the
    /// part of <c>rdf:about</c> that follows the register URI, which is what a document reference is compared with.
    /// </remarks>
    internal static class OfficialCodeListReader
    {
        internal const string Iwxxm20252Version = "http://icao.int/iwxxm/2025-2";

        private static readonly XNamespace Rdf = "http://www.w3.org/1999/02/22-rdf-syntax-ns#";
        private static readonly XNamespace Skos = "http://www.w3.org/2004/02/skos/core#";
        private static readonly XNamespace Owl = "http://www.w3.org/2002/07/owl#";
        private static readonly XNamespace Reg = "http://purl.org/linked-data/registry#";

        /// <summary>Returns the URI of the register published by the embedded RDF file (<c>reg:Register rdf:about</c>).</summary>
        internal static string? ReadRegisterUri(string resourcePath) =>
            Load(resourcePath).Descendants(Reg + "Register").Select(r => r.Attribute(Rdf + "about")?.Value).FirstOrDefault();

        /// <summary>
        /// Returns the codes of <paramref name="registerUri"/> valid for IWXXM 2025-2, read from the embedded RDF file
        /// <paramref name="resourcePath"/> (relative to <c>Resources/</c>, e.g. <c>Codelists/codes.wmo.int-49-2-SigWxPhenomena.rdf</c>).
        /// </summary>
        internal static HashSet<string> ReadCodes(string resourcePath, string registerUri)
        {
            XDocument document = Load(resourcePath);
            string prefix = registerUri + "/";
            var codes = new HashSet<string>(StringComparer.Ordinal);
            foreach (XElement concept in document.Descendants(Skos + "Concept"))
            {
                string? about = concept.Attribute(Rdf + "about")?.Value;
                if (about == null || !about.StartsWith(prefix, StringComparison.Ordinal) || about.Length == prefix.Length)
                    continue;

                foreach (XElement version in concept.Elements(Owl + "versionInfo"))
                {
                    if (string.Equals(version.Attribute(Rdf + "resource")?.Value, Iwxxm20252Version, StringComparison.Ordinal))
                    {
                        codes.Add(about.Substring(prefix.Length));
                        break;
                    }
                }
            }

            return codes;
        }

        private static XDocument Load(string resourcePath)
        {
            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null
            };

            using (Stream stream = EmbeddedResourceLoader.GetRequiredResourceStream(resourcePath))
            using (XmlReader reader = XmlReader.Create(stream, settings))
            {
                return XDocument.Load(reader);
            }
        }
    }
}
