# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.0-rc.1] - 2026-10-28

### Added

- **Core Architecture (DDD & Clean Architecture)**:
  - Implemented `Result<T>` and `ValidationError` (`ErrorCategory`, `ValidationSeverity`) in `Iwxxm.Net.Domain.Common`.
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
- **Anti-Corruption Layer (ACL) & Serialization**:
  - Implemented `IwxxmSuiteMapper` and `SigwxMapper` to translate GML 3.2.1, AIXM 5.1.1, METCE, and IWXXM 2025-2 structures into pure Domain Aggregate Roots.
  - Implemented `XmlSerializerSigwxDeserializer` with cached static `XmlSerializer` instances for WAFS SIGWX.
- **Public Facades & Test Suite**:
  - Exposed `IwxxmParser` (`Parse`, `ParseFile`, `ParseAs<TReport>`, `ParseFileAs<TReport>`, `ParseBulletin`, `ParseBulletinFile`, `CreateDispatcher()`) and `IwxxmSigwxParser`.
  - Validated all 29 official WMO IWXXM 2025-2 XML examples (`WAFS-Example.xml`, `metar-A3-1.xml`, `speci-A3-2.xml`, `taf-A5-1.xml`, `sigmet-*.xml`, `airmet-*.xml`, `vona-Example.xml`, `qvaci-Example.xml`, `vaac-*.xml`, `tcac-*.xml`, `spacewx-*.xml`, `collect-A3-1-A3-2.xml`).
