using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Iwxxm.Net.Reports;
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
        public void ParseFileAs_MetarA31_MapsMetar()
        {
            // Act
            var result = IwxxmParser.ParseFileAs<Metar>("Samples/wmo-examples/metar-A3-1.xml");

            // Assert
            Assert.True(result.IsSuccess, string.Join(" | ", result.Errors));
            var metar = result.Value;
            Assert.Equal(IwxxmReportType.Metar, metar.ReportType);
            Assert.Equal("YUDO", metar.AerodromeDesignator);
            Assert.False(metar.IsAutomatedStation);
            Assert.NotNull(metar.SurfaceWind);
            Assert.Equal(240.0, metar.SurfaceWind!.MeanDirectionDegrees);
            Assert.Equal(new Speed(4.0, SpeedUnit.MetersPerSecond), metar.SurfaceWind.MeanSpeed);
            Assert.Equal(600.0, metar.PrevailingVisibilityMeters);
            Assert.Contains(new AerodromeWeather("DZ", isKnown: true), metar.PresentWeather);
            Assert.Contains(new AerodromeWeather("FG", isKnown: true), metar.PresentWeather);
            Assert.Single(metar.RunwayVisualRanges);
            Assert.Equal(17.0, metar.AirTemperatureCelsius);
            Assert.Equal(16.0, metar.DewpointTemperatureCelsius);
            Assert.Equal(1018.0, metar.QnhHpa);
            Assert.Equal(2, metar.TrendForecasts.Count);
        }

        [Fact]
        public void ParseFileAs_SpeciA32_MapsSpeci()
        {
            // Act
            var result = IwxxmParser.ParseFileAs<Speci>("Samples/wmo-examples/speci-A3-2.xml");

            // Assert
            Assert.True(result.IsSuccess, string.Join(" | ", result.Errors));
            var speci = result.Value;
            Assert.Equal(IwxxmReportType.Speci, speci.ReportType);
            Assert.Equal("YUDO", speci.AerodromeDesignator);
            Assert.NotNull(speci.SurfaceWind);
        }

        [Fact]
        public void ParseFileAs_TafA51_MapsTafReportAndForecastGroups()
        {
            // Act
            var result = IwxxmParser.ParseFileAs<Taf>("Samples/wmo-examples/taf-A5-1.xml");

            // Assert
            Assert.True(result.IsSuccess, string.Join(" | ", result.Errors));
            var taf = result.Value;
            Assert.Equal(IwxxmReportType.Taf, taf.ReportType);
            Assert.Equal("YUDO", taf.AerodromeDesignator);
            Assert.NotNull(taf.ValidFromUtc);
            Assert.NotNull(taf.ValidUntilUtc);
            Assert.NotNull(taf.BaseForecast);
            Assert.NotEmpty(taf.ChangeForecasts);
        }

        [Fact]
        public void ParseFileAs_SigmetExamples_MapStandardVolcanicAndTropicalCycloneVariants()
        {
            // 1. Standard SIGMET (Thunderstorm)
            var tsSigmet = IwxxmParser.ParseFileAs<Sigmet>("Samples/wmo-examples/sigmet-A6-1a-TS.xml");
            Assert.True(tsSigmet.IsSuccess, string.Join(" | ", tsSigmet.Errors));
            Assert.Equal(IwxxmReportType.Sigmet, tsSigmet.Value.ReportType);
            Assert.Equal("YUDD", tsSigmet.Value.AtsUnitDesignator);
            Assert.Equal("YUDD", tsSigmet.Value.FirDesignator);
            Assert.Equal(new CodeListValue<SigmetPhenomenon>(SigmetPhenomenon.ObscuredThunderstorm, "OBSC_TS"), tsSigmet.Value.Phenomenon);
            Assert.NotEmpty(tsSigmet.Value.EvolvingConditions);

            // 2. Volcanic Ash SIGMET
            var vaSigmet = IwxxmParser.ParseFileAs<Sigmet>("Samples/wmo-examples/sigmet-VA-EGGX.xml");
            Assert.True(vaSigmet.IsSuccess, string.Join(" | ", vaSigmet.Errors));
            Assert.Equal(IwxxmReportType.VolcanicAshSigmet, vaSigmet.Value.ReportType);
            Assert.NotNull(vaSigmet.Value.EruptingVolcano);
            Assert.Equal("MT HEKLA", vaSigmet.Value.EruptingVolcano!.Name);
            Assert.Equal(new GeoCoordinate(63.98, -19.67), vaSigmet.Value.EruptingVolcano.Position);

            // 3. Tropical Cyclone SIGMET
            var tcSigmet = IwxxmParser.ParseFileAs<Sigmet>("Samples/wmo-examples/sigmet-A6-2-TC.xml");
            Assert.True(tcSigmet.IsSuccess, string.Join(" | ", tcSigmet.Errors));
            Assert.Equal(IwxxmReportType.TropicalCycloneSigmet, tcSigmet.Value.ReportType);
            Assert.Equal("Gloria", tcSigmet.Value.TropicalCycloneName);
        }

        [Fact]
        public void ParseFileAs_AirmetExample_MapsAirmetReport()
        {
            // Act
            var result = IwxxmParser.ParseFileAs<Airmet>("Samples/wmo-examples/airmet-A6-1a-TS.xml");

            // Assert
            Assert.True(result.IsSuccess, string.Join(" | ", result.Errors));
            var airmet = result.Value;
            Assert.Equal(IwxxmReportType.Airmet, airmet.ReportType);
            Assert.Equal("YUDD", airmet.AtsUnitDesignator);
            Assert.Equal(new CodeListValue<AirmetPhenomenon>(AirmetPhenomenon.IsolatedThunderstorms, "ISOL_TS"), airmet.Phenomenon);
            Assert.Single(airmet.EvolvingConditions);
        }

        [Fact]
        public void ParseFileAs_VonaExample_MapsVolcanoObservatoryNotice()
        {
            // Act
            var result = IwxxmParser.ParseFileAs<Vona>("Samples/wmo-examples/vona-A7-1.xml");

            // Assert
            Assert.True(result.IsSuccess, string.Join(" | ", result.Errors));
            var vona = result.Value;
            Assert.Equal(IwxxmReportType.VolcanoObservatoryNotice, vona.ReportType);
            Assert.Single(vona.Volcanoes);
            Assert.Equal("KARYMSKY", vona.Volcanoes[0].Name);
            Assert.Equal("300130", vona.Volcanoes[0].IavceiNumber);
            Assert.Equal(AviationColourCode.Yellow, vona.CurrentColourCode!.Code);
            Assert.Equal("YELLOW", vona.CurrentColourCode.Notation);
            Assert.Equal(AviationColourCode.Orange, vona.PreviousColourCode!.Code);
            Assert.NotNull(vona.AshCloudElevation);
        }

        [Fact]
        public void ParseFileAs_QvaciExample_MapsQuantitativeAshConcentrationWithInteriorPolygonHole()
        {
            // Act
            var result = IwxxmParser.ParseFileAs<Qvaci>("Samples/wmo-examples/qvaci-Example.xml");

            // Assert
            Assert.True(result.IsSuccess, string.Join(" | ", result.Errors));
            var qvaci = result.Value;
            Assert.Equal(IwxxmReportType.QuantitativeVolcanicAshConcentration, qvaci.ReportType);
            Assert.Single(qvaci.Volcanoes);
            Assert.Equal("KRAKATAU", qvaci.Volcanoes[0].Name);
            Assert.Equal("262000", qvaci.Volcanoes[0].IavceiNumber);
            Assert.NotEmpty(qvaci.AshConcentrationClouds);
            Assert.Contains(qvaci.AshConcentrationClouds, c => c.Area.InteriorRings.Count > 0);
        }

        [Fact]
        public void ParseFileAs_VaacTcacAndSpaceWxExamples_MapAdvisoryReports()
        {
            // 1. Volcanic Ash Advisory (VAAC)
            var vaac = IwxxmParser.ParseFileAs<Vaa>("Samples/wmo-examples/va-advisory-A7-2.xml");
            Assert.True(vaac.IsSuccess, string.Join(" | ", vaac.Errors));
            Assert.Equal(IwxxmReportType.VolcanicAshAdvisory, vaac.Value.ReportType);
            Assert.Equal("TOKYO", vaac.Value.IssuingVaacName);
            Assert.NotNull(vaac.Value.Volcano);
            Assert.Equal("KARYMSKY 1000-13", vaac.Value.Volcano!.Name);

            // 2. Tropical Cyclone Advisory (TCAC)
            var tcac = IwxxmParser.ParseFileAs<Tca>("Samples/wmo-examples/tc-advisory-A2-2.xml");
            Assert.True(tcac.IsSuccess, string.Join(" | ", tcac.Errors));
            Assert.Equal(IwxxmReportType.TropicalCycloneAdvisory, tcac.Value.ReportType);
            Assert.Equal("GLORIA", tcac.Value.CycloneName);
            Assert.NotEmpty(tcac.Value.Forecasts);

            // 3. Space Weather Advisory (SWXC)
            var spaceWx = IwxxmParser.ParseFileAs<SpaceWx>("Samples/wmo-examples/spacewx-A7-3.xml");
            Assert.True(spaceWx.IsSuccess, string.Join(" | ", spaceWx.Errors));
            Assert.Equal(IwxxmReportType.SpaceWeatherAdvisory, spaceWx.Value.ReportType);
            Assert.Equal("DONLON", spaceWx.Value.IssuingSwxcName);
            Assert.Contains("HF_COM", spaceWx.Value.Effects);
            Assert.NotEmpty(spaceWx.Value.Analyses);
        }

        [Fact]
        public void ParseBulletinFile_MetarNilCollect_UnwrapsMeteorologicalBulletinAndNestedReports()
        {
            // Act
            var result = IwxxmParser.ParseBulletinFile("Samples/wmo-examples/metar-NIL-collect.xml");

            // Assert
            Assert.True(result.IsSuccess, string.Join(" | ", result.Errors));
            var bulletin = result.Value;
            Assert.Equal(IwxxmReportType.MeteorologicalBulletin, bulletin.ReportType);
            Assert.Equal("A_LAYU31YUDO221630_C_YUDO_20120822163000.xml", bulletin.BulletinIdentifier);
            Assert.Single(bulletin.Reports);
            Assert.Equal(IwxxmReportType.Metar, bulletin.Reports[0].ReportType);
        }

        [Fact]
        public void Parse_UnregisteredWmo492CodeListUri_FailsInOfflineRdfSchematronRule()
        {
            // Arrange: Replace valid SCT cloud amount URI with an unregistered code URI in WMO 49-2 RDF registry
            string validMetarXml = File.ReadAllText("Samples/wmo-examples/metar-A3-1.xml");
            string invalidCodeListXml = validMetarXml.Replace(
                "xlink:href=\"http://codes.wmo.int/49-2/CloudAmountReportedAtAerodrome/SCT\"",
                "xlink:href=\"http://codes.wmo.int/49-2/CloudAmountReportedAtAerodrome/INVALID_NONEXISTENT_AMOUNT\"");

            // Act
            var result = IwxxmParser.Parse(invalidCodeListXml);

            // Assert
            Assert.True(result.IsFailure);
            Assert.Contains(result.Errors, e =>
                e.Category == ErrorCategory.BusinessRuleViolation &&
                e.Code == "IWXXM.RdfCodeListRegistry-1");
        }

        [Fact]
        public void ParseFileAs_MismatchedReportType_ReturnsUnexpectedReportTypeError()
        {
            // Act: Attempt to parse a METAR XML file as a Taf
            var result = IwxxmParser.ParseFileAs<Taf>("Samples/wmo-examples/metar-A3-1.xml");

            // Assert
            Assert.True(result.IsFailure);
            Assert.Contains(result.Errors, e => e.Code == "IWXXM-UNEXPECTED-REPORT-TYPE");
        }

        [Fact]
        public void Process_StreamExceedingMaxDocumentBytes_FailsWithXmlSizeLimit()
        {
            // Arrange: Configure strict 256-byte limit and pass a larger XML payload
            var strictOptions = new IwxxmParserOptions(maxDocumentBytes: 256);
            var dispatcher = new IwxxmParserBuilder().WithOptions(strictOptions).Build();

            using (var fileStream = File.OpenRead("Samples/wmo-examples/metar-A3-1.xml"))
            {
                // Act
                var result = dispatcher.Process(fileStream);

                // Assert
                Assert.True(result.IsFailure);
                Assert.Contains(result.Errors, e =>
                    e.Category == ErrorCategory.MalformedInput &&
                    e.Code == "XML-SIZE-LIMIT");
            }
        }

        [Fact]
        public void Process_DocumentExceedingMaxCharactersInDocument_FailsWithXmlMalformed()
        {
            // Arrange: Allow stream bytes but restrict XmlReader MaxCharactersInDocument to 128 chars
            var strictOptions = new IwxxmParserOptions(maxDocumentBytes: 1024 * 1024, maxCharactersInDocument: 128);
            var dispatcher = new IwxxmParserBuilder().WithOptions(strictOptions).Build();

            using (var fileStream = File.OpenRead("Samples/wmo-examples/metar-A3-1.xml"))
            {
                // Act
                var result = dispatcher.Process(fileStream);

                // Assert
                Assert.True(result.IsFailure);
                Assert.Contains(result.Errors, e =>
                    e.Category == ErrorCategory.MalformedInput &&
                    e.Code == "XML-MALFORMED");
            }
        }

        [Fact]
        public void ParseFileToGeoJson_WafsSigwxQvaciAndMetar_ProducesValidRfc7946FeatureCollectionWithLonLatOrder()
        {
            // 1. METAR (YUDO ARP is Lat 12.34, Lon -12.34 in GML -> must be [-12.34, 12.34] in RFC 7946 GeoJSON)
            var metarGeoJsonResult = IwxxmParser.ParseFileToGeoJson("Samples/wmo-examples/metar-A3-1.xml", GeoJsonExportOptions.Pretty);
            Assert.True(metarGeoJsonResult.IsSuccess, string.Join(" | ", metarGeoJsonResult.Errors));

            using (JsonDocument metarDoc = JsonDocument.Parse(metarGeoJsonResult.Value))
            {
                JsonElement root = metarDoc.RootElement;
                Assert.Equal("FeatureCollection", root.GetProperty("type").GetString());
                Assert.Equal(4, root.GetProperty("bbox").GetArrayLength());

                JsonElement firstFeature = root.GetProperty("features")[0];
                Assert.Equal("Feature", firstFeature.GetProperty("type").GetString());
                JsonElement geom = firstFeature.GetProperty("geometry");
                Assert.Equal("Point", geom.GetProperty("type").GetString());
                JsonElement coords = geom.GetProperty("coordinates");
                Assert.Equal(-12.34, coords[0].GetDouble(), 3); // Longitude first (RFC 7946 Section 3.1.1)
                Assert.Equal(12.34, coords[1].GetDouble(), 3);  // Latitude second

                JsonElement props = firstFeature.GetProperty("properties");
                Assert.Equal("YUDO", props.GetProperty("aerodromeDesignator").GetString());
                Assert.Equal(1018.0, props.GetProperty("qnhHpa").GetDouble());
            }

            // 2. QVACI (Must export hollow polygon with exterior ring + interior hole ring)
            var qvaciGeoJsonResult = IwxxmParser.ParseFileToGeoJson("Samples/wmo-examples/qvaci-Example.xml");
            Assert.True(qvaciGeoJsonResult.IsSuccess, string.Join(" | ", qvaciGeoJsonResult.Errors));

            using (JsonDocument qvaciDoc = JsonDocument.Parse(qvaciGeoJsonResult.Value))
            {
                JsonElement features = qvaciDoc.RootElement.GetProperty("features");
                Assert.True(features.GetArrayLength() >= 2);

                bool foundHollowPolygon = false;
                foreach (JsonElement feat in features.EnumerateArray())
                {
                    JsonElement geom = feat.GetProperty("geometry");
                    if (geom.GetProperty("type").GetString() == "Polygon" &&
                        geom.GetProperty("coordinates").GetArrayLength() > 1)
                    {
                        foundHollowPolygon = true;
                        break;
                    }
                }
                Assert.True(foundHollowPolygon, "Expected QVACI GeoJSON export to include a Polygon with an interior hole ring.");
            }

            // 3. WAFS SIGWX (Must export JetStreams, Clouds, Turbulence, Icing, Tropopause, Volcanoes, etc.)
            var wafsGeoJsonResult = IwxxmParser.ParseFileToGeoJson("Samples/wmo-examples/WAFS-Example.xml");
            Assert.True(wafsGeoJsonResult.IsSuccess, string.Join(" | ", wafsGeoJsonResult.Errors));

            using (JsonDocument wafsDoc = JsonDocument.Parse(wafsGeoJsonResult.Value))
            {
                JsonElement features = wafsDoc.RootElement.GetProperty("features");
                Assert.True(features.GetArrayLength() >= 9);
            }
        }
    }
}
