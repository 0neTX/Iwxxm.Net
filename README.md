# Iwxxm.Net — ICAO/WMO IWXXM 2025-2 Aeronautical Meteorology Processor for .NET

[![GitHub Packages](https://img.shields.io/badge/GitHub%20Packages-v1.0.0--rc-blue)](https://github.com/0neTX/Iwxxm.Net/packages)
[![Target Framework](https://img.shields.io/badge/target-netstandard2.0%20%7C%20net48%20%7C%20net8.0-purple)](https://learn.microsoft.com/en-us/dotnet/standard/net-standard)
[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)

**`Iwxxm.Net`** is a zero-dependency **.NET Standard 2.0** library architected with **Domain-Driven Design (DDD)** and **Clean Architecture** for parsing, dual-validating (**XSD + ISO Schematron XSLT + Offline WMO RDF/SKOS Code-List Registries**), mapping, and exporting (**Native RFC 7946 GeoJSON**) the complete **ICAO / WMO IWXXM 2025-2** aeronautical meteorological report suite and **SWIM / AMHS Collective Bulletins** (`collect:MeteorologicalBulletin`).

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
   - [4.10 Dependency Injection (`Microsoft.Extensions.DependencyInjection`)](#410-dependency-injection-microsoftextensionsdependencyinjection)
5. [Domain Value Objects & Spatial Primitives](#domain-value-objects--spatial-primitives)
6. [Clean Architecture Overview](#clean-architecture-overview)
7. [Local Docker CI/CD Pipeline & Benchmarks](#local-docker-cicd-pipeline--benchmarks)
8. [Design-Time XSD Class Generation](#design-time-xsd-class-generation)
9. [License](#license)

---

## Supported IWXXM 2025-2 Report Suite

| Category | Root XML Element(s) | `IwxxmReportType` | Domain Aggregate Root |
| :--- | :--- | :--- | :--- |
| **WAFS Significant Weather** | `iwxxm:WAFSSignificantWeatherForecast`, `iwxxm:SIGWXReport` | `SignificantWeather` | `SignificantWeatherReport` |
| **Aerodrome Routine Observation** | `iwxxm:METAR` | `Metar` | `AerodromeWeatherReport` |
| **Aerodrome Special Observation** | `iwxxm:SPECI` | `Speci` | `AerodromeWeatherReport` |
| **Terminal Aerodrome Forecast** | `iwxxm:TAF` | `Taf` | `TafReport` |
| **En-Route SIGMET** | `iwxxm:SIGMET` | `Sigmet` | `SigmetReport` |
| **Volcanic Ash SIGMET** | `iwxxm:VolcanicAshSIGMET` | `VolcanicAshSigmet` | `SigmetReport` |
| **Tropical Cyclone SIGMET** | `iwxxm:TropicalCycloneSIGMET` | `TropicalCycloneSigmet` | `SigmetReport` |
| **Low-Level AIRMET** | `iwxxm:AIRMET` | `Airmet` | `AirmetReport` |
| **Volcano Observatory Notice** | `iwxxm:VolcanoObservatoryNoticeForAviation` | `VolcanoObservatoryNotice` | `VonaReport` |
| **Quantitative Volcanic Ash** | `iwxxm:QuantitativeVolcanicAshConcentrationInformation` | `QuantitativeVolcanicAshConcentration` | `QvaciReport` |
| **Volcanic Ash Advisory** | `iwxxm:VolcanicAshAdvisory` | `VolcanicAshAdvisory` | `VolcanicAshAdvisoryReport` |
| **Tropical Cyclone Advisory** | `iwxxm:TropicalCycloneAdvisory` | `TropicalCycloneAdvisory` | `TropicalCycloneAdvisoryReport` |
| **Space Weather Advisory** | `iwxxm:SpaceWeatherAdvisory` | `SpaceWeatherAdvisory` | `SpaceWeatherAdvisoryReport` |
| **SWIM / AMHS Collective Bulletin** | `collect:MeteorologicalBulletin` | `MeteorologicalBulletin` | `MeteorologicalBulletin` |

---

## Key Features

- **Zero Third-Party Runtime Dependencies**: Built 100% on the native .NET Base Class Library (`System.Xml`, `XmlSchemaSet`, `XslCompiledTransform`, `XmlSerializer`, `XDocument`). No external GIS, JSON, validation, or mediator packages required at runtime.
- **Broad .NET Compatibility**: Targets `netstandard2.0` for seamless execution across **.NET Framework 4.8** and **.NET 8+**.
- **Embedded Air-Gapped Schemas, Rules & WMO RDF Registries**: Bundles the XSD schemas (`iwxxm-2025-2.xsd`, `iwxxm-sigwx.xsd`, `collect.xsd`, `aixm.xsd`, `metce.xsd`, `gml.xsd`, `xlink.xsd`), precompiled ISO Schematron business rules (`iwxxm-2025-2-sigwx.xslt`), and the offline WMO No. 306 / 49-2 SKOS/RDF code-list registry (`codes.wmo.int-49-2.rdf`) as `<EmbeddedResource>` streams resolved strictly in-memory via `EmbeddedXmlUrlResolver` with zero disk or network I/O.
- **Dual Validation Pipeline**:
  1. **Structural Validation**: `XmlReaderSettings` against a lazily compiled, thread-safe `XmlSchemaSet` with XXE/DTD protection (`DtdProcessing.Prohibit`).
  2. **Business Rule & Offline RDF Code-List Validation**: Executes precompiled Schematron XSLT rules via `XslCompiledTransform` (including offline `document('../Codelists/codes.wmo.int-49-2.rdf')` lookups and WAFS BUFR code-table checks) and extracts SVRL (`<svrl:failed-assert>`) findings.
- **Native Spatial & WMO Code Value Objects**: Translates GML 3.2.1 geometries (`gml:Curve`, `gml:CubicSpline`, `gml:LineStringSegment`, `gml:Polygon`, `gml:PolygonPatch` with interior holes `gml:interior`, `gml:CircleByCenterPoint`, `gml:Point`, `gml:posList`, `gml:pos`) and AIXM 5.1.1 `AirspaceVolume` bounds into immutable Domain Value Objects (`GeoCoordinate`, `GeoPolygon`, `FlightLevel`) using a zero-array-allocation coordinate scanner (`GmlCoordinateTokenizer`).
- **Native RFC 7946 GeoJSON Export**: Converts any `IIwxxmReport` (or `collect:MeteorologicalBulletin`) directly into an RFC 7946 `FeatureCollection` with `[longitude, latitude]` coordinate tuples, polygon interior hole rings, and computed `bbox` bounds—with zero third-party JSON or GIS dependencies.
- **Functional `Result<T>` Pattern**: Explicit, allocation-friendly error handling categorized by pipeline stage (`XmlWellFormedness`, `XsdSchemaValidation`, `SchematronBusinessRule`, `Deserialization`, `DomainMapping`).

---

## Installation

Packages are published to **GitHub Packages** (`https://github.com/0neTX/Iwxxm.Net/packages`).

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

---

## Comprehensive Usage Examples

### 4.1 Polymorphic SWIM / AMHS Stream Processing (`IwxxmParser`)

When consuming heterogeneous SWIM, AMHS, or WMO WIS2 feeds where the incoming XML may be any IWXXM 2025-2 report or a `collect:MeteorologicalBulletin`, use `IwxxmParser.Parse` (or `IwxxmParser.ParseFile`):

```csharp
using System;
using Iwxxm.Net.Domain.Aggregates;
using Iwxxm.Net.Domain.Common;
using Iwxxm.Net.Presentation;

Result<IIwxxmReport> result = IwxxmParser.ParseFile("incoming-swim-message.xml");

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
    case AerodromeWeatherReport obs:
        Console.WriteLine($"[{obs.ReportType}] {obs.AerodromeDesignator} at {obs.ObservationTimeUtc:O} — QNH: {obs.QnhHpa} hPa");
        break;

    case TafReport taf:
        Console.WriteLine($"[TAF] {taf.AerodromeDesignator} ({taf.ValidFromUtc:O} to {taf.ValidUntilUtc:O}) — {taf.ChangeForecasts.Count} change groups");
        break;

    case SigmetReport sigmet:
        Console.WriteLine($"[{sigmet.ReportType}] FIR {sigmet.FirDesignator} #{sigmet.SequenceNumber} — Phenomenon: {sigmet.PhenomenonUri}");
        break;

    case AirmetReport airmet:
        Console.WriteLine($"[AIRMET] FIR {airmet.FirDesignator} #{airmet.SequenceNumber} — Phenomenon: {airmet.PhenomenonUri}");
        break;

    case SignificantWeatherReport sigwx:
        Console.WriteLine($"[SIGWX] Centre: {sigwx.IssuingCentre} ({sigwx.LowerLimit}-{sigwx.UpperLimit}) — JetStreams: {sigwx.JetStreams.Count}");
        break;

    case QvaciReport qvaci:
        Console.WriteLine($"[QVACI] Centre: {qvaci.OriginatingCentre} — Ash Contours: {qvaci.AshConcentrationClouds.Count}");
        break;

    case VonaReport vona:
        Console.WriteLine($"[VONA] Notice #{vona.NoticeNumber} — Colour Code: {vona.CurrentColourCodeUri}");
        break;

    case VolcanicAshAdvisoryReport vaa:
        Console.WriteLine($"[VAA] VAAC {vaa.IssuingVaacName} Advisory #{vaa.AdvisoryNumber} — Volcano: {vaa.Volcano?.Name}");
        break;

    case TropicalCycloneAdvisoryReport tca:
        Console.WriteLine($"[TCA] TCAC {tca.IssuingTcacDesignator} — Cyclone {tca.CycloneName} (#{tca.AdvisoryNumber})");
        break;

    case SpaceWeatherAdvisoryReport swx:
        Console.WriteLine($"[SWX] SWXC {swx.IssuingSwxcName} Advisory #{swx.AdvisoryNumber} — Regions: {swx.Analyses.Count}");
        break;

    case MeteorologicalBulletin bulletin:
        Console.WriteLine($"[BULLETIN] {bulletin.BulletinIdentifier} containing {bulletin.Reports.Count} valid reports");
        break;
}
```

---

### 4.2 Aerodrome Observations & Forecasts (`METAR`, `SPECI`, `TAF`)

#### Parsing `METAR` / `SPECI` Reports

```csharp
using System;
using Iwxxm.Net.Domain.Aggregates;
using Iwxxm.Net.Domain.Common;
using Iwxxm.Net.Presentation;

Result<AerodromeWeatherReport> metarResult =
    IwxxmParser.ParseFileAs<AerodromeWeatherReport>("Samples/wmo-examples/metar-A3-1.xml");

if (metarResult.IsSuccess)
{
    AerodromeWeatherReport metar = metarResult.Value;

    Console.WriteLine($"Report ID       : {metar.Id}");
    Console.WriteLine($"Type            : {metar.ReportType}"); // Metar or Speci
    Console.WriteLine($"Aerodrome       : {metar.AerodromeDesignator} ({metar.AerodromeName})");
    Console.WriteLine($"ARP Coordinate  : {metar.AerodromePosition}"); // e.g., (52.3086, 4.7639)
    Console.WriteLine($"Observation UTC : {metar.ObservationTimeUtc:O}");
    Console.WriteLine($"Automated (AUTO): {metar.IsAutomatedStation}");
    Console.WriteLine($"CAVOK           : {metar.CloudAndVisibilityOk}");
    Console.WriteLine($"Temperature     : {metar.AirTemperatureCelsius} °C (Dewpoint: {metar.DewpointTemperatureCelsius} °C)");
    Console.WriteLine($"Altimeter (QNH) : {metar.QnhHpa} hPa");
    Console.WriteLine($"Visibility      : {metar.PrevailingVisibilityMeters} m");

    if (metar.SurfaceWind != null)
    {
        Console.WriteLine($"Surface Wind    : {metar.SurfaceWind.MeanDirectionDegrees}° at {metar.SurfaceWind.MeanSpeedMetersPerSecond} m/s " +
                          $"(Gust: {metar.SurfaceWind.GustSpeedMetersPerSecond} m/s, Variable: {metar.SurfaceWind.IsVariableDirection})");
    }

    foreach (RunwayVisualRangeInfo rvr in metar.RunwayVisualRanges)
    {
        Console.WriteLine($"RVR Runway {rvr.RunwayDesignator}: {rvr.MeanRvrMeters} m (Tendency: {rvr.PastTendency})");
    }

    foreach (string wxUri in metar.PresentWeatherUris)
    {
        Console.WriteLine($"Present Weather : {wxUri}"); // e.g., http://codes.wmo.int/306/4678/RA
    }

    foreach (AerodromeCloudLayerInfo layer in metar.CloudLayers)
    {
        Console.WriteLine($"Cloud Layer     : Amount={layer.AmountUri}, Base={layer.BaseFeet} ft, Type={layer.CloudTypeUri}");
    }

    foreach (AerodromeForecastGroupInfo trend in metar.TrendForecasts)
    {
        Console.WriteLine($"TREND ({trend.ChangeIndicator}): Visibility={trend.PrevailingVisibilityMeters} m");
    }
}
```

#### Parsing `TAF` Reports (Base Forecast, `BECOMING`, `TEMPO`, `FM`, and Cancel Reports)

```csharp
using System;
using Iwxxm.Net.Domain.Aggregates;
using Iwxxm.Net.Domain.Common;
using Iwxxm.Net.Presentation;

Result<TafReport> tafResult =
    IwxxmParser.ParseFileAs<TafReport>("Samples/wmo-examples/taf-A5-1.xml");

if (tafResult.IsSuccess)
{
    TafReport taf = tafResult.Value;

    Console.WriteLine($"TAF for {taf.AerodromeDesignator} ({taf.ValidFromUtc:O} -> {taf.ValidUntilUtc:O})");
    Console.WriteLine($"Status: {taf.ReportStatus}, IsCancelReport: {taf.IsCancelReport}, IsNil: {taf.IsNilForecast}");

    if (taf.BaseForecast != null)
    {
        Console.WriteLine($"Base Visibility : {taf.BaseForecast.PrevailingVisibilityMeters} m");
        Console.WriteLine($"Base Wind       : {taf.BaseForecast.SurfaceWind?.MeanDirectionDegrees}° / {taf.BaseForecast.SurfaceWind?.MeanSpeedMetersPerSecond} m/s");
    }

    foreach (AerodromeForecastGroupInfo change in taf.ChangeForecasts)
    {
        Console.WriteLine($"Change [{change.ChangeIndicator}] ({change.ValidFromUtc:HH:mm}Z - {change.ValidUntilUtc:HH:mm}Z): " +
                          $"Vis={change.PrevailingVisibilityMeters} m, Weather={string.Join(", ", change.WeatherUris)}");
    }
}
```

---

### 4.3 En-Route Hazards (`SIGMET`, `VolcanicAshSIGMET`, `TropicalCycloneSIGMET`, `AIRMET`)

`SigmetReport` unifies standard `SIGMET`, `VolcanicAshSIGMET`, and `TropicalCycloneSIGMET` while preserving the specific `ReportType` discriminator:

```csharp
using System;
using Iwxxm.Net.Domain.Aggregates;
using Iwxxm.Net.Domain.Common;
using Iwxxm.Net.Presentation;

Result<SigmetReport> sigmetResult =
    IwxxmParser.ParseFileAs<SigmetReport>("Samples/wmo-examples/sigmet-VA-EGGX.xml");

if (sigmetResult.IsSuccess)
{
    SigmetReport sigmet = sigmetResult.Value;

    Console.WriteLine($"SIGMET Variant  : {sigmet.ReportType}"); // Sigmet, VolcanicAshSigmet, or TropicalCycloneSigmet
    Console.WriteLine($"ATS Unit / MWO  : {sigmet.AtsUnitDesignator} ({sigmet.AtsUnitName}) / MWO {sigmet.MwoDesignator}");
    Console.WriteLine($"FIR             : {sigmet.FirDesignator} ({sigmet.FirName})");
    Console.WriteLine($"Sequence Number : {sigmet.SequenceNumber}");
    Console.WriteLine($"Validity        : {sigmet.ValidFromUtc:O} to {sigmet.ValidUntilUtc:O}");
    Console.WriteLine($"Phenomenon URI  : {sigmet.PhenomenonUri}");

    if (sigmet.EruptingVolcano != null)
    {
        Console.WriteLine($"Erupting Volcano: {sigmet.EruptingVolcano.Name} at {sigmet.EruptingVolcano.Position}");
    }

    foreach (EnRoutePhenomenonVolume volume in sigmet.EvolvingConditions)
    {
        Console.WriteLine($"Evolving Volume {volume.Id}:");
        Console.WriteLine($"  Vertical Extent : {volume.LowerLimit?.ToString() ?? "SFC"} to {volume.UpperLimit}");
        Console.WriteLine($"  Intensity Change: {volume.IntensityChange}");
        Console.WriteLine($"  Motion          : {volume.DirectionOfMotionDegrees}° at {volume.SpeedOfMotionKnots} kt");
        Console.WriteLine($"  Polygon Vertices: {volume.Area?.ExteriorRing.Count ?? 0}");
    }

    foreach (EnRoutePhenomenonVolume forecast in sigmet.ForecastPositions)
    {
        Console.WriteLine($"Forecast Volume {forecast.Id}: {forecast.Area?.ExteriorRing.Count ?? 0} vertices");
    }
}
```

---

### 4.4 WAFS Significant Weather Forecasts (`WAFSSignificantWeatherForecast` & `SIGWXReport`)

You can parse WAFS SIGWX charts via either `IwxxmSigwxParser.ParseFile` or `IwxxmParser.ParseFileAs<SignificantWeatherReport>`:

```csharp
using System;
using Iwxxm.Net.Domain.Aggregates;
using Iwxxm.Net.Domain.Common;
using Iwxxm.Net.Domain.ValueObjects;
using Iwxxm.Net.Presentation;

Result<SignificantWeatherReport> result =
    IwxxmSigwxParser.ParseFile("Samples/wmo-examples/WAFS-Example.xml");

if (result.IsSuccess)
{
    SignificantWeatherReport sigwx = result.Value;

    Console.WriteLine($"WAFC Issuing Centre : {sigwx.IssuingCentre}");
    Console.WriteLine($"Validity Period     : {sigwx.ValidFromUtc:O} -> {sigwx.ValidUntilUtc:O}");
    Console.WriteLine($"Vertical Envelope   : {sigwx.LowerLimit} -> {sigwx.UpperLimit}");

    // 1. Jet Streams (CubicSpline / LineString trajectories + wind symbol core points)
    foreach (JetStream js in sigwx.JetStreams)
    {
        Console.WriteLine($"JetStream {js.Id}: Max {js.MaxWindSpeedKnots:F1} kt at {js.CoreFlightLevel} ({js.AxisTrajectory.Count} axis points, {js.CorePoints.Count} core symbols)");
    }

    // 2. Clouds (WMO BUFR 0-20-008 Distribution & 0-20-012 Cloud Type)
    foreach (Cloud cloud in sigwx.Clouds)
    {
        Console.WriteLine($"Cloud {cloud.Id}: {cloud.DistributionCode} {cloud.TypeCode} ({cloud.BaseFlightLevel?.ToString() ?? "XXX"}-{cloud.TopFlightLevel})");
    }

    // 3. Clear Air Turbulence (WMO BUFR 0-11-030 Extended Degree of Turbulence)
    foreach (Turbulence turb in sigwx.Turbulences)
    {
        Console.WriteLine($"Turbulence {turb.Id}: {turb.Degree} ({turb.BaseFlightLevel}-{turb.TopFlightLevel})");
    }

    // 4. Airframe Icing (WMO BUFR 0-20-041 Airframe Icing)
    foreach (AirframeIcing icing in sigwx.AirframeIcings)
    {
        Console.WriteLine($"Icing {icing.Id}: {icing.Degree} ({icing.BaseFlightLevel}-{icing.TopFlightLevel})");
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
using Iwxxm.Net.Domain.Aggregates;
using Iwxxm.Net.Domain.Common;
using Iwxxm.Net.Presentation;

Result<QvaciReport> qvaciResult =
    IwxxmParser.ParseFileAs<QvaciReport>("Samples/wmo-examples/qvaci-Example.xml");

if (qvaciResult.IsSuccess)
{
    QvaciReport qvaci = qvaciResult.Value;
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
var vaa = IwxxmParser.ParseFileAs<VolcanicAshAdvisoryReport>("Samples/wmo-examples/va-advisory-A7-2.xml").Value;
Console.WriteLine($"VAAC {vaa.IssuingVaacName} #{vaa.AdvisoryNumber}: {vaa.ObservedAshClouds.Count} observed, {vaa.ForecastAshClouds.Count} forecast clouds");

var tca = IwxxmParser.ParseFileAs<TropicalCycloneAdvisoryReport>("Samples/wmo-examples/tc-advisory-A2-2.xml").Value;
Console.WriteLine($"TCAC {tca.IssuingTcacDesignator} Cyclone {tca.CycloneName}: Obs Pressure={tca.Observation?.CentralPressureHpa} hPa, {tca.Forecasts.Count} forecast track points");

var swx = IwxxmParser.ParseFileAs<SpaceWeatherAdvisoryReport>("Samples/wmo-examples/spacewx-A7-3.xml").Value;
Console.WriteLine($"SWXC {swx.IssuingSwxcName} #{swx.AdvisoryNumber}: Effects=[{string.Join(", ", swx.Effects)}], {swx.Analyses.Count} analysis regions");
```

---

### 4.6 SWIM / AMHS Collective Bulletins (`collect:MeteorologicalBulletin`)

SWIM and AMHS networks frequently bundle multiple `METAR`, `SPECI`, or `TAF` reports inside a `<collect:MeteorologicalBulletin>` envelope. `MeteorologicalBulletin` exposes both `Reports` (all valid reports) and `ReportResults` (individual `Result<IIwxxmReport>` outcomes per `<collect:meteorologicalInformation>` entry):

```csharp
using System;
using Iwxxm.Net.Domain.Aggregates;
using Iwxxm.Net.Domain.Common;
using Iwxxm.Net.Presentation;

Result<MeteorologicalBulletin> bulletinResult =
    IwxxmParser.ParseBulletinFile("Samples/wmo-examples/metar-NIL-collect.xml");

if (bulletinResult.IsSuccess)
{
    MeteorologicalBulletin bulletin = bulletinResult.Value;
    Console.WriteLine($"Bulletin ID : {bulletin.BulletinIdentifier}");
    Console.WriteLine($"Valid Count : {bulletin.Reports.Count} / {bulletin.ReportResults.Count}");

    foreach (IIwxxmReport childReport in bulletin.Reports)
    {
        if (childReport is AerodromeWeatherReport metar)
        {
            Console.WriteLine($"  -> {metar.AerodromeDesignator} (NIL={metar.IsNilObservation}, QNH={metar.QnhHpa})");
        }
    }
}
```

---

### 4.7 Native RFC 7946 GeoJSON Export (`IwxxmGeoJsonExporter` & `ParseFileToGeoJson`)

`Iwxxm.Net` includes a zero-dependency **RFC 7946 GeoJSON Exporter** that converts any `IIwxxmReport` (or `MeteorologicalBulletin`) into a standards-compliant GeoJSON `FeatureCollection`:
- Automatically swaps GML `[latitude, longitude]` coordinates into **RFC 7946 Section 3.1.1 `[longitude, latitude]`** order.
- Preserves **interior hole rings** (`GeoPolygon.InteriorRings`) as secondary linear rings in GeoJSON `Polygon` coordinates (`[exteriorRing, hole1, ...]`).
- Computes the global **`bbox`** (`[minLon, minLat, maxLon, maxLat]`) across all features.
- Synthesizes a `TC_TRACK` `LineString` connecting observed and forecast tropical cyclone positions in `TropicalCycloneAdvisoryReport`.

```csharp
using System;
using Iwxxm.Net.Application.Ports;
using Iwxxm.Net.Domain.Common;
using Iwxxm.Net.Presentation;

// Option A: One-step XML -> Dual Validation -> Domain Aggregate -> RFC 7946 GeoJSON
Result<string> geoJsonResult = IwxxmParser.ParseFileToGeoJson(
    "Samples/wmo-examples/qvaci-Example.xml",
    new GeoJsonExportOptions(indented: true, includeBoundingBox: true));

if (geoJsonResult.IsSuccess)
{
    Console.WriteLine(geoJsonResult.Value);
}

// Option B: Export an already-parsed IIwxxmReport domain object
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
        "coreFlightLevel": "FL500",
        "coreFlightLevelHundredFeet": 500,
        "maxWindSpeedKnots": 120.13
      }
    }
  ]
}
```

---

### 4.8 Dual Validation & Structured Error Diagnostics (`Result<T>` & `ValidationError`)

Every parsing operation returns an immutable `Result<T>` without throwing exceptions on invalid XML, XSD schema violations, Schematron rule failures, or unregistered WMO RDF code-list URIs:

```csharp
using System;
using Iwxxm.Net.Domain.Aggregates;
using Iwxxm.Net.Domain.Common;
using Iwxxm.Net.Presentation;

Result<IIwxxmReport> result = IwxxmParser.Parse(untrustedXmlPayload);

if (result.IsFailure)
{
    foreach (ValidationError error in result.Errors)
    {
        switch (error.Category)
        {
            case ErrorCategory.XmlWellFormedness:
                Console.Error.WriteLine($"[XML Syntax / Size Limit] {error.Code} at Line {error.LineNumber}: {error.Message}");
                break;

            case ErrorCategory.XsdSchemaValidation:
                Console.Error.WriteLine($"[XSD Structural Violation] {error.Code} (Line {error.LineNumber}, Pos {error.LinePosition}): {error.Message}");
                break;

            case ErrorCategory.SchematronBusinessRule:
                // Includes WMO IWXXM Schematron assertions & offline codes.wmo.int-49-2.rdf lookups
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
using Iwxxm.Net.Application.Ports;
using Iwxxm.Net.Domain.Aggregates;
using Iwxxm.Net.Domain.Common;
using Iwxxm.Net.Presentation;

var strictOptions = new IwxxmParserOptions(
    maxDocumentBytes: 2 * 1024 * 1024,        // 2 MB maximum stream size
    maxCharactersInDocument: 2 * 1024 * 1024, // 2M character cap in XmlReader
    maxCharactersFromEntities: 512);

IIwxxmReportDispatcher dispatcher = IwxxmParser.CreateDispatcher(strictOptions);

using Stream incomingStream = GetNetworkStream();
Result<IIwxxmReport> result = dispatcher.Process(incomingStream);
// Streams exceeding maxDocumentBytes fail immediately with code "XML-SIZE-LIMIT-EXCEEDED"
```

---

### 4.10 Dependency Injection (`Microsoft.Extensions.DependencyInjection`)

All processors, dispatchers, and GeoJSON exporters are stateless and thread-safe, making them ideal for singleton registration in ASP.NET Core or Worker Service applications:

```csharp
using Iwxxm.Net.Application.Ports;
using Iwxxm.Net.Domain.Common;
using Iwxxm.Net.Presentation;
using Microsoft.Extensions.DependencyInjection;

services.AddSingleton<IIwxxmReportDispatcher>(_ =>
    IwxxmParser.CreateDispatcher(IwxxmParserOptions.Default));

services.AddSingleton<IIwxxmSigwxProcessor>(_ =>
    IwxxmSigwxParser.CreateProcessor(IwxxmParserOptions.Default));

services.AddSingleton<IGeoJsonExporter>(_ =>
    IwxxmGeoJsonExporter.CreateExporter());
```

---

## Domain Value Objects & Spatial Primitives

| Value Object | Namespace | Description |
| :--- | :--- | :--- |
| **`GeoCoordinate`** | `Iwxxm.Net.Domain.ValueObjects` | Immutable `readonly struct` enforcing WGS84 geodetic bounds (`Lat` $\in [-90, 90]$, `Lon` $\in [-180, 180]$). |
| **`GeoPolygon`** | `Iwxxm.Net.Domain.ValueObjects` | Immutable polygon enforcing $\ge 3$ distinct vertices, automatic ring closure, and optional `InteriorRings` (`IReadOnlyList<IReadOnlyList<GeoCoordinate>>`) for hollow contours. |
| **`FlightLevel`** | `Iwxxm.Net.Domain.ValueObjects` | Immutable vertical altitude (`readonly struct`) with automatic unit conversion from `FL` (hundreds of feet), `M` / `m` (ICAO standard conversion $1\text{ ft} = 0.3048\text{ m}$), and `[ft_i]` / `FT`. |
| **`WmoCodeRegistry`** | `Iwxxm.Net.Domain.ValueObjects` | Resolves WMO BUFR code-table URIs (`0-20-008`, `0-20-012`, `0-11-030`, `0-20-041`) and TAC abbreviations into `CloudDistributionCode`, `CloudTypeCode`, `TurbulenceDegreeCode`, and `IcingDegreeCode`. |

---

## Clean Architecture Overview

The library strictly enforces **Clean Architecture** and **Domain-Driven Design (DDD)**, verified by reflection-based architectural unit tests (`CleanArchitectureComplianceTests`):

```text
src/Iwxxm.Net/
├── Domain/                     # Pure C# Domain Model (Zero XML / Infrastructure dependencies)
│   ├── Common/                 # Result<T>, ValidationError, ErrorCategory, ValidationSeverity, IwxxmParserOptions
│   ├── ValueObjects/           # GeoCoordinate, GeoPolygon (with InteriorRings), FlightLevel, WmoCodes, SIGWX features
│   └── Aggregates/             # IIwxxmReport, SignificantWeatherReport, AerodromeWeatherReport, TafReport,
│                               # SigmetReport, AirmetReport, VonaReport, QvaciReport, VolcanicAshAdvisoryReport,
│                               # TropicalCycloneAdvisoryReport, SpaceWeatherAdvisoryReport, MeteorologicalBulletin
├── Application/                # Application Layer (Depends ONLY on Domain)
│   ├── Ports/                  # IIwxxmReportDispatcher, IIwxxmSigwxProcessor, IGeoJsonExporter, InternalValidationPorts
│   └── UseCases/               # DispatchIwxxmReportUseCase, ProcessSigwxReportUseCase<T>
├── Infrastructure/             # Internal Adapters (System.Xml, XSD, XSLT, Offline RDF, ACL Mapping, RFC 7946 GeoJSON)
│   ├── Generated/              # XSD-generated classes (namespace Iwxxm20252.Generated)
│   ├── Resources/              # EmbeddedResourceLoader & EmbeddedXmlUrlResolver (air-gapped XSD/XSLT/RDF)
│   ├── Validation/             # XsdValidator & XsltSchematronValidator (SVRL + offline RDF document() lookup)
│   ├── Serialization/          # XmlSerializerSigwxDeserializer
│   ├── Mapping/                # IwxxmSuiteMapper, SigwxMapper & GmlCoordinateTokenizer (Anti-Corruption Layer)
│   └── GeoJson/                # Rfc7946GeoJsonExporter (Zero-dependency RFC 7946 FeatureCollection writer)
└── Presentation/               # Public Facades & Composition Roots
    ├── IwxxmParser.cs          # Full IWXXM 2025-2 Suite & SWIM Collective Bulletin Parser
    ├── IwxxmSigwxParser.cs     # Specialized WAFS SIGWX Parser
    └── IwxxmGeoJsonExporter.cs # Native RFC 7946 GeoJSON Exporter Facade
```

---

## Local Docker CI/CD Pipeline & Benchmarks

### Running the Full CI/CD Pipeline Locally via Docker

You can execute all CI/CD stages (`restore` -> `architecture-audit` -> `build` -> `test` with code coverage -> `pack` `.nupkg` & `.snupkg`) locally inside an isolated `.NET 8.0 SDK` container:

```bash
# Run the full pipeline and export compiled NuGet packages + coverage to ./artifacts
bash tools/run-local-ci.sh

# Specify a custom incremental build number:
BUILD_NUMBER=5 bash tools/run-local-ci.sh

# Or target individual stages directly via docker build:
docker build --target build .
docker build --target test .
docker build --target pack --build-arg PACKAGE_VERSION=1.0.0-rc.2 .
```

### Running the BenchmarkDotNet Performance Suite

```bash
dotnet run --project tests/Iwxxm.Net.Benchmarks/Iwxxm.Net.Benchmarks.csproj --configuration Release
```

---

## Design-Time XSD Class Generation

This repository uses a pinned local .NET tool manifest ([`.config/dotnet-tools.json`](.config/dotnet-tools.json)) with `XmlSchemaClassGenerator-console` (`dotnet-xscgen`):

```bash
dotnet tool restore
./tools/generate-xsd-classes.sh
```

---

## License

Licensed under the [MIT License](LICENSE).
