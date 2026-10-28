# Technical Debt & Architectural Backlog (`Iwxxm.Net`)

This document tracks the planned technical debt, architectural enhancements, and roadmap items deferred from the `1.0.0-rc.1` (Phase 1 — Complete WAFS SIGWX 2025-2) release into **Phase 2** and **Phase 3**.

---

## Summary Matrix

| ID | Phase | Category | Title | Priority | Target Release |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **TD-201** | Phase 2 | Architecture / Application | Polymorphic `IIwxxmReport` & `IwxxmReportDispatcher` Router | High | `1.1.0` |
| **TD-202** | Phase 2 | Domain / ACL / XSD | En-Route & Volcanic Hazard Reports (`QVACI`, `VONA`, `VA/TC/SWX Advisory`, `SIGMET`, `AIRMET`) | High | `1.1.0` |
| **TD-203** | Phase 2 | Domain / ACL / XSD | Aerodrome Reports (`METAR`, `SPECI`, `TAF`) | Medium | `1.2.0` |
| **TD-204** | Phase 2 | Infrastructure / SWIM | Collective Bulletin Unwrapping (`collect:MeteorologicalBulletin`) | High | `1.1.0` |
| **TD-205** | Phase 2 | Infrastructure / Schematron | Full Offline RDF/SKOS Code-List Registry Validation (`codes.wmo.int-*.rdf`) | Medium | `1.2.0` |
| **TD-301** | Phase 3 | Security / Hardening | XML DoS & Memory Bound Hardening (`MaxCharactersInDocument`) | High | `1.0.0` (GA) |
| **TD-302** | Phase 3 | Performance / Memory | Zero-Allocation `gml:posList` Tokenizer (Eliminate `string.Split` Gen0 Arrays) | Medium | `1.0.0` (GA) |
| **TD-303** | Phase 3 | Quality / Benchmarks | `BenchmarkDotNet` Performance & Memory Allocation Suite | Medium | `1.0.0` (GA) |
| **TD-304** | Phase 3 | DevOps / CI-CD | Automated GitHub Actions Pipeline (`.nupkg` / `.snupkg` Publishing) | High | `1.0.0` (GA) |

---

## Phase 2: Expansion to Full IWXXM 2025-2 Report Suite & SWIM Collectives

### Current State (Phase 1)
In `1.0.0-rc.1`, `Iwxxm.Net` provides complete XSD validation, ISO Schematron XSLT business rule validation, and Anti-Corruption Layer (ACL) Domain mapping exclusively for **WAFS Significant Weather (`WAFSSignificantWeatherForecast` / `SIGWXReport`)** and all 9 of its meteorological features (`JETSTREAM`, `CLOUD`, `TROPOPAUSE`, `VOLCANO`, `TURBULENCE`, `AIRFRAME_ICING`, `TROPICAL_CYCLONE`, `SANDSTORM`, `RADIATION`).

The remaining 28 official WMO IWXXM 2025-2 sample files in `tests/Iwxxm.Net.Tests/Samples/wmo-examples/` (covering `METAR`, `TAF`, `SIGMET`, `AIRMET`, `VONA`, `QVACI`, `VolcanicAshAdvisory`, `TropicalCycloneAdvisory`, `SpaceWeatherAdvisory`, and `collect:MeteorologicalBulletin`) are currently tested only as negative XSD rejection cases against the SIGWX parser.

### TD-201: Polymorphic `IIwxxmReport` & `IwxxmReportDispatcher` Router
- **Context**: Downstream aviation consumers (SWIM AMHS/JMS message brokers) often receive heterogeneous streams of IWXXM XML documents on a single topic or queue without knowing the report type ahead of time.
- **Debt / Limitation**: `IwxxmSigwxParser` currently assumes the root element is either `<iwxxm:WAFSSignificantWeatherForecast>` or `<iwxxm:SIGWXReport>`.
- **Remediation Plan**:
  1. Introduce a marker interface `IIwxxmReport` in `Iwxxm.Net.Domain.Aggregates` with common metadata (`Id`, `IssueTime`, `ReportStatus`, `PermissibleUsage`).
  2. Implement a fast, non-allocating `XmlReader` root-element inspector (`IwxxmReportDispatcher`) in the Application/Presentation layer that inspects the root local name and namespace URI before dispatching to the specialized use case (`ProcessSigwxReportUseCase`, `ProcessSigmetReportUseCase`, `ProcessMetarSpeciUseCase`, etc.).

### TD-202: En-Route & Volcanic Hazard Reports (`QVACI`, `VONA`, `VA/TC/SWX Advisory`, `SIGMET`, `AIRMET`)
- **Context**: IWXXM 2025-2 introduces Quantitative Volcanic Ash Concentration Information (`QVACI`) alongside Volcano Observatory Notice for Aviation (`VONA`), `VolcanicAshAdvisory`, `TropicalCycloneAdvisory`, `SpaceWeatherAdvisory`, `SIGMET`, `WSIGMET`, `WVAAdvisory`, and `AIRMET`.
- **Debt / Limitation**: The embedded `iwxxm-sigwx.xsd` and `Iwxxm20252.Generated.cs` only include the SIGWX/WAFS subset of the IWXXM 2025-2 schema.
- **Remediation Plan**:
  1. Embed the complete official WMO IWXXM 2025-2 XSD set (`iwxxm.xsd`, `metce.xsd`, `om.xsd`, `sam.xsd`, `sams.xsd`, `sigmet.xsd`, `airmet.xsd`, `vona.xsd`, `qvaci.xsd`, `vaadvisory.xsd`, `tcadvisory.xsd`, `spacewx.xsd`) as `<EmbeddedResource>`.
  2. Run `tools/generate-xsd-classes.sh` (`dotnet xscgen`) across the full schema set to generate DTOs under `Iwxxm20252.Generated`.
  3. Create pure Domain Aggregate Roots and ACL Mappers reusing the existing spatial Value Objects (`GeoCoordinate`, `GeoPolygon`, `FlightLevel`).

### TD-203: Aerodrome Reports (`METAR`, `SPECI`, `TAF`)
- **Context**: Aerodrome routine/special meteorological reports (`METAR`/`SPECI`) and Terminal Aerodrome Forecasts (`TAF`) represent high-volume IWXXM traffic.
- **Debt / Limitation**: Not yet modeled in the Domain layer.
- **Remediation Plan**:
  1. Create `MetarReport`, `SpeciReport`, and `TafReport` Aggregate Roots in `Iwxxm.Net.Domain.Aggregates`.
  2. Model runway visual range (RVR), prevailing visibility, surface wind, present weather codes (`http://codes.wmo.int/306/4678/`), and change groups (`TEMPO`, `BECMG`, `PROB30`/`PROB40`) as immutable Value Objects.

### TD-204: SWIM / AMHS Collective Bulletin Unwrapping (`collect:MeteorologicalBulletin`)
- **Context**: In operational ICAO EUR/NAT and FAA SWIM networks, individual IWXXM reports are frequently bundled inside a `<collect:MeteorologicalBulletin>` envelope (`http://def.wmo.int/collect/2014`), as demonstrated in `tests/Iwxxm.Net.Tests/Samples/wmo-examples/collect-example.xml`.
- **Debt / Limitation**: Passing a `collect:MeteorologicalBulletin` directly to `IwxxmSigwxParser` currently fails XSD validation because the root element is `MeteorologicalBulletin` rather than a single report.
- **Remediation Plan**:
  1. Embed `collect.xsd` (`http://def.wmo.int/collect/2014`).
  2. Add `IwxxmBulletinParser.ParseBulletin(Stream)` returning `Result<IReadOnlyList<Result<IIwxxmReport>>>`, allowing partial success when one report inside a multi-report bulletin fails validation while sibling reports succeed.

### TD-205: Full Offline RDF/SKOS Code-List Registry Validation
- **Context**: Official WMO `iwxxm.sch` rules use `document('codes.wmo.int-49-2-*.rdf')` to check `xlink:href` attributes against offline RDF/SKOS files.
- **Debt / Limitation**: In Phase 1, `iwxxm-2025-2-sigwx.xslt` validates the 4 WAFS BUFR code tables (`0-20-008`, `0-20-012`, `0-11-030`, `0-20-041`) and nil reasons directly via XPath rules without external `document()` calls, because `XsltSettings.Default` disables external `XmlResolver` for security.
- **Remediation Plan**:
  1. Implement a custom, air-gapped `XmlUrlResolver` (`EmbeddedRdfXmlResolver`) in `Iwxxm.Net.Infrastructure.Validation` that intercepts `document('codes.wmo.int-*.rdf')` requests during `XslCompiledTransform` execution and resolves them strictly from `<EmbeddedResource>` streams, blocking all network/filesystem I/O.

---

## Phase 3: Production Hardening, Zero-Allocation Performance & CI/CD

### TD-301: XML DoS & Memory Bound Hardening
- **Context**: `XsdValidator` and `XmlSerializerSigwxDeserializer` already enforce `DtdProcessing = DtdProcessing.Prohibit` and `XmlResolver = null` to prevent XXE (XML External Entity) and Billion Laughs attacks. However, `ProcessSigwxReportUseCase` buffers the input stream into a `MemoryStream` and does not yet enforce an upper bound on document character count.
- **Debt / Limitation**: An untrusted multi-gigabyte XML stream could cause excessive memory pressure (LOH allocations) before XSD validation completes.
- **Remediation Plan**:
  1. Set `MaxCharactersInDocument = 10_485_760` (10 MB configurable via `IwxxmParserOptions`) and `MaxCharactersFromEntities = 1024` on all `XmlReaderSettings` instances.
  2. Guard `CopyToSeekableMemoryStream` in `ProcessSigwxReportUseCase` with a configurable maximum byte limit, returning an `ErrorCategory.MalformedXml` (`XML-SIZE-LIMIT`) result if exceeded.

### TD-302: Zero-Allocation `gml:posList` Tokenizer
- **Context**: `SigwxMapper.ParsePosListToCoordinates` currently calls `posListText.Split(WhitespaceSeparators, StringSplitOptions.RemoveEmptyEntries)` to tokenize space-delimited coordinate sequences (`lat1 lon1 lat2 lon2 ...`).
- **Debt / Limitation**: On large global WAFS SIGWX forecasts containing dozens of high-resolution `gml:PolygonPatch` and `gml:CubicSpline` contours, `string.Split` allocates intermediate `string[]` arrays in Gen0 GC heap.
- **Remediation Plan**:
  1. Replace `string.Split` in `SigwxMapper.ParsePosListToCoordinates` with a single-pass character index scanner (`int start`, `int end`) over `string` (compatible with `.NET Standard 2.0` without requiring external `System.Memory` NuGet dependencies) or conditional compilation (`#if NET8_0_OR_GREATER`) using `ReadOnlySpan<char>` and `double.TryParse(ReadOnlySpan<char>, ...)`.

### TD-303: `BenchmarkDotNet` Performance & Memory Allocation Suite
- **Context**: High-throughput aviation message processors require predictable latency and minimal GC pause time.
- **Debt / Limitation**: No automated performance benchmark project exists in the solution yet.
- **Remediation Plan**:
  1. Create `tests/Iwxxm.Net.Benchmarks/Iwxxm.Net.Benchmarks.csproj` targeting `net8.0` with `[MemoryDiagnoser]`.
  2. Benchmark cold-start schema compilation vs. warm cached `Lazy<XmlSchemaSet>` and `Lazy<XslCompiledTransform>` execution across `WAFS-Example.xml`.

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
