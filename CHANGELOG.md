# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.0-rc.1] - 2026-10-28

### Added

- **Core Architecture (DDD & Clean Architecture)**:
  - Implemented `Result<T>`, `ValidationError` (`ErrorCategory`, `ValidationSeverity`), and `IwxxmParserOptions` in `Iwxxm.Net.Domain.Common`.
  - Added native geospatial and vertical Value Objects (`GeoCoordinate`, `GeoPolygon` with `InteriorRings` support for hollow contours, `FlightLevel`) in `Iwxxm.Net.Domain.ValueObjects` with support for `FL`, meters (`M`), and feet (`[ft_i]`).
  - Added strongly-typed WMO BUFR code-list enums and resolver (`WmoCodeRegistry`, `CloudDistributionCode`, `CloudTypeCode`, `TurbulenceDegreeCode`, `IcingDegreeCode`) in `Iwxxm.Net.Domain.ValueObjects`.
  - Added polymorphic `IIwxxmReport` contract and `IwxxmReportType` enumeration across all IWXXM 2025-2 report families.
  - Added complete SIGWX/WAFS domain Value Objects (`JetStream`, `JetStreamCorePoint`, `Cloud`, `Tropopause`, `TropopauseSpotHeight`, `Volcano`, `Turbulence`, `AirframeIcing`, `TropicalCyclone`, `Sandstorm`, `RadiationIncident`) and the `SignificantWeatherReport` Aggregate Root.
  - Added full IWXXM 2025-2 Domain Aggregate Roots:
    - Aerodrome reports: `AerodromeWeatherReport` (`METAR`, `SPECI`) and `TafReport` (`TAF`).
    - En-route hazards: `SigmetReport` (`SIGMET`, `VolcanicAshSIGMET`, `TropicalCycloneSIGMET`) and `AirmetReport` (`AIRMET`).
    - Volcanic & Space Weather hazards: `VonaReport` (`VolcanoObservatoryNoticeForAviation`), `QvaciReport` (`QuantitativeVolcanicAshConcentrationInformation`), `VolcanicAshAdvisoryReport` (`VolcanicAshAdvisory`), `TropicalCycloneAdvisoryReport` (`TropicalCycloneAdvisory`), and `SpaceWeatherAdvisoryReport` (`SpaceWeatherAdvisory`).
    - SWIM / AMHS Collective Bulletin container: `MeteorologicalBulletin` (`collect:MeteorologicalBulletin`).
  - Defined `IIwxxmReportDispatcher`, `IIwxxmSigwxProcessor`, `DispatchIwxxmReportUseCase`, and `ProcessSigwxReportUseCase<TDto>` in `Iwxxm.Net.Application`.
- **Dual Validation Engine (XSD + Schematron XSLT + Offline WMO RDF Registries)**:
  - Embedded IWXXM 2025-2 (`iwxxm-2025-2.xsd`, `iwxxm-sigwx.xsd`), WMO Collect (`collect.xsd`), AIXM 5.1.1 (`aixm.xsd`), WMO METCE (`metce.xsd`), OGC GML 3.2.1 (`gml.xsd`), and W3C XLink (`xlink.xsd`) schemas.
  - Embedded precompiled ISO Schematron XSLT 1.0 business rules (`iwxxm-2025-2-sigwx.xslt`) and the offline WMO No. 306 / 49-2 SKOS/RDF code-list registry (`Resources/Codelists/codes.wmo.int-49-2.rdf`).
  - Implemented `EmbeddedResourceLoader` and `EmbeddedXmlUrlResolver` for zero-I/O in-memory `<xs:import>`, `<xs:include>`, and XSLT `document('../Codelists/codes.wmo.int-49-2.rdf')` resolution.
  - Implemented thread-safe `XsdValidator` (`Lazy<XmlSchemaSet>`) and `XsltSchematronValidator` (`Lazy<XslCompiledTransform>` with SVRL `<svrl:failed-assert>` parsing).
- **Production Security Hardening, Native GeoJSON Export & Zero-Allocation Performance (Phase 3)**:
  - Added `IwxxmParserOptions` enforcing bounded stream buffering (`MaxDocumentBytes = 10 MB`) and `XmlReaderSettings` character caps (`MaxCharactersInDocument = 10,485,760`, `MaxCharactersFromEntities = 1024`).
  - Implemented `GmlCoordinateTokenizer` for zero-array-allocation scanning of `<gml:posList>` and `<gml:pos>` coordinate sequences.
  - Implemented zero-dependency Native RFC 7946 GeoJSON exporter (`IGeoJsonExporter`, `Rfc7946GeoJsonExporter`, `IwxxmGeoJsonExporter.ToGeoJson`, `IwxxmParser.ParseToGeoJson`, `IwxxmParser.ParseFileToGeoJson`) enforcing `[longitude, latitude]` coordinate tuples, `Polygon` interior hole rings, and `[minLon, minLat, maxLon, maxLat]` bounding boxes across all 14 `IwxxmReportType` variants.
  - Added `Iwxxm.Net.Benchmarks` (`BenchmarkDotNet` with `[MemoryDiagnoser]`) and GitHub Actions CI/CD workflow (`.github/workflows/ci-cd.yml`).
- **Public Facades & Test Suite**:
  - Exposed `IwxxmParser` (`Parse`, `ParseFile`, `ParseAs<TReport>`, `ParseFileAs<TReport>`, `ParseBulletin`, `ParseBulletinFile`, `ParseToGeoJson`, `ParseFileToGeoJson`, `CreateDispatcher()`), `IwxxmGeoJsonExporter`, and `IwxxmSigwxParser`.
  - Validated all 29 official WMO IWXXM 2025-2 XML examples (`WAFS-Example.xml`, `metar-A3-1.xml`, `speci-A3-2.xml`, `taf-A5-1.xml`, `sigmet-*.xml`, `airmet-*.xml`, `vona-A7-1.xml`, `qvaci-Example.xml`, `va-advisory-A7-2.xml`, `tc-advisory-A2-2.xml`, `spacewx-*.xml`, `collect-A3-1-A3-2.xml`, `metar-NIL-collect.xml`).
