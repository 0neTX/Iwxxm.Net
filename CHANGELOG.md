# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.0-rc.1] - 2026-10-28

### Added

- **Core Architecture (DDD & Clean Architecture)**:
  - Implemented `Result<T>` and `ValidationError` (`ErrorCategory`, `ValidationSeverity`) in `Iwxxm.Net.Domain.Common`.
  - Added native geospatial and vertical Value Objects (`GeoCoordinate`, `GeoPolygon`, `FlightLevel`) in `Iwxxm.Net.Domain.ValueObjects` with support for `FL`, meters (`M`), and feet (`[ft_i]`).
  - Added strongly-typed WMO BUFR code-list enums and resolver (`WmoCodeRegistry`, `CloudDistributionCode`, `CloudTypeCode`, `TurbulenceDegreeCode`, `IcingDegreeCode`) in `Iwxxm.Net.Domain.ValueObjects`.
  - Added complete SIGWX/WAFS domain Value Objects (`JetStream`, `JetStreamCorePoint`, `Cloud`, `Tropopause`, `TropopauseSpotHeight`, `Volcano`, `Turbulence`, `AirframeIcing`, `TropicalCyclone`, `Sandstorm`, `RadiationIncident`) and the `SignificantWeatherReport` Aggregate Root.
  - Defined `IIwxxmSigwxProcessor` inbound port and `ProcessSigwxReportUseCase<TDto>` pipeline orchestrator in `Iwxxm.Net.Application`.
- **Dual Validation Engine (XSD + Schematron XSLT)**:
  - Embedded IWXXM 2025-2, OGC GML 3.2.1, and W3C XLink XSD schemas alongside precompiled ISO Schematron XSLT 1.0 business rules (`iwxxm-2025-2-sigwx.xslt`), including WMO BUFR code-table rules (`0-20-008`, `0-20-012`, `0-11-030`, `0-20-041`) and nil-reason rules.
  - Implemented `EmbeddedResourceLoader` and `EmbeddedXmlUrlResolver` for zero-I/O in-memory `<xs:import>` / `<xs:include>` resolution.
  - Implemented thread-safe `XsdValidator` (`Lazy<XmlSchemaSet>`) and `XsltSchematronValidator` (`Lazy<XslCompiledTransform>` with SVRL `<svrl:failed-assert>` parsing).
- **Anti-Corruption Layer (ACL) & Serialization**:
  - Implemented `XmlSerializerSigwxDeserializer` with cached static `XmlSerializer` instances supporting both official WMO `<iwxxm:WAFSSignificantWeatherForecast>` and `<iwxxm:SIGWXReport>`.
  - Implemented `SigwxMapper` to extract coordinates from `<gml:posList>`, `<gml:CubicSpline>`, `<gml:LineStringSegment>`, `<gml:PolygonPatch>`, `<gml:Ring>`, `<gml:LinearRing>`, and `<gml:pos>` and map all 9 WAFS meteorological features (`JETSTREAM`, `CLOUD`, `TROPOPAUSE`, `VOLCANO`, `TURBULENCE`, `AIRFRAME_ICING`, `TROPICAL_CYCLONE`, `SANDSTORM`, `RADIATION`).
- **Public Facade, Test Suite & Technical Debt Documentation**:
  - Exposed `IwxxmSigwxParser` (`Parse(Stream)`, `Parse(string)`, `ParseFile(string)`, and `CreateProcessor()`).
  - Added all 29 official WMO IWXXM 2025-2 XML examples (`WAFS-Example.xml`, etc.) and Clean Architecture compliance tests.
  - Documented Phase 2 and Phase 3 architectural backlog in [`TECHNICAL_DEBT.md`](TECHNICAL_DEBT.md).
