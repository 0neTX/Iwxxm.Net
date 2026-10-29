# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Fixed

- **WMO BUFR code mappings** (`WmoCodeRegistry`, now `Iwxxm.Net.Reports`): every official code of tables 0-20-008, 0-20-012, 0-11-030 and 0-20-041 now resolves to the member that represents its official meaning, and every member value equals the official code. Previously, among others, turbulence code 8 ("Nil") resolved to `LightClearAir`, 19 ("Moderate, occasional severe") to `Extreme`, 20 ("Moderate, frequently severe") to `ExtremeClearAir`, 10/11 ("Moderate"/"Severe", cloud/clear air not specified) to clear-air members; icing code 3 ("Light icing in precipitation") resolved to `Moderate` and codes 10-12 were ignored; cloud distribution codes 13-16 were shifted. Codes of a different table and undefined codes now resolve to `Unknown`. Data-driven tests iterate every concept of the official RDF tables.
- **Units of measure**: `@uom` is no longer ignored. The official SPECI A3-2 example (`<iwxxm:meanWindSpeed uom="[kn_i]">25.0</iwxxm:meanWindSpeed>`) was previously exposed as 25 m/s; it is now `Speed(25, Knots)` (12.8611 m/s). Jet-stream speeds in `km/h` are no longer treated as knots. Single-unit quantities (`Cel`, `hPa`, `deg`, visibility/RVR in `m`, ash density in `mg/m3`) are verified. A missing or unknown unit is reported as a `DomainMapping` error (`ACL-MEASURE-UOM-MISSING`, `ACL-MEASURE-UOM-UNSUPPORTED`, `ACL-MEASURE-UOM-MISMATCH`, `ACL-MEASURE-VALUE-INVALID`); no unit is assumed. `FlightLevel` uses the exact 0.3048 m/ft factor.
- **TAF business rule** `TAF.BaseForecastMandatory-1` never fired when `isCancelReport` was absent (XPath 1.0 `@isCancelReport != 'true'` is false for a missing attribute); it now uses `not(@isCancelReport = 'true')`. `Common.BasicReport-2` now matches the official rule (only `permissibleUsage="OPERATIONAL"`).
- **XSD validation now uses the official schemas**: the previous hand-written XSD subsets (no facets, `MeasureType` as `xs:string`, defaulted `reportStatus`/`permissibleUsage`, missing `recentWeather`/`windShear`/`extension`, non-nillable temperatures) were wrongly described as official. Every standard root is now validated against byte-identical copies of the official WMO IWXXM 2025-2 schemas and their official imports (GML 3.2.1, O&M 2.0, ISO 19139, AIXM 5.1.1, METCE 1.2, OPM 1.2, COLLECT 1.2, XLink/XML), 80 files embedded under `Resources/Xsd/Official/` with path-preserving logical names. Validation goes through `XDocument.Validate` so that schema default attributes (`xlink:type`) do not cause spurious errors in official examples that do not declare the XLink prefix.
- **Code lists**: the single 84-concept `codes.wmo.int-49-2.rdf` was replaced by the 20 official WMO RDF code lists (716 concepts, 690 distinct URIs). Each `codes.wmo.int` `xlink:href` is checked against the official list of its register (with the official IWXXM 2025-2 `owl:versionInfo` filter where the official rules apply it), so valid codes such as `http://codes.wmo.int/306/4678/-SHRA` are accepted. The WAFS BUFR rules now check membership in the official tables instead of a URI prefix.

### Changed

- **Hexagonal architecture** (AUD-A1 / section 8.3): ports split into inbound use-case interfaces (`Application/Ports/In`) and public outbound ports (`Application/Ports/Out`, namespace `Iwxxm.Net.Extensibility`); a dedicated composition root (`IwxxmParserBuilder`, `Composition/`) is the only code that references Infrastructure; the facades are instance-based and depend only on inbound ports; `IwxxmParserOptions` moved from Domain to Application; Domain error categories are technology-agnostic.
- **SIGWX transport model** is read by an internal reflection-based reader (`AttributeDrivenXmlReader`) instead of `XmlSerializer`, which cannot process internal types. Unknown elements are ignored, the last occurrence wins for single-valued members, repeated elements are appended, `xsi:nil` on nullable members yields `null`, conversion errors are still reported as `XML-DESERIALIZATION-ERROR`.
- **Architecture tests** are IL-level (`HexagonalArchitectureTests`, NetArchTest.Rules/Mono.Cecil as a test-only dependency) and replace the reflection-signature checks and the `grep` audit in the Dockerfile.
- **Build**: .NET 10 SDK (`global.json` 10.0.100, Docker image `mcr.microsoft.com/dotnet/sdk:10.0`, CI `10.0.x`); test and benchmark projects target `net10.0`; `Microsoft.SourceLink.GitHub` 8.0.0 → 10.0.403 (build-only, fixes NuGet audit warning NU1902 for `Microsoft.Build.Tasks.Git` 8.0.0). The pack stage builds both packages and fails if `Iwxxm.Net` declares any dependency.
- `EmbeddedXmlUrlResolver` is now an air-gapped `XmlResolver` that maps official `schemaLocation` URLs (`http://schemas.wmo.int/`, `http://schemas.opengis.net/`, `http://www.aixm.aero/schema/`, `http://www.w3.org/`) and relative includes to embedded resources; unknown URIs, file URIs and non-embedded files fail. Embedded resource lookups are exact (no suffix matching). Manifest resource names changed to `Iwxxm.Net/Resources/<relative path>`.
- `XsdValidator` selects the schema set from the root element QName and detects unknown roots through `XmlSchemaSet.GlobalElements` (`XSD-UNKNOWN-ROOT-ELEMENT`) instead of English message text. The SIGWX processor rejects non-SIGWX roots with `XSD-UNEXPECTED-ROOT-ELEMENT`.
- The compact `iwxxm:SIGWXReport` root is documented as a **non-standard extension**; its hand-written schemas moved to `Resources/Xsd/Legacy/` and are used only for that root.
- README and package description now state that XSD validation uses the official WMO 2025-2 schemas and that business-rule validation is a partial XSLT 1.0 subset (11 of 165 official rule IDs).
- GeoJSON export writes measures as reported plus a `...Uom` property (e.g. `windSpeed` + `windSpeedUom`) and adds the official numeric BUFR codes (`distributionNumericCode`, `typeNumericCode`, `degreeNumericCode`).

### Added

- **`Iwxxm.Net.Validation.Saxon`** (new optional package, `net10.0`, AUD-S1): `OfficialSchematronValidator` runs the byte-identical official `iwxxm.sch` (all 165 rule patterns, `queryBinding="xslt2"`) with SaxonCS-HE 13.1.0 and the vendored SchXslt 1.10.1 compiler (MIT); compiled once per process and cached thread-safely; air-gapped (`document()` code-list lookups resolve to the official RDFs embedded in `Iwxxm.Net`, every other URI is refused); SVRL findings map to `ValidationError` (official pattern id as `Code`, `BusinessRuleViolation`, severity from `@role`/`@flag`, SVRL `location`). Registered with `IwxxmParserBuilder.UseOfficialSchematron()`. All 29 official examples pass; runs without a Saxonica license key (Home Edition only).
- `IwxxmParserBuilder` (`WithOptions`, `UseSchemaValidator`, `UseSchematronValidator(validator | factory)`, `WithoutSchematron`, `UseDocumentMapper`, `UseSigwxDocumentMapper`, `UseGeoJsonExporter`, `Build`, `BuildSigwxParser`, `BuildGeoJsonExporter`).
- Public outbound ports `ISchemaValidator`, `ISchematronValidator`, `IIwxxmDocumentMapper`, `ISigwxDocumentMapper`, `IGeoJsonExporter` (`Iwxxm.Net.Extensibility`) and the inbound port `IGeoJsonExportService`.
- Instance API on the facades (`IwxxmParser.Process`, `ProcessFile`, `ProcessAs<T>`, `ProcessFileAs<T>`, `ProcessBulletin`, `ProcessBulletinFile`, `ProcessToGeoJson`, `ProcessFileToGeoJson`; `IwxxmSigwxParser.Process`/`ProcessFile`; `IwxxmGeoJsonExporter.Export`) and shared `Default` instances.
- `Speed` / `SpeedUnit` and `Length` / `LengthUnit` immutable Domain value objects (value + unit, exact structural equality, exact conversions).
- `WmoCodeRegistry.TryGetNumericCode` and the `...TableUri` constants; `Cloud.DistributionNumericCode`, `Cloud.TypeNumericCode`, `Turbulence.DegreeNumericCode`, `AirframeIcing.DegreeNumericCode` expose the raw official code.
- Upstream ReadMe/release-note files of the official schemas are packed under `schema-notices/`.

### BREAKING CHANGES

- **Namespaces** (consumers no longer reference layer names):
  - `Iwxxm.Net.Presentation`, `Iwxxm.Net.Domain.Common`, `Iwxxm.Net.Application.Ports` (inbound ports and options) → `Iwxxm.Net`.
  - `Iwxxm.Net.Domain.Aggregates`, `Iwxxm.Net.Domain.ValueObjects` → `Iwxxm.Net.Reports`.
  - `IGeoJsonExporter` (now an outbound adapter port, `Export(report, options)` with non-optional options) → `Iwxxm.Net.Extensibility`; facades implement the new inbound port `IGeoJsonExportService`.
- **`ErrorCategory` members renamed** (numeric values unchanged; `ValidationError.Code` strings are unchanged and remain the stable contract): `XmlWellFormedness` → `MalformedInput`, `XsdSchemaValidation` → `SchemaViolation`, `SchematronBusinessRule` → `BusinessRuleViolation`; `Deserialization` and `DomainMapping` are unchanged. `ValidationError.ToString()` prints the new names.
- **Composition**: `IwxxmParser.CreateDispatcher(options)`, `IwxxmSigwxParser.CreateProcessor(options)`, `IwxxmGeoJsonExporter.CreateExporter()` and the constructors `IwxxmParser(IwxxmParserOptions?)`, `IwxxmSigwxParser(IwxxmParserOptions?)`, `IwxxmGeoJsonExporter()` were removed. Use `new IwxxmParserBuilder().WithOptions(options).Build()` / `.BuildSigwxParser()` / `.BuildGeoJsonExporter()`; facade constructors now take inbound ports. The static `Parse*` / `ToGeoJson` members are unchanged.
- **`Iwxxm20252.Generated`** types (48) are now `internal` (AUD-H8).
- **Tooling**: building the solution requires the .NET 10 SDK (the core package still targets `netstandard2.0` / `netstandard2.1`).

- **Enum values and members** (`Iwxxm.Net.Reports`, formerly `Iwxxm.Net.Domain.ValueObjects`): numeric values now equal the official codes.
  - `CloudDistributionCode`: `Unknown` is now `-1` (official code 0 is `SkyClear`); `Dense` 14 → 13, `Layers` 16 → 14, `FrequentEmbedded` 13 → 17; `DenseEmbedded` was **removed** (no official meaning); added `SkyClear`, `Few`, `Scattered`, `Broken`, `Overcast`, `ScatteredBroken`, `BrokenOvercast`, `Obscured`, `Embedded`, `MissingValue`.
  - `TurbulenceDegreeCode`: `Nil`, `Light`, `Moderate`, `Severe` now mean "cloud/clear air not specified" (8-11, previously 0-3); in-cloud codes 0-3 are `NilInCloud`, `LightInCloud`, `ModerateInCloud`, `SevereInCloud`; `LightClearAir` 8 → 5, `ModerateClearAir` 10 → 6, `SevereClearAir` 11 → 7, `ExtremeClearAir` 20 → 12, `Extreme` 19 → 14; added `NilClearAir`, `ExtremeInCloud`, the combined intensities 15-23 and `MissingValue`.
  - `IcingDegreeCode`: `Moderate` 3 → 4; added `LightInCloud`, `LightInPrecipitation`, `ModerateInCloud`, `ModerateInPrecipitation`, `SevereInCloud`, `SevereInPrecipitation`, `Trace`, `TraceInCloud`, `TraceInPrecipitation`, `MissingValue`.
  - `CloudTypeCode`: added members for codes 10-42 and 59-63; the TAC abbreviation `TCU` now resolves to `CumulusMediocrisOrCongestus` (official code 32, "Towering cumulus (TCU)") instead of `Cumulus`.
- **Unit-agnostic measure properties** (values are no longer converted or assumed):
  - `SurfaceWindInfo.MeanSpeedMetersPerSecond` → `MeanSpeed` (`Speed?`), `GustSpeedMetersPerSecond` → `GustSpeed` (`Speed?`); constructor parameters renamed accordingly.
  - `AerodromeCloudLayerInfo.BaseFeet` → `Base` (`Length?`).
  - `EnRoutePhenomenonVolume.SpeedOfMotionKnots` → `SpeedOfMotion` (`Speed?`).
  - `TropicalCycloneTrackPoint.MaxSurfaceWindSpeedMetersPerSecond` → `MaxSurfaceWindSpeed` (`Speed?`).
  - `VolcanicAshAdvisoryReport.SourceElevationMeters` → `SourceElevation` (`Length?`).
  - `JetStream.MaxWindSpeedKnots` → `MaxWindSpeed` (`Speed`), `JetStreamCorePoint.WindSpeedKnots` → `WindSpeed` (`Speed`); `JetStream.Create` takes a `Speed`. `JetStream` equality is now exact (no tolerance).
- **GeoJSON property names**: `windSpeedMetersPerSecond`/`windGustMetersPerSecond` → `windSpeed`/`windSpeedUom`, `windGust`/`windGustUom`; `speedOfMotionKnots` → `speedOfMotion`/`speedOfMotionUom`; `maxSurfaceWindSpeedMetersPerSecond` → `maxSurfaceWindSpeed`/`maxSurfaceWindSpeedUom`; `sourceElevationMeters` → `sourceElevation`/`sourceElevationUom`; `maxWindSpeedKnots`/`windSpeedKnots` → `maxWindSpeed`/`maxWindSpeedUom`, `windSpeed`/`windSpeedUom`.
- **Stricter validation**: documents that the previous hand-written XSDs accepted but the official schemas reject (e.g. non-numeric measures, missing `reportStatus`/`permissibleUsage`) now fail with `SchemaViolation` (formerly `XsdSchemaValidation`) errors; documents with an unknown or mismatching `@uom` now fail with `DomainMapping` errors.
- Embedded manifest resource names changed (`Iwxxm.Net.Resources.Xsd.<file>` → `Iwxxm.Net/Resources/Xsd/...`); `Resources/Codelists/codes.wmo.int-49-2.rdf` was removed.

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
