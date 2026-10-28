# Iwxxm.Net — ICAO/WMO IWXXM 2025-2 Aeronautical Meteorology Processor for .NET

[![NuGet Version](https://img.shields.io/badge/nuget-v1.0.0--rc.1-blue)](https://www.nuget.org/packages/Iwxxm.Net)
[![Target Framework](https://img.shields.io/badge/target-netstandard2.0%20%7C%20net48%20%7C%20net8.0-purple)](https://learn.microsoft.com/en-us/dotnet/standard/net-standard)
[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)

**`Iwxxm.Net`** is a zero-dependency **.NET Standard 2.0** library designed with **Domain-Driven Design (DDD)** and **Clean Architecture** for parsing, dual-validating (XSD + ISO Schematron XSLT + Offline WMO RDF Code-List Registries), and mapping the complete **ICAO / WMO IWXXM 2025-2** aeronautical meteorological report suite and **SWIM / AMHS Collective Bulletins** (`collect:MeteorologicalBulletin`).

---

## Supported IWXXM 2025-2 Report Suite

| Category | Root XML Element(s) | Domain Aggregate Root |
| :--- | :--- | :--- |
| **WAFS Significant Weather** | `WAFSSignificantWeatherForecast`, `SIGWXReport` | `SignificantWeatherReport` |
| **Aerodrome Observations** | `METAR`, `SPECI` | `AerodromeWeatherReport` |
| **Aerodrome Forecasts** | `TAF` | `TafReport` |
| **En-Route Hazards (SIGMET)** | `SIGMET`, `VolcanicAshSIGMET`, `TropicalCycloneSIGMET` | `SigmetReport` |
| **Low-Level Hazards (AIRMET)** | `AIRMET` | `AirmetReport` |
| **Volcano Observatory Notice** | `VolcanoObservatoryNoticeForAviation` | `VonaReport` |
| **Quantitative Volcanic Ash** | `QuantitativeVolcanicAshConcentrationInformation` | `QvaciReport` |
| **Volcanic Ash Advisory** | `VolcanicAshAdvisory` | `VolcanicAshAdvisoryReport` |
| **Tropical Cyclone Advisory** | `TropicalCycloneAdvisory` | `TropicalCycloneAdvisoryReport` |
| **Space Weather Advisory** | `SpaceWeatherAdvisory` | `SpaceWeatherAdvisoryReport` |
| **SWIM / AMHS Bulletin** | `collect:MeteorologicalBulletin` | `MeteorologicalBulletin` |

---

## Key Features

- **Zero Third-Party Runtime Dependencies**: Built 100% on the native .NET Base Class Library (`System.Xml`, `XmlSchemaSet`, `XslCompiledTransform`, `XmlSerializer`, `XDocument`). No external GIS, validation, or mediator packages required at runtime.
- **Broad .NET Compatibility**: Targets `netstandard2.0` for seamless execution across **.NET Framework 4.8** and **.NET 8+**.
- **Embedded Air-Gapped Schemas, Rules & WMO RDF Registries**: Bundles the XSD schemas (`iwxxm-2025-2.xsd`, `iwxxm-sigwx.xsd`, `collect.xsd`, `aixm.xsd`, `metce.xsd`, `gml.xsd`, `xlink.xsd`), precompiled ISO Schematron business rules (`iwxxm-2025-2-sigwx.xslt`), and the offline WMO No. 306 / 49-2 SKOS/RDF code-list registry (`codes.wmo.int-49-2.rdf`) as `<EmbeddedResource>` streams resolved strictly in-memory via `EmbeddedXmlUrlResolver` with zero disk or network I/O.
- **Dual Validation Pipeline**:
  1. **Structural Validation**: `XmlReaderSettings` against a lazily compiled, thread-safe `XmlSchemaSet` with XXE/DTD protection (`DtdProcessing.Prohibit`).
  2. **Business Rule & RDF Code-List Validation**: Executes precompiled Schematron XSLT rules via `XslCompiledTransform` (including offline `document('../Codelists/codes.wmo.int-49-2.rdf')` lookups and WAFS BUFR code-table checks) and extracts SVRL (`<svrl:failed-assert>`) findings.
- **Native Spatial & WMO Code Value Objects**: Translates GML 3.2.1 geometries (`gml:Curve`, `gml:CubicSpline`, `gml:LineStringSegment`, `gml:Polygon`, `gml:PolygonPatch` with interior holes `gml:interior`, `gml:CircleByCenterPoint`, `gml:Point`, `gml:posList`, `gml:pos`) and AIXM 5.1.1 `AirspaceVolume` bounds into immutable Domain Value Objects (`GeoCoordinate`, `GeoPolygon`, `FlightLevel`).
- **Functional `Result<T>` Pattern**: Explicit, allocation-friendly error handling categorized by pipeline stage (`XmlWellFormedness`, `XsdSchemaValidation`, `SchematronBusinessRule`, `XmlDeserialization`, `DomainMappingRule`).

---

## Installation

```bash
dotnet add package Iwxxm.Net --version 1.0.0-rc.1
```

---

## Quick Start

### 1. Polymorphic IWXXM 2025-2 Parsing (`IwxxmParser`)

Use `IwxxmParser` when consuming heterogeneous SWIM/AMHS streams containing any IWXXM 2025-2 report or `collect:MeteorologicalBulletin`:

```csharp
using System;
using Iwxxm.Net.Domain.Aggregates;
using Iwxxm.Net.Domain.Common;
using Iwxxm.Net.Presentation;

Result<IIwxxmReport> result = IwxxmParser.ParseFile("qvaci-Example.xml");

if (result.IsSuccess)
{
    switch (result.Value)
    {
        case AerodromeWeatherReport metarOrSpeci:
            Console.WriteLine($"{metarOrSpeci.ReportType} at {metarOrSpeci.AerodromeIcao}: QNH {metarOrSpeci.QnhHectopascals} hPa");
            break;

        case TafReport taf:
            Console.WriteLine($"TAF for {taf.AerodromeIcao}: {taf.ChangeForecasts.Count} change groups");
            break;

        case SigmetReport sigmet:
            Console.WriteLine($"{sigmet.ReportType} #{sigmet.SequenceNumber} for FIR {sigmet.FirIcaoOrName}: {sigmet.PhenomenonUri}");
            break;

        case QvaciReport qvaci:
            Console.WriteLine($"QVACI for volcano {qvaci.VolcanoName}: {qvaci.ConcentrationClouds.Count} ash clouds");
            break;

        case SignificantWeatherReport sigwx:
            Console.WriteLine($"WAFS SIGWX from {sigwx.IssuingCentre} ({sigwx.LowerLimit}-{sigwx.UpperLimit})");
            break;

        case MeteorologicalBulletin bulletin:
            Console.WriteLine($"Bulletin {bulletin.BulletinIdentifier} containing {bulletin.Reports.Count} reports");
            break;
    }
}
```

### 2. Strongly-Typed Report Parsing (`ParseFileAs<TReport>` & `ParseBulletinFile`)

```csharp
Result<AerodromeWeatherReport> metarResult = IwxxmParser.ParseFileAs<AerodromeWeatherReport>("metar-A3-1.xml");
Result<MeteorologicalBulletin> bulletinResult = IwxxmParser.ParseBulletinFile("collect-A3-1-A3-2.xml");
Result<SignificantWeatherReport> sigwxResult = IwxxmSigwxParser.ParseFile("WAFS-Example.xml");
```

### 3. Dependency Injection (`IIwxxmReportDispatcher` & `IIwxxmSigwxProcessor`)

```csharp
using Iwxxm.Net.Application.Ports;
using Iwxxm.Net.Presentation;
using Microsoft.Extensions.DependencyInjection;

services.AddSingleton<IIwxxmReportDispatcher>(_ => IwxxmParser.CreateDispatcher());
services.AddSingleton<IIwxxmSigwxProcessor>(_ => IwxxmSigwxParser.CreateProcessor());
```

---

## Architecture Overview

The library strictly enforces **Clean Architecture** and **Domain-Driven Design (DDD)**:

```text
src/Iwxxm.Net/
├── Domain/                     # Pure C# Domain Model (Zero XML / Infrastructure dependencies)
│   ├── Common/                 # Result<T>, ValidationError, ErrorCategory, ValidationSeverity
│   ├── ValueObjects/           # GeoCoordinate, GeoPolygon (with InteriorRings), FlightLevel, WmoCodes, SIGWX features
│   └── Aggregates/             # IIwxxmReport, SignificantWeatherReport, AerodromeWeatherReport, TafReport,
│                               # SigmetReport, AirmetReport, VonaReport, QvaciReport, VolcanicAshAdvisoryReport,
│                               # TropicalCycloneAdvisoryReport, SpaceWeatherAdvisoryReport, MeteorologicalBulletin
├── Application/                # Application Layer (Depends ONLY on Domain)
│   ├── Ports/                  # IIwxxmReportDispatcher, IIwxxmSigwxProcessor, IXmlSchemaValidator, ISchematronValidator
│   └── UseCases/               # DispatchIwxxmReportUseCase, ProcessSigwxReportUseCase<T>
├── Infrastructure/             # Internal Adapters (System.Xml, XSD, XSLT, Offline RDF, ACL Mapping)
│   ├── Generated/              # XSD-generated classes (namespace Iwxxm20252.Generated)
│   ├── Resources/              # EmbeddedResourceLoader & EmbeddedXmlUrlResolver (air-gapped XSD/XSLT/RDF)
│   ├── Validation/             # XsdValidator & XsltSchematronValidator (SVRL + offline RDF document() lookup)
│   ├── Serialization/          # XmlSerializerSigwxDeserializer
│   └── Mapping/                # IwxxmSuiteMapper & SigwxMapper (Anti-Corruption Layer)
└── Presentation/               # Public Facades & Composition Roots
    ├── IwxxmParser.cs          # Full IWXXM 2025-2 Suite & SWIM Collective Bulletin Parser
    └── IwxxmSigwxParser.cs     # Specialized WAFS SIGWX Parser
```

---

## Design-Time XSD Class Generation

This repository uses a pinned local .NET tool manifest ([`.config/dotnet-tools.json`](.config/dotnet-tools.json)) with `XmlSchemaClassGenerator-console` (`dotnet-xscgen`):

```bash
dotnet tool restore
./tools/generate-xsd-classes.sh
```

---

## Roadmap & Technical Debt

See [**`TECHNICAL_DEBT.md`**](TECHNICAL_DEBT.md) for the complete architectural status:

- [x] **Phase 1 (`v1.0.0-rc.1`)**: Core Clean Architecture, Dual XSD + ISO Schematron XSLT validation engine, GML 3.2.1 Spatial ACL, WMO BUFR code-list registry (`WmoCodeRegistry`), and complete mapping of all 9 IWXXM 2025-2 **SIGWX / WAFS** phenomena.
- [x] **Phase 2 (`v1.0.0-rc.1`)**: Polymorphic `IIwxxmReport` & `IwxxmParser` dispatcher, En-Route & Volcanic Hazards (`QVACI`, `VONA`, `VolcanicAshAdvisory`, `TropicalCycloneAdvisory`, `SpaceWeatherAdvisory`, `SIGMET`, `AIRMET`), Aerodrome Reports (`METAR`/`SPECI`, `TAF`), SWIM Collective Bulletin unwrapping (`collect:MeteorologicalBulletin`), and Offline WMO 49-2 RDF/SKOS code-list validation (`codes.wmo.int-49-2.rdf`).
- [ ] **Phase 3 (`v1.0.0` GA)**: XML DoS character-bound hardening, zero-allocation `gml:posList` tokenizer, `BenchmarkDotNet` memory diagnostics suite, and GitHub Actions CI/CD pipeline.

---

## License

Licensed under the [MIT License](LICENSE).
