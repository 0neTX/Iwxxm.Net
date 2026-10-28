# Technical Debt & Architectural Backlog (`Iwxxm.Net`)

This document tracks the planned technical debt, architectural enhancements, and roadmap items across **Phase 1**, **Phase 2**, and **Phase 3**.

---

## Summary Matrix

| ID | Phase | Category | Title | Status | Target Release |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **TD-201** | Phase 2 | Architecture / Application | Polymorphic `IIwxxmReport` & `IwxxmParser` Dispatcher | **Completed** | `1.0.0-rc.1` |
| **TD-202** | Phase 2 | Domain / ACL / XSD | En-Route & Volcanic Hazard Reports (`QVACI`, `VONA`, `VA/TC/SWX Advisory`, `SIGMET`, `AIRMET`) | **Completed** | `1.0.0-rc.1` |
| **TD-203** | Phase 2 | Domain / ACL / XSD | Aerodrome Reports (`METAR`, `SPECI`, `TAF`) | **Completed** | `1.0.0-rc.1` |
| **TD-204** | Phase 2 | Infrastructure / SWIM | Collective Bulletin Unwrapping (`collect:MeteorologicalBulletin`) | **Completed** | `1.0.0-rc.1` |
| **TD-205** | Phase 2 | Infrastructure / Schematron | Full Offline RDF/SKOS Code-List Registry Validation (`codes.wmo.int-49-2.rdf`) | **Completed** | `1.0.0-rc.1` |
| **TD-301** | Phase 3 | Security / Hardening | XML DoS & Memory Bound Hardening (`MaxCharactersInDocument`) | Open (High) | `1.0.0` (GA) |
| **TD-302** | Phase 3 | Performance / Memory | Zero-Allocation `gml:posList` Tokenizer (Eliminate `string.Split` Gen0 Arrays) | Open (Medium) | `1.0.0` (GA) |
| **TD-303** | Phase 3 | Quality / Benchmarks | `BenchmarkDotNet` Performance & Memory Allocation Suite | Open (Medium) | `1.0.0` (GA) |
| **TD-304** | Phase 3 | DevOps / CI-CD | Automated GitHub Actions Pipeline (`.nupkg` / `.snupkg` Publishing) | Open (High) | `1.0.0` (GA) |

---

## Phase 2 (Completed): Expansion to Full IWXXM 2025-2 Report Suite & SWIM Collectives

All 5 items in Phase 2 (`TD-201` through `TD-205`) have been implemented and verified against all 29 official WMO IWXXM 2025-2 XML examples (`tests/Iwxxm.Net.Tests/Samples/wmo-examples/*.xml`):

- **TD-201 (Completed)**: Introduced `IIwxxmReport` and `IwxxmReportType` in `Iwxxm.Net.Domain.Aggregates`, `IIwxxmReportDispatcher` and `DispatchIwxxmReportUseCase` in `Iwxxm.Net.Application`, and the public `IwxxmParser` facade (`Parse`, `ParseFile`, `ParseAs<TReport>`, `ParseBulletin`) in `Iwxxm.Net.Presentation`.
- **TD-202 (Completed)**: Added embedded XSD schemas (`iwxxm-2025-2.xsd`, `aixm.xsd`, `metce.xsd`), Domain Aggregate Roots (`SigmetReport`, `AirmetReport`, `VonaReport`, `QvaciReport`, `VolcanicAshAdvisoryReport`, `TropicalCycloneAdvisoryReport`, `SpaceWeatherAdvisoryReport`), `GeoPolygon.InteriorRings` support for hollow ash clouds (`QVACI`), and `IwxxmSuiteMapper`.
- **TD-203 (Completed)**: Added `AerodromeWeatherReport` (`METAR`/`SPECI`), `TafReport`, `SurfaceWindInfo`, `RunwayVisualRangeInfo`, `AerodromeCloudLayerInfo`, and `AerodromeForecastGroupInfo` (`BASE`, `BECMG`, `TEMPO`, `FM`, `PROB30`/`PROB40`).
- **TD-204 (Completed)**: Added embedded `collect.xsd` (`http://def.wmo.int/collect/2014`) and `MeteorologicalBulletin` aggregate root capable of unwrapping multi-report SWIM/AMHS bulletins while preserving individual report types.
- **TD-205 (Completed)**: Embedded offline WMO SKOS/RDF code-list registry `Resources/Codelists/codes.wmo.int-49-2.rdf` and enabled air-gapped `document('../Codelists/codes.wmo.int-49-2.rdf')` resolution inside `XsltSchematronValidator` via `EmbeddedXmlUrlResolver` (`IWXXM.RdfCodeListRegistry-1`).

---

## Phase 3 (Active Backlog): Production Hardening, Zero-Allocation Performance & CI/CD

### TD-301: XML DoS & Memory Bound Hardening
- **Context**: `XsdValidator` and `XmlSerializerSigwxDeserializer` already enforce `DtdProcessing = DtdProcessing.Prohibit` and `XmlResolver = null` to prevent XXE (XML External Entity) and Billion Laughs attacks. However, `ProcessSigwxReportUseCase` and `DispatchIwxxmReportUseCase` buffer the input stream into a `MemoryStream` and do not yet enforce an upper bound on document character count.
- **Debt / Limitation**: An untrusted multi-gigabyte XML stream could cause excessive memory pressure (LOH allocations) before XSD validation completes.
- **Remediation Plan**:
  1. Set `MaxCharactersInDocument = 10_485_760` (10 MB configurable via `IwxxmParserOptions`) and `MaxCharactersFromEntities = 1024` on all `XmlReaderSettings` instances.
  2. Guard `CopyToSeekableMemoryStream` with a configurable maximum byte limit, returning an `ErrorCategory.XmlWellFormedness` (`XML-SIZE-LIMIT`) result if exceeded.

### TD-302: Zero-Allocation `gml:posList` Tokenizer
- **Context**: `SigwxMapper.ParsePosListToCoordinates` and `IwxxmSuiteMapper.ParsePosListCoordinates` currently call `posListText.Split(WhitespaceSeparators, StringSplitOptions.RemoveEmptyEntries)` to tokenize space-delimited coordinate sequences (`lat1 lon1 lat2 lon2 ...`).
- **Debt / Limitation**: On large global WAFS SIGWX or QVACI forecasts containing dozens of high-resolution `gml:PolygonPatch` and `gml:CubicSpline` contours, `string.Split` allocates intermediate `string[]` arrays in Gen0 GC heap.
- **Remediation Plan**:
  1. Replace `string.Split` with a single-pass character index scanner (`int start`, `int end`) over `string` (compatible with `.NET Standard 2.0` without requiring external `System.Memory` NuGet dependencies) or conditional compilation (`#if NET8_0_OR_GREATER`) using `ReadOnlySpan<char>` and `double.TryParse(ReadOnlySpan<char>, ...)`.

### TD-303: `BenchmarkDotNet` Performance & Memory Allocation Suite
- **Context**: High-throughput aviation message processors require predictable latency and minimal GC pause time.
- **Debt / Limitation**: No automated performance benchmark project exists in the solution yet.
- **Remediation Plan**:
  1. Create `tests/Iwxxm.Net.Benchmarks/Iwxxm.Net.Benchmarks.csproj` targeting `net8.0` with `[MemoryDiagnoser]`.
  2. Benchmark cold-start schema compilation vs. warm cached `Lazy<XmlSchemaSet>` and `Lazy<XslCompiledTransform>` execution across `WAFS-Example.xml`, `qvaci-Example.xml`, and `collect-A3-1-A3-2.xml`.

### TD-304: Automated CI/CD Pipeline (`.github/workflows/ci-cd.yml`)
- **Context**: Building, testing, verifying deterministic symbol packages (`.snupkg`), and publishing to NuGet.org is currently manual.
- **Debt / Limitation**: No GitHub Actions or Azure DevOps pipeline file is checked into the repository.
- **Remediation Plan**:
  1. Add `.github/workflows/ci-cd.yml` running on `ubuntu-latest` and `windows-latest`:
     - `dotnet tool restore`
     - `dotnet build --configuration Release`
     - `dotnet test --configuration Release --collect:"XPlat Code Coverage"`
     - `dotnet pack --configuration Release -p:ContinuousIntegrationBuild=true`
     - NuGet package signature/metadata verification and tag-triggered release publishing.
