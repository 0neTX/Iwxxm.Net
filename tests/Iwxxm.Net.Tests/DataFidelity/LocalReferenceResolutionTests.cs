using System;
using System.Linq;
using System.Xml.Linq;
using Iwxxm.Net.Infrastructure.Mapping.Xml;
using Iwxxm.Net.Reports;
using Xunit;
using static Iwxxm.Net.Tests.DataFidelity.SampleDocuments;

namespace Iwxxm.Net.Tests.DataFidelity
{
    /// <summary>
    /// AUD-H2: local <c>xlink:href="#id"</c> references are resolved through a per-document <c>gml:id</c> index;
    /// remote or unknown references stay explicit <c>null</c> and reference cycles are not followed.
    /// </summary>
    public sealed class LocalReferenceResolutionTests
    {
        [Fact]
        public void Taf_BaseForecastValidityGivenByReference_IsTheTafValidPeriod()
        {
            var result = IwxxmParser.ParseFileAs<Taf>(PathOf("taf-A5-1.xml"));

            Assert.True(result.IsSuccess, string.Join(" | ", result.Errors));
            Taf taf = result.Value;
            Assert.NotNull(taf.ValidFromUtc);
            Assert.Equal(taf.ValidFromUtc, taf.BaseForecast!.ValidFromUtc);
            Assert.Equal(taf.ValidUntilUtc, taf.BaseForecast.ValidUntilUtc);
        }

        [Fact]
        public void Taf_BaseForecastReferencingAnUnknownIdentifier_HasNoValidity()
        {
            XDocument doc = Load("taf-A5-1.xml");
            doc.Descendants(IwxxmNs + "baseForecast").Single()
                .Descendants(IwxxmNs + "phenomenonTime").Single()
                .SetAttributeValue(XlinkNs + "href", "#uuid.does-not-exist");

            var result = MapDirectly(doc);

            Assert.True(result.IsSuccess, string.Join(" | ", result.Errors));
            var taf = Assert.IsType<Taf>(result.Value);
            Assert.Null(taf.BaseForecast!.ValidFromUtc);
            Assert.Null(taf.BaseForecast.ValidUntilUtc);
        }

        [Fact]
        public void SpaceWeather_ForecastByReference_YieldsOneAnalysisPerSpaceWeatherAnalysis()
        {
            var result = IwxxmParser.ParseFileAs<SpaceWx>(PathOf("spacewx-A7-4.xml"));

            Assert.True(result.IsSuccess, string.Join(" | ", result.Errors));
            var analyses = result.Value.Analyses;
            Assert.Equal(5, analyses.Count);

            SpaceWeatherAnalysis observation = analyses[0];
            Assert.Equal(TimeIndicator.Observation, observation.TimeIndicator);
            Assert.Equal(new DateTimeOffset(2020, 11, 8, 1, 0, 0, TimeSpan.Zero), observation.PhenomenonTimeUtc);
            Assert.Equal(2, observation.Regions.Count);

            SpaceWeatherAnalysis plus6 = analyses[1];
            Assert.Equal(TimeIndicator.Forecast, plus6.TimeIndicator);
            Assert.Equal(new DateTimeOffset(2020, 11, 8, 7, 0, 0, TimeSpan.Zero), plus6.PhenomenonTimeUtc);
            Assert.Equal(new[] { "uuid.c264db5c-3f0b-4444-ab5e-71f48a514644", "uuid.00f9026a-845a-4472-acee-9ec5d7ba6839" }, plus6.Regions.Select(r => r.Id).ToArray());
            Assert.Equal(observation.Regions, plus6.Regions);
            Assert.All(plus6.Regions, r => Assert.Equal("MOD", r.Intensity));
            Assert.Null(plus6.NilReason);

            Assert.Equal(
                new[] { new DateTimeOffset(2020, 11, 8, 13, 0, 0, TimeSpan.Zero), new DateTimeOffset(2020, 11, 8, 19, 0, 0, TimeSpan.Zero), new DateTimeOffset(2020, 11, 9, 1, 0, 0, TimeSpan.Zero) },
                analyses.Skip(2).Select(a => a.PhenomenonTimeUtc!.Value).ToArray());
            Assert.All(analyses.Skip(2), a =>
            {
                Assert.Empty(a.Regions);
                Assert.True(a.IsNothingOfOperationalSignificance);
                Assert.Equal(NilReason.NothingOfOperationalSignificance, a.NilReason!.Code);
                Assert.Equal("nothingOfOperationalSignificance", a.NilReason.Notation);
            });
        }

        [Fact]
        public void Qvaci_FeatureTimesGivenByReference_AreResolved()
        {
            var result = IwxxmParser.ParseFileAs<Qvaci>(PathOf("qvaci-Example.xml"));

            Assert.True(result.IsSuccess, string.Join(" | ", result.Errors));
            Assert.NotEmpty(result.Value.AshConcentrationClouds);
            Assert.All(result.Value.AshConcentrationClouds, c =>
                Assert.Equal(new DateTimeOffset(2021, 12, 17, 12, 0, 0, TimeSpan.Zero), c.PhenomenonTimeUtc));
        }

        [Fact]
        public void Resolver_RemoteAndUnknownReferences_AreNotResolved()
        {
            XElement root = XElement.Parse(
                $"<r xmlns:gml='{GmlNs}' xmlns:xlink='{XlinkNs}'>" +
                "<gml:TimeInstant gml:id='t1'/>" +
                "<p1 xlink:href='#t1'/><p2 xlink:href='http://example.org/doc.xml#t1'/><p3 xlink:href='#t2'/><p4/></r>");
            new XDocument(root);

            Assert.Same(root.Element(GmlNs + "TimeInstant"), GmlReferenceResolver.Resolve(root.Element("p1")!));
            Assert.Null(GmlReferenceResolver.Resolve(root.Element("p2")!));
            Assert.Null(GmlReferenceResolver.Resolve(root.Element("p3")!));
            Assert.Null(GmlReferenceResolver.Resolve(root.Element("p4")!));
        }

        [Fact]
        public void Resolver_ReferenceCycles_AreNotFollowed()
        {
            XElement root = XElement.Parse(
                $"<r xmlns:gml='{GmlNs}' xmlns:xlink='{XlinkNs}'>" +
                "<a gml:id='a' xlink:href='#b'/><b gml:id='b' xlink:href='#a'/>" +
                "<c gml:id='c'><p xlink:href='#c'/></c>" +
                "<s gml:id='s' xlink:href='#s'/></r>");
            new XDocument(root);

            Assert.Null(GmlReferenceResolver.Resolve(root.Element("a")!));
            Assert.Null(GmlReferenceResolver.Resolve(root.Element("c")!.Element("p")!));
            Assert.Null(GmlReferenceResolver.Resolve(root.Element("s")!));
        }

        [Fact]
        public void Resolver_ReferenceChains_AreFollowedToTheReferencedObject()
        {
            XElement root = XElement.Parse(
                $"<r xmlns:gml='{GmlNs}' xmlns:xlink='{XlinkNs}'>" +
                "<gml:TimePeriod gml:id='period'><gml:beginPosition>2020-01-01T00:00:00Z</gml:beginPosition></gml:TimePeriod>" +
                "<alias gml:id='alias' xlink:href='#period'/><p xlink:href='#alias'/></r>");
            new XDocument(root);

            Assert.Same(root.Element(GmlNs + "TimePeriod"), GmlReferenceResolver.Resolve(root.Element("p")!));
        }
    }
}
