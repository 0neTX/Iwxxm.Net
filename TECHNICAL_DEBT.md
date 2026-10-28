# Technical Debt & Architectural Backlog (`Iwxxm.Net`)

This document tracks the architectural evolution and resolution status of all items planned across **Phase 1**, **Phase 2**, and **Phase 3**.

---

## Summary Matrix

| ID | Phase | Category | Title | Status | Target Release |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **TD-201** | Phase 2 | Architecture / Application | Polymorphic `IIwxxmReport` & `IwxxmParser` Dispatcher | **Completed** | `1.0.0-rc.1` |
| **TD-202** | Phase 2 | Domain / ACL / XSD | En-Route & Volcanic Hazard Reports (`QVACI`, `VONA`, `VA/TC/SWX Advisory`, `SIGMET`, `AIRMET`) | **Completed** | `1.0.0-rc.1` |
| **TD-203** | Phase 2 | Domain / ACL / XSD | Aerodrome Reports (`METAR`, `SPECI`, `TAF`) | **Completed** | `1.0.0-rc.1` |
| **TD-204** | Phase 2 | Infrastructure / SWIM | Collective Bulletin Unwrapping (`collect:MeteorologicalBulletin`) | **Completed** | `1.0.0-rc.1` |
| **TD-205** | Phase 2 | Infrastructure / Schematron | Full Offline RDF/SKOS Code-List Registry Validation (`codes.wmo.int-49-2.rdf`) | **Completed** | `1.0.0-rc.1` |
| **TD-301** | Phase 3 | Security / Hardening | XML DoS & Memory Bound Hardening (`IwxxmParserOptions`, `MaxCharactersInDocument`) | **Completed** | `1.0.0-rc.1` |
| **TD-302** | Phase 3 | Performance / Memory | Zero-Allocation `gml:posList` Tokenizer (`GmlCoordinateTokenizer`) | **Completed** | `1.0.0-rc.1` |
| **TD-303** | Phase 3 | Quality / Benchmarks | `BenchmarkDotNet` Performance & Memory Allocation Suite (`Iwxxm.Net.Benchmarks`) | **Completed** | `1.0.0-rc.1` |
| **TD-304** | Phase 3 | DevOps / CI-CD | Automated GitHub Actions Pipeline (`.github/workflows/ci-cd.yml`) | **Completed** | `1.0.0-rc.1` |

---

## Phase 2 (Completed): Expansion to Full IWXXM 2025-2 Report Suite & SWIM Collectives

All 5 items in Phase 2 (`TD-201` through `TD-205`) have been implemented and verified against all 29 official WMO IWXXM 2025-2 XML examples (`tests/Iwxxm.Net.Tests/Samples/wmo-examples/*.xml`):

- **TD-201 (Completed)**: Introduced `IIwxxmReport` and `IwxxmReportType` in `Iwxxm.Net.Domain.Aggregates`, `IIwxxmReportDispatcher` and `DispatchIwxxmReportUseCase` in `Iwxxm.Net.Application`, and the public `IwxxmParser` facade (`Parse`, `ParseFile`, `ParseAs<TReport>`, `ParseBulletin`) in `Iwxxm.Net.Presentation`.
- **TD-202 (Completed)**: Added embedded XSD schemas (`iwxxm-2025-2.xsd`, `aixm.xsd`, `metce.xsd`), Domain Aggregate Roots (`SigmetReport`, `AirmetReport`, `VonaReport`, `QvaciReport`, `VolcanicAshAdvisoryReport`, `TropicalCycloneAdvisoryReport`, `SpaceWeatherAdvisoryReport`), `GeoPolygon.InteriorRings` support for hollow ash clouds (`QVACI`), and `IwxxmSuiteMapper`.
- **TD-203 (Completed)**: Added `AerodromeWeatherReport` (`METAR`/`SPECI`), `TafReport`, `SurfaceWindInfo`, `RunwayVisualRangeInfo`, `AerodromeCloudLayerInfo`, and `AerodromeForecastGroupInfo` (`BASE`, `BECMG`, `TEMPO`, `FM`, `PROB30`/`PROB40`).
- **TD-204 (Completed)**: Added embedded `collect.xsd` (`http://def.wmo.int/collect/2014`) and `MeteorologicalBulletin` aggregate root capable of unwrapping multi-report SWIM/AMHS bulletins while preserving individual report types.
- **TD-205 (Completed)**: Embedded offline WMO SKOS/RDF code-list registry `Resources/Codelists/codes.wmo.int-49-2.rdf` and enabled air-gapped `document('../Codelists/codes.wmo.int-49-2.rdf')` resolution inside `XsltSchematronValidator` via `EmbeddedXmlUrlResolver` (`IWXXM.RdfCodeListRegistry-1`).

---

## Phase 3 (Completed): Production Hardening, Zero-Allocation Performance & CI/CD

All 4 items in Phase 3 (`TD-301` through `TD-304`) have been implemented:

- **TD-301 (Completed — XML DoS & Memory Bound Hardening)**:
  - Introduced `IwxxmParserOptions` (`MaxDocumentBytes = 10 MB`, `MaxCharactersInDocument = 10,485,760`, `MaxCharactersFromEntities = 1024`).
  - Enforced bounded 8 KB chunked stream buffering in `ProcessSigwxReportUseCase` and `DispatchIwxxmReportUseCase`, rejecting oversized streams immediately with `XML-SIZE-LIMIT` (`ErrorCategory.XmlWellFormedness`) before LOH exhaustion.
  - Enforced `MaxCharactersInDocument` and `MaxCharactersFromEntities` on all `XmlReaderSettings` instances across `XsdValidator`, `XsltSchematronValidator`, `XmlSerializerSigwxDeserializer`, and `IwxxmSuiteMapper`.
- **TD-302 (Completed — Zero-Allocation `gml:posList` Tokenizer)**:
  - Implemented `GmlCoordinateTokenizer` in `Iwxxm.Net.Infrastructure.Mapping` using a single-pass character index scanner (`TryReadNextDouble`) that parses decimal coordinates directly without `string.Split` array or substring heap allocations on `.NET Standard 2.0`.
  - Wired `SigwxMapper` and `IwxxmSuiteMapper` to use `GmlCoordinateTokenizer.ParsePosList` and `GmlCoordinateTokenizer.ParseSinglePoint`.
- **TD-303 (Completed — `BenchmarkDotNet` Performance & Memory Allocation Suite)**:
  - Added `tests/Iwxxm.Net.Benchmarks/Iwxxm.Net.Benchmarks.csproj` and `IwxxmParserBenchmarks.cs` with `[MemoryDiagnoser]` measuring steady-state throughput and GC allocations across WAFS SIGWX, QVACI, SWIM Collective Bulletins, and `GmlCoordinateTokenizer`.
- **TD-304 (Completed — Automated CI/CD Pipeline)**:
  - Created `.github/workflows/ci-cd.yml` running multi-OS (`ubuntu-latest`, `windows-latest`) builds, XPlat code coverage tests, deterministic `.nupkg` / `.snupkg` artifact packaging, and automatic incremental publishing (`1.0.0-rc.<run_number>`) exclusively to GitHub Packages (`https://github.com/0neTX/Iwxxm.Net/packages`) on every commit to `main`.
