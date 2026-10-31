using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;
using Iwxxm.Net.Extensibility;
using Iwxxm.Net.Validation.Saxon.Internal;
using Xunit;

namespace Iwxxm.Net.Validation.Saxon.Tests
{
    /// <summary>
    /// AUD-L8: the <c>ValidationError.Location</c> of an official Schematron finding is an absolute, namespace-independent
    /// XPath 1.0 that selects exactly the offending node of the instance (the raw SchXslt locations use the XPath 3.0
    /// <c>Q{namespace}local</c> syntax that <c>System.Xml.XPath</c> cannot parse).
    /// </summary>
    public sealed class SvrlLocationTests
    {
        private const string Iwxxm = "http://icao.int/iwxxm/2025-2";
        private const string Collect = "http://def.wmo.int/collect/2014";

        [Fact]
        public void BulletinEntry_IsLocatedWithItsPositionalPredicate()
        {
            // Second meteorologicalInformation: the NIL METAR of the bulletin sample is followed by the broken A3-1 METAR.
            XDocument bulletin = XDocument.Load("Samples/wmo-examples/metar-NIL-collect.xml");
            XElement metar = XDocument.Load("Samples/wmo-examples/metar-A3-1.xml").Root!;
            foreach (XElement qnh in metar.Descendants(XName.Get("qnh", Iwxxm)))
                qnh.SetAttributeValue("uom", "mbar");
            bulletin.Root!.Elements(XName.Get("meteorologicalInformation", Collect)).Last()
                .AddAfterSelf(new XElement(XName.Get("meteorologicalInformation", Collect), metar));
            string xml = bulletin.ToString(SaveOptions.DisableFormatting);

            IReadOnlyList<ValidationError> findings = new OfficialSchematronValidator()
                .Validate(new IwxxmDocumentContext(Encoding.UTF8.GetBytes(xml)));

            ValidationError error = Assert.Single(findings, e => e.Code == "METAR_SPECI.MeteorologicalAerodromeObservation-5");
            Assert.StartsWith("/*[namespace-uri()='" + Collect + "' and local-name()='MeteorologicalBulletin'][1]/*[namespace-uri()='" + Collect + "' and local-name()='meteorologicalInformation'][2]/", error.Location, StringComparison.Ordinal);
            XElement offending = LocationAssert.SelectsExactlyOneNode(xml, error, "MeteorologicalAerodromeObservation");
            XElement entry = offending.Ancestors(XName.Get("meteorologicalInformation", Collect)).Single();
            Assert.Single(entry.ElementsBeforeSelf(entry.Name));
        }

        [Fact]
        public void Location_DoesNotDependOnThePrefixesOfTheInstance()
        {
            // Same document, prefixes rewritten: the location of the finding is unchanged.
            string original = File.ReadAllText("Samples/wmo-examples/metar-A3-1.xml").Replace("<iwxxm:qnh uom=\"hPa\">", "<iwxxm:qnh uom=\"mbar\">");
            string renamed = original.Replace("iwxxm:", "w:").Replace("xmlns:iwxxm=", "xmlns:w=");
            var validator = new OfficialSchematronValidator();

            ValidationError first = Assert.Single(validator.Validate(new IwxxmDocumentContext(Encoding.UTF8.GetBytes(original))));
            ValidationError second = Assert.Single(validator.Validate(new IwxxmDocumentContext(Encoding.UTF8.GetBytes(renamed))));

            Assert.Equal(first.Location, second.Location);
            LocationAssert.SelectsExactlyOneNode(renamed, second, "MeteorologicalAerodromeObservation");
        }

        [Fact]
        public void Location_IsEvaluableByAnXPathEngineWithoutNamespaceBindings()
        {
            const string xml = "<a xmlns='urn:one'><b xmlns='urn:two'/><b xmlns='urn:one'/><b xmlns='urn:one' x:y='1' xmlns:x='urn:three'/></a>";
            XDocument document = XDocument.Parse(xml);

            string location = SvrlLocation.ToXPath("/Q{urn:one}a[1]/Q{urn:one}b[2]/@Q{urn:three}y")!;

            Assert.DoesNotContain("Q{", location, StringComparison.Ordinal);
            XAttribute attribute = Assert.IsAssignableFrom<XAttribute>(Assert.Single(((IEnumerable<object>)document.XPathEvaluate(location)).ToList()));
            Assert.Equal("1", attribute.Value);
            Assert.Equal("urn:one", attribute.Parent!.Name.NamespaceName);
            Assert.Equal("/*[namespace-uri()='urn:one' and local-name()='a'][1]/*[namespace-uri()='urn:one' and local-name()='b'][2]/@*[namespace-uri()='urn:three' and local-name()='y']", location);
        }

        [Theory]
        [InlineData(null, null)]
        [InlineData("", "")]
        [InlineData("/x", "/x")]
        [InlineData("/", "/")]
        [InlineData("/a[1]/text()[2]", "/a[1]/text()[2]")]
        [InlineData("/Q{}root[1]/comment()[1]", "/*[namespace-uri()='' and local-name()='root'][1]/comment()[1]")]
        [InlineData("/Q{urn:x}r[1]/processing-instruction(\"pi\")[1]", "/*[namespace-uri()='urn:x' and local-name()='r'][1]/processing-instruction(\"pi\")[1]")]
        public void ToXPath_RewritesOnlyTheBracedUriSteps(string? raw, string? expected)
        {
            Assert.Equal(expected, SvrlLocation.ToXPath(raw));
        }

        [Fact]
        public void ToXPath_QuotesNamespaceUrisSafely()
        {
            // A namespace URI is attacker-controlled: it must never be able to break out of the XPath string literal.
            Assert.Equal("/*[namespace-uri()=\"urn:it's\" and local-name()='r'][1]", SvrlLocation.ToXPath("/Q{urn:it's}r[1]"));
            Assert.Equal(
                "/*[namespace-uri()=concat('urn:a', \"'\", 'b\"c') and local-name()='r'][1]",
                SvrlLocation.ToXPath("/Q{urn:a'b\"c}r[1]"));

            XDocument document = XDocument.Parse("<r xmlns=\"urn:a'b&quot;c\"/>");
            string location = SvrlLocation.ToXPath("/Q{urn:a'b\"c}r[1]")!;
            Assert.Equal(document.Root, Assert.Single(((IEnumerable<object>)document.XPathEvaluate(location)).ToList()));
        }

        [Fact]
        public void SvrlReader_RewritesTheSchXsltLocation()
        {
            const string svrl =
                "<svrl:schematron-output xmlns:svrl=\"http://purl.oclc.org/dsdl/svrl\">" +
                "<svrl:failed-assert id=\"R-1\" test=\"a\" location=\"/Q{urn:x}r[1]/Q{urn:x}c[3]\"><svrl:text>t</svrl:text></svrl:failed-assert>" +
                "</svrl:schematron-output>";

            List<ValidationError> findings;
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(svrl)))
            {
                findings = SvrlReportReader.Read(stream, 10, 1_000_000);
            }

            Assert.Equal(
                "/*[namespace-uri()='urn:x' and local-name()='r'][1]/*[namespace-uri()='urn:x' and local-name()='c'][3]",
                Assert.Single(findings).Location);
        }
    }

    /// <summary>Evaluates the reported location against the instance and checks the selected node.</summary>
    internal static class LocationAssert
    {
        public static XElement SelectsExactlyOneNode(string instanceXml, ValidationError error, string expectedLocalName)
        {
            Assert.False(string.IsNullOrEmpty(error.Location), "The finding has no location.");
            string location = error.Location!;
            Assert.StartsWith("/", location, StringComparison.Ordinal);
            Assert.DoesNotContain("Q{", location, StringComparison.Ordinal);

            // Well formed: System.Xml.XPath (XPath 1.0) compiles it.
            XPathExpression expression = XPathExpression.Compile(location);
            Assert.Equal(XPathResultType.NodeSet, expression.ReturnType);

            // Evaluated against the instance with an empty namespace manager: exactly one element is selected.
            XDocument document = XDocument.Parse(instanceXml, LoadOptions.SetLineInfo);
            var nodes = ((IEnumerable<object>)document.XPathEvaluate(location)).ToList();
            XElement element = Assert.IsType<XElement>(Assert.Single(nodes));
            Assert.Equal(expectedLocalName, element.Name.LocalName);

            // The same result through the DOM API, with an explicit (empty) namespace manager.
            var dom = new XmlDocument();
            dom.LoadXml(instanceXml);
            XmlNodeList selected = dom.SelectNodes(location, new XmlNamespaceManager(dom.NameTable))!;
            Assert.Single(selected);
            Assert.Equal(expectedLocalName, selected[0]!.LocalName);
            return element;
        }
    }
}
