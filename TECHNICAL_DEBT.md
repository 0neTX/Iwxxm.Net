# Technical Debt & Architectural Backlog (`Iwxxm.Net`)

Single source of truth for technical debt. Section 11 keeps the historical Phase 2/3 backlog (TD-2xx/TD-3xx) with statuses re-assessed by the audit below.

## Audit

Architecture and conformance audit of `Iwxxm.Net` (baseline commit `88b2ba1`) against the official WMO IWXXM 2025-2 reference repository ([wmo-im/iwxxm](https://github.com/wmo-im/iwxxm)).

- **Scope**: `src/Iwxxm.Net` (Domain, Application, Infrastructure, Presentation, Resources), tests, benchmarks, build, packaging, Dockerfile, CI/CD.
- **Method**: static review of every source file, cross-check against official XSDs (`IWXXM/*.xsd`), Schematron rules (`IWXXM/rule/iwxxm.sch`), WMO code-list RDFs (`IWXXM/rule/*.rdf`) and the official examples (`IWXXM/examples/*.xml` + `.tac`), plus a containerised build/test run (`podman build --target test`).
- **Severity scale**: Critical (incorrect data or false conformance in a safety-related domain) · High (silent data loss, security/release risk) · Medium (correctness/robustness gaps) · Low (hygiene, documentation, ergonomics).

---

## 1. Executive Summary

The codebase is well structured and the pipeline is green (0 warnings, 82/82 tests, 73.4 % line / 55.2 % branch coverage, valid `.nupkg` / `.snupkg`). However, the library **claims IWXXM conformance it does not provide**:

- The 29 test samples are byte-identical to the official WMO examples, but the embedded XSDs, Schematron and code lists are hand-written subsets shaped around exactly those 29 documents. Outside of them, validation guarantees little in either direction.
- Several WMO code tables are decoded to the wrong meaning, and units of measure are assumed instead of read. Both produce **wrong values in a safety-related domain**.

| Area | Official WMO 2025-2 | Embedded (baseline) |
| :--- | :--- | :--- |
| XSD complex types | 197 | 85 |
| XSD enumeration / pattern facets | ~200 | 0 |
| Schematron rules (`sch:pattern`) | 165 | 11 official IDs + invented IDs |
| Code-list concepts | 690 (20 RDF files) | 84 (1 RDF file) |

## 2. Findings Matrix

| ID | Severity | Area | Title | Status |
| :--- | :--- | :--- | :--- | :--- |
| AUD-C1 | Critical | Domain | WMO BUFR code tables decoded to wrong meanings | Resolved (Unreleased) |
| AUD-C2 | Critical | Mapping | Units of measure (`uom`) ignored for speeds and heights | Resolved (Unreleased) |
| AUD-C3 | Critical | Validation | Embedded "official" XSDs, Schematron and code lists are hand-written subsets | Partially resolved (Unreleased): official XSDs and code lists embedded; Schematron still an XSLT 1.0 subset (11/165 rules) |
| AUD-H1 | High | Mapping | `gml:CircleByCenterPoint` geometries silently dropped | Open |
| AUD-H2 | High | Mapping | Local `xlink:href="#id"` references never resolved | Open |
| AUD-H3 | High | Domain/Mapping | AIRMET cancellation and most AIRMET content not modelled | Open |
| AUD-H4 | High | Mapping | METAR/SPECI trend surface wind always lost | Open |
| AUD-H5 | High | Mapping/Domain | Missing values replaced by invented defaults | Open |
| AUD-H6 | High | Validation | TAF Schematron rule can never fire (XPath 1.0 `!=` semantics) | Resolved (Unreleased) |
| AUD-H7 | High | DevOps | Local CI script can publish a package accidentally | Open |
| AUD-H8 | High | Packaging | 48 `Iwxxm20252.Generated` types leak as public API | Resolved (Unreleased) |
| AUD-M1 | Medium | GeoJSON | RFC 7946 right-hand rule not enforced | Open |
| AUD-M2 | Medium | GeoJSON | Antimeridian crossings not split; naive bbox | Open |
| AUD-M3 | Medium | GeoJSON | `NaN` written as a bare token (invalid JSON) | Open |
| AUD-M4 | Medium | Presentation | Exceptions escape the `Result` contract | Open |
| AUD-M5 | Medium | Presentation | `Parse(string)` ignores the declared XML encoding | Open |
| AUD-M6 | Medium | Domain | Equality / hash-code contracts broken in value objects | Partially resolved (`JetStream` exact equality); `GeoCoordinate`, `GeoPolygon` open |
| AUD-M7 | Medium | Domain | Vertical limits modelled lossily (no reference, nil reason, unit) | Open |
| AUD-M8 | Medium | Domain | Report metadata stringly typed, silently defaulted, incomplete | Open |
| AUD-M9 | Medium | Mapping | Time context flattened (OBS/FCST, VAA +6/+12/+18 HR, trend indicators) | Open |
| AUD-M10 | Medium | Mapping | CRS / axis order / dimension assumed, never read | Open |
| AUD-M11 | Medium | Mapping | TAC qualifiers and NSW/NSC semantics dropped | Open |
| AUD-M12 | Medium | Mapping | One bad element fails the whole bulletin / WAFS chart | Open |
| AUD-M13 | Medium | Mapping | SIGWX geometry fidelity (segment order, patches, holes) | Open |
| AUD-M14 | Medium | Validation | SVRL memory amplification and misreported error category | Open |
| AUD-M15 | Medium | Validation | Unknown-root detection depends on English exception text | Resolved (Unreleased) |
| AUD-M16 | Medium | Generation | "Generated" code is hand-written; regeneration not reproducible | Open |
| AUD-M17 | Medium | Testing | `netstandard2.0` / .NET Framework never tested | Open |
| AUD-M18 | Medium | Build | SDK and analyzer drift (`latestMajor`, prerelease, `AnalysisLevel=latest`) | Open |
| AUD-M19 | Medium | CI/CD | Over-broad permissions and fragile versioning | Open |
| AUD-M20 | Medium | Testing | Architecture checks easy to bypass | Open |
| AUD-M21 | Medium | Testing | Test-quality gaps (weak assertions, untested public APIs) | Open |
| AUD-L1..L14 | Low | Various | See section 6 (AUD-L7 resolved: exact resource lookup) | Open |
| AUD-U1..U8 | Improvement | API / DX | See section 7 | Open |
| AUD-A1 | Medium | Architecture | Naming, Clean Code and Hexagonal deviations | Partially resolved (Unreleased): section 8.3 (Hexagonal) resolved; sections 8.1 (naming) and 8.2 (Clean Code) open |
| AUD-N1 | Critical (candidate) | Validation | `XDocument.Validate` recursion: deeply nested input may cause an uncatchable `StackOverflowException` (regression from streaming validation) | Open — under review |
| AUD-N2 | Medium | Validation | Shared `XmlSchemaSet` used by concurrent `XDocument.Validate` calls; thread safety unverified, no concurrency test | Open — under review |
| AUD-N3 | Medium | Validation | Full DOM materialised before XSD validation (higher memory than streaming); compile exceptions cached by `Lazy` | Open |
| AUD-N4 | Low | Mapping | Per-field official unit subsets (e.g. wind `m/s`/`[kn_i]`, cloud base `m`/`[ft_i]`) documented but not enforced by `UomMeasureReader` | Open |
| AUD-S1 | High | Validation | Official Schematron (`iwxxm.sch`, XSLT 2.0) not executable without an XSLT 2.0+ engine | Resolved (Unreleased): optional `Iwxxm.Net.Validation.Saxon` package (SaxonCS-HE 13.1.0, `net10.0`) behind the public `ISchematronValidator` port; the default `netstandard` configuration still uses the XSLT 1.0 subset (see AUD-C3) |

---

## 3. Critical Findings

### AUD-C1 — WMO BUFR code tables decoded to wrong meanings

- **Where**: `src/Iwxxm.Net/Domain/ValueObjects/WmoCodes.cs:85-157`.
- **Evidence** (verified against `IWXXM/rule/codes.wmo.int-bufr4-codeflag-*.rdf`, `dcterms:description`):

  | Table | Code | Official meaning | Baseline mapping |
  | :--- | :--- | :--- | :--- |
  | 0-11-030 (turbulence) | 8 | Nil | `LightClearAir` |
  | 0-11-030 | 10 / 11 | Moderate / Severe, cloud or clear air not specified | `ModerateClearAir` / `SevereClearAir` |
  | 0-11-030 | 19 | Moderate, occasional severe | `Extreme` |
  | 0-11-030 | 20 | Moderate, frequently severe | `ExtremeClearAir` |
  | 0-11-030 | 4-7, 9, 12-18 | (various) | `Unknown` |
  | 0-20-041 (icing) | 3 | Light icing in precipitation | `Moderate` |
  | 0-20-041 | 10-12 | Trace icing variants | `Unknown` |
  | 0-20-008 (cloud distribution) | 13 / 14 / 15 / 16 | Dense / Layers / Obscured / Embedded | `FrequentEmbedded` / `Dense` / `DenseEmbedded` / `Layers` |

- **Failure scenario**: the official `WAFS-Example.xml` uses `0-20-041/3` (light) and the API reports `IcingDegreeCode.Moderate`; a `0-11-030/19` report is surfaced as *Extreme* turbulence.
- **Fix**: derive the mappings from the official RDF tables, keep the raw code on the value object, add one test per official concept.

### AUD-C2 — Units of measure (`uom`) ignored for speeds and heights

- **Where**: `src/Iwxxm.Net/Infrastructure/Mapping/IwxxmSuiteMapper.cs:1143-1146` (surface wind), `:1162` (cloud base), `:1215-1216` and `:1239-1240` (speed of motion), `:826` / `:846` (TC max wind), `:737` (VAA source elevation); `SigwxMapper.cs:736-741` (`ConvertSpeedToKnots`).
- **Evidence**: `uom` is only read for flight levels (`IwxxmSuiteMapper.cs:1014`). Official `speci-A3-2.xml:55` contains `<iwxxm:meanWindSpeed uom="[kn_i]">25.0</iwxxm:meanWindSpeed>`, stored verbatim in `SurfaceWindInfo.MeanSpeedMetersPerSecond`. `iwxxm.sch` allows `m/s` or `[kn_i]` for wind, `km/h` or `[kn_i]` for speed of motion, `m` or `[ft_i]` for cloud base.
- **Failure scenario**: SPECI A3-2 reports 25 m/s (≈ 49 kt) instead of 25 kt; "MOV E 40KMH" becomes 40 kt; a cloud base in metres lands in `BaseFeet`; SIGWX treats `km/h` as knots.
- **Fix**: read `@uom` everywhere, convert explicitly to the unit implied by the domain property, fail on missing or unknown units.

### AUD-C3 — Embedded "official" XSDs, Schematron and code lists are hand-written subsets

- **Where**: `src/Iwxxm.Net/Resources/Xsd/*`, `Resources/Schematron/iwxxm-2025-2-sigwx.xslt`, `Resources/Codelists/codes.wmo.int-49-2.rdf`; claims in `Iwxxm.Net.csproj:37`, `README.md:58-63`, `CHANGELOG.md`.
- **Evidence**:
  - XSD: 85 vs 197 complex types; 0 facets; no substitution groups or abstract elements.
  - `gml.xsd:42-49`: `MeasureType` is `xs:string` with an optional `uom`.
  - `gml.xsd:65-74`: `posList` is `xs:string` (official: `doubleList`).
  - `gml.xsd:34`: `gml:id` optional.
  - `iwxxm-2025-2.xsd:20-21`: `reportStatus` / `permissibleUsage` have defaults; official `common.xsd:19,24` marks them `use="required"`.
  - `iwxxm-2025-2.xsd:230-246`: no `recentWeather`, `windShear`, `seaCondition`, `extension`; temperatures and QNH not nillable.
  - `iwxxm-sigwx.xsd:374`: defines a `SIGWXReport` root that exists in no official schema.
  - Schematron: 11 of 165 official rule IDs implemented; METAR/SPECI (43), AIRMET (20), SIGMET (15), TAF (14), VAA (14), TCA (10) and SWX (5) rules absent. Non-official IDs presented alongside official ones (`IWXXM.RdfCodeListRegistry-1`, `METAR_SPECI.ObservationMandatory-1`, `TAF.BaseForecastMandatory-1`, `VONA.VONA-1`, `QVACI.*`, `IWXXM-SIGWX-00x`).
  - Code lists: 84 of 690 concepts; `ValidateWmoRdfCodeList` (`xslt:58-72`) rejects every `http://codes.wmo.int/` href not in those 84.
- **Failure scenarios**:
  - Invalid accepted: `<airTemperature>abc</airTemperature>`; reports without `reportStatus` / `permissibleUsage`.
  - Valid rejected: METAR with `recentWeather`, `windShear`, `xsi:nil` temperature or `extension`; any present-weather code outside the 84 (e.g. `-SHRA`); every Space Weather phenomenon code.
- **Fix**: embed the unmodified official schema tree with an air-gapped URL resolver; embed the 20 official code lists; document the real Schematron coverage (the official `iwxxm.sch` uses `queryBinding="xslt2"`, which `XslCompiledTransform` cannot execute without a third-party XSLT 2.0 engine).

---

## 4. High Findings

### AUD-H1 — `gml:CircleByCenterPoint` silently dropped
- **Where**: `IwxxmSuiteMapper.cs:1347-1368` only reads `posList`; generated `CurveSegmentArrayPropertyType` (`Iwxxm20252.Generated.cs:219-225`) only knows LineStringSegment and CubicSpline.
- **Failure**: "WI 250NM OF TC CENTRE" (`sigmet-A6-2-TC.xml:102`), the TCA CB area (`tc-advisory-A2-2.xml:65`) and SWX DAYSIDE/NIGHTSIDE (`spacewx-A7-3.xml:171`) vanish; in SIGWX a circle raises `ACL-CURVE-EMPTY` and fails the whole chart.
- **Fix**: model circles (centre + radius + uom) or densify to polygons; at minimum emit a warning.

### AUD-H2 — Local `xlink:href="#id"` references never resolved
- **Where**: `IwxxmSuiteMapper.cs:909-950` and all time/feature lookups.
- **Failure**: TAF A5-1 base forecast loses its validity (`taf-A5-1.xml:49`); SWX A7-4 "+6 HR" forecast is lost (`spacewx-A7-4.xml:114-115`); QVACI feature times lost; `nothingOfOperationalSignificance` analyses indistinguishable from missing data.
- **Fix**: build a `gml:id → XElement` index per document and resolve local references; emit one analysis per `SpaceWeatherAnalysis` with its nil reason.

### AUD-H3 — AIRMET cancellation and content not modelled
- **Where**: `IwxxmSuiteMapper.cs:506-563`; `EnRouteHazardReports.cs:195,251`.
- **Evidence**: `airmet.xsd:94,103,124` (`cancelledReportSequenceNumber`, `cancelledReportValidPeriod`, `isCancelReport`) and `airmet.xsd:165-218` (`cloudBase`, `cloudTop`, `surfaceVisibility`, `surfaceVisibilityCause`, `surfaceWindDirection`, `surfaceWindSpeed`) are not mapped.
- **Failure**: an AIRMET CNL decodes as an active AIRMET; BKN CLD / SFC VIS / SFC WIND content lost.

### AUD-H4 — METAR/SPECI trend surface wind always lost
- **Where**: `IwxxmSuiteMapper.cs:1137-1138` accepts only `AerodromeSurfaceWind` / `AerodromeSurfaceWindForecast`; trend uses `AerodromeSurfaceWindTrendForecast` (`common.xsd:226-228`, `metarSpeci.xsd:170`).
- **Fix**: add the missing element name (one-line change).

### AUD-H5 — Missing values replaced by invented defaults
- `issueTime` → `DateTimeOffset.MinValue` when missing/unparseable/by reference (`IwxxmSuiteMapper.cs:234,350,419…`).
- SIGMET/AIRMET validity `?? issueTime` (`:493-494`, `:557-558`).
- QVACI density defaults to `0.0` (`:692`); volcano name defaults to `"UNKNOWN"`.
- `reportStatus` / `permissibleUsage` default to `NORMAL` / `OPERATIONAL` in every `Create` (e.g. `AerodromeReports.cs:239`) — can turn a TEST message into an operational one. `MeteorologicalBulletin.cs:21-22,81` hard-codes metadata and `MinValue`.
- **Fix**: return an error or keep the field nullable.

### AUD-H6 — TAF Schematron rule can never fire
- **Where**: `iwxxm-2025-2-sigwx.xslt:325` — `(@isCancelReport != 'true')` is false in XPath 1.0 when the attribute is absent, so a TAF without `baseForecast` passes.
- **Fix**: `not(@isCancelReport = 'true')`.

### AUD-H7 — Local CI can publish a package accidentally
- **Where**: `tools/run-local-ci.sh:8,29`; `Dockerfile:83-100`.
- **Evidence**: the script forwards `GITHUB_TOKEN` to the `ci` container, whose `CMD` runs `dotnet nuget push` whenever the token is non-empty; version defaults to `1.0.0-rc.1` (collides with CI run #1, hidden by `--skip-duplicate`); `COPY . .` includes uncommitted changes and `.git`.
- **Fix**: publish only from CI; if a local push is required, use an explicit flag, a dedicated variable and refuse dirty trees.

### AUD-H8 — Generated XSD types leak as public API
- **Where**: `Infrastructure/Generated/Iwxxm20252.Generated.cs` (48 public types in namespace `Iwxxm20252.Generated`).
- **Evidence**: a consumer project compiled `new Iwxxm20252.Generated.PointType()` against the packaged DLL; `CleanArchitectureComplianceTests.cs:103` only inspects `Iwxxm.Net.Infrastructure*`.
- **Fix**: make them `internal`; extend the architecture test.
- **Status**: Resolved (Unreleased). All 48 types (and `XmlNamespaces`) are `internal`. Because `XmlSerializer` only processes public types ("is inaccessible due to its protection level"), SIGWX deserialization now uses the internal reflection-based `AttributeDrivenXmlReader`, which honours the same `XmlElement`/`XmlAttribute`/`XmlText`/`XmlIgnore` metadata and the `{Member}Specified` convention. `HexagonalArchitectureTests.GeneratedTransportModel_IsNotPublic` and `PublicTypes_LiveOnlyInRoleBasedNamespaces` check it at IL level.

### AUD-S1 — Official Schematron not executable on the BCL
- **Where**: `Resources/Schematron/iwxxm-2025-2-sigwx.xslt` (XSLT 1.0 subset, 11 of 165 rule IDs); official `IWXXM/rule/iwxxm.sch` uses `queryBinding="xslt2"`.
- **Status**: Resolved (Unreleased) with the optional package `Iwxxm.Net.Validation.Saxon` (`net10.0`), registered via `IwxxmParserBuilder.UseOfficialSchematron()`. The core package keeps zero dependencies and its default subset (AUD-C3 stays partially resolved for the default configuration).
  - Runs the byte-identical official `iwxxm.sch` (SHA-256 `e13f5c48…7179`, checked by a test); no rule is rewritten. Compiled once per process with the vendored SchXslt 1.10.1 (MIT, `2.0/pipeline-for-svrl.xsl`), cached as an immutable Saxon `XsltExecutable`; one transformer per validation.
  - Air-gapped: every URI Saxon dereferences is served from an embedded catalog (SchXslt modules, `iwxxm.sch`, the official RDFs embedded in `Iwxxm.Net`, base URIs under the reserved `.invalid` TLD); anything else is refused (no fallback).
  - SVRL mapping: `Code` = assert `@id` or the preceding `svrl:active-pattern/@id` (official rule id), `BusinessRuleViolation`, severity from `@role`/`@flag` (official rules declare none → `Error`), `Location` = SVRL XPath.
  - Results: all 29 official examples pass, standalone and through the full pipeline (also the SIGWX pipeline for `WAFS-Example.xml`). Negative tests fire `METAR_SPECI.MeteorologicalAerodromeObservation-5` (qnh not in `hPa`), `SIGMET.SIGMET-3`, `TAF.TAF-6`, `TAF.TAF-7` and `Common.CloudLayer.amount` (code list, offline). Finding on the official rules: an ordinary TAF with no `iwxxm:baseForecast` element at all is not reported by `TAF.TAF-5`, because `iwxxm:baseForecast/@nilReason != '…/missing'` is false for an empty sequence in XPath 2.0 (characterised by a test).
  - Licensing: the SaxonCS-HE 13.1.0 nupkg `LICENSE.txt` states it is distributed by Saxonica Ltd as an "executable form" of the open-source SaxonJ-HE under the Mozilla Public License 2.0; the package targets `lib/net10.0` only and depends on AngleSharp, DotNet.Bundle, J2N, Microsoft.AspNetCore.StaticFiles, Microsoft.Extensions.Configuration(.Json/.Xml), NSec.Cryptography, Singulink.Numerics.*, System.IO.Hashing, XmlResolver and XmlResolverData. `new Processor(false)` (HE capabilities) runs without any license key; a test asserts edition `HE`.
- **Residual**: SVRL is materialised in memory per validation (AUD-M14 still applies to large documents); first compilation costs roughly a second (`OfficialSchematronValidator.Warmup()`); compile failures are cached by `Lazy` (as in AUD-N3).

---

## 5. Medium Findings

| ID | Where | Problem | Recommended fix |
| :--- | :--- | :--- | :--- |
| AUD-M1 | `Rfc7946GeoJsonExporter.cs:560-569` | Rings written in source order; `sigmet-VA-EGGX.xml` exterior is clockwise → spherical renderers (d3-geo) draw the complement. | Compute signed area; exterior CCW, holes CW. |
| AUD-M2 | `Rfc7946GeoJsonExporter.cs:587-602` | No antimeridian splitting; bbox is plain min/max. | Split per RFC 7946 §3.1.9; west > east bbox per §5.2. |
| AUD-M3 | `IwxxmSuiteMapper.cs:994`, `Rfc7946GeoJsonExporter.cs:803` | `NaN` accepted and emitted as bare `NaN`. | Reject non-finite values or write `null`. |
| AUD-M4 | `IwxxmParser.cs:96,104,190,204,216,228`; `IwxxmSigwxParser.cs:102`; `DispatchIwxxmReportUseCase.cs:90-108` | `FileStream` / exporter / pipeline exceptions escape although README promises a non-throwing API. | Wrap I/O and each pipeline step; map to error codes; document argument exceptions. |
| AUD-M5 | `IwxxmParser.cs:80,167`; `IwxxmSigwxParser.cs:79` | String converted to UTF-8 bytes and read as a byte stream → `encoding="UTF-16"` fails, `ISO-8859-1` produces mojibake. | Use a `TextReader` path (`StringReader`). |
| AUD-M6 | `GeoCoordinate.cs:59,67`; `JetStream.cs:112`; `GeoPolygon.cs:97` | Tolerance equality with non-tolerant hash; non-transitive; holes compared by count only. | Quantize once and compare exactly; compare ring contents. |
| AUD-M7 | `FlightLevel.cs:15,44-46`; `IwxxmSuiteMapper.cs:1008-1016` | Single `int`; no SFC/MSL/STD reference, no nil reason; metres AMSL turned into FL ("15KM AMSL" → FL492); `UNL` / unknown units → `null`. | Introduce a `VerticalLimit` value object (value, unit, reference, nil reason). |
| AUD-M8 | `IIwxxmReport.cs:51,56`; `MeteorologicalBulletin.cs` | Status/usage as strings; missing `permissibleUsageReason`, `permissibleUsageSupplementary`, translation metadata; bulletin metadata forced onto `IIwxxmReport`. | Enums; fail on missing; add fields; segregate interfaces. |
| AUD-M9 | `IwxxmSuiteMapper.cs:432-467,757-770,1168-1209` | SIGMET `phenomenonTime` / `@timeIndicator`, VAA forecast times and `@status`, METAR trend `timeIndicator` not read. | Model time context per element. |
| AUD-M10 | `IwxxmSuiteMapper.cs:1334,1357` | `srsName`, `axisLabels`, `srsDimension` ignored; dimension hard-coded to 2. | Read CRS metadata; swap axes when needed; support 3-D. |
| AUD-M11 | `IwxxmSuiteMapper.cs:1151-1153,1186-1192` | RVR/wind operators, extreme wind directions, minimum visibility, vertical visibility, TAF TX/TN not read; NSW/NSC indistinguishable from "not reported". | Map the elements; preserve nil reasons. |
| AUD-M12 | `IwxxmSuiteMapper.cs:211-214`; `SigwxMapper.cs:145-148` | One invalid child fails the whole bulletin / chart. | Partial success with per-item errors. |
| AUD-M13 | `SigwxMapper.cs:648-660,677` | Mixed segment types reordered; only first `PolygonPatch`; interior rings lost. | Preserve segment order; all patches; holes. |
| AUD-M14 | `XsltSchematronValidator.cs:71,111-118`; `XsdValidator.cs:116-133` | Unbounded SVRL buffer (~10x input) then parsed with the *input* limit → `XML-MALFORMED` and lost findings; XSD errors uncapped. | Stream SVRL through a bounded reader; cap error count. |
| AUD-M15 | `XsdValidator.cs:118-121` | Unknown root detected by matching the English message "Could not find schema information…"; localized runtimes return zero errors. | Look up the root QName in `schemaSet.GlobalElements`. |
| AUD-M16 | `Iwxxm20252.Generated.cs:1-6`; `tools/generate-xsd-classes.sh:27-30`; `Iwxxm.Net.csproj:69` | Header claims xscgen output but code is hand-written (`List<T>`, hand logic, no `GeneratedCodeAttribute`); script and MSBuild target use different inputs/options; target runs once per TFM. | Single generation script + CI drift check. |
| AUD-M17 | `Iwxxm.Net.Tests.csproj:4`; `ci-cd.yml:20`; `Dockerfile:7` | Tests target `net8.0` only and run on .NET 9 via `DOTNET_ROLL_FORWARD=Major`; `netstandard2.0` / .NET Framework never exercised. | Multi-target tests (`net9.0;net48`), drop global roll-forward. |
| AUD-M18 | `Dockerfile:6`; `global.json`; `Directory.Build.props:10-12` | Floating `sdk:9.0`, `rollForward: latestMajor`, `allowPrerelease: true`, `AnalysisLevel=latest` + warnings-as-errors. | Pin image/SDK, `latestFeature`, no prerelease, fixed `AnalysisLevel`. |
| AUD-M19 | `ci-cd.yml:14,18,114` | Workflow-wide `packages: write`; publish on every `main` push; `run_number` versioning + `--skip-duplicate`; hard-coded release notes. | Least-privilege per job; tag-based versioning (MinVer / Nerdbank.GitVersioning). |
| AUD-M20 | `Dockerfile:30`; `CleanArchitectureComplianceTests.cs:110-118` | Grep misses indented/aliased/fully-qualified usings; reflection test ignores generic arguments and method bodies. | NetArchTest / ArchUnitNET (IL-level). |
| AUD-M21 | `IwxxmSuiteParserTests.cs:39,273-285` | 29-sample theory asserts only `IsSuccess` + Id; size-limit test cannot distinguish limit from malformed XML; no XXE/DOCTYPE tests; `ToGeoJson`, `CreateProcessor`, `ParseBulletin(Stream)`, `ParseAs(...)`, `GmlCoordinateTokenizer` and thread safety untested. | Property-level and negative tests; concurrency test. |

---

## 6. Low Findings

| ID | Topic | Detail |
| :--- | :--- | :--- |
| AUD-L1 | Tokenizer | "Zero-allocation" false on `netstandard2.0` (`Substring` fallback, `GmlCoordinateTokenizer.cs:256`); fast path not correctly rounded (~0.6 % of values 1 ulp off); `char.IsWhiteSpace` accepts non-XML whitespace; `ParseSinglePoint` ignores extra tokens. |
| AUD-L2 | Rings | `IwxxmSuiteMapper.cs:1363` does not de-duplicate shared vertices between curve members. |
| AUD-L3 | Unmapped content | TC SIGMET `supplementaryAnalysisCollection`, VA SIGMET `volcanicAshMovedToFIR`, SIGMET `cancelledReportValidPeriod`, TCA movement/intensity change, VAA `eruptionDate`; SIGWX `VolcanicAshCloudConcentration`, `activityStatus`, `ashDensityEqualsOrExceeds`, `sourceElevation`. |
| AUD-L4 | SIGWX | Unknown phenomena and by-reference features skipped silently (`SigwxMapper.cs:80-84`); tropopause vertices invented as "CONTOUR" spot heights (`:360-374`). |
| AUD-L5 | Performance | SIGWX parsed twice (XDocument + XmlSerializer); bulletin SIGWX re-serialized to a `MemoryStream`; RDF re-parsed on every `Transform`. |
| AUD-L6 | Lazy caching | `Lazy<>` caches compile exceptions forever. |
| AUD-L7 | Resolver | Case-insensitive suffix match can pick the wrong resource; `FileNotFoundException` lists all embedded names. |
| AUD-L8 | Schematron | SIGWX-002 tests `< 41.0` m/s but message says 41.1 (80 kt = 41.16 m/s); SVRL locations are not real XPaths; warnings and `successful-report` fail the whole parse. |
| AUD-L9 | GeoJSON | `G15` does not round-trip doubles (use `R`/`G17`); 3-position holes accepted; `null` in string list → `NullReferenceException` (`:845`); U+2028/2029 not escaped. |
| AUD-L10 | Domain | Aerodrome/en-route "value objects" use reference equality; inconsistent invariants (empty AIRMET ATS unit allowed, `ValidUntil > ValidFrom` only checked for SIGWX, `*Utc` offsets not checked). |
| AUD-L11 | Dockerfile | `nuget.config` not copied before restore; unnecessary `dotnet tool restore`; `.git` not in `.dockerignore`. |
| AUD-L12 | Packaging | `GeneratePackageOnBuild=true` packs on every build (`Iwxxm.Net.csproj:11`). |
| AUD-L13 | Documentation | `XML-SIZE-LIMIT-EXCEEDED` vs actual `XML-SIZE-LIMIT` (`README:573`); ".NET 8.0 SDK container" vs `sdk:9.0` (`README:645`); "never throws" claim; CHANGELOG lists non-existent `collect-A3-1-A3-2.xml`; benchmark command lacks `-- --filter '*'`. |
| AUD-L14 | Dependencies / CI | Outdated test packages (Test.Sdk 17.10 → 18.x, xunit 2.8.1 → 2.9.3, runner 2.8.1 → 4.x, coverlet 6.0.2 → 10.x, BenchmarkDotNet 0.14 → 0.15); SourceLink package redundant on SDK ≥ 8; actions pinned by tag, no NuGet cache, coverage not uploaded. |

---

## 7. Usability Improvements

| ID | Improvement |
| :--- | :--- |
| AUD-U1 | Public facade in the root `Iwxxm.Net` namespace instead of `Presentation` / `Application.Ports` / `Domain.Common`; ship an `AddIwxxm(options)` DI extension. |
| AUD-U2 | Async API: `ParseAsync(Stream, CancellationToken)`; share one bounded-buffer helper between use cases. |
| AUD-U3 | Make validation ports public/pluggable so consumers can swap or disable Schematron. |
| AUD-U4 | Partial results for bulletins (successful reports + per-item errors). |
| AUD-U5 | Separate warnings from errors; succeed with warnings. |
| AUD-U6 | Richer `Result<T>`: `Map`, `Bind`, `Match`, `TryGetValue`. |
| AUD-U7 | `VerticalLimit` value object (value, unit, reference, nil reason) with FL as a derived view. |
| AUD-U8 | Enums for `reportStatus` / `permissibleUsage` and full translation metadata. |

## 8. Naming, Clean Code & Hexagonal Architecture

### 8.1 Is the model naming driven by the XSD?

Only partially, and inconsistently. The **Infrastructure** layer (`Iwxxm20252.Generated`, mappers) mirrors the XSD vocabulary, which is correct for an anti-corruption layer. The **Domain** layer uses its own names, which is also correct in DDD (ubiquitous language), but they are neither consistently aligned with the official IWXXM / ICAO Annex 3 vocabulary nor consistent among themselves:

| Official IWXXM element | Domain type (Current) | Target Operational Acronym | Broken Principle & Observation |
| :--- | :--- | :--- | :--- |
| `METAR` (`MeteorologicalAerodromeObservation`) | `AerodromeWeatherReport` | `MetarReport` (or `Metar`) | **DDD Ubiquitous Language**: Invented generic name combining METAR/SPECI; lacks operational clarity. |
| `SPECI` (`MeteorologicalAerodromeObservation`) | `AerodromeWeatherReport` | `SpeciReport` (or `Speci`) | **DDD Ubiquitous Language**: Distinct operational aggregate collapsed into METAR container. |
| `TAF` | `TafReport` | `TafReport` (or `Taf`) | Consistent acronym. |
| `SIGMET` / `AIRMET` | `SigmetReport` / `AirmetReport` | `SigmetReport` / `AirmetReport` | Consistent acronym. |
| `VolcanoObservatoryNoticeForAviation` | `VonaReport` | `VonaReport` (or `Vona`) | Consistent acronym. |
| `QuantitativeVolcanicAshConcentrationInformation` | `QvaciReport` | `QvaciReport` (or `Qvaci`) | Consistent acronym. |
| `VolcanicAshAdvisory` / `TropicalCycloneAdvisory` / `SpaceWeatherAdvisory` | `…AdvisoryReport` | `VaaReport`, `TcaReport`, `SpaceWxReport` | **Clean Code (Naming & Conciseness)**: "Advisory" and "Report" are redundant. |
| `WAFSSignificantWeatherForecast` | `SignificantWeatherReport` | `SigwxReport` (or `Sigwx`) | **DDD Ubiquitous Language**: Forecast renamed as "report"; operational standard is SIGWX. |
| `AerodromeSurfaceWind` / `AerodromeCloud` / `AerodromeRunwayVisualRange` | `SurfaceWindInfo` / `AerodromeCloudLayerInfo` / `RunwayVisualRangeInfo` | `SurfaceWind`, `AerodromeCloudLayer`, `RunwayVisualRange` | **Clean Code (Noise Words)**: `Info` suffix is an anti-pattern; en-route models use `Cloud`, `Turbulence`. |

**Agreed Architectural Decision**:
1. Adopt **Customary Operational Aeronautical Acronyms** for domain aggregates (`Metar`, `Speci`, `Taf`, `Sigwx`, `Sigmet`, `Airmet`, `Vona`, `Qvaci`, `Vaa`, `Tca`, `SpaceWx`, and `MeteorologicalBulletin`).
2. Drop all noise-word suffixes (`*Info`, redundant `*Report` where applicable) in Value Objects.
3. Keep XSD-specific transport types strictly encapsulated inside `Infrastructure`.

---

### 8.2 Clean Code & SOLID Principles Analysis

> **Prioritization**: DDD and Clean Code items are ranked by criticality, with scope estimates and a recommended execution order, in [`docs/ARCHITECTURE_ANALYSIS.md` §7](docs/ARCHITECTURE_ANALYSIS.md#7-priorización-de-la-deuda-ddd-y-clean-code).

> **Nota de Calibración Pragmática (IWXXM 2025-2 vs Principios de Diseño)**:  
> Dado que la especificación OACI/OMM 2025-2 define un conjunto finito y cerrado de 13 tipos de reportes, **no se requiere un framework dinámico de plugins externos (evitando sobreingeniería / YAGNI)**. La resolución de OCP en exportadores y mappers se calibra de forma pragmática mediante polimorfismo estático / Visitor pattern.  
> Sin embargo, **el principio de responsabilidad única (SRP) sigue siendo crítico**: permite aislar el radio de impacto (*blast radius*) de correcciones de bugs de mapeo en esquemas complejos (evitando que tocar cenizas volcánicas o avisos espaciales rompa el viento de un METAR) y prepara la arquitectura para una futura coexistencia multiversión en redes SWIM (2021-2, 2023-1, 2025-2) sin alterar el Dominio Canónico.

Every architectural deficiency below directly breaks one or more foundational Clean Code or SOLID design principles:

| Issue | Broken Clean Code / SOLID Principle | Evidence | Architectural Impact & Recommendation |
| :--- | :--- | :--- | :--- |
| **God classes** | **Single Responsibility Principle (SRP)**<br>*(Clean Code: "Classes should have only one reason to change")* | `IwxxmSuiteMapper.cs` (1359 lines / 34 methods); `SigwxMapper.cs` (987 lines); `Rfc7946GeoJsonExporter.cs` (984 lines). | **Critical**. A modification to Space Weather advisory parsing or TC wind schemas forces recompilation and risks regressions in aerodrome surface wind mapping. **Fix**: Decompose into per-aggregate mappers (`MetarReportMapper`, `TafReportMapper`, `SigmetReportMapper`, etc.) behind a composite mapper, and modular GeoJSON feature extractors. |
| **Type-switch dispatch** | **Open/Closed Principle (OCP)**<br>*(Clean Code: "Open for extension, closed for modification")* | `Rfc7946GeoJsonExporter.cs:77-125`<br>(`switch (report) { case MeteorologicalBulletin: ... case SignificantWeatherReport: ... }`). | **High**. Adding a new report type requires modifying existing exporter dispatch methods. **Fix**: Implement the polymorphic **Visitor Pattern** (`IIwxxmReportVisitor<TResult>`) on `IIwxxmReport` or an extensible feature provider registry. |
| **Several public types per file** | **Clean Code File Organization**<br>*(Clean Code: "One primary concept / type per file")* | `AerodromeReports.cs` (6 public types); `VolcanicAndSpaceReports.cs` (8 public types); `EnRouteHazardReports.cs` (3 public types). | **Medium**. Obscures navigation, increases Git merge conflict probability, and conceals domain concepts. **Fix**: Split into dedicated, single-type files (`SurfaceWind.cs`, `Metar.cs`, etc.). |
| **Primitive obsession & linked-data leakage** | **Primitive Obsession Code Smell**<br>*(Clean Code: "Encapsulate domain concepts in Value Objects")* | `reportStatus` and `permissibleUsage` stored as raw `string`; WMO RDF code-list URIs (`amountUri`, `presentWeather` hrefs) stored directly in domain entities. | **Medium**. Domain lacks compile-time safety and accepts arbitrary strings. Linked Data URIs are an external infrastructure concern. **Fix**: Strongly typed Domain enums (`ReportStatus`, `PermissibleUsage`) and Domain Value Objects; map RDF URIs at the Anti-Corruption Layer. |
| **Leaking I/O exceptions outside `Result<T>`** | **Robustness Principle & Functional Error Handling**<br>*(Clean Code: "Error handling is a separate concern; do not violate API contracts")* | `IwxxmParser.cs:198-201` (`ProcessFile` creates `FileStream` without handling `UnauthorizedAccessException` or `IOException`). | **Medium**. Consumers expecting the promised non-throwing `Result<T>` encounter unhandled runtime exceptions. **Fix**: Wrap I/O operations in structured `try/catch` blocks returning explicit `Result<T>.Failure` errors. |
| **Incomplete value object equality contracts** | **Liskov Substitution Principle (LSP) & .NET Contract Consistency**<br>*(Clean Code: "Follow standard platform contracts consistently")* | `GeoCoordinate.cs:59,67` uses `1e-6` floating-point tolerance in `Equals` but exact doubles in `GetHashCode()`. `GeoPolygon.cs:97` compares holes only by count. | **Medium**. Inconsistent equality breaks hash-based collections (`HashSet<T>`, `Dictionary<K, V>`), causing silent look-up failures. **Fix**: Quantize coordinates before hashing, and implement structural comparison across all polygon rings. |
| **Bloated envelope interface** | **Interface Segregation Principle (ISP)**<br>*(SOLID: "Clients should not be forced to depend on methods they do not use")* | `IIwxxmReport.cs` forces `MeteorologicalBulletin` (a SWIM transport envelope) to implement `TranslationFailedTac` (always null) and `PermissibleUsage`. | **Medium**. Forces container envelopes to mimic report payload contracts. **Fix**: Segregate `IMeteorologicalReport` from `IBulletinEnvelope`. |
| **I/O coupling in application ports** | **Dependency Inversion Principle (DIP)**<br>*(Hexagonal: "Keep I/O at the outermost boundaries")* | `IIwxxmDocumentMapper.Map(Stream xmlStream)` accepts a binary `Stream`, forcing Infrastructure to re-apply XML reader quotas. | **Medium**. Application port is polluted with low-level stream handling. **Fix**: Boundary sanitizes input and passes a clean reader/document abstraction. |

---

### 8.3 Hexagonal Architecture

**Status: Resolved (Unreleased)** — see the Status column below. Namespace map, layer rules and the public API are documented in README ("Namespaces & Hexagonal Architecture") and CONTRIBUTING ("Hexagonal Architecture Rules").

What is correct:
- Domain has zero dependencies on Application, Infrastructure, `System.Xml`, or `System.IO`.
- Use cases live in Application and depend only on Ports.
- Infrastructure adapters implement Application ports (driven adapters).
- Architecture constraints enforced at bytecode (IL) level via NetArchTest and Mono.Cecil in `HexagonalArchitectureTests.cs`.

Deviations:

| Issue | Broken Principle | Evidence | Recommendation | Status |
| :--- | :--- | :--- | :--- | :--- |
| Driving adapter wired to driven adapters | **Inversion of Control (IoC) / Separation of Concerns** | `Presentation/IwxxmParser.cs:8-9` imported Infrastructure namespaces. | Move wiring into a dedicated composition root (`IwxxmParserBuilder`). Facades depend only on inbound ports. | Resolved (Unreleased) |
| No explicit inbound (driving) ports | **Ports & Adapters Boundary Separation** | Public ports mixed driving and driven responsibilities. | Separate `Ports/In` (use-case contracts) from `Ports/Out` (adapters). | Resolved (Unreleased) |
| Driven ports not pluggable | **Dependency Inversion Principle (DIP)** | Validation and mapping ports were `internal`. | Expose public outbound interfaces in `Iwxxm.Net.Extensibility` to allow custom adapters. | Resolved (Unreleased) |
| Infrastructure concerns in Domain | **Domain Model Purity** | XML reader quota options resided in `Domain/Common`. | Moved to `Application/Options`. | Resolved (Unreleased) |
| Layer names leaked to public API | **Information Hiding (Parnas Principle)** | Consumers imported `Iwxxm.Net.Presentation`. | Public surface concentrated in `Iwxxm.Net`, `Iwxxm.Net.Reports`, and `Iwxxm.Net.Extensibility`. | Resolved (Unreleased) |
| Static service locator | **Inversion of Control (IoC) / Testability** | Static `Parse*` methods over `Lazy` singletons with default options. | Instance-based facade + DI registration; keep static helpers as thin convenience wrappers. | Resolved (Unreleased) |

Verification: `tests/Iwxxm.Net.Tests/HexagonalArchitectureTests.cs` checks these rules at IL level (NetArchTest.Rules / Mono.Cecil, test-only dependency) and replaces the former reflection-signature tests and the Dockerfile `grep` audit.

## 9. Verified Strengths

- XML hardening: `DtdProcessing.Prohibit`, `XmlResolver = null` on every input reader, sealed air-gapped resolver, `enableScript: false`, size quotas.
- GeoJSON writes `[lon, lat]`, closes rings, uses `InvariantCulture`, escapes control characters.
- Numbers parsed with `InvariantCulture`; times normalised to UTC; no O(n²) scans.
- All 12 README code samples compile against the packaged assembly; nupkg contains both TFMs, README, LICENSE, icon and SourceLink-enabled symbols.

## 10. Recommended Remediation Order

1. AUD-C1 and AUD-C2 (operational safety).
2. AUD-C3 + AUD-H6 + AUD-M15 (truthful validation).
3. AUD-H2, AUD-H1, AUD-H3, AUD-H4 (silent data loss).
4. AUD-H7, AUD-M19 (release safety).
5. AUD-H8, AUD-M17, AUD-M21 (API surface and test depth).

---

## 11. Historical Backlog (Phase 2 / Phase 3) — Re-assessed

Items originally reported as *Completed* for `1.0.0-rc.1`. Statuses below reflect the audit findings.

| ID | Title | Original status | Re-assessed status |
| :--- | :--- | :--- | :--- |
| TD-201 | Polymorphic `IIwxxmReport` & `IwxxmParser` dispatcher | Completed | Completed (see AUD-A1 for type-switch and API-surface debt) |
| TD-202 | En-route & volcanic hazard reports (QVACI, VONA, VA/TC/SWX advisories, SIGMET, AIRMET) | Completed | Partial — AIRMET cancellation/content (AUD-H3), circles (AUD-H1), `xlink:href` (AUD-H2) missing; hand-written XSDs replaced by official ones (AUD-C3) |
| TD-203 | Aerodrome reports (METAR, SPECI, TAF) | Completed | Partial — trend wind (AUD-H4), qualifiers/NSW/NSC (AUD-M11), time context (AUD-M9) missing; units fixed (AUD-C2) |
| TD-204 | Collective bulletin unwrapping (`collect:MeteorologicalBulletin`) | Completed | Completed — now validated against the official `iwxxm-collect.xsd` closure; partial-success debt in AUD-M12 |
| TD-205 | Offline RDF/SKOS code-list registry | Completed | Superseded — the single 84-concept `codes.wmo.int-49-2.rdf` was replaced by the 20 official WMO code lists (AUD-C3) |
| TD-301 | XML DoS & memory bound hardening | Completed | Partial — size quotas and DTD prohibition in place; no nesting-depth bound (AUD-N1), SVRL amplification (AUD-M14) |
| TD-302 | Zero-allocation `gml:posList` tokenizer | Completed | Partial — not zero-allocation on `netstandard2.0`, rounding issues (AUD-L1) |
| TD-303 | BenchmarkDotNet suite | Completed | Completed — README command needs `-- --filter '*'` (AUD-L13) |
| TD-304 | GitHub Actions CI/CD pipeline | Completed | Completed with debt — permissions/versioning (AUD-M19), local publish risk (AUD-H7) |
