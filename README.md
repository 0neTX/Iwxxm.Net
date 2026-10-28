# Iwxxm.Net — ICAO/WMO IWXXM Aeronautical Meteorology Processor for .NET

[![NuGet Version](https://img.shields.io/badge/nuget-v1.0.0--rc.1-blue)](https://www.nuget.org/packages/Iwxxm.Net)
[![Target Framework](https://img.shields.io/badge/target-netstandard2.0%20%7C%20net48%20%7C%20net8.0-purple)](https://learn.microsoft.com/en-us/dotnet/standard/net-standard)
[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)

**`Iwxxm.Net`** is a zero-dependency **.NET Standard 2.0** library designed with **Domain-Driven Design (DDD)** and **Clean Architecture** for parsing, dual-validating (XSD + ISO Schematron XSLT), and mapping aeronautical meteorological reports in **ICAO / WMO IWXXM 2025-2** format.

In version **`1.0.0-rc.1`** (Phase 1), the library provides complete support for **Significant Weather (SIGWX)** reports (`<iwxxm:WAFSSignificantWeatherForecast>` and `<iwxxm:SIGWXReport>`), mapping all 9 official WAFS SIGWX meteorological phenomena: **Jet Streams**, **Turbulence (CAT)**, **Airframe Icing**, **Cumulonimbus / Cloud Distributions**, **Tropopause Heights & Contours**, **Volcanoes**, **Tropical Cyclones**, **Sandstorms / Duststorms**, and **Radiation Incidents**, along with strongly-typed WMO BUFR code-list registries (`WmoCodeRegistry`).

---

## Key Features

- **Zero Third-Party Runtime Dependencies**: Built 100% on the native .NET Base Class Library (`System.Xml`, `XmlSchemaSet`, `XslCompiledTransform`, `XmlSerializer`). No external GIS, validation, or mediator packages required at runtime.
- **Broad .NET Compatibility**: Targets `netstandard2.0` for seamless execution across **.NET Framework 4.8** and **.NET 8+**.
- **Embedded Official Schemas & Rules**: Bundles the XSD schemas (`iwxxm-sigwx.xsd`, `gml.xsd`, `xlink.xsd`) and precompiled ISO Schematron business rules (`iwxxm-2025-2-sigwx.xslt`) as assembly `<EmbeddedResource>` streams, resolved entirely in-memory via a custom `EmbeddedXmlUrlResolver` with zero disk or network I/O.
- **Dual Validation Pipeline**:
  1. **Structural Validation**: `XmlReaderSettings` against a lazily compiled, thread-safe `XmlSchemaSet` with XXE/DTD protection (`DtdProcessing.Prohibit`).
  2. **Business Rule Validation**: Executes precompiled Schematron XSLT rules via `XslCompiledTransform` (including WMO BUFR code-table checks `0-20-008`, `0-20-012`, `0-11-030`, `0-20-041` and nil-reason rules) and extracts SVRL (`<svrl:failed-assert>`) findings.
- **Native Spatial & WMO Code Value Objects**: An Anti-Corruption Layer (`SigwxMapper`) translates GML 3.2.1 geometries (`gml:Curve`, `gml:CubicSpline`, `gml:LineStringSegment`, `gml:Polygon`, `gml:PolygonPatch`, `gml:Ring`, `gml:Point`, `gml:posList`, `gml:pos`) and WMO BUFR URIs into immutable Domain Value Objects (`GeoCoordinate`, `GeoPolygon`, `FlightLevel`, `CloudDistributionCode`, `CloudTypeCode`, `TurbulenceDegreeCode`, `IcingDegreeCode`).
- **Functional `Result<T>` Pattern**: Explicit, allocation-friendly error handling categorized by pipeline stage (`XmlWellFormedness`, `XsdSchemaValidation`, `SchematronBusinessRule`, `Deserialization`, `DomainMapping`).

---

## Installation

```bash
dotnet add package Iwxxm.Net --version 1.0.0-rc.1
```

---

## Quick Start

### 1. Static Facade Usage

```csharp
using System;
using Iwxxm.Net.Domain.Aggregates;
using Iwxxm.Net.Domain.Common;
using Iwxxm.Net.Presentation;

Result<SignificantWeatherReport> result = IwxxmSigwxParser.ParseFile("WAFS-Example.xml");

if (result.IsSuccess)
{
    SignificantWeatherReport report = result.Value;
    Console.WriteLine($"Report ID: {report.Id}");
    Console.WriteLine($"Originating WAFC: {report.IssuingCentre}");
    Console.WriteLine($"Vertical Extent: {report.LowerLimit} - {report.UpperLimit}");
    Console.WriteLine($"Validity: {report.ValidFromUtc:O} to {report.ValidUntilUtc:O}");

    foreach (var jetStream in report.JetStreams)
    {
        Console.WriteLine($"JetStream {jetStream.Id}: Max {jetStream.MaxWindSpeedKnots:F1} kt at {jetStream.CoreFlightLevel} ({jetStream.AxisTrajectory.Count} vertices)");
    }

    foreach (var cloud in report.Clouds)
    {
        Console.WriteLine($"Cloud {cloud.Id}: {cloud.DistributionCode} {cloud.TypeCode} (Base: {cloud.BaseFlightLevel?.ToString() ?? "N/A"}, Top: {cloud.TopFlightLevel})");
    }

    foreach (var turbulence in report.Turbulences)
    {
        Console.WriteLine($"Turbulence {turbulence.Id}: {turbulence.Degree} ({turbulence.BaseFlightLevel} - {turbulence.TopFlightLevel})");
    }

    foreach (var icing in report.AirframeIcings)
    {
        Console.WriteLine($"Icing {icing.Id}: {icing.Degree} ({icing.BaseFlightLevel} - {icing.TopFlightLevel})");
    }

    foreach (var volcano in report.Volcanoes)
    {
        Console.WriteLine($"Volcano {volcano.Name} at {volcano.Position}");
    }
}
else
{
    foreach (ValidationError error in result.Errors)
    {
        Console.Error.WriteLine(error);
    }
}
```

### 2. Dependency Injection (`IIwxxmSigwxProcessor`)

```csharp
using Iwxxm.Net.Application.Ports;
using Iwxxm.Net.Presentation;
using Microsoft.Extensions.DependencyInjection;

services.AddSingleton<IIwxxmSigwxProcessor>(_ => IwxxmSigwxParser.CreateProcessor());
```

---

## Architecture Overview

The library strictly enforces **Clean Architecture** and **Domain-Driven Design (DDD)**:

```text
src/Iwxxm.Net/
├── Domain/                     # Pure C# Domain Model (Zero XML / Infrastructure dependencies)
│   ├── Common/                 # Result<T>, ValidationError, ErrorCategory, ValidationSeverity
│   ├── ValueObjects/           # GeoCoordinate, GeoPolygon, FlightLevel, WmoCodes, JetStream, Cloud, Tropopause, Volcano, Turbulence, AirframeIcing, WafsPointPhenomena
│   └── Aggregates/             # SignificantWeatherReport (Aggregate Root)
├── Application/                # Application Layer (Depends ONLY on Domain)
│   ├── Ports/                  # IIwxxmSigwxProcessor, IXmlSchemaValidator, ISchematronValidator, ISigwxXmlDeserializer<T>, ISigwxAclMapper<T>
│   └── UseCases/               # ProcessSigwxReportUseCase<T> (5-step pipeline orchestrator)
├── Infrastructure/             # Internal Adapters (System.Xml, XSD, XSLT, ACL Mapping)
│   ├── Generated/              # XSD-generated classes (namespace Iwxxm20252.Generated)
│   ├── Resources/              # EmbeddedResourceLoader & EmbeddedXmlUrlResolver
│   ├── Validation/             # XsdValidator & XsltSchematronValidator (SVRL parser)
│   ├── Serialization/          # XmlSerializerSigwxDeserializer (cached XmlSerializer instances)
│   └── Mapping/                # SigwxMapper (Anti-Corruption Layer for GML 3.2.1 -> Domain)
└── Presentation/               # Public Facade & Composition Root
    └── IwxxmSigwxParser.cs
```

---

## Design-Time XSD Class Generation

Standard `xsd.exe` fails on OGC GML 3.2.1 and ICAO IWXXM schemas due to complex substitution groups and `xlink` attribute groups. This repository uses a pinned local .NET tool manifest ([`.config/dotnet-tools.json`](.config/dotnet-tools.json)) with `XmlSchemaClassGenerator-console` (`dotnet-xscgen`):

```bash
# Restore local tools defined in .config/dotnet-tools.json
dotnet tool restore

# Regenerate C# classes from embedded XSDs via script:
./tools/generate-xsd-classes.sh

# Or via MSBuild target:
dotnet build -p:RegenerateXsdClasses=true
```

---

## Roadmap & Technical Debt

See [**`TECHNICAL_DEBT.md`**](TECHNICAL_DEBT.md) for the complete architectural backlog covering **Phase 2** and **Phase 3**:

- [x] **Phase 1 (`v1.0.0-rc.1`)**: Core Clean Architecture, Dual XSD + ISO Schematron XSLT validation engine, GML 3.2.1 Spatial ACL, WMO BUFR code-list registry (`WmoCodeRegistry`), and complete mapping of all 9 IWXXM 2025-2 **SIGWX / WAFS** phenomena (`JetStream`, `Cloud`, `Tropopause`, `Volcano`, `Turbulence`, `AirframeIcing`, `TropicalCyclone`, `Sandstorm`, `RadiationIncident`).
- [ ] **Phase 2 (`v1.1.0` – `v1.2.0`)**: Polymorphic `IIwxxmReport` dispatcher, En-Route & Volcanic Hazards (`QVACI`, `VONA`, `VolcanicAshAdvisory`, `TropicalCycloneAdvisory`, `SpaceWeatherAdvisory`, `SIGMET`, `AIRMET`), Aerodrome Reports (`METAR`/`SPECI`, `TAF`), and SWIM Collective Bulletin unwrapping (`collect:MeteorologicalBulletin`).
- [ ] **Phase 3 (`v1.0.0` GA)**: XML DoS character-bound hardening, zero-allocation `gml:posList` tokenizer, `BenchmarkDotNet` memory diagnostics suite, and GitHub Actions CI/CD pipeline.

---

## License

Licensed under the [MIT License](LICENSE).
