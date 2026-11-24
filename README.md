# Iwxxm.Net

**ICAO/WMO IWXXM 2025-2 aeronautical meteorology for .NET**: parse, validate, map and export METAR, SPECI, TAF, SIGMET, AIRMET, VAA, TCA, space-weather advisories, VONA, QVACI, WAFS SIGWX and SWIM/AMHS collective bulletins.

[![License: MIT](https://img.shields.io/badge/license-MIT-green.svg)](LICENSE)
[![Target frameworks](https://img.shields.io/badge/targets-netstandard2.0%20%7C%20netstandard2.1-purple)](https://learn.microsoft.com/en-us/dotnet/standard/net-standard)
[![.NET SDK](https://img.shields.io/badge/build%20SDK-.NET%2010-512bd4)](global.json)

`Iwxxm.Net` reads the XML, checks it against the official WMO schemas and rules, and hands you a small, immutable, strongly typed domain model, or an RFC 7946 GeoJSON document, RFC 4180 CSV, or (with an optional package) an OGC GeoPackage file. It has **no third-party runtime dependencies** and runs from .NET Framework 4.6.1 to .NET 10 (`netstandard2.0` and `netstandard2.1`).

New here? Start with the **[Getting started guide](docs/GUIDE.md)**: a progressive tour with runnable examples, diagrams, and worked examples that read jet streams and cumulonimbus clouds from an official WAFS chart.

## What you get

- **Every IWXXM 2025-2 product**, plus `collect:MeteorologicalBulletin` envelopes with partial results (one bad report does not discard the rest).
- **Honest data**: nothing is invented. A missing value is `null`; speeds and lengths keep their reported unit (`Speed`, `Length`); vertical limits keep unit and reference (`VerticalLimit`: FL, MSL, SFC); WMO code lists become typed `CodeListValue<T>` that never drop an unknown code.
- **Failures are values**: every operation returns a `Result<T>` with structured, stable error codes (`IwxxmErrorCodes` for the ones you branch on) and warnings; only argument mistakes throw.
- **Safe by default**: no DTDs, no external resolution, size and character limits, no network or file-system access at runtime. All schemas, rules and code lists are embedded.
- **Native RFC 7946 GeoJSON export**, with circles, polygon holes, antimeridian handling and measures written as reported, and an RFC 4180 CSV export with OGC WKT geometries.
- **Optional OGC GeoPackage export** (`Iwxxm.Net.Export.GeoPackage`): the features of the GeoJSON export in a `.gpkg` file that QGIS and GDAL open, one table per geometry kind plus the reports and the document metadata. It brings SQLite as a dependency; the core stays dependency-free.
- **Hexagonal architecture** with a public Schematron port (`Iwxxm.Net.Extensibility`) to replace the business-rule engine (the other adapters are internal); the layer rules are enforced by tests.

### Validation scope

| Gate | What runs by default | Complete option |
| :--- | :--- | :--- |
| **XSD** | The **unmodified official WMO IWXXM 2025-2 schemas** with their official imports (OGC GML 3.2.1, O&M 2.0, ISO 19139, AIXM 5.1.1, METCE, OPM, COLLECT, W3C), embedded byte for byte. | - |
| **Business rules** | **Partial**: an XSLT 1.0 subset of the official Schematron (**11 of the 165 rule ids**, some for a subset of their contexts), the membership of every `codes.wmo.int` reference in the **20 official WMO RDF code lists**, and a few library-specific checks. A document accepted by default may still violate official rules that are not implemented. | `UseOfficialSchematron()` from **`Iwxxm.Net.Validation.Saxon`** runs the unmodified `iwxxm.sch` (all 165 rules) with SaxonCS-HE. |

`iwxxm:SIGWXReport` is a **non-standard compact root** specific to this library (it is declared by no official schema) and is validated against a separate, hand-written schema subset. The official root is `iwxxm:WAFSSignificantWeatherForecast`.

## Supported report suite

| Category | Root XML element(s) | `IwxxmReportType` | Domain type |
| :--- | :--- | :--- | :--- |
| WAFS Significant Weather | `iwxxm:WAFSSignificantWeatherForecast`, `iwxxm:SIGWXReport` (non-standard) | `SignificantWeather` | `Sigwx` |
| Aerodrome routine observation | `iwxxm:METAR` | `Metar` | `Metar` (base `AerodromeObservation`) |
| Aerodrome special observation | `iwxxm:SPECI` | `Speci` | `Speci` (base `AerodromeObservation`) |
| Terminal aerodrome forecast | `iwxxm:TAF` | `Taf` | `Taf` |
| En-route SIGMET | `iwxxm:SIGMET` | `Sigmet` | `Sigmet` |
| Volcanic ash SIGMET | `iwxxm:VolcanicAshSIGMET` | `VolcanicAshSigmet` | `Sigmet` |
| Tropical cyclone SIGMET | `iwxxm:TropicalCycloneSIGMET` | `TropicalCycloneSigmet` | `Sigmet` |
| Low-level AIRMET | `iwxxm:AIRMET` | `Airmet` | `Airmet` |
| Volcano observatory notice | `iwxxm:VolcanoObservatoryNoticeForAviation` | `VolcanoObservatoryNotice` | `Vona` |
| Quantitative volcanic ash | `iwxxm:QuantitativeVolcanicAshConcentrationInformation` | `QuantitativeVolcanicAshConcentration` | `Qvaci` |
| Volcanic ash advisory | `iwxxm:VolcanicAshAdvisory` | `VolcanicAshAdvisory` | `Vaa` |
| Tropical cyclone advisory | `iwxxm:TropicalCycloneAdvisory` | `TropicalCycloneAdvisory` | `Tca` |
| Space weather advisory | `iwxxm:SpaceWeatherAdvisory` | `SpaceWeatherAdvisory` | `SpaceWx` |
| SWIM / AMHS collective bulletin | `collect:MeteorologicalBulletin` | `MeteorologicalBulletin` | `MeteorologicalBulletin` |

Every parsed document derives from the closed abstract root `IwxxmDocument`: either an `IwxxmReport` (the eleven report aggregates above, with their own status, usage and translation metadata) or the `MeteorologicalBulletin` envelope. See [section 4 of the guide](docs/GUIDE.md#4-which-document-did-i-get).

## Installation

Packages are published only for release tags `vMAJOR.MINOR.PATCH[-PRERELEASE]` (the tag is the package version) or a deliberate manual workflow run on a tag; pull requests and pushes to `main` build and test `-ci.<run>.<attempt>` prereleases that are never published.

Packages are published on [NuGet.org](https://www.nuget.org/packages/Iwxxm.Net) (with `.snupkg` symbols and SourceLink) and mirrored on GitHub Packages.

Install directly from NuGet.org:

```bash
dotnet add package Iwxxm.Net
dotnet add package Iwxxm.Net.Validation.Saxon   # optional: the complete official Schematron
dotnet add package Iwxxm.Net.Export.GeoPackage  # optional: export to OGC GeoPackage (.gpkg); brings SQLite
```

If your environment does not have `nuget.org` configured as a source, add it once:

```bash
dotnet nuget add source "https://api.nuget.org/v3/index.json" --name "nuget.org"
```

| Package | Targets | Runtime dependencies |
| :--- | :--- | :--- |
| `Iwxxm.Net` | `netstandard2.0`, `netstandard2.1` | none |
| `Iwxxm.Net.Validation.Saxon` | `net10.0` | `Iwxxm.Net`, `SaxonCS-HE` 13.1.0 (MPL-2.0, with its own transitive dependencies) |
| `Iwxxm.Net.Export.GeoPackage` | `netstandard2.0` | `Iwxxm.Net`, `Microsoft.Data.Sqlite` 10.0.15 (MIT), which brings SQLitePCLRaw 2.1.13 (Apache-2.0) and the native SQLite binaries |

**`Iwxxm.Net.Export.GeoPackage` is not part of a release yet** (it is listed under `Unreleased` in the [changelog](CHANGELOG.md)), so it is not on NuGet.org until the next tag that includes it; until then, build it from source (`dotnet pack src/Iwxxm.Net.Export.GeoPackage`).

**Size of the GeoPackage package.** The package itself is small, but restoring it downloads about 19 MB, 95% of it the native SQLite libraries for every supported platform (`SQLitePCLRaw.lib.e_sqlite3`). They end up in the application's `runtimes/` folder. Applications that do not export GeoPackages should not reference it. On .NET Framework there are extra steps; read [the notes in the guide](docs/GUIDE.md#net-framework-and-native-sqlite) before relying on it there.

Building from source needs the **.NET 10 SDK** (see [`global.json`](global.json)).

## Quick start

```csharp
using System;
using Iwxxm.Net;
using Iwxxm.Net.Reports;

Result<IwxxmDocument> result = IwxxmParser.ParseFile("samples/metar-A3-1.xml");

if (result.IsFailure)
{
    foreach (ValidationError error in result.Errors)
    {
        Console.Error.WriteLine(error);   // [Category:Severity] CODE [location]: message
    }
    return;
}

switch (result.Value)
{
    case Metar metar:
        Console.WriteLine($"METAR {metar.AerodromeDesignator} at {metar.ObservationTimeUtc:u}");
        Console.WriteLine($"  QNH {metar.QnhHpa} hPa, visibility {metar.PrevailingVisibilityMeters} m");
        Console.WriteLine($"  wind {metar.SurfaceWind?.MeanDirectionDegrees} deg at {metar.SurfaceWind?.MeanSpeed} ({metar.SurfaceWind?.MeanSpeed?.ToKnots():F1} kt)");
        break;

    case Taf taf:
        Console.WriteLine($"TAF {taf.AerodromeDesignator}: {taf.ChangeForecasts.Count} change groups");
        break;

    case Sigmet sigmet:
        Console.WriteLine($"{sigmet.ReportType} {sigmet.FirDesignator} #{sigmet.SequenceNumber}");
        break;

    default:
        Console.WriteLine($"{result.Value.ReportType}");
        break;
}
```
```output
METAR YUDO at 2012-08-22 16:30:00Z
  QNH 1018 hPa, visibility 600 m
  wind 240 deg at 4 m/s (7.8 kt)
```

The wind arrived in metres per second and stays that way until *you* ask for knots. That habit, "keep what was reported", runs through the whole library; the [guide](docs/GUIDE.md) explains it with real data.

### Which entry point do I use?

- **Common case**: `IwxxmParser.Default`, or the static `IwxxmParser.Parse*` helpers that wrap it (`IwxxmGeoJsonExporter.Default` likewise). Default limits, built-in rule subset. WAFS SIGWX and bulletins need no other entry point: `IwxxmParser.ParseFileAs<Sigwx>(path)` reads the official and the compact SIGWX root (`IwxxmSigwxParser` is an optional narrow facade over the same mapper).
- **Options or Schematron selection**: `IwxxmParserBuilder` (`WithOptions`, `UseSchematronValidator`, `WithoutSchematron`, and `UseOfficialSchematron()` from `Iwxxm.Net.Validation.Saxon`), then `Build()`.

The facade constructors are `internal`; `ISchematronValidator` and `IwxxmDocumentContext` are the only public extension points. Details in the [guide](docs/GUIDE.md#which-entry-point-do-i-use).

To export GeoJSON, CSV or GeoPackage, validate with the full official rule set, or tighten the security limits, see the guide's sections on [GeoJSON](docs/GUIDE.md#8-geojson-export), [CSV](docs/GUIDE.md#8a-csv-export), [GeoPackage](docs/GUIDE.md#8b-geopackage-export), [validation levels](docs/GUIDE.md#5-validation-levels) and [options](docs/GUIDE.md#6-options-and-security-limits).

### Export to GeoPackage

With `Iwxxm.Net.Export.GeoPackage` installed, parse and write a `.gpkg` file in one call. The result holds the full path of the file, or the usual failure values (`GEOPACKAGE-EXPORT-ERROR` when the file cannot be written):

```csharp
using System;
using System.IO;
using Iwxxm.Net;

Result<string> result = IwxxmParserGeoPackageExtensions.ParseFileToGeoPackage("samples/sigmet-A6-2-TC.xml", "sigmet.gpkg");
Console.WriteLine(result.IsSuccess ? $"written: {Path.GetFileName(result.Value)}" : result.Errors[0].ToString());
```
```output
written: sigmet.gpkg
```

The file has `iwxxm_points`, `iwxxm_lines` and `iwxxm_polygons` tables (only those that have rows), an `iwxxm_attributes` table for features without geometry, and `iwxxm_reports` and `iwxxm_metadata`. Each feature row keeps the GeoJSON properties as JSON text. The output is deterministic: no timestamp comes from the clock. Table layout, options, the .NET Framework requirements and what is not included are in the [guide](docs/GUIDE.md#8b-geopackage-export) and the [reference](docs/REFERENCE.md#geopackage-tables-and-columns).

## Documentation

| Document | What is in it |
| :--- | :--- |
| [Getting started guide](docs/GUIDE.md) | A didactic tour: pipeline, `Result<T>`, document types, validation, GeoJSON, CSV and GeoPackage export, value objects, and worked SIGWX (jet streams, clouds), METAR/TAF and SIGMET examples. |
| [Reference](docs/REFERENCE.md) | One example per product family, the composition root and ports, the GeoPackage table layout, the complete error-code table (every stable code, checked against the source by a test), value-object catalogue and namespace map. |
| [Documentation index](docs/README.md) | The map of everything under `docs/`. |
| [Contributing](CONTRIBUTING.md) | Pull request workflow, local setup, architecture rules, how to add a report family, golden-master tests. |
| [Code of conduct](CODE_OF_CONDUCT.md) | Contributor Covenant v2.1 community standards and reporting guidelines. |
| [Releasing](docs/RELEASING.md) | Versioning, the tag procedure, package contents and the v1.0.0 publication checklist. |
| [Third-party notices](THIRD-PARTY-NOTICES.md) | Licences and notices of the schemas, code lists, rules and packages that are redistributed or depended on. |
| [Security policy](SECURITY.md) | Supported versions, how to report a vulnerability privately, scope and secure-usage guidance. |
| [Changelog](CHANGELOG.md) | Release notes. |
| [Demo console app](samples/Iwxxm.Net.Demo/README.md) | A small `net10.0` consumer (not part of the solution, CI or packages): parses any IWXXM file, prints its data, can export GeoJSON, CSV and GeoPackage (`--geopackage`) and can run the complete official Schematron (`--official-schematron`). |

## Public namespaces

| Namespace | Contents |
| :--- | :--- |
| `Iwxxm.Net` | Facades (`IwxxmParser`, `IwxxmGeoJsonExporter`, `IwxxmCsvExporter`, and the optional SIGWX-only `IwxxmSigwxParser`), the composition root `IwxxmParserBuilder`, options, `Result<T>`, `ValidationError`, `ErrorCategory`, `IwxxmErrorCodes` |
| `Iwxxm.Net.Reports` | The domain model: the closed document hierarchy and every value object |
| `Iwxxm.Net.Extensibility` | `ISchematronValidator` and `IwxxmDocumentContext`, the extension point for custom business-rule engines |
| `Iwxxm.Net.Validation.Saxon` | `OfficialSchematronValidator` (optional package) |
| `Iwxxm.Net.Export.GeoPackage` | `IwxxmGeoPackageExporter`, `GeoPackageExportOptions`, `GeoPackageVersion` (optional package; its parser members are in `IwxxmParserGeoPackageExtensions`, namespace `Iwxxm.Net`) |

Consumers never need a layer name in a `using` directive; `Iwxxm.Net.Application.*`, `Iwxxm.Net.Infrastructure.*` and `Iwxxm20252.Generated` are `internal`. The layer map is in the [reference](docs/REFERENCE.md#namespaces-and-layers).

## Build, test and package

The reference build is the container pipeline, which runs `restore`, `build` (warnings as errors), `test` (with coverage, including the IL-level architecture tests and the official Schematron tests) and `pack` (both `.nupkg` and `.snupkg`, with a guard that `Iwxxm.Net` declares no dependency):

```bash
podman build --target test -t iwxxm-net-test .        # or: docker build --target test .
podman build --target pack --build-arg PACKAGE_VERSION=1.0.0 .

bash tools/run-local-ci.sh                            # full pipeline, artifacts in ./artifacts
BUILD_NUMBER=5 bash tools/run-local-ci.sh             # custom incremental build number
CONTAINER_ENGINE=podman bash tools/run-local-ci.sh    # use Podman instead of Docker
```

Without containers: `dotnet build Iwxxm.Net.slnx -c Release` and `dotnet test Iwxxm.Net.slnx -c Release` (details in [CONTRIBUTING.md](CONTRIBUTING.md)).

The local pipeline **never publishes**: the `ci` image only copies the artifacts to `/out`, and the script does not forward `GITHUB_TOKEN`. Packages are published by the GitHub Actions workflow. A deliberate local publish is an explicit opt-in that refuses to run unless every condition holds:

```bash
# Requires --publish, a dedicated key (GITHUB_TOKEN is ignored), an explicit version and a clean git tree
IWXXM_NUGET_PUBLISH_API_KEY=... PACKAGE_VERSION=1.2.3 bash tools/run-local-ci.sh --publish
```

Benchmarks (BenchmarkDotNet; indicative numbers in the [performance notes](docs/REFERENCE.md#performance-notes)):

```bash
dotnet run --project tests/Iwxxm.Net.Benchmarks/Iwxxm.Net.Benchmarks.csproj --configuration Release -- --filter "*"
```

The internal SIGWX transport model (`Infrastructure/Generated/Iwxxm20252.Generated.cs`) is maintained by hand; [CONTRIBUTING.md](CONTRIBUTING.md#sigwx-transport-model-hand-maintained) explains the optional `xscgen` reference scaffold.

## License and third-party notices

Licensed under the [MIT License](LICENSE). The complete list of third-party material, with licences, copyright notices and open points, is in [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md); the file is also at the root of both NuGet packages.

- The embedded official WMO IWXXM schemas, Schematron rules and code lists, and the imported OGC, ISO, EUROCONTROL and W3C schemas, are redistributed unmodified; their upstream ReadMe and release notes ship under `schema-notices/` in the `Iwxxm.Net` package. Some of those upstream sources state no licence; the open points are listed in [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md#12-open-points-for-the-maintainer).
- `Iwxxm.Net.Validation.Saxon` vendors **SchXslt 1.10.1** (Copyright (c) 2018-2021 David Maus, MIT License), shipped under `licenses/SchXslt/` in the package.
- `Iwxxm.Net.Export.GeoPackage` depends on **Microsoft.Data.Sqlite 10.0.15** (MIT), which brings **SQLitePCLRaw 2.1.13** (Apache-2.0) and the native **SQLite** binaries; nothing of it is copied into this repository's packages, the consumer's restore downloads it. See section 7A of [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
- `Iwxxm.Net.Validation.Saxon` depends on **SaxonCS-HE 13.1.0** (Saxonica Ltd.). Its bundled `LICENSE.txt` states that SaxonCS-HE is distributed as an "executable form" of the open-source SaxonJ-HE product under the **Mozilla Public License 2.0** (file-level copyleft that applies to Saxon's own files, not to code that merely uses it). No Saxonica licence key is required for the Home Edition features used here.
