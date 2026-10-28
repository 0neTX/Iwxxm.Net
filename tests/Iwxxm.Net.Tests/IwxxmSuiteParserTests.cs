using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Iwxxm.Net.Domain.Aggregates;
using Iwxxm.Net.Domain.Common;
using Iwxxm.Net.Domain.ValueObjects;
using Iwxxm.Net.Presentation;
using Xunit;

namespace Iwxxm.Net.Tests
{
    /// <summary>
    /// End-to-end integration tests for <see cref="IwxxmParser"/> covering the complete
    /// ICAO/WMO IWXXM 2025-2 report suite, SWIM/AMHS <c>collect:MeteorologicalBulletin</c> containers,
    /// and offline WMO RDF/SKOS code-list validation.
    /// </summary>
    public sealed class IwxxmSuiteParserTests
    {
        public static IEnumerable<object[]> GetAllOfficialWmoExamples()
        {
            const string examplesDir = "Samples/wmo-examples";
            foreach (string filePath in Directory.GetFiles(examplesDir, "*.xml").OrderBy(f => f))
            {
                yield return new object[] { filePath };
            }
        }

        [Theory]
        [MemberData(nameof(GetAllOfficialWmoExamples))]
        public void ParseFile_All29OfficialWmoExamples_PassDualValidationAndMapToDomainAggregate(string xmlFilePath)
        {
            // Act
            var result = IwxxmParser.ParseFile(xmlFilePath);

            // Assert
            Assert.True(result.IsSuccess, $"Failed on '{xmlFilePath}': {string.Join(" | ", result.Errors)}");
            Assert.NotNull(result.Value);
            Assert.False(string.IsNullOrWhiteSpace(result.Value.Id));
        }

        [Fact]
        public void ParseFileAs_MetarA31_MapsAerodromeWeatherReport()
        {
            // Act
            var result = IwxxmParser.ParseFileAs<AerodromeWeatherReport>("Samples/wmo-examples/metar-A3-1.xml");

            // Assert
            Assert.True(result.IsSuccess, string.Join(" | ", result.Errors));
            var metar = result.Value;
            Assert.Equal(IwxxmReportType.Metar, metar.ReportType);
            Assert.Equal("YUDO", metar.AerodromeIcao);
            Assert.False(metar.AutomatedStation);
            Assert.NotNull(metar.SurfaceWind);
            Assert.Equal(240.0, metar.SurfaceWind.MeanWindDirectionDegrees);
            Assert.Equal(4.0, metar.SurfaceWind.MeanWindSpeed);
            Assert.Equal(600.0, metar.PrevailingVisibilityMeters);
            Assert.Contains("http://codes.wmo.int/49-2/AerodromePresentWeather/RA", metar.PresentWeatherUris);
            Assert.Contains("http://codes.wmo.int/49-2/AerodromePresentWeather/FG", metar.PresentWeatherUris);
            Assert.Equal(3, metar.RunwayVisualRanges.Count);
            Assert.Equal(16.0, metar.AirTemperatureCelsius);
            Assert.Equal(16.0, metar.DewpointTemperatureCelsius);
            Assert.Equal(1018.0, metar.QnhHectopascals);
            Assert.NotEmpty(metar.TrendForecasts);
        }

        [Fact]
        public void ParseFileAs_SpeciA32_MapsSpeciAerodromeWeatherReport()
        {
            // Act
            var result = IwxxmParser.ParseFileAs<AerodromeWeatherReport>("Samples/wmo-examples/speci-A3-2.xml");

            // Assert
            Assert.True(result.IsSuccess, string.Join(" | ", result.Errors));
            var speci = result.Value;
            Assert.Equal(IwxxmReportType.Speci, speci.ReportType);
            Assert.Equal("YUDO", speci.AerodromeIcao);
            Assert.NotNull(speci.SurfaceWind);
        }

        [Fact]
        public void ParseFileAs_TafA51_MapsTafReportAndForecastGroups()
        {
            // Act
            var result = IwxxmParser.ParseFileAs<TafReport>("Samples/wmo-examples/taf-A5-1.xml");

            // Assert
            Assert.True(result.IsSuccess, string.Join(" | ", result.Errors));
            var taf = result.Value;
            Assert.Equal(IwxxmReportType.Taf, taf.ReportType);
            Assert.Equal("YUDO", taf.AerodromeIcao);
            Assert.NotNull(taf.ValidPeriodStart);
            Assert.NotNull(taf.ValidPeriodEnd);
            Assert.NotNull(taf.BaseForecast);
            Assert.NotEmpty(taf.ChangeForecasts);
        }

        [Fact]
        public void ParseFileAs_SigmetExamples_MapStandardVolcanicAndTropicalCycloneVariants()
        {
            // 1. Standard SIGMET (Thunderstorm)
            var tsSigmet = IwxxmParser.ParseFileAs<SigmetReport>("Samples/wmo-examples/sigmet-A6-1a-TS.xml");
            Assert.True(tsSigmet.IsSuccess, string.Join(" | ", tsSigmet.Errors));
            Assert.Equal(IwxxmReportType.Sigmet, tsSigmet.Value.ReportType);
            Assert.Equal("YUDD", tsSigmet.Value.IssuingAtsUnit);
            Assert.Equal("YUCC", tsSigmet.Value.FirIcaoOrName);
            Assert.Equal("http://codes.wmo.int/49-2/SigWxPhenomena/OBSC_TS", tsSigmet.Value.PhenomenonUri);
            Assert.NotEmpty(tsSigmet.Value.AnalysisVolumes);

            // 2. Volcanic Ash SIGMET
            var vaSigmet = IwxxmParser.ParseFileAs<SigmetReport>("Samples/wmo-examples/sigmet-VA-EGGX.xml");
            Assert.True(vaSigmet.IsSuccess, string.Join(" | ", vaSigmet.Errors));
            Assert.Equal(IwxxmReportType.VolcanicAshSigmet, vaSigmet.Value.ReportType);
            Assert.Equal("EYJAFJALLAJOKULL", vaSigmet.Value.VolcanoName);
            Assert.Equal(new GeoCoordinate(63.63, -19.62), vaSigmet.Value.VolcanoPosition);

            // 3. Tropical Cyclone SIGMET
            var tcSigmet = IwxxmParser.ParseFileAs<SigmetReport>("Samples/wmo-examples/sigmet-TC-VTBS.xml");
            Assert.True(tcSigmet.IsSuccess, string.Join(" | ", tcSigmet.Errors));
            Assert.Equal(IwxxmReportType.TropicalCycloneSigmet, tcSigmet.Value.ReportType);
            Assert.Equal("NANGKA", tcSigmet.Value.TropicalCycloneName);
        }

        [Fact]
        public void ParseFileAs_AirmetExample_MapsAirmetReport()
        {
            // Act
            var result = IwxxmParser.ParseFileAs<AirmetReport>("Samples/wmo-examples/airmet-A6-1a-TS.xml");

            // Assert
            Assert.True(result.IsSuccess, string.Join(" | ", result.Errors));
            var airmet = result.Value;
            Assert.Equal(IwxxmReportType.Airmet, airmet.ReportType);
            Assert.Equal("YUDD", airmet.IssuingAtsUnit);
            Assert.Equal("http://codes.wmo.int/49-2/AirWxPhenomena/ISOL_TS", airmet.PhenomenonUri);
            Assert.Single(airmet.AnalysisVolumes);
        }

        [Fact]
        public void ParseFileAs_VonaExample_MapsVolcanoObservatoryNotice()
        {
            // Act
            var result = IwxxmParser.ParseFileAs<VonaReport>("Samples/wmo-examples/vona-Example.xml");

            // Assert
            Assert.True(result.IsSuccess, string.Join(" | ", result.Errors));
            var vona = result.Value;
            Assert.Equal(IwxxmReportType.VolcanoObservatoryNoticeForAviation, vona.ReportType);
            Assert.Equal("Cerro Hudson", vona.VolcanoName);
            Assert.Equal("358057", vona.VolcanoIdentifier);
            Assert.Equal("http://codes.wmo.int/49-2/AviationColourCode/RED", vona.CurrentColourCodeUri);
            Assert.Equal("http://codes.wmo.int/49-2/AviationColourCode/ORANGE", vona.PreviousColourCodeUri);
            Assert.True(vona.IsVolcanicAshCloudPresent);
        }

        [Fact]
        public void ParseFileAs_QvaciExample_MapsQuantitativeAshConcentrationWithInteriorPolygonHole()
        {
            // Act
            var result = IwxxmParser.ParseFileAs<QvaciReport>("Samples/wmo-examples/qvaci-Example.xml");

            // Assert
            Assert.True(result.IsSuccess, string.Join(" | ", result.Errors));
            var qvaci = result.Value;
            Assert.Equal(IwxxmReportType.QuantitativeVolcanicAshConcentration, qvaci.ReportType);
            Assert.Equal("GRIMSVOTN", qvaci.VolcanoName);
            Assert.Equal("373010", qvaci.VolcanoIdentifier);
            Assert.NotEmpty(qvaci.ConcentrationClouds);
            Assert.Contains(qvaci.ConcentrationClouds, c => c.Area.InteriorRings.Count > 0);
        }

        [Fact]
        public void ParseFileAs_VaacTcacAndSpaceWxExamples_MapAdvisoryReports()
        {
            // 1. Volcanic Ash Advisory (VAAC)
            var vaac = IwxxmParser.ParseFileAs<VolcanicAshAdvisoryReport>("Samples/wmo-examples/vaac-A2-1.xml");
            Assert.True(vaac.IsSuccess, string.Join(" | ", vaac.Errors));
            Assert.Equal(IwxxmReportType.VolcanicAshAdvisory, vaac.Value.ReportType);
            Assert.Equal("RJTD", vaac.Value.IssuingCentreCode);
            Assert.Equal("KARYMSKY", vaac.Value.VolcanoName);

            // 2. Tropical Cyclone Advisory (TCAC)
            var tcac = IwxxmParser.ParseFileAs<TropicalCycloneAdvisoryReport>("Samples/wmo-examples/tcac-A2-2.xml");
            Assert.True(tcac.IsSuccess, string.Join(" | ", tcac.Errors));
            Assert.Equal(IwxxmReportType.TropicalCycloneAdvisory, tcac.Value.ReportType);
            Assert.Equal("ROSLYN", tcac.Value.TropicalCycloneName);
            Assert.NotEmpty(tcac.Value.ForecastPositions);

            // 3. Space Weather Advisory (SWXC)
            var spaceWx = IwxxmParser.ParseFileAs<SpaceWeatherAdvisoryReport>("Samples/wmo-examples/spacewx-A7-3.xml");
            Assert.True(spaceWx.IsSuccess, string.Join(" | ", spaceWx.Errors));
            Assert.Equal(IwxxmReportType.SpaceWeatherAdvisory, spaceWx.Value.ReportType);
            Assert.Equal("DONLON", spaceWx.Value.IssuingCentreName);
            Assert.NotEmpty(spaceWx.Value.PhenomenaUris);
            Assert.Equal(5, spaceWx.Value.Analyses.Count);
        }

        [Fact]
        public void ParseBulletinFile_CollectA31A32_UnwrapsMeteorologicalBulletinAndNestedReports()
        {
            // Act
            var result = IwxxmParser.ParseBulletinFile("Samples/wmo-examples/collect-A3-1-A3-2.xml");

            // Assert
            Assert.True(result.IsSuccess, string.Join(" | ", result.Errors));
            var bulletin = result.Value;
            Assert.Equal(IwxxmReportType.MeteorologicalBulletin, bulletin.ReportType);
            Assert.Equal("A_LAXX31YPYX160000_C_YPYX_202305160005--.xml", bulletin.BulletinIdentifier);
            Assert.Equal(2, bulletin.Reports.Count);
            Assert.Equal(IwxxmReportType.Metar, bulletin.Reports[0].ReportType);
            Assert.Equal(IwxxmReportType.Speci, bulletin.Reports[1].ReportType);
        }

        [Fact]
        public void Parse_UnregisteredWmo492CodeListUri_FailsInOfflineRdfSchematronRule()
        {
            // Arrange: Replace valid RA present weather URI with an unregistered code URI in WMO 49-2 RDF registry
            string validMetarXml = File.ReadAllText("Samples/wmo-examples/metar-A3-1.xml");
            string invalidCodeListXml = validMetarXml.Replace(
                "xlink:href=\"http://codes.wmo.int/49-2/AerodromePresentWeather/RA\"",
                "xlink:href=\"http://codes.wmo.int/49-2/AerodromePresentWeather/INVALID_NONEXISTENT_WEATHER\"");

            // Act
            var result = IwxxmParser.Parse(invalidCodeListXml);

            // Assert
            Assert.True(result.IsFailure);
            Assert.Contains(result.Errors, e =>
                e.Category == ErrorCategory.SchematronBusinessRule &&
                e.Code == "IWXXM.RdfCodeListRegistry-1");
        }

        [Fact]
        public void ParseFileAs_MismatchedReportType_ReturnsUnexpectedReportTypeError()
        {
            // Act: Attempt to parse a METAR XML file as a TafReport
            var result = IwxxmParser.ParseFileAs<TafReport>("Samples/wmo-examples/metar-A3-1.xml");

            // Assert
            Assert.True(result.IsFailure);
            Assert.Contains(result.Errors, e => e.Code == "IWXXM-UNEXPECTED-REPORT-TYPE");
        }
    }
}
