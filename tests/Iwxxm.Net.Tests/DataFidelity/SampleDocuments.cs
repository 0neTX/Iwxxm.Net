using System.IO;
using System.Text;
using System.Xml.Linq;
using Iwxxm.Net.Infrastructure.Mapping;
using Iwxxm.Net.Reports;

namespace Iwxxm.Net.Tests.DataFidelity
{
    /// <summary>
    /// Loads official WMO examples and builds minimal synthetic variants of them for the data-fidelity tests.
    /// </summary>
    internal static class SampleDocuments
    {
        internal static readonly XNamespace IwxxmNs = "http://icao.int/iwxxm/2025-2";
        internal static readonly XNamespace GmlNs = "http://www.opengis.net/gml/3.2";
        internal static readonly XNamespace XlinkNs = "http://www.w3.org/1999/xlink";
        internal static readonly XNamespace AixmNs = "http://www.aixm.aero/schema/5.1.1";
        internal static readonly XNamespace CollectNs = "http://def.wmo.int/collect/2014";

        internal static string PathOf(string fileName) => Path.Combine("Samples", "wmo-examples", fileName);

        internal static XDocument Load(string fileName) => XDocument.Load(PathOf(fileName), LoadOptions.PreserveWhitespace);

        internal static string Text(string fileName) => File.ReadAllText(PathOf(fileName));

        /// <summary>Maps a document with the mapper only (no XSD / Schematron validation).</summary>
        internal static Result<IwxxmDocument> MapDirectly(string xml)
        {
            return new IwxxmSuiteMapper().Map(new Extensibility.IwxxmDocumentContext(Encoding.UTF8.GetBytes(xml)));
        }

        internal static Result<IwxxmDocument> MapDirectly(XDocument document) => MapDirectly(document.ToString(SaveOptions.DisableFormatting));
    }
}
