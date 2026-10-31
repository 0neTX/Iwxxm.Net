using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Iwxxm.Net.Reports;
using Xunit;

namespace Iwxxm.Net.Tests.DataFidelity
{
    /// <summary>Low-severity findings AUD-L2 (shared ring vertices), AUD-L3 (unmapped content), AUD-L8 (rules) and AUD-L9 (GeoJSON).</summary>
    public sealed class LowFindingsTests
    {
        private const string LinearRing =
            "<gml:LinearRing>\n                                                            <gml:posList>\n                                                                54.0 -12.0 50.0 -12.0 50.0 -8.0 54.0 -8.0 54.0 -12.0\n                                                            </gml:posList>\n                                                        </gml:LinearRing>";

        [Fact]
        public void RingOfSeveralCurveMembers_KeepsEachSharedVertexOnce()
        {
            string ring =
                "<gml:Ring>" +
                "<gml:curveMember><gml:Curve gml:id=\"c1\"><gml:segments><gml:LineStringSegment><gml:posList>54.0 -12.0 50.0 -12.0 50.0 -8.0</gml:posList></gml:LineStringSegment></gml:segments></gml:Curve></gml:curveMember>" +
                "<gml:curveMember><gml:Curve gml:id=\"c2\"><gml:segments><gml:LineStringSegment><gml:posList>50.0 -8.0 54.0 -8.0 54.0 -12.0</gml:posList></gml:LineStringSegment></gml:segments></gml:Curve></gml:curveMember>" +
                "</gml:Ring>";
            string xml = SampleDocuments.Text("sigmet-A6-1a-TS.xml").Replace(LinearRing, ring);
            Assert.NotEqual(SampleDocuments.Text("sigmet-A6-1a-TS.xml"), xml);

            Result<IwxxmDocument> result = IwxxmParser.Parse(xml);

            Assert.True(result.IsSuccess, string.Join(" | ", result.Errors));
            GeoPolygon area = Assert.IsType<GeoPolygon>(Assert.IsType<Sigmet>(result.Value).EvolvingConditions[0].Area);
            Assert.Equal(5, area.ExteriorRing.Count);
            Assert.Equal(area.ExteriorRing.Count, area.ExteriorRing.Distinct().Count() + 1);
        }

        [Fact]
        public void VolcanicAshSigmet_MovedToFir_IsMapped()
        {
            string xml = SampleDocuments.Text("sigmet-VA-EGGX.xml");
            int insertAt = xml.IndexOf("</iwxxm:VolcanicAshSIGMET>", StringComparison.Ordinal);
            xml = xml.Insert(insertAt,
                "<iwxxm:volcanicAshMovedToFIR><aixm:Airspace gml:id=\"moved-fir\"><aixm:timeSlice><aixm:AirspaceTimeSlice gml:id=\"moved-fir-ts\">" +
                "<gml:validTime/><aixm:interpretation>SNAPSHOT</aixm:interpretation><aixm:type>FIR</aixm:type>" +
                "<aixm:designator>BIRD</aixm:designator><aixm:name>REYKJAVIK FIR</aixm:name>" +
                "</aixm:AirspaceTimeSlice></aixm:timeSlice></aixm:Airspace></iwxxm:volcanicAshMovedToFIR>");

            Result<IwxxmDocument> result = SampleDocuments.MapDirectly(xml);

            Assert.True(result.IsSuccess, string.Join(" | ", result.Errors));
            Sigmet sigmet = Assert.IsType<Sigmet>(result.Value);
            Assert.Equal("BIRD", sigmet.VolcanicAshMovedToFirDesignator);
            Assert.Equal("REYKJAVIK FIR", sigmet.VolcanicAshMovedToFirName);
        }

        [Fact]
        public void TropicalCycloneSigmet_SupplementaryAnalyses_AreMapped()
        {
            string xml = SampleDocuments.Text("sigmet-A6-2-TC.xml");
            int start = xml.IndexOf("<iwxxm:analysisCollection>", StringComparison.Ordinal);
            int end = xml.IndexOf("</iwxxm:analysisCollection>", StringComparison.Ordinal) + "</iwxxm:analysisCollection>".Length;
            string analysis = xml.Substring(start, end - start);
            string supplementary = Regex.Replace(analysis, "gml:id=\"([^\"]+)\"", "gml:id=\"sup-$1\"")
                .Replace("<iwxxm:analysisCollection>", "<iwxxm:supplementaryAnalysisCollection>")
                .Replace("</iwxxm:analysisCollection>", "</iwxxm:supplementaryAnalysisCollection>");
            int tc = xml.IndexOf("<iwxxm:tropicalCyclone>", StringComparison.Ordinal);
            xml = xml.Insert(tc, supplementary);

            Result<IwxxmDocument> result = SampleDocuments.MapDirectly(xml);

            Assert.True(result.IsSuccess, string.Join(" | ", result.Errors));
            Sigmet sigmet = Assert.IsType<Sigmet>(result.Value);
            Assert.Equal(sigmet.EvolvingConditions.Count, sigmet.SupplementaryEvolvingConditions.Count);
            Assert.All(sigmet.SupplementaryEvolvingConditions, v => Assert.StartsWith("sup-", v.Id, StringComparison.Ordinal));
        }

        [Fact]
        public void TropicalCycloneAdvisory_MovementAndIntensityChange_AreMapped()
        {
            Tca tca = Assert.IsType<Tca>(IwxxmParser.ParseFile(SampleDocuments.PathOf("tc-advisory-A2-2.xml")).Value);
            TropicalCycloneTrackPoint observed = tca.Observation!;

            Assert.Equal(TropicalCycloneMovement.Moving, observed.Movement);
            Assert.Equal(315, observed.MovementDirectionDegrees);
            Assert.Equal(new Speed(20, SpeedUnit.KilometersPerHour), observed.MovementSpeed);
            Assert.Equal(TropicalCycloneIntensityChange.Intensify, observed.IntensityChange);
            Assert.All(tca.Forecasts, p => Assert.Null(p.Movement));
        }

        [Fact]
        public void WafsVolcano_ActivityStatusAndSourceElevation_AreMapped()
        {
            string xml = SampleDocuments.Text("WAFS-Example.xml").Replace(
                "<iwxxm:name>ETNA</iwxxm:name>",
                "<iwxxm:name>ETNA</iwxxm:name>" +
                "<iwxxm:sourceElevation><iwxxm:ElevatedLevel gml:id=\"etna-src\"><iwxxm:elevation uom=\"M\">3329</iwxxm:elevation>" +
                "<iwxxm:verticalReference>MSL</iwxxm:verticalReference></iwxxm:ElevatedLevel></iwxxm:sourceElevation>" +
                "<iwxxm:activityStatus>ERUPTION_ONGOING</iwxxm:activityStatus>");

            Result<Sigwx> result = IwxxmSigwxParser.Parse(xml);

            Assert.True(result.IsSuccess, string.Join(" | ", result.Errors));
            Volcano etna = Assert.Single(result.Value.Volcanoes);
            Assert.Equal(VolcanoActivityStatus.EruptionOngoing, etna.ActivityStatus);
            Assert.Equal(3329, etna.SourceElevation!.Value.Value);
        }

        [Theory]
        [InlineData("41.1", true)]
        [InlineData("41.15", true)]
        [InlineData("41.16", false)]
        [InlineData("51.5", false)]
        public void JetStreamThreshold_Is80KnotsIn_MetresPerSecond(string speed, bool fires)
        {
            string xml = SampleDocuments.Text("WAFS-Example.xml").Replace(
                "<iwxxm:windSpeed uom=\"m/s\">61.8</iwxxm:windSpeed>",
                "<iwxxm:windSpeed uom=\"m/s\">" + speed + "</iwxxm:windSpeed>");

            Result<Sigwx> result = IwxxmSigwxParser.Parse(xml);

            Assert.Equal(fires, result.IsFailure);
            if (fires)
            {
                Assert.All(result.Errors, e => Assert.Equal("IWXXM-SIGWX-002", e.Code));
                ValidationError error = result.Errors[0];
                Assert.Contains("41.15552 m/s", error.Message, StringComparison.Ordinal);
                Assert.Matches(@"^/iwxxm:WAFSSignificantWeatherForecast\[1\](/[\w:]+\[\d+\])+$", error.Location);
            }
        }

        [Fact]
        public void BuiltInRuleLocations_AreXPaths()
        {
            string xml = SampleDocuments.Text("metar-A3-1.xml").Replace("http://codes.wmo.int/306/4678/DZ", "http://codes.wmo.int/306/4678/ZZ");

            ValidationError error = Assert.Single(IwxxmParser.Parse(xml).Errors);

            Assert.Equal("/iwxxm:METAR[1]/iwxxm:observation[1]/iwxxm:MeteorologicalAerodromeObservation[1]/iwxxm:presentWeather[1]", error.Location);
        }
    }
}
