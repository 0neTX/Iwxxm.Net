using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Iwxxm.Net.Domain.Common;
using Iwxxm.Net.Domain.ValueObjects;
using Iwxxm.Net.Presentation;
using Xunit;

namespace Iwxxm.Net.Tests
{
    /// <summary>
    /// End-to-end integration tests for <see cref="IwxxmSigwxParser"/> using the official
    /// WMO IWXXM 2025-2 examples from https://github.com/wmo-im/iwxxm/tree/master/IWXXM/examples.
    /// </summary>
    public sealed class SigwxParserTests
    {
        private const string OfficialWmoWafsExamplePath = "Samples/wmo-examples/WAFS-Example.xml";
        private const string CompactSamplePath = "Samples/sigwx-swh-2025-2.xml";

        [Fact]
        public void ParseFile_OfficialWmoWafsExample_ReturnsSuccessAndMapsAllDomainObjects()
        {
            // Act: Parse the official WMO WAFS-Example.xml from wmo-im/iwxxm
            var result = IwxxmSigwxParser.ParseFile(OfficialWmoWafsExamplePath);

            // Assert
            Assert.True(result.IsSuccess, string.Join(" | ", result.Errors));
            var report = result.Value;

            Assert.Equal("uuid.ce3b61b9-d360-4da1-83b9-67e668406869", report.Id);
            Assert.Equal("London", report.IssuingCentre);
            Assert.Equal("NORMAL", report.ReportStatus);
            Assert.Equal("OPERATIONAL", report.PermissibleUsage);
            Assert.Equal(new FlightLevel(100), report.LowerLimit);
            Assert.Equal(new FlightLevel(600), report.UpperLimit);

            // 1. Verify JetStream mapped from MeteorologicalFeature (CubicSpline + 5 WAFSJetStreamWindSymbols)
            Assert.Single(report.JetStreams);
            var jetStream = report.JetStreams[0];
            Assert.Equal("uuid.8c3d3467-e398-4627-ab23-f9e57c2215b5", jetStream.Id);
            Assert.Equal(11, jetStream.AxisTrajectory.Count);
            Assert.Equal(new GeoCoordinate(35.4, -104.7), jetStream.AxisTrajectory[0]);
            Assert.Equal(new GeoCoordinate(41.9, -78.9), jetStream.AxisTrajectory[10]);
            Assert.Equal(5, jetStream.CorePoints.Count);
            // Max wind speed is 61.8 m/s (~120.13 kt) at 15240 m (FL500)
            Assert.Equal(new FlightLevel(500), jetStream.CoreFlightLevel);
            Assert.True(jetStream.MaxWindSpeedKnots > 120.0);

            // 2. Verify Cloud mapped from MeteorologicalFeature (ElevatedVolume + PolygonPatch + CubicSpline)
            Assert.Single(report.Clouds);
            var cloud = report.Clouds[0];
            Assert.Equal("uuid.6f770e58-488b-4d89-b828-4c952b7d6a65", cloud.Id);
            Assert.Equal("http://codes.wmo.int/bufr4/codeflag/0-20-008/10", cloud.CloudAmount);
            Assert.Equal(CloudDistributionCode.Occasional, cloud.DistributionCode);
            Assert.Equal("http://codes.wmo.int/bufr4/codeflag/0-20-012/9", cloud.CloudType);
            Assert.Equal(CloudTypeCode.Cumulonimbus, cloud.TypeCode);
            Assert.Null(cloud.BaseFlightLevel); // lowerElevation has xsi:nil="true" nilReason=".../inapplicable"
            Assert.Equal(new FlightLevel(410), cloud.TopFlightLevel); // 12496.8 m = 41000 ft = FL410
            Assert.Equal(9, cloud.Area.ExteriorRing.Count);

            // 3. Verify Tropopause mapped from MeteorologicalFeature (Polygon + ElevatedLevel 14020 m = FL460)
            Assert.Single(report.Tropopauses);
            var tropopause = report.Tropopauses[0];
            Assert.Equal("uuid.4cefc622-5784-49db-9d3d-7463c7a05dc6", tropopause.Id);
            Assert.NotNull(tropopause.ContourPolygon);
            Assert.Equal(new FlightLevel(460), tropopause.SpotHeights[0].FlightLevel);

            // 4. Verify Volcano mapped from MeteorologicalFeature (ETNA at 37.7, 15.0)
            Assert.Single(report.Volcanoes);
            var volcano = report.Volcanoes[0];
            Assert.Equal("uuid.bb8c4828-5d6e-41fb-ac96-130a8c3a7cf2", volcano.Id);
            Assert.Equal("ETNA", volcano.Name);
            Assert.Equal(new GeoCoordinate(37.7, 15.0), volcano.Position);

            // 5. Verify Turbulence mapped from MeteorologicalFeature (ElevatedVolume FL340-FL400 + DegreeOfTurbulence 10)
            Assert.Single(report.Turbulences);
            var turbulence = report.Turbulences[0];
            Assert.Equal("uuid.aaaf9f38-dc4c-410d-b056-c130a049bde9", turbulence.Id);
            Assert.Equal("http://codes.wmo.int/bufr4/codeflag/0-11-030/10", turbulence.RawDegreeReference);
            Assert.Equal(TurbulenceDegreeCode.ModerateClearAir, turbulence.Degree);
            Assert.Equal(new FlightLevel(340), turbulence.BaseFlightLevel); // 10363.2 m = 34000 ft = FL340
            Assert.Equal(new FlightLevel(400), turbulence.TopFlightLevel);  // 12192.0 m = 40000 ft = FL400
            Assert.Equal(17, turbulence.Area.ExteriorRing.Count);

            // 6. Verify AirframeIcing mapped from MeteorologicalFeature (ElevatedVolume FL100-FL240 + DegreeOfIcing 3)
            Assert.Single(report.AirframeIcings);
            var icing = report.AirframeIcings[0];
            Assert.Equal("uuid.48f96fb3-6534-4593-b760-9c65d0b9fa96", icing.Id);
            Assert.Equal("http://codes.wmo.int/bufr4/codeflag/0-20-041/3", icing.RawDegreeReference);
            Assert.Equal(IcingDegreeCode.Moderate, icing.Degree);
            Assert.Equal(new FlightLevel(100), icing.BaseFlightLevel);
            Assert.Equal(new FlightLevel(240), icing.TopFlightLevel);
            Assert.Equal(21, icing.Area.ExteriorRing.Count);

            // 7. Verify TropicalCyclone mapped from MeteorologicalFeature (FRED at 25.0, -78.0)
            Assert.Single(report.TropicalCyclones);
            var cyclone = report.TropicalCyclones[0];
            Assert.Equal("uuid.024d97bd-0dae-4a8e-8b94-3bac68c961e9", cyclone.Id);
            Assert.Equal("FRED", cyclone.Name);
            Assert.Equal(new GeoCoordinate(25.0, -78.0), cyclone.Position);

            // 8. Verify Sandstorm mapped from MeteorologicalFeature (Point at 25.0, -78.0)
            Assert.Single(report.Sandstorms);
            var sandstorm = report.Sandstorms[0];
            Assert.Equal("uuid.53fd5553-39d1-4f79-8439-d695d22e8590", sandstorm.Id);
            Assert.Equal(new GeoCoordinate(25.0, -78.0), sandstorm.Position);

            // 9. Verify RadiationIncident mapped from MeteorologicalFeature (Point at 53.2, -24.1 with nil siteName/incidentTime)
            Assert.Single(report.RadiationIncidents);
            var radiation = report.RadiationIncidents[0];
            Assert.Equal("uuid.821e1c84-9688-4585-861f-153770ed4a90", radiation.Id);
            Assert.Equal(new GeoCoordinate(53.2, -24.1), radiation.Position);
            Assert.Null(radiation.SiteName);
            Assert.Null(radiation.IncidentTimeUtc);
        }

        [Fact]
        public void ParseFile_CompactSigwxReportSample_ReturnsSuccessAndMapsAllDomainObjects()
        {
            // Act
            var result = IwxxmSigwxParser.ParseFile(CompactSamplePath);

            // Assert
            Assert.True(result.IsSuccess, string.Join(" | ", result.Errors));
            var report = result.Value;

            Assert.Equal("uuid.sigwx.wafc.egrr.202610281800", report.Id);
            Assert.Equal("EGRR", report.IssuingCentre);
            Assert.Equal(new FlightLevel(250), report.LowerLimit);
            Assert.Equal(new FlightLevel(630), report.UpperLimit);
            Assert.Single(report.JetStreams);
            Assert.Single(report.Clouds);
            Assert.Equal(CloudDistributionCode.OccasionalEmbedded, report.Clouds[0].DistributionCode);
            Assert.Equal(CloudTypeCode.Cumulonimbus, report.Clouds[0].TypeCode);
            Assert.Single(report.Tropopauses);
            Assert.Single(report.Volcanoes);
        }

        [Fact]
        public void Parse_OfficialWmoWafsExampleWithInvalidPhenomenonCategory_FailsInSchematronValidation()
        {
            // Arrange: Violate official WMO rule WAFSSignificantWeatherForecast.WAFSSignificantWeatherForecast-1
            string validXml = File.ReadAllText(OfficialWmoWafsExamplePath);
            string invalidSchematronXml = validXml.Replace(
                "<iwxxm:phenomenonCategory>weatherForecasts</iwxxm:phenomenonCategory>",
                "<iwxxm:phenomenonCategory>weatherObservations</iwxxm:phenomenonCategory>");

            // Act
            var result = IwxxmSigwxParser.Parse(invalidSchematronXml);

            // Assert
            Assert.True(result.IsFailure);
            Assert.Contains(result.Errors, e =>
                e.Category == ErrorCategory.SchematronBusinessRule &&
                e.Code == "WAFSSignificantWeatherForecast.WAFSSignificantWeatherForecast-1");
        }

        [Fact]
        public void Parse_OfficialWmoWafsExampleWithInvalidWmoBufrCodeUri_FailsInSchematronValidation()
        {
            // Arrange: Violate WMO BUFR code-list rule WAFSSignificantWeatherForecast.DegreeOfTurbulence
            string validXml = File.ReadAllText(OfficialWmoWafsExamplePath);
            string invalidSchematronXml = validXml.Replace(
                "xlink:href=\"http://codes.wmo.int/bufr4/codeflag/0-11-030/10\"",
                "xlink:href=\"http://codes.wmo.int/bufr4/codeflag/0-99-999/10\"");

            // Act
            var result = IwxxmSigwxParser.Parse(invalidSchematronXml);

            // Assert
            Assert.True(result.IsFailure);
            Assert.Contains(result.Errors, e =>
                e.Category == ErrorCategory.SchematronBusinessRule &&
                e.Code == "WAFSSignificantWeatherForecast.DegreeOfTurbulence");
        }

        [Fact]
        public void Parse_OfficialWmoWafsExampleWithSubThresholdJetStreamSpeed_FailsInSchematronValidation()
        {
            // Arrange: Replace 51.5 m/s (~100 kt) with 25.0 m/s (~48.6 kt < 80 kt ICAO threshold)
            string validXml = File.ReadAllText(OfficialWmoWafsExamplePath);
            string invalidSchematronXml = validXml.Replace(
                "<iwxxm:windSpeed uom=\"m/s\">51.5</iwxxm:windSpeed>",
                "<iwxxm:windSpeed uom=\"m/s\">25.0</iwxxm:windSpeed>");

            // Act
            var result = IwxxmSigwxParser.Parse(invalidSchematronXml);

            // Assert
            Assert.True(result.IsFailure);
            Assert.Contains(result.Errors, e =>
                e.Category == ErrorCategory.SchematronBusinessRule &&
                e.Code == "IWXXM-SIGWX-002");
        }

        [Fact]
        public void Parse_MissingRequiredXsdElement_FailsInXsdSchemaValidation()
        {
            // Arrange: Remove required <iwxxm:issueTime> element from WAFS-Example.xml
            string validXml = File.ReadAllText(OfficialWmoWafsExamplePath);
            string invalidXsdXml = validXml.Replace("<iwxxm:phenomenonCategory>weatherForecasts</iwxxm:phenomenonCategory>", string.Empty);

            // Act
            var result = IwxxmSigwxParser.Parse(invalidXsdXml);

            // Assert
            Assert.True(result.IsFailure);
            Assert.All(result.Errors, e => Assert.Equal(ErrorCategory.XsdSchemaValidation, e.Category));
        }

        [Fact]
        public void Parse_OutOfBoundsLatitudeInPosList_FailsInDomainAclMapping()
        {
            // Arrange: Inject latitude 95.4 (outside [-90, 90]) into WAFS-Example.xml JetStream posList
            string validXml = File.ReadAllText(OfficialWmoWafsExamplePath);
            string invalidCoordXml = validXml.Replace("35.4 -104.7", "95.4 -104.7");

            // Act
            var result = IwxxmSigwxParser.Parse(invalidCoordXml);

            // Assert
            Assert.True(result.IsFailure);
            Assert.Contains(result.Errors, e =>
                e.Category == ErrorCategory.DomainMapping &&
                e.Code == "GEO-INVALID-LAT");
        }

        public static IEnumerable<object[]> GetNonSigwxWmoExampleFiles()
        {
            const string examplesDir = "Samples/wmo-examples";
            if (!Directory.Exists(examplesDir))
            {
                yield break;
            }

            foreach (string filePath in Directory.GetFiles(examplesDir, "*.xml").OrderBy(f => f))
            {
                if (!filePath.EndsWith("WAFS-Example.xml", StringComparison.OrdinalIgnoreCase))
                {
                    yield return new object[] { filePath };
                }
            }
        }

        [Theory]
        [MemberData(nameof(GetNonSigwxWmoExampleFiles))]
        public void ParseFile_NonSigwxOfficialWmoExamples_AreRejectedBySigwxXsdValidator(string nonSigwxFilePath)
        {
            // Act: Passing METAR, TAF, SIGMET, AIRMET, VONA, QVACI, VA/TC/SpaceWx Advisory XML files
            // from https://github.com/wmo-im/iwxxm/tree/master/IWXXM/examples into the SIGWX parser
            var result = IwxxmSigwxParser.ParseFile(nonSigwxFilePath);

            // Assert: Must fail cleanly at the XSD validation stage without unhandled exceptions
            Assert.True(result.IsFailure);
            Assert.Contains(result.Errors, e => e.Category == ErrorCategory.XsdSchemaValidation);
        }
    }
}
