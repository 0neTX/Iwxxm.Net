# Iwxxm.Net — ICAO/WMO IWXXM 2025-2 Aeronautical Meteorology Processor for .NET

[![GitHub Packages](https://img.shields.io/badge/GitHub%20Packages-v1.0.0--rc-blue)](https://github.com/0neTX/Iwxxm.Net/packages)
[![Target Frameworks](https://img.shields.io/badge/targets-netstandard2.0%20%7C%20netstandard2.1-purple)](https://learn.microsoft.com/en-us/dotnet/standard/net-standard)
[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)

**`Iwxxm.Net`** is a zero-dependency **.NET Standard 2.0 & .NET Standard 2.1** multi-targeted library architected with **Domain-Driven Design (DDD)** and **Hexagonal (Ports & Adapters) Architecture** for parsing, dual-validating (**XSD + ISO Schematron XSLT + Offline WMO RDF/SKOS Code-List Registries**), mapping, and exporting (**Native RFC 7946 GeoJSON**) the complete **ICAO / WMO IWXXM 2025-2** aeronautical meteorological report suite and **SWIM / AMHS Collective Bulletins** (`collect:MeteorologicalBulletin`).

> **Validation scope at a glance**
> - **XSD**: every standard document is validated against the **unmodified official WMO IWXXM 2025-2 schemas** (`IWXXM/*.xsd` from [wmo-im/iwxxm](https://github.com/wmo-im/iwxxm)) together with the official imported schemas (OGC GML 3.2.1, OGC O&M 2.0, ISO 19139, AIXM 5.1.1, WMO METCE 1.2, OPM 1.2, COLLECT 1.2, W3C XLink/XML), embedded byte-for-byte.
> - **Business rules (Schematron)**: **partial**. The official `iwxxm.sch` requires XSLT 2.0, which `XslCompiledTransform` cannot run, so the library ships its own XSLT 1.0 subset: **11 of the 165 official rule IDs** are implemented (some only for a subset of their official contexts), plus code-list membership checks of every `codes.wmo.int` `xlink:href` against the **20 official WMO RDF code lists** and a few library-specific checks. A document accepted by the default configuration may still violate official rules that are not implemented. **This subset is what the core package runs by default** (`IwxxmParser.Default`, `IwxxmSigwxParser.Default`, `new IwxxmParserBuilder().Build()`); full official rule coverage requires `UseOfficialSchematron()` from the optional package below.
> - **Complete official rules (optional)**: the separate package **`Iwxxm.Net.Validation.Saxon`** (`net10.0`, SaxonCS-HE) runs the **unmodified official `iwxxm.sch` (all 165 rule patterns)** through `IwxxmParserBuilder.UseOfficialSchematron()`; see [4.11](#411-official-wmo-schematron-optional-iwxxmnetvalidationsaxon).
> - **`iwxxm:SIGWXReport`** is a **NON-STANDARD** compact root specific to this library (it is not declared by any official schema); it is validated against a separate, hand-written legacy schema subset.

---

## Table of Contents

1. [Supported IWXXM 2025-2 Report Suite](#supported-iwxxm-2025-2-report-suite)
2. [Key Features](#key-features)
3. [Installation](#installation)
4. [Comprehensive Usage Examples](#comprehensive-usage-examples)
   - [4.1 Polymorphic SWIM / AMHS Stream Processing (`IwxxmParser`)](#41-polymorphic-swim--amhs-stream-processing-iwxxmparser)
   - [4.2 Aerodrome Observations & Forecasts (`METAR`, `SPECI`, `TAF`)](#42-aerodrome-observations--forecasts-metar-speci-taf)
   - [4.3 En-Route Hazards (`SIGMET`, `VolcanicAshSIGMET`, `TropicalCycloneSIGMET`, `AIRMET`)](#43-en-route-hazards-sigmet-volcanicashsigmet-tropicalcyclonesigmet-airmet)
   - [4.4 WAFS Significant Weather Forecasts (`WAFSSignificantWeatherForecast` & `SIGWXReport`)](#44-wafs-significant-weather-forecasts-wafssignificantweatherforecast--sigwxreport)
   - [4.5 Volcanic Ash, Tropical Cyclone & Space Weather Advisories (`QVACI`, `VONA`, `VAA`, `TCA`, `SWX`)](#45-volcanic-ash-tropical-cyclone--space-weather-advisories-qvaci-vona-vaa-tca-swx)
   - [4.6 SWIM / AMHS Collective Bulletins (`collect:MeteorologicalBulletin`)](#46-swim--amhs-collective-bulletins-collectmeteorologicalbulletin)
   - [4.7 Native RFC 7946 GeoJSON Export (`IwxxmGeoJsonExporter` & `ParseFileToGeoJson`)](#47-native-rfc-7946-geojson-export-iwxxmgeojsonexporter--parsefiletogeojson)
   - [4.8 Dual Validation & Structured Error Diagnostics (`Result<T>` & `ValidationError`)](#48-dual-validation--structured-error-diagnostics-resultt--validationerror)
   - [4.9 Security Hardening & XML DoS Protection (`IwxxmParserOptions`)](#49-security-hardening--xml-dos-protection-iwxxmparseroptions)
   - [4.10 Composition Root & Dependency Injection (`IwxxmParserBuilder`)](#410-composition-root--dependency-injection-iwxxmparserbuilder)
   - [4.11 Official WMO Schematron (optional `Iwxxm.Net.Validation.Saxon`)](#411-official-wmo-schematron-optional-iwxxmnetvalidationsaxon)
5. [Domain Value Objects & Spatial Primitives](#domain-value-objects--spatial-primitives)
6. [Namespaces & Hexagonal Architecture](#namespaces--hexagonal-architecture)
7. [Local Docker CI/CD Pipeline & Benchmarks](#local-docker-cicd-pipeline--benchmarks)
8. [Design-Time XSD Class Generation](#design-time-xsd-class-generation)
9. [License & Third-Party Notices](#license--third-party-notices)

---

## Supported IWXXM 2025-2 Report Suite

| Category | Root XML Element(s) | `IwxxmReportType` | Domain Aggregate Root |
| :--- | :--- | :--- | :--- |
| **WAFS Significant Weather** | `iwxxm:WAFSSignificantWeatherForecast`, `iwxxm:SIGWXReport` (non-standard extension) | `SignificantWeather` | `Sigwx` |
| **Aerodrome Routine Observation** | `iwxxm:METAR` | `Metar` | `Metar` (base `AerodromeObservation`) |
| **Aerodrome Special Observation** | `iwxxm:SPECI` | `Speci` | `Speci` (base `AerodromeObservation`) |
| **Terminal Aerodrome Forecast** | `iwxxm:TAF` | `Taf` | `Taf` |
| **En-Route SIGMET** | `iwxxm:SIGMET` | `Sigmet` | `Sigmet` |
| **Volcanic Ash SIGMET** | `iwxxm:VolcanicAshSIGMET` | `VolcanicAshSigmet` | `Sigmet` |
| **Tropical Cyclone SIGMET** | `iwxxm:TropicalCycloneSIGMET` | `TropicalCycloneSigmet` | `Sigmet` |
| **Low-Level AIRMET** | `iwxxm:AIRMET` | `Airmet` | `Airmet` |
| **Volcano Observatory Notice** | `iwxxm:VolcanoObservatoryNoticeForAviation` | `VolcanoObservatoryNotice` | `Vona` |
| **Quantitative Volcanic Ash** | `iwxxm:QuantitativeVolcanicAshConcentrationInformation` | `QuantitativeVolcanicAshConcentration` | `Qvaci` |
| **Volcanic Ash Advisory** | `iwxxm:VolcanicAshAdvisory` | `VolcanicAshAdvisory` | `Vaa` |
| **Tropical Cyclone Advisory** | `iwxxm:TropicalCycloneAdvisory` | `TropicalCycloneAdvisory` | `Tca` |
| **Space Weather Advisory** | `iwxxm:SpaceWeatherAdvisory` | `SpaceWeatherAdvisory` | `SpaceWx` |
| **SWIM / AMHS Collective Bulletin** | `collect:MeteorologicalBulletin` | `MeteorologicalBulletin` | `MeteorologicalBulletin` |

Every parsed document derives from the closed abstract root `IwxxmDocument`, which has two branches: `IwxxmReport` (the eleven meteorological report aggregates above, which carry their own status, usage and translation metadata) and the `MeteorologicalBulletin` envelope (bulletin identifier, contained reports and metadata derived from them). WMO code-list references (`xlink:href`) are translated into Domain types: `CodeListValue<TCode>` (e.g. `AerodromeCloudAmount`, `SigmetPhenomenon`, `AviationColourCode`) or `AerodromeWeather`, keeping the reported code as `Notation`; a code outside the official IWXXM 2025-2 list is kept as `Unknown`, never dropped.

---

## Key Features

- **Zero Third-Party Runtime Dependencies**: Built 100% on the native .NET Base Class Library (`System.Xml`, `XmlSchemaSet`, `XslCompiledTransform`, `XDocument`). No external GIS, JSON, validation, or mediator packages required at runtime.
- **Multi-Targeted `.NET Standard 2.0` & `.NET Standard 2.1`**: Ships dedicated `lib/netstandard2.0/Iwxxm.Net.dll` (for **.NET Framework 4.6.1–4.8**) and `lib/netstandard2.1/Iwxxm.Net.dll` (for **.NET Core 3.0+ and .NET 5 / 6 / 7 / 8 / 9 / 10+**, leveraging native `ReadOnlySpan<char>` APIs) inside a single NuGet package.
- **Embedded Air-Gapped Official Schemas, Rules & WMO RDF Code Lists**: Bundles byte-identical copies of the official WMO IWXXM 2025-2 XSD schemas and of every external schema reachable from `iwxxm.xsd` / `iwxxm-collect.xsd` (`Resources/Xsd/Official/`, path-preserving, 80 files), the library's XSLT 1.0 business-rule subset (`iwxxm-2025-2-sigwx.xslt`), and the 20 official WMO RDF/SKOS code lists (`IWXXM/rule/*.rdf`) as `<EmbeddedResource>` streams. `EmbeddedXmlUrlResolver` maps the official `schemaLocation` URLs (`http://schemas.wmo.int/...`, `http://schemas.opengis.net/...`, `http://www.aixm.aero/schema/...`, `http://www.w3.org/...`) and relative includes onto those resources; any other URI fails, and no disk or network I/O is ever performed. Upstream ReadMe/release-note files are shipped in the package under `schema-notices/`.
- **Dual Validation Pipeline**:
  1. **Structural Validation (official XSD)**: the root element QName selects the schema set: the official IWXXM 2025-2 / COLLECT set for every standard root (unknown roots are rejected through `XmlSchemaSet.GlobalElements`, not message text), or the non-standard legacy set for `iwxxm:SIGWXReport`. Both sets are compiled lazily once per process (thread-safe) with XXE/DTD protection (`DtdProcessing.Prohibit`); the official set compiles in roughly 0.15-0.5 s on first use.
  2. **Business Rule & Official RDF Code-List Validation (partial)**: Executes the XSLT 1.0 rule subset via `XslCompiledTransform` (11 of 165 official rule IDs, official BUFR code-table membership for WAFS SIGWX, and membership of every `codes.wmo.int` URI in the matching official code list, with the official `owl:versionInfo` filter for IWXXM 2025-2) and extracts SVRL (`<svrl:failed-assert>`) findings.
- **Units of Measure Preserved**: Speeds and lengths whose unit is not fixed by the official rules (surface wind and gusts, tropical-cyclone maximum wind, jet-stream wind, speed of motion, cloud base, volcanic source elevation) are exposed as `Speed` / `Length` value objects carrying the reported value and unit (`m/s`, `[kn_i]`, `km/h`, `m`, `[ft_i]`, ...), with exact conversion methods. Single-unit quantities (`Cel`, `hPa`, `deg`, visibility/RVR in `m`, ash density in `mg/m3`) keep unit-specific property names and the mapper rejects any other unit. A missing or unknown `@uom` is a `DomainMapping` error; no unit is ever assumed.
- **Native Spatial & WMO Code Value Objects**: Translates GML 3.2.1 geometries (`gml:Curve`, `gml:CubicSpline`, `gml:LineStringSegment`, `gml:Polygon`, `gml:PolygonPatch` with interior holes `gml:interior`, `gml:CircleByCenterPoint`, `gml:Point`, `gml:posList`, `gml:pos`) and AIXM 5.1.1 `AirspaceVolume` bounds into immutable Domain Value Objects (`GeoCoordinate`, `GeoPolygon`, `VerticalLimit`) using a zero-array-allocation coordinate scanner (`GmlCoordinateTokenizer`).
- **Native RFC 7946 GeoJSON Export**: Converts any `IwxxmReport` (or `collect:MeteorologicalBulletin`) directly into an RFC 7946 `FeatureCollection` with `[longitude, latitude]` coordinate tuples, polygon interior hole rings, and computed `bbox` bounds—with zero third-party JSON or GIS dependencies.
- **Functional `Result<T>` Pattern**: Explicit, allocation-friendly error handling categorized by technology-agnostic pipeline stage (`MalformedInput`, `SchemaViolation`, `BusinessRuleViolation`, `Deserialization`, `DomainMapping`); the stable machine-readable contract is `ValidationError.Code`.

---

## Installation

Packages are published to **GitHub Packages** (`https://github.com/0neTX/Iwxxm.Net/packages`) only for release tags `vMAJOR.MINOR.PATCH[-PRERELEASE]` (the tag is the package version) or a deliberate manual workflow run; pull requests and pushes to `main` build and test `-ci.<run>.<attempt>` prereleases that are never published.

Configure the GitHub Packages NuGet source:

```bash
dotnet nuget add source "https://nuget.pkg.github.com/0neTX/index.json" \
  --name "github-0netx" \
  --username "<GITHUB_USERNAME>" \
  --password "<GITHUB_PAT>" \
  --store-password-in-clear-text
```

Install the package:

```bash
dotnet add package Iwxxm.Net --prerelease
```

Optionally, to validate against the complete official WMO Schematron (`net10.0` applications only):

```bash
dotnet add package Iwxxm.Net.Validation.Saxon --prerelease
```

| Package | Targets | Runtime dependencies |
| :--- | :--- | :--- |
| `Iwxxm.Net` | `netstandard2.0`, `netstandard2.1` | none |
| `Iwxxm.Net.Validation.Saxon` | `net10.0` | `Iwxxm.Net`, `SaxonCS-HE` 13.1.0 (MPL-2.0, which brings its own transitive dependencies) |

---

## Comprehensive Usage Examples

### 4.1 Polymorphic SWIM / AMHS Stream Processing (`IwxxmParser`)

When consuming heterogeneous SWIM, AMHS, or WMO WIS2 feeds where the incoming XML may be any IWXXM 2025-2 report or a `collect:MeteorologicalBulletin`, use `IwxxmParser.Parse` (or `IwxxmParser.ParseFile`):

```csharp
using System;
using Iwxxm.Net;
using Iwxxm.Net.Reports;

Result<IwxxmDocument> result = IwxxmParser.ParseFile("incoming-swim-message.xml");

if (result.IsFailure)
{
    foreach (ValidationError err in result.Errors)
    {
        Console.Error.WriteLine(err);
    }
    return;
}

switch (result.Value)
{
    case AerodromeObservation obs: // Metar or Speci
        Console.WriteLine($"[{obs.ReportType}] {obs.AerodromeDesignator} at {obs.ObservationTimeUtc:O} — QNH: {obs.QnhHpa} hPa");
        break;

    case Taf taf:
        Console.WriteLine($"[TAF] {taf.AerodromeDesignator} ({taf.ValidFromUtc:O} to {taf.ValidUntilUtc:O}) — {taf.ChangeForecasts.Count} change groups");
        break;

    case Sigmet sigmet:
        Console.WriteLine($"[{sigmet.ReportType}] FIR {sigmet.FirDesignator} #{sigmet.SequenceNumber} — Phenomenon: {sigmet.Phenomenon?.Code} ({sigmet.Phenomenon?.Notation})");
        break;

    case Airmet airmet:
        Console.WriteLine($"[AIRMET] FIR {airmet.FirDesignator} #{airmet.SequenceNumber} — Phenomenon: {airmet.Phenomenon?.Code}");
        break;

    case Sigwx sigwx:
        Console.WriteLine($"[SIGWX] Centre: {sigwx.IssuingCentre} ({sigwx.LowerLimit} - {sigwx.UpperLimit}) — JetStreams: {sigwx.JetStreams.Count}"); // e.g. "FL100 STD - FL600 STD"
        break;

    case Qvaci qvaci:
        Console.WriteLine($"[QVACI] Centre: {qvaci.OriginatingCentre} — Ash Contours: {qvaci.AshConcentrationClouds.Count}");
        break;

    case Vona vona:
        Console.WriteLine($"[VONA] Notice #{vona.NoticeNumber} — Colour Code: {vona.CurrentColourCode?.Code}"); // e.g. Yellow
        break;

    case Vaa vaa:
        Console.WriteLine($"[VAA] VAAC {vaa.IssuingVaacName} Advisory #{vaa.AdvisoryNumber} — Volcano: {vaa.Volcano?.Name}");
        break;

    case Tca tca:
        Console.WriteLine($"[TCA] TCAC {tca.IssuingTcacDesignator} — Cyclone {tca.CycloneName} (#{tca.AdvisoryNumber})");
        break;

    case SpaceWx swx:
        Console.WriteLine($"[SWX] SWXC {swx.IssuingSwxcName} Advisory #{swx.AdvisoryNumber} — Regions: {swx.Analyses.Count}");
        break;

    case MeteorologicalBulletin bulletin:
        Console.WriteLine($"[BULLETIN] {bulletin.BulletinIdentifier} containing {bulletin.Reports.Count} valid reports");
        break;
}
```

For exhaustive, compiler-checked handling, implement `IIwxxmDocumentVisitor<TResult>` (one `Visit` overload per concrete document type: `Metar`, `Speci`, `Taf`, `Sigmet`, `Airmet`, `Vona`, `Qvaci`, `Vaa`, `Tca`, `SpaceWx`, `Sigwx` and `MeteorologicalBulletin`) and call `document.Accept(visitor)`: when a document type is added, every visitor stops compiling until it handles it.

```csharp
sealed class DescribeVisitor : IIwxxmDocumentVisitor<string>
{
    public string Visit(Metar metar) => $"METAR {metar.AerodromeDesignator}";
    public string Visit(Speci speci) => $"SPECI {speci.AerodromeDesignator}";
    public string Visit(Taf taf) => $"TAF {taf.AerodromeDesignator}";
    public string Visit(Sigmet sigmet) => $"{sigmet.ReportType} {sigmet.FirDesignator}";
    public string Visit(Airmet airmet) => $"AIRMET {airmet.FirDesignator}";
    public string Visit(Vona vona) => $"VONA {vona.NoticeNumber}";
    public string Visit(Qvaci qvaci) => $"QVACI {qvaci.OriginatingCentre}";
    public string Visit(Vaa vaa) => $"VAA {vaa.AdvisoryNumber}";
    public string Visit(Tca tca) => $"TCA {tca.CycloneName}";
    public string Visit(SpaceWx swx) => $"SWX {swx.AdvisoryNumber}";
    public string Visit(Sigwx sigwx) => $"SIGWX {sigwx.IssuingCentre}";
    public string Visit(MeteorologicalBulletin bulletin) => $"BULLETIN {bulletin.BulletinIdentifier}";
}

string description = result.Value.Accept(new DescribeVisitor());
```

---

### 4.2 Aerodrome Observations & Forecasts (`METAR`, `SPECI`, `TAF`)

#### Parsing `METAR` / `SPECI` Reports

```csharp
using System;
using Iwxxm.Net;
using Iwxxm.Net.Reports;

// ParseFileAs<Metar> or ParseFileAs<Speci> for a specific kind; AerodromeObservation accepts both.
Result<AerodromeObservation> metarResult =
    IwxxmParser.ParseFileAs<AerodromeObservation>("Samples/wmo-examples/metar-A3-1.xml");

if (metarResult.IsSuccess)
{
    AerodromeObservation metar = metarResult.Value;

    Console.WriteLine($"Report ID       : {metar.Id}");
    Console.WriteLine($"Type            : {metar.ReportType}"); // Metar or Speci
    Console.WriteLine($"Aerodrome       : {metar.AerodromeDesignator} ({metar.AerodromeName})");
    Console.WriteLine($"ARP Coordinate  : {metar.AerodromePosition}"); // e.g., (52.3086, 4.7639)
    Console.WriteLine($"Observation UTC : {metar.ObservationTimeUtc:O}"); // null when given only by an unresolved xlink:href
    Console.WriteLine($"Status / usage  : {metar.ReportStatus} / {metar.PermissibleUsage} {metar.PermissibleUsageReason}"); // Normal / Operational
    Console.WriteLine($"Automated (AUTO): {metar.IsAutomatedStation}");
    Console.WriteLine($"CAVOK           : {metar.CloudAndVisibilityOk}");
    Console.WriteLine($"Temperature     : {metar.AirTemperatureCelsius} °C (Dewpoint: {metar.DewpointTemperatureCelsius} °C)");
    Console.WriteLine($"Altimeter (QNH) : {metar.QnhHpa} hPa");
    Console.WriteLine($"Visibility      : {metar.PrevailingVisibilityMeters} m");

    if (metar.SurfaceWind != null)
    {
        // Speeds keep the reported unit (m/s or [kn_i]); convert explicitly when a fixed unit is needed
        Console.WriteLine($"Surface Wind    : {metar.SurfaceWind.MeanDirectionDegrees}° at {metar.SurfaceWind.MeanSpeed} " +
                          $"({metar.SurfaceWind.MeanSpeed?.ToMetersPerSecond():F1} m/s, Gust: {metar.SurfaceWind.GustSpeed?.ToKnots():F0} kt, " +
                          $"Variable: {metar.SurfaceWind.IsVariableDirection})");
    }

    foreach (RunwayVisualRange rvr in metar.RunwayVisualRanges)
    {
        Console.WriteLine($"RVR Runway {rvr.RunwayDesignator}: {rvr.MeanRvrMeters} m (Tendency: {rvr.PastTendency})");
    }

    foreach (AerodromeWeather wx in metar.PresentWeather)
    {
        Console.WriteLine($"Present Weather : {wx.Notation} (official code: {wx.IsKnown})"); // e.g., "DZ (official code: True)"
    }

    foreach (AerodromeCloudLayer layer in metar.CloudLayers)
    {
        // Amount / CloudType are code-list values (e.g. AerodromeCloudAmount.Scattered, notation "SCT");
        // Base keeps the reported unit (m or [ft_i]), e.g. "1000 [ft_i]"
        Console.WriteLine($"Cloud Layer     : Amount={layer.Amount?.Code}, Base={layer.Base} ({layer.Base?.ToFeet():F0} ft), Type={layer.CloudType?.Code}");
    }

    foreach (AerodromeForecastGroup trend in metar.TrendForecasts)
    {
        Console.WriteLine($"TREND ({trend.ChangeIndicator}): Visibility={trend.PrevailingVisibilityMeters} m");
    }
}
```

#### Parsing `TAF` Reports (Base Forecast, `BECOMING`, `TEMPO`, `FM`, and Cancel Reports)

```csharp
using System;
using Iwxxm.Net;
using Iwxxm.Net.Reports;

Result<Taf> tafResult =
    IwxxmParser.ParseFileAs<Taf>("Samples/wmo-examples/taf-A5-1.xml");

if (tafResult.IsSuccess)
{
    Taf taf = tafResult.Value;

    Console.WriteLine($"TAF for {taf.AerodromeDesignator} ({taf.ValidFromUtc:O} -> {taf.ValidUntilUtc:O})");
    Console.WriteLine($"Status: {taf.ReportStatus}, IsCancelReport: {taf.IsCancelReport}, IsNil: {taf.IsNilForecast}");

    if (taf.BaseForecast != null)
    {
        Console.WriteLine($"Base Visibility : {taf.BaseForecast.PrevailingVisibilityMeters} m");
        Console.WriteLine($"Base Wind       : {taf.BaseForecast.SurfaceWind?.MeanDirectionDegrees}° / {taf.BaseForecast.SurfaceWind?.MeanSpeed}");
    }

    foreach (AerodromeForecastGroup change in taf.ChangeForecasts)
    {
        Console.WriteLine($"Change [{change.ChangeIndicator}] ({change.ValidFromUtc:HH:mm}Z - {change.ValidUntilUtc:HH:mm}Z): " +
                          $"Vis={change.PrevailingVisibilityMeters} m, Weather={string.Join(", ", change.Weather.Select(w => w.Notation))}");
    }
}
```

---

### 4.3 En-Route Hazards (`SIGMET`, `VolcanicAshSIGMET`, `TropicalCycloneSIGMET`, `AIRMET`)

`Sigmet` unifies standard `SIGMET`, `VolcanicAshSIGMET`, and `TropicalCycloneSIGMET` while preserving the specific `ReportType` discriminator:

```csharp
using System;
using Iwxxm.Net;
using Iwxxm.Net.Reports;

Result<Sigmet> sigmetResult =
    IwxxmParser.ParseFileAs<Sigmet>("Samples/wmo-examples/sigmet-VA-EGGX.xml");

if (sigmetResult.IsSuccess)
{
    Sigmet sigmet = sigmetResult.Value;

    Console.WriteLine($"SIGMET Variant  : {sigmet.ReportType}"); // Sigmet, VolcanicAshSigmet, or TropicalCycloneSigmet
    Console.WriteLine($"ATS Unit / MWO  : {sigmet.AtsUnitDesignator} ({sigmet.AtsUnitName}) / MWO {sigmet.MwoDesignator}");
    Console.WriteLine($"FIR             : {sigmet.FirDesignator} ({sigmet.FirName})");
    Console.WriteLine($"Sequence Number : {sigmet.SequenceNumber}");
    Console.WriteLine($"Validity        : {sigmet.ValidFromUtc:O} to {sigmet.ValidUntilUtc:O}"); // null only for an unresolved xlink:href
    if (sigmet.PermissibleUsage == PermissibleUsage.NonOperational)
    {
        Console.WriteLine($"NON-OPERATIONAL ({sigmet.PermissibleUsageReason}): {sigmet.PermissibleUsageSupplementary}");
    }
    Console.WriteLine($"Phenomenon      : {sigmet.Phenomenon?.Code} ({sigmet.Phenomenon?.Notation})"); // e.g. VolcanicAsh (VA)

    if (sigmet.EruptingVolcano != null)
    {
        Console.WriteLine($"Erupting Volcano: {sigmet.EruptingVolcano.Name} at {sigmet.EruptingVolcano.Position}");
    }

    foreach (EnRoutePhenomenonVolume volume in sigmet.EvolvingConditions)
    {
        Console.WriteLine($"Evolving Volume {volume.Id}:");
        // VerticalLimit keeps value, unit and reference: "FL250 STD", "15000 M MSL", "GND SFC", "NIL(unknown)".
        // A missing limit is null (never assumed to be the surface).
        Console.WriteLine($"  Vertical Extent : {volume.LowerLimit?.ToString() ?? "(not reported)"} to {volume.UpperLimit}");
        Console.WriteLine($"  Upper as FL     : {volume.UpperLimit?.AsFlightLevel()?.ToString() ?? "n/a (not an STD flight level)"}");
        Console.WriteLine($"  Intensity Change: {volume.IntensityChange}");
        Console.WriteLine($"  Motion          : {volume.DirectionOfMotionDegrees}° at {volume.SpeedOfMotion} ({volume.SpeedOfMotion?.ToKnots():F0} kt)");
        Console.WriteLine($"  Time Context    : {volume.TimeIndicator} at {volume.PhenomenonTimeUtc:O}"); // OBS/FCST of its collection
        // The area is a closed union: GeoPolygon or GeoCircle ("WI 250NM OF TC CENTRE").
        string area = volume.Area?.Match(
            polygon => $"polygon, {polygon.ExteriorRing.Count} vertices",
            circle => $"circle, {circle.Radius.Value} {circle.Radius.UnitOfMeasure} around {circle.Centre}") ?? "(none)";
        Console.WriteLine($"  Area            : {area}");
    }

    foreach (EnRoutePhenomenonVolume forecast in sigmet.ForecastPositions)
    {
        int vertices = forecast.Area is GeoPolygon polygon ? polygon.ExteriorRing.Count : 0;
        Console.WriteLine($"Forecast Volume {forecast.Id}: {vertices} vertices at {forecast.PhenomenonTimeUtc:O}");
    }
}

// AIRMET: a cancellation (CNL) is distinguishable and never describes an active hazard.
Airmet airmet = IwxxmParser.ParseFileAs<Airmet>("Samples/wmo-examples/airmet-A6-1a-TS.xml").Value;
if (airmet.IsCancelReport)
{
    Console.WriteLine($"AIRMET {airmet.SequenceNumber} cancels AIRMET {airmet.CancelledReportSequenceNumber} ({airmet.CancelledReportValidFromUtc:O} - {airmet.CancelledReportValidUntilUtc:O})");
}
foreach (AirmetEvolvingCondition condition in airmet.EvolvingConditions)
{
    // AIRMET-specific content next to the shared volume: BKN/OVC CLD, SFC VIS, SFC WIND.
    Console.WriteLine($"  {condition.Id}: cloud {condition.CloudBase}-{condition.CloudTop}, vis {condition.SurfaceVisibility}, wind {condition.SurfaceWindDirectionDegrees}/{condition.SurfaceWindSpeed}, top FL {condition.Volume.UpperLimit?.AsFlightLevel()}");
}
```

---

### 4.4 WAFS Significant Weather Forecasts (`WAFSSignificantWeatherForecast` & `SIGWXReport`)

You can parse WAFS SIGWX charts via either `IwxxmSigwxParser.ParseFile` or `IwxxmParser.ParseFileAs<Sigwx>`:

```csharp
using System;
using Iwxxm.Net;
using Iwxxm.Net.Reports;

Result<Sigwx> result =
    IwxxmSigwxParser.ParseFile("Samples/wmo-examples/WAFS-Example.xml");

if (result.IsSuccess)
{
    Sigwx sigwx = result.Value;

    Console.WriteLine($"WAFC Issuing Centre : {sigwx.IssuingCentre}");
    Console.WriteLine($"Validity Period     : {sigwx.ValidFromUtc:O} -> {sigwx.ValidUntilUtc:O}");
    Console.WriteLine($"Vertical Envelope   : {sigwx.LowerLimit} -> {sigwx.UpperLimit}");

    // 1. Jet Streams (CubicSpline / LineString trajectories + wind symbol core points)
    foreach (JetStream js in sigwx.JetStreams)
    {
        Console.WriteLine($"JetStream {js.Id}: Max {js.MaxWindSpeed} ({js.MaxWindSpeed.ToKnots():F1} kt) at {js.CoreLevel} ({js.AxisTrajectory.Count} axis points, {js.CorePoints.Count} core symbols)");
    }

    // 2. Clouds (WMO BUFR 0-20-008 Distribution & 0-20-012 Cloud Type)
    foreach (Cloud cloud in sigwx.Clouds)
    {
        Console.WriteLine($"Cloud {cloud.Id}: {cloud.Distribution?.Code} {cloud.CloudType?.Code} ({cloud.Base?.ToString() ?? "XXX"}-{cloud.Top})");
    }

    // 3. Clear Air Turbulence (WMO BUFR 0-11-030 Extended Degree of Turbulence)
    foreach (Turbulence turb in sigwx.Turbulences)
    {
        Console.WriteLine($"Turbulence {turb.Id}: {turb.Degree?.Code} ({turb.Base}-{turb.Top})"); // e.g. "10363.2 M MSL-12192 M MSL", never converted to FL340-FL400
    }

    // 4. Airframe Icing (WMO BUFR 0-20-041 Airframe Icing)
    foreach (AirframeIcing icing in sigwx.AirframeIcings)
    {
        Console.WriteLine($"Icing {icing.Id}: {icing.Degree?.Code} ({icing.Base}-{icing.Top})");
    }

    // 5. Tropopause Contours & Spot Heights
    foreach (Tropopause tropo in sigwx.Tropopauses)
    {
        Console.WriteLine($"Tropopause {tropo.Id} ({tropo.LevelType}): {tropo.SpotHeights.Count} spot heights");
    }

    // 6. Point Phenomena: Volcanoes, Tropical Cyclones, Sandstorms, Radiation Incidents
    foreach (Volcano v in sigwx.Volcanoes)
        Console.WriteLine($"Volcano {v.Name} at {v.Position}");
    foreach (TropicalCyclone tc in sigwx.TropicalCyclones)
        Console.WriteLine($"Tropical Cyclone {tc.Name} at {tc.Position}");
}
```

---

### 4.5 Volcanic Ash, Tropical Cyclone & Space Weather Advisories (`QVACI`, `VONA`, `VAA`, `TCA`, `SWX`)

#### Quantitative Volcanic Ash Concentration (`QVACI`) with Interior Polygon Holes (`gml:interior`)

IWXXM 2025-2 introduces `QuantitativeVolcanicAshConcentrationInformation` (`QVACI`), where ash concentration contours can contain hollow interior rings (`gml:PolygonPatch` with `<gml:exterior>` and `<gml:interior>`):

```csharp
using System;
using Iwxxm.Net;
using Iwxxm.Net.Reports;

Result<Qvaci> qvaciResult =
    IwxxmParser.ParseFileAs<Qvaci>("Samples/wmo-examples/qvaci-Example.xml");

if (qvaciResult.IsSuccess)
{
    Qvaci qvaci = qvaciResult.Value;
    Console.WriteLine($"Originating Centre: {qvaci.OriginatingCentre} (Model Init: {qvaci.DispersionModelInitialisationTimeUtc:O})");

    foreach (VolcanicAshConcentrationCloud cloud in qvaci.AshConcentrationClouds)
    {
        Console.WriteLine($"Ash Contour {cloud.Id}:");
        Console.WriteLine($"  Density         : {cloud.AshDensityMgPerM3} mg/m³");
        Console.WriteLine($"  Vertical Layer  : {cloud.LowerLimit} to {cloud.UpperLimit}");
        Console.WriteLine($"  Exterior Vertices: {cloud.Area.ExteriorRing.Count}");
        Console.WriteLine($"  Interior Holes  : {cloud.Area.InteriorRings.Count}");
    }
}
```

#### Space Weather (`SWX`), Volcanic Ash (`VAA`), and Tropical Cyclone (`TCA`) Advisories

```csharp
var vaa = IwxxmParser.ParseFileAs<Vaa>("Samples/wmo-examples/va-advisory-A7-2.xml").Value;
Console.WriteLine($"VAAC {vaa.IssuingVaacName} #{vaa.AdvisoryNumber}: {vaa.ObservedAshClouds.Count} observed clouds at {vaa.Observation?.PhenomenonTimeUtc:O}");
foreach (VolcanicAshConditions forecast in vaa.Forecasts) // one entry per +6/+12/+18 HR offset
{
    Console.WriteLine($"  FCST {forecast.PhenomenonTimeUtc:O}: {forecast.Status} ({forecast.AshClouds.Count} clouds)"); // e.g. NoVolcanicAshExpected
}

var tca = IwxxmParser.ParseFileAs<Tca>("Samples/wmo-examples/tc-advisory-A2-2.xml").Value;
Console.WriteLine($"TCAC {tca.IssuingTcacDesignator} Cyclone {tca.CycloneName}: Obs Pressure={tca.Observation?.CentralPressureHpa} hPa, {tca.Forecasts.Count} forecast track points, {tca.CumulonimbusCloudLocations.Count} CB extents");

var swx = IwxxmParser.ParseFileAs<SpaceWx>("Samples/wmo-examples/spacewx-A7-3.xml").Value;
Console.WriteLine($"SWXC {swx.IssuingSwxcName} #{swx.AdvisoryNumber}: Effects=[{string.Join(", ", swx.Effects)}]");
foreach (SpaceWeatherAnalysis analysis in swx.Analyses) // observation and each forecast offset
{
    Console.WriteLine(analysis.IsNothingOfOperationalSignificance
        ? $"  {analysis.TimeIndicator} {analysis.PhenomenonTimeUtc:O}: NO SWX EXP"
        : $"  {analysis.TimeIndicator} {analysis.PhenomenonTimeUtc:O}: {analysis.Regions.Count} intensity/region groups");
}
```

---

### 4.6 SWIM / AMHS Collective Bulletins (`collect:MeteorologicalBulletin`)

SWIM and AMHS networks frequently bundle multiple `METAR`, `SPECI`, or `TAF` reports inside a `<collect:MeteorologicalBulletin>` envelope. `MeteorologicalBulletin` exposes `Reports` (all mapped reports), `ReportResults` (individual `Result<IwxxmReport>` outcomes per `<collect:meteorologicalInformation>` entry) and `ItemErrors` (the entries that could not be mapped, with their index and `gml:id`). **Partial results**: a bulletin succeeds as long as one report maps; each skipped report also appears as a `Warning` in `Result.Warnings`. A bulletin with no mapped report fails with `DOMAIN-BULLETIN-EMPTY`. The same applies per feature to WAFS charts (`Sigwx.FeatureErrors`). Schema and Schematron validation remain document-level.

```csharp
using System;
using Iwxxm.Net;
using Iwxxm.Net.Reports;

Result<MeteorologicalBulletin> bulletinResult =
    IwxxmParser.ParseBulletinFile("Samples/wmo-examples/metar-NIL-collect.xml");

if (bulletinResult.IsSuccess)
{
    MeteorologicalBulletin bulletin = bulletinResult.Value;
    Console.WriteLine($"Bulletin ID : {bulletin.BulletinIdentifier}");
    Console.WriteLine($"Valid Count : {bulletin.Reports.Count} / {bulletin.ReportResults.Count}");
    foreach (ItemMappingError skipped in bulletin.ItemErrors) // also reported in bulletinResult.Warnings
    {
        Console.WriteLine($"  skipped entry #{skipped.Index} ({skipped.ItemId}): {skipped.Errors[0].Code}");
    }

    foreach (IwxxmReport childReport in bulletin.Reports)
    {
        if (childReport is Metar metar)
        {
            Console.WriteLine($"  -> {metar.AerodromeDesignator} (NIL={metar.IsNilObservation}, QNH={metar.QnhHpa})");
        }
    }
}
```

---

### 4.7 Native RFC 7946 GeoJSON Export (`IwxxmGeoJsonExporter` & `ParseFileToGeoJson`)

`Iwxxm.Net` includes a zero-dependency **RFC 7946 GeoJSON Exporter** that converts any `IwxxmDocument` (an `IwxxmReport` or a `MeteorologicalBulletin`) into a standards-compliant GeoJSON `FeatureCollection`:
- Automatically swaps GML `[latitude, longitude]` coordinates into **RFC 7946 Section 3.1.1 `[longitude, latitude]`** order.
- Preserves **interior hole rings** (`GeoPolygon.InteriorRings`) as secondary linear rings in GeoJSON `Polygon` coordinates (`[exteriorRing, hole1, ...]`).
- Computes the global **`bbox`** (`[west, south, east, north]`) across all features; when a geometry crosses the antimeridian, `west` > `east` (RFC 7946 §5.2).
- Applies the **right-hand rule** (exterior rings counterclockwise, holes clockwise) and **splits geometries crossing ±180** into `MultiLineString` / `MultiPolygon` (§3.1.9); rings that go around a pole are written as polar caps. The Domain model keeps the source order.
- Writes **circles** (`GeoCircle`, `gml:CircleByCenterPoint`; RFC 7946 has no circle) as a densified polygon of 64 segments (geodesic points at the radius on the IUGG mean sphere) plus the properties `center` (`[lon, lat]`), `radius`, `radiusUom` and `circleSegments`, from which consumers can rebuild the exact circle.
- Never writes `NaN`/`Infinity` (non-finite values are rejected by the mappers; a non-finite property would be written as `null`).
- Synthesizes a `TC_TRACK` `LineString` connecting observed and forecast tropical cyclone positions in `Tca`.
- Emits measures **as reported, without unit conversion**: each speed/length property is written as a value plus a sibling `...Uom` property holding the UCUM unit (e.g. `"windSpeed": 25, "windSpeedUom": "[kn_i]"`, `"speedOfMotion"` / `"speedOfMotionUom"`, `"maxSurfaceWindSpeed"` / `"maxSurfaceWindSpeedUom"`, `"sourceElevation"` / `"sourceElevationUom"`, `"maxWindSpeed"` / `"maxWindSpeedUom"`). WMO BUFR codes are emitted as both the enum name and the official numeric code (`degreeNumericCode`, `distributionNumericCode`, `typeNumericCode`).

```csharp
using System;
using Iwxxm.Net;

// Option A: One-step XML -> Dual Validation -> Domain Aggregate -> RFC 7946 GeoJSON
Result<string> geoJsonResult = IwxxmParser.ParseFileToGeoJson(
    "Samples/wmo-examples/qvaci-Example.xml",
    new GeoJsonExportOptions(indented: true, includeBoundingBox: true));

if (geoJsonResult.IsSuccess)
{
    Console.WriteLine(geoJsonResult.Value);
}

// Option B: Export an already-parsed IwxxmReport domain object
var reportResult = IwxxmParser.ParseFile("Samples/wmo-examples/WAFS-Example.xml");
if (reportResult.IsSuccess)
{
    string compactGeoJson = IwxxmGeoJsonExporter.ToGeoJson(
        reportResult.Value,
        new GeoJsonExportOptions(indented: false, includeBoundingBox: true));
}
```

#### Example GeoJSON Output Structure

```json
{
  "type": "FeatureCollection",
  "bbox": [-104.7, -60, 150, 62.7],
  "metadata": {
    "id": "uuid.ce3b61b9-d360-4da1-83b9-67e668406869",
    "reportType": "SignificantWeather",
    "issueTimeUtc": "2023-05-29T18:00:00.0000000+00:00",
    "reportStatus": "NORMAL",
    "permissibleUsage": "OPERATIONAL"
  },
  "features": [
    {
      "type": "Feature",
      "id": "uuid.8c3d3467-e398-4627-ab23-f9e57c2215b5",
      "geometry": {
        "type": "LineString",
        "coordinates": [[-104.7, 35.4], [-97.8, 39.2], [-78.9, 41.9]]
      },
      "properties": {
        "featureType": "JETSTREAM",
        "reportId": "uuid.ce3b61b9-d360-4da1-83b9-67e668406869",
        "reportType": "SignificantWeather",
        "issueTimeUtc": "2023-05-29T18:00:00.0000000+00:00",
        "coreLevel": 15240,
        "coreLevelUom": "M",
        "coreLevelReference": "MSL",
        "maxWindSpeed": 61.8,
        "maxWindSpeedUom": "m/s"
      }
    }
  ]
}
```

---

### 4.8 Dual Validation & Structured Error Diagnostics (`Result<T>` & `ValidationError`)

Every parsing operation returns an immutable `Result<T>` (with `Map`, `Bind`, `Match` and `TryGetValue`; non-fatal findings are in `Warnings`) without throwing exceptions on invalid XML, XSD schema violations, Schematron rule failures, unregistered WMO RDF code-list URIs, file-system errors or failing adapters. **Only argument validation throws**: `ArgumentNullException` for a `null` stream and `ArgumentException` for a `null`, empty or whitespace file path.

| Code | Category | Meaning |
| :--- | :--- | :--- |
| `FILE-NOT-FOUND` | `MalformedInput` | The file or its directory does not exist. |
| `ACCESS-DENIED` | `MalformedInput` | Permission denied (or the path is a directory). |
| `FILE-READ-ERROR` | `MalformedInput` | Sharing violation, path too long, malformed path or other I/O error. |
| `XML-SIZE-LIMIT`, `XML-MALFORMED`, `STREAM-*` | `MalformedInput` | Size limits, malformed XML or DTD (checked once by the use case before any adapter), unreadable or empty stream. |
| `XSD-TOO-MANY-ERRORS`, `SCHEMATRON-TOO-MANY-ERRORS` | `SchemaViolation` / `BusinessRuleViolation` | More findings than `IwxxmParserOptions.MaxValidationErrors`; only the first ones are reported. |
| `SCHEMATRON-REPORT-SIZE-LIMIT`, `SCHEMATRON-UNAVAILABLE` | `BusinessRuleViolation` | The SVRL report exceeds `MaxRuleReportBytes` / the rules could not be compiled (retried on the next validation). |
| `XSD-*` / `SCHEMA-VALIDATOR-ERROR` | `SchemaViolation` | Schema violation / the schema validator threw or returned nothing. |
| official rule ids / `RULE-VALIDATOR-ERROR` | `BusinessRuleViolation` | Schematron or code-list rule / the rule validator threw or returned nothing. |
| `MAPPER-ERROR` | `DomainMapping` | The document mapper threw or returned nothing. |
| `GEOJSON-EXPORT-ERROR` | `DomainMapping` | The GeoJSON exporter threw (`*ToGeoJson` members). |
| `ACL-REPORT-STATUS-MISSING` / `-INVALID`, `ACL-PERMISSIBLE-USAGE-MISSING` / `-INVALID`, `ACL-PERMISSIBLE-USAGE-REASON-INVALID` | `DomainMapping` | Required metadata absent or not an official code (never defaulted to `NORMAL` / `OPERATIONAL`). |
| `ACL-ISSUE-TIME-MISSING`, `ACL-OBSERVATION-TIME-MISSING`, `ACL-VALID-PERIOD-MISSING`, `ACL-TIME-INVALID` | `DomainMapping` | Mandatory time absent (an `issueTime` given only by `xlink:href` is also reported) or unparseable. |
| `ACL-ASH-DENSITY-MISSING`, `ACL-VOLCANO-NAME-MISSING` | `DomainMapping` | Mandatory QVACI density / volcano name element absent. |
| `ACL-VERTICAL-UOM-UNSUPPORTED`, `ACL-VERTICAL-REFERENCE-UNSUPPORTED` | `DomainMapping` | Vertical unit or reference outside AIXM / UCUM codes. |
| `DOMAIN-<REPORT>-VALID-PERIOD`, `DOMAIN-TAF-CANCELLED-VALID-PERIOD`, `DOMAIN-FORECAST-GROUP-PERIOD` | `DomainMapping` | End of a period not strictly after its start. |
| `DOMAIN-TIME-NOT-UTC`, `DOMAIN-ISSUE-TIME-MISSING` | `DomainMapping` | A `*Utc` value with a non-zero offset / a default issue time. |
| `DOMAIN-DIRECTION-OUT-OF-RANGE`, `DOMAIN-NEGATIVE-SPEED`, `DOMAIN-NEGATIVE-VISIBILITY`, `DOMAIN-NEGATIVE-RVR` | `DomainMapping` | Physical ranges: direction within [0, 360]°, non-negative magnitudes. |
| `DOMAIN-AIRMET-ATS-UNIT`, `DOMAIN-REPORT-STATUS-INVALID`, `DOMAIN-PERMISSIBLE-USAGE-INVALID`, `DOMAIN-BULLETIN-EMPTY`, `DOMAIN-VERTICAL-LIMIT-INVALID` | `DomainMapping` | Structural invariants of the aggregate factories. |


```csharp
using System;
using Iwxxm.Net;
using Iwxxm.Net.Reports;

Result<IwxxmDocument> result = IwxxmParser.Parse(untrustedXmlPayload);

if (result.IsFailure)
{
    foreach (ValidationError error in result.Errors)
    {
        switch (error.Category)
        {
            case ErrorCategory.MalformedInput:
                Console.Error.WriteLine($"[XML Syntax / Size Limit] {error.Code} at Line {error.LineNumber}: {error.Message}");
                break;

            case ErrorCategory.SchemaViolation:
                Console.Error.WriteLine($"[XSD Structural Violation] {error.Code} (Line {error.LineNumber}, Pos {error.LinePosition}): {error.Message}");
                break;

            case ErrorCategory.BusinessRuleViolation:
                // Built-in rule subset + official RDF code-list lookups, or every official rule with UseOfficialSchematron()
                Console.Error.WriteLine($"[WMO Schematron / RDF Rule] {error.Code} at XPath '{error.Location}': {error.Message}");
                break;

            case ErrorCategory.DomainMapping:
                Console.Error.WriteLine($"[Domain Invariant Failure] {error.Code} at '{error.Location}': {error.Message}");
                break;
        }
    }
}
```

---

### 4.9 Security Hardening & XML DoS Protection (`IwxxmParserOptions`)

For internet-facing SWIM/AMHS gateways, `IwxxmParserOptions` enforces strict memory and character quotas before buffering or parsing XML streams (in addition to `DtdProcessing.Prohibit` and `XmlResolver = null` on untrusted XML):

```csharp
using System.IO;
using Iwxxm.Net;
using Iwxxm.Net.Reports;

var strictOptions = new IwxxmParserOptions(
    maxDocumentBytes: 2 * 1024 * 1024,        // 2 MB maximum stream size
    maxCharactersInDocument: 2 * 1024 * 1024, // 2M character cap in XmlReader
    maxCharactersFromEntities: 512);

IwxxmParser parser = new IwxxmParserBuilder()
    .WithOptions(strictOptions)
    .Build();

using Stream incomingStream = GetNetworkStream();
Result<IwxxmDocument> result = parser.Process(incomingStream);
// Streams exceeding maxDocumentBytes fail immediately with code "XML-SIZE-LIMIT" (ErrorCategory.MalformedInput)
```

---

### 4.10 Composition Root & Dependency Injection (`IwxxmParserBuilder`)

`IwxxmParserBuilder` is the composition root: it wires the driven adapters to the use cases and returns immutable, thread-safe facades (`IwxxmParser`, `IwxxmSigwxParser`, `IwxxmGeoJsonExporter`). Every adapter can be replaced through the public outbound ports in `Iwxxm.Net.Extensibility`, and business-rule validation can be disabled. The static `IwxxmParser.Parse*` / `IwxxmSigwxParser.Parse*` / `IwxxmGeoJsonExporter.ToGeoJson` members remain as thin wrappers over a default instance (`IwxxmParser.Default`, ...).

```csharp
using Iwxxm.Net;
using Iwxxm.Net.Extensibility;
using Microsoft.Extensions.DependencyInjection;

var builder = new IwxxmParserBuilder()
    .WithOptions(IwxxmParserOptions.Default)
    .UseSchematronValidator(new MyRuleEngine())   // ISchematronValidator adapter
    // .WithoutSchematron()                       // or: XSD validation only
    // .UseSchemaValidator(...) / .UseDocumentMapper(...) / .UseSigwxDocumentMapper(...) / .UseGeoJsonExporter(...)
    ;

services.AddSingleton(builder.Build());               // IwxxmParser (convenience facade)
services.AddSingleton(builder.BuildSigwxParser());    // IwxxmSigwxParser
services.AddSingleton(builder.BuildGeoJsonExporter()); // IwxxmGeoJsonExporter

// The facades do not implement the inbound ports (HEX-02); they expose the port they delegate to.
// Inject the inbound ports where only the use-case contract is needed:
services.AddSingleton<IIwxxmReportDispatcher>(sp => sp.GetRequiredService<IwxxmParser>().Dispatcher);
services.AddSingleton<IIwxxmSigwxProcessor>(sp => sp.GetRequiredService<IwxxmSigwxParser>().Processor);
```

| Port | Kind | Namespace | Default adapter |
| :--- | :--- | :--- | :--- |
| `IIwxxmReportDispatcher` | inbound (use case) | `Iwxxm.Net` | full-suite validation + mapping pipeline |
| `IIwxxmSigwxProcessor` | inbound (use case) | `Iwxxm.Net` | SIGWX validation + mapping pipeline |
| `IGeoJsonExportService` | inbound (use case) | `Iwxxm.Net` | RFC 7946 export |
| `ISchemaValidator` | outbound | `Iwxxm.Net.Extensibility` | embedded official WMO XSDs |
| `ISchematronValidator` | outbound | `Iwxxm.Net.Extensibility` | built-in XSLT 1.0 rule subset (or `UseOfficialSchematron()`) |
| `IIwxxmDocumentMapper` | outbound | `Iwxxm.Net.Extensibility` | built-in Anti-Corruption Layer mapper |
| `ISigwxDocumentMapper` | outbound | `Iwxxm.Net.Extensibility` | built-in SIGWX mapper |
| `IGeoJsonExporter` | outbound | `Iwxxm.Net.Extensibility` | built-in RFC 7946 writer |

Outbound ports receive an **`IwxxmDocumentContext`** (`Iwxxm.Net.Extensibility`): the use case has already bounded the size, converted text input to UTF-8 and parsed the document once with the hardened reader settings (DTD prohibited, no resolver, character quotas), so a malformed or DTD-bearing document never reaches an adapter (`XML-MALFORMED`). `OpenRead()` returns a read-only stream of the document bytes for adapters that need raw XML; the built-in adapters share the parsed tree. An adapter can be called directly with `new IwxxmDocumentContext(bytes, options)`. Validators return `IReadOnlyList<ValidationError>`: findings with `ValidationSeverity.Error` stop the pipeline, `Warning` findings are returned as `Result<T>.Warnings` of the successful result. Each validator reports at most `IwxxmParserOptions.MaxValidationErrors` findings (then a `*-TOO-MANY-ERRORS` sentinel), and Schematron reports are bounded by `MaxRuleReportBytes` (`SCHEMATRON-REPORT-SIZE-LIMIT`).

---

### 4.11 Official WMO Schematron (optional `Iwxxm.Net.Validation.Saxon`)

The official `iwxxm.sch` uses `queryBinding="xslt2"`, which the .NET BCL cannot execute. The optional package **`Iwxxm.Net.Validation.Saxon`** runs it with **SaxonCS-HE** (Saxonica's Home Edition for .NET):

- The **byte-identical official `iwxxm.sch`** (165 rule patterns, no rewriting) is compiled once per process to XSLT with the vendored **SchXslt 1.10.1** compiler (MIT) and cached as an immutable, thread-safe Saxon executable (first compilation takes roughly a second).
- **Air-gapped**: the official `document('codes.wmo.int-*.rdf')` code-list lookups resolve to the official WMO RDF files embedded in `Iwxxm.Net`; every other URI (network, file system) is refused.
- SVRL `failed-assert` / `successful-report` findings become `ValidationError`s with `ErrorCategory.BusinessRuleViolation`, the **official rule (pattern) id** as `Code` (e.g. `METAR_SPECI.MeteorologicalAerodromeObservation-5`), the official message, the SVRL XPath `Location`, and `Severity` from `@role`/`@flag` (the official rules declare none, so all failed assertions are `Error`; a fired `successful-report` without an error role is a `Warning`). The SVRL report is streamed from a size-bounded buffer and findings are capped (see 4.10).
- All 29 official WMO examples pass. Note that the official rules themselves are not exhaustive: for example, an ordinary TAF whose `iwxxm:baseForecast` element is entirely absent is not reported by `TAF.TAF-5` (its test compares an empty attribute sequence).
- Requires a `net10.0` application. No Saxonica license key is needed: the package only requests Saxon-HE capabilities (`new Processor(false)`), verified in CI by the package tests.

```csharp
using Iwxxm.Net;

IwxxmParser parser = new IwxxmParserBuilder()
    .UseOfficialSchematron()   // extension method from Iwxxm.Net.Validation.Saxon
    .Build();

Result<IwxxmDocument> result = parser.ProcessFile("metar.xml");
// e.g. qnh reported in "mbar" -> Code "METAR_SPECI.MeteorologicalAerodromeObservation-5"
```

`OfficialSchematronValidator.Warmup()` compiles the rule set eagerly (for example during application start-up).

---

## Domain Value Objects & Spatial Primitives

| Value Object | Namespace | Description |
| :--- | :--- | :--- |
| **`GeoCoordinate`** | `Iwxxm.Net.Reports` | Immutable `readonly struct` enforcing WGS84 geodetic bounds (`Lat` $\in [-90, 90]$, `Lon` $\in [-180, 180]$). Values are quantized once at construction to 7 decimal degrees (`GeoCoordinate.Precision` = 1e-7, ~1.1 cm) and compared exactly, so `Equals`/`GetHashCode` agree and equality is transitive. |
| **`GeoSurface`** | `Iwxxm.Net.Reports` | Closed union of horizontal surfaces: `GeoPolygon` or `GeoCircle` (`Match(polygon => ..., circle => ...)` or pattern matching). Used by `EnRoutePhenomenonVolume.Area`, `SpaceWeatherAnalysisRegion.Areas` and the SIGWX `Cloud`/`Turbulence`/`AirframeIcing` areas. |
| **`GeoCircle`** | `Iwxxm.Net.Reports` | Circle from `gml:CircleByCenterPoint`: `Centre` (`GeoCoordinate`) and `Radius` (`Length`, value and unit as reported, strictly positive). |
| **`GeoPolygon`** | `Iwxxm.Net.Reports` | Immutable polygon enforcing $\ge 3$ distinct vertices, automatic ring closure, and optional `InteriorRings` (`IReadOnlyList<IReadOnlyList<GeoCoordinate>>`) for hollow contours. Equality and hash code cover every exterior and interior vertex. |
| **`VerticalLimit`** | `Iwxxm.Net.Reports` | Immutable `readonly struct` keeping a vertical limit exactly as reported: `Value`, `Unit` (`VerticalUnit`: `FlightLevel`, `Feet`, `Meters`, `Kilometers`, `StandardMeters`), `Reference` (`VerticalReference`: `Sfc`, `Msl`, `W84`, `Std`, `Other`; `null` when not reported), `SpecialValue` (`GND`, `UNL`, `FLOOR`, `CEILING`) and `NilReason`. "15 km AMSL" is not FL492 and `GND SFC` is not FL000. Exact structural equality; `TryCompareTo` only compares commensurable limits. |
| **`FlightLevel`** | `Iwxxm.Net.Reports` | Derived view: `VerticalLimit.AsFlightLevel()` returns a flight level only for STD limits in `FL` (or exact hundreds of `FT`). |
| **`ReportStatus` / `PermissibleUsage` / `PermissibleUsageReason`** | `Iwxxm.Net.Reports` | Typed report metadata (`common.xsd`). Missing or unknown required values are mapping errors; a report is never presented as operational by default. |
| **`Speed`** | `Iwxxm.Net.Reports` | Immutable `readonly struct` holding a reported speed value and its `SpeedUnit` (`MetersPerSecond`, `Knots`, `KilometersPerHour`); exact conversions `ToMetersPerSecond()`, `ToKnots()`, `ToKilometersPerHour()` (1 kt = 1852/3600 m/s). Equality is exact on value and unit. |
| **`Length`** | `Iwxxm.Net.Reports` | Immutable `readonly struct` holding a reported length and its `LengthUnit` (`Meters`, `Feet`, `Kilometers`, `NauticalMiles`, `StatuteMiles`); exact conversions `ToMeters()`, `ToFeet()`, ... (1 ft = 0.3048 m). |
| **BUFR codes** | `Iwxxm.Net.Reports` | `Cloud.Distribution`, `Cloud.CloudType`, `Turbulence.Degree` and `AirframeIcing.Degree` are `CodeListValue<CloudDistributionCode / CloudTypeCode / TurbulenceDegreeCode / IcingDegreeCode>`, translated by the Anti-Corruption Layer from WMO BUFR code-table URIs (`0-20-008`, `0-20-012`, `0-11-030`, `0-20-041`), bare codes and TAC abbreviations. Every official code maps to a member whose numeric value equals the official code and whose name reflects the official meaning (verified against the official RDF tables); `Notation` keeps the code as reported. |
| **`CodeListValue<NilReason>`** | `Iwxxm.Net.Reports` | `@nilReason` translated against the official register `http://codes.wmo.int/common/nil`: distinguishes `NSW` (`AerodromeForecastGroup.IsNoSignificantWeather`), `NSC`/`NCD` (`AerodromeCloud.IsNoSignificantCloud` / `IsNoCloudDetected`), `VV///`, `//` and `SKC` from content that is not reported. |
| **`RelationalOperator`** | `Iwxxm.Net.Reports` | TAC `P`/`M` qualifiers on wind and gust speeds, RVR and prevailing visibility (`SurfaceWind.MeanSpeedOperator`, `RunwayVisualRange.MeanRvrOperator`, `AerodromeHorizontalVisibility.PrevailingOperator`). |
| **`TranslationMetadata`** | `Iwxxm.Net.Reports` | `IwxxmReport.Translation`: `translatedBulletinID`, reception time, translation centre designator/name and translation time (`null` when the report was not translated from TAC). |

---

## Namespaces & Hexagonal Architecture

### Public namespaces (consumer view)

Consumers never need a layer name in a `using` directive. The public API is grouped by role:

| Namespace | Contents |
| :--- | :--- |
| `Iwxxm.Net` | Facades (`IwxxmParser`, `IwxxmSigwxParser`, `IwxxmGeoJsonExporter`), composition root (`IwxxmParserBuilder`), options (`IwxxmParserOptions`, `GeoJsonExportOptions`), `Result<T>`, `ValidationError`, `ErrorCategory`, `ValidationSeverity`, inbound ports (`IIwxxmReportDispatcher`, `IIwxxmSigwxProcessor`, `IGeoJsonExportService`), and the `UseOfficialSchematron()` extension of the optional Saxon package |
| `Iwxxm.Net.Reports` | Domain model: closed document hierarchy (`IwxxmDocument` → `IwxxmReport` / `MeteorologicalBulletin`, `IIwxxmDocumentVisitor<TResult>`, `AerodromeObservation` → `Metar` / `Speci`, `Taf`, `Sigmet`, `Airmet`, `Vona`, `Qvaci`, `Vaa`, `Tca`, `SpaceWx`, `Sigwx`) and value objects (`GeoCoordinate`, `GeoPolygon`, `VerticalLimit`, `FlightLevel`, `Speed`, `Length`, `SurfaceWind`, `RunwayVisualRange`, `AerodromeCloudLayer`, `AerodromeForecastGroup`, `CodeListValue<TCode>`, `AerodromeWeather`, WMO codes, ...) |
| `Iwxxm.Net.Extensibility` | Outbound ports for custom adapters (`ISchemaValidator`, `ISchematronValidator`, `IIwxxmDocumentMapper`, `ISigwxDocumentMapper`, `IGeoJsonExporter`) |
| `Iwxxm.Net.Validation.Saxon` | `OfficialSchematronValidator` (optional package) |

Everything else (`Iwxxm.Net.Application.*`, `Iwxxm.Net.Infrastructure.*`, `Iwxxm20252.Generated`) is `internal`.

### Layers (source view)

Folders express the hexagonal layers; IL-level tests (`HexagonalArchitectureTests`, NetArchTest/Mono.Cecil) enforce the dependency rules on the compiled assembly:

```text
src/Iwxxm.Net/
├── Domain/                     # Depends on nothing (no System.Xml, System.IO, Application or Infrastructure)
│   ├── Common/                 # Result<T>, ValidationError, ErrorCategory, ValidationSeverity        (ns Iwxxm.Net)
│   ├── ValueObjects/           # GeoCoordinate, GeoPolygon, VerticalLimit, SurfaceWind, ..., CodeLists/ (ns Iwxxm.Net.Reports)
│   └── Aggregates/             # IwxxmDocument, IwxxmReport, IIwxxmDocumentVisitor<TResult>, every aggregate (ns Iwxxm.Net.Reports)
├── Application/                # Depends only on Domain (no System.Xml)
│   ├── Options/                # IwxxmParserOptions, GeoJsonExportOptions                              (ns Iwxxm.Net)
│   ├── Ports/In/               # Inbound ports (use-case interfaces)                                   (ns Iwxxm.Net)
│   ├── Ports/Out/              # Outbound ports (driven adapters)                                      (ns Iwxxm.Net.Extensibility)
│   └── UseCases/               # DispatchIwxxmReportUseCase, ProcessSigwxReportUseCase, ExportGeoJsonUseCase (internal)
├── Infrastructure/             # Driven adapters, all internal
│   ├── Generated/              # XSD transport model (internal, namespace Iwxxm20252.Generated)
│   ├── Resources/              # EmbeddedResourceLoader & EmbeddedXmlUrlResolver (air-gapped XSD/XSLT/RDF)
│   ├── Validation/             # XsdValidator, XsltSchematronValidator, DisabledSchematronValidator
│   ├── Serialization/          # SigwxXmlDeserializer + AttributeDrivenXmlReader (reflection reader for the internal model)
│   ├── Mapping/                # IwxxmSuiteMapper (composite), SigwxDocumentMapper, SigwxMapper, UomMeasureReader, GmlCoordinateTokenizer
│   │   ├── Reports/            # One mapper per report family + IwxxmReportMapperRegistry (dispatch by root QName)
│   │   ├── Xml/                # Shared LINQ to XML readers: GML time/geometry, AIXM features, text, error codes
│   │   ├── CodeLists/          # WMO code-list ACL: translation tables checked against the embedded official RDFs
│   │   └── Wafs/               # WAFS/compact SIGWX mappers and one extractor per phenomenon family
│   └── GeoJson/                # Rfc7946GeoJsonExporter + GeoJsonFeatureCollector (IIwxxmDocumentVisitor)
│       ├── Features/           # One feature builder per report family
│       └── Writing/            # RFC 8259 JSON writer and FeatureCollection writer
├── Composition/                # IwxxmParserBuilder: the only place that references Infrastructure  (ns Iwxxm.Net)
└── Presentation/               # Facades over inbound ports only                                       (ns Iwxxm.Net)
    ├── IwxxmParser.cs
    ├── IwxxmSigwxParser.cs
    └── IwxxmGeoJsonExporter.cs

src/Iwxxm.Net.Validation.Saxon/ # Optional driven adapter package (net10.0): ISchematronValidator over SaxonCS-HE
```

Rules checked by `HexagonalArchitectureTests`: Domain depends on nothing outside the Domain; Application depends only on Domain; Presentation depends only on inbound ports (never on Infrastructure, use cases or outbound ports); Infrastructure is reached only through the composition root and never calls facades; `Iwxxm20252.Generated` and Infrastructure types are not public; public types live only in the documented namespaces; every root-namespace type is assigned to a layer; and `Iwxxm.Net` has zero third-party runtime dependencies. The static convenience members of the facades obtain their default instance from the composition root.

---

## Local Docker CI/CD Pipeline & Benchmarks

### Running the Full CI/CD Pipeline Locally via Docker

You can execute all CI/CD stages (`restore` -> `build` -> `test` with code coverage, including the IL-level architecture tests and the official Schematron tests -> `pack` both `.nupkg` & `.snupkg` packages, with a guard that `Iwxxm.Net` declares no dependency) locally inside an isolated `.NET 10 SDK` container (Docker or Podman, e.g. `podman build --target test -t iwxxm-net-test .`):

```bash
# Run the full pipeline and export compiled NuGet packages + coverage to ./artifacts
bash tools/run-local-ci.sh

# Specify a custom incremental build number:
BUILD_NUMBER=5 bash tools/run-local-ci.sh

# Use Podman instead of Docker:
CONTAINER_ENGINE=podman bash tools/run-local-ci.sh

# Or target individual stages directly via docker build:
docker build --target build .
docker build --target test .
docker build --target pack --build-arg PACKAGE_VERSION=1.0.0-rc.2 .
```

The local pipeline **never publishes**: the `ci` image only copies the artifacts to `/out`, and the script does not forward `GITHUB_TOKEN`. Packages are published by the GitHub Actions workflow. A deliberate local publish is an explicit opt-in that refuses to run unless every condition holds:

```bash
# Requires --publish, a dedicated key (GITHUB_TOKEN is ignored), an explicit version and a clean git tree
IWXXM_NUGET_PUBLISH_API_KEY=... PACKAGE_VERSION=1.2.3 bash tools/run-local-ci.sh --publish
```

### Running the BenchmarkDotNet Performance Suite

```bash
dotnet run --project tests/Iwxxm.Net.Benchmarks/Iwxxm.Net.Benchmarks.csproj --configuration Release -- --filter "*"
```

---

## SIGWX Transport Model (hand-maintained)

The internal SIGWX transport classes (`src/Iwxxm.Net/Infrastructure/Generated/Iwxxm20252.Generated.cs`) are maintained by hand; no build step regenerates them. A pinned local .NET tool manifest ([`.config/dotnet-tools.json`](.config/dotnet-tools.json)) provides `XmlSchemaClassGenerator-console` (`dotnet-xscgen`) for an optional reference scaffold, written to `artifacts/xscgen-reference/` for manual comparison only:

```bash
dotnet tool restore   # optional: only for the xscgen reference scaffold (tools/generate-xsd-classes.sh)
./tools/generate-xsd-classes.sh
```

---

## License & Third-Party Notices

Licensed under the [MIT License](LICENSE).

- The embedded official WMO IWXXM schemas, Schematron rules and code lists, and the imported OGC/ISO/EUROCONTROL/W3C schemas are redistributed unmodified; their upstream ReadMe/release notes ship under `schema-notices/` in the `Iwxxm.Net` package.
- `Iwxxm.Net.Validation.Saxon` vendors **SchXslt 1.10.1** (Copyright (c) 2018-2021 David Maus, MIT License), shipped under `licenses/SchXslt/` in the package.
- `Iwxxm.Net.Validation.Saxon` depends on **SaxonCS-HE 13.1.0** (Saxonica Ltd.). Its bundled `LICENSE.txt` states that SaxonCS-HE is distributed as an "executable form" of the open-source SaxonJ-HE product under the **Mozilla Public License 2.0** (file-level copyleft that applies to Saxon's own files, not to code that merely uses it). No Saxonica license key is required for the Home Edition features used here.
