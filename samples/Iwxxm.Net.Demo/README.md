# Iwxxm.Net.Demo

A small `net10.0` console application that consumes the library the way an application would. It parses one IWXXM 2025-2 file (any report type, a WAFS SIGWX forecast, or a `collect:MeteorologicalBulletin`), prints its structured data and, on request, exports it as GeoJSON. With `--official-schematron` it validates against the complete official WMO Schematron (165 rules) through the optional `Iwxxm.Net.Validation.Saxon` package.

It is a **sample**, not part of the product: it is not in `Iwxxm.Net.slnx`, the Dockerfile, the CI workflow or the NuGet packages, and `dotnet test` / `dotnet pack` of the repository never see it.

## What it demonstrates

| Topic | Where |
| :--- | :--- |
| Entry point: `IwxxmParser.Default`, or `IwxxmParserBuilder` for `--no-schematron` (`WithoutSchematron()`) and `--official-schematron` (`UseOfficialSchematron()` from `Iwxxm.Net.Validation.Saxon`) | `Program.cs` |
| `Result<T>`: errors and warnings (code, severity, category, location, line) are always printed | `Program.cs`, `ConsoleReport.cs` |
| The closed document hierarchy through `IIwxxmDocumentVisitor<TResult>` (adding a document type to the library breaks the build here) | `DocumentSummaryVisitor.cs` |
| Value objects: `Speed`/`Length` with `Amount` and unit, `VerticalLimit`, `CodeListValue<T>`, `GeoSurface.Match` (polygon or circle) | `FactSheet.cs` |
| `builder.BuildGeoJsonExporter().Export` on an already parsed document, from the same builder as the parser (returns a `Result<string>`); `ErrorCategory.Io` to tell an unreadable input from a bad message | `Program.cs` |

Values the message does not carry are shown as `-`; nothing is defaulted.

## Build and run

The repository pins SDK 10 in `global.json`; the sample targets `net10.0` because the optional `Iwxxm.Net.Validation.Saxon` project it references only targets `net10.0` (SaxonCS-HE 13.x ships a single `net10.0` asset). The core library still targets `netstandard2.0`/`2.1`. SaxonCS-HE restores transitively through that project reference (its version is pinned centrally in `Directory.Packages.props`), so building needs network access once, to restore it.

The sample sets `InvariantGlobalization` to `false`: SaxonCS resolves the `en` culture and the official Schematron cannot be compiled in globalization-invariant mode. The Debian-based `runtime` image includes ICU, so no extra package is needed.

With a local SDK 10:

```bash
dotnet run --project samples/Iwxxm.Net.Demo -c Release -- tests/Iwxxm.Net.Tests/Samples/wmo-examples/metar-A3-1.xml
```

With podman only (nothing installed on the host). Build with the SDK 10 image into a scratch directory, run on the .NET 10 runtime image (`runtime`, not `aspnet`; the Saxon package needs nothing beyond the base runtime):

```bash
# 1. Build (the repository is mounted read-write, so output goes to a separate directory)
mkdir -p /tmp/iwxxm-demo
podman run --rm -v "$PWD":/workspace -v /tmp/iwxxm-demo:/out -w /workspace \
  mcr.microsoft.com/dotnet/sdk:10.0.403 \
  dotnet build samples/Iwxxm.Net.Demo/Iwxxm.Net.Demo.csproj -c Release -o /out

# 2. Run on the .NET 10 runtime
podman run --rm \
  -v /tmp/iwxxm-demo:/app:ro \
  -v "$PWD/tests/Iwxxm.Net.Tests/Samples":/samples:ro \
  -v /tmp/iwxxm-geo:/geo \
  mcr.microsoft.com/dotnet/runtime:10.0 \
  dotnet /app/Iwxxm.Net.Demo.dll /samples/wmo-examples/metar-A3-1.xml

# 3. Same file against the complete official Schematron (165 rules)
podman run --rm \
  -v /tmp/iwxxm-demo:/app:ro \
  -v "$PWD/tests/Iwxxm.Net.Tests/Samples":/samples:ro \
  mcr.microsoft.com/dotnet/runtime:10.0 \
  dotnet /app/Iwxxm.Net.Demo.dll /samples/wmo-examples/metar-A3-1.xml --official-schematron
```

(`dotnet build` also writes `bin/` and `obj/` folders under `src/Iwxxm.Net` and `samples/`; they are git-ignored. Copy the repository to a scratch directory first if you want a pristine tree.)

## Usage

```text
Iwxxm.Net.Demo <file.xml> [options]

  --geojson [path|-]   Also export RFC 7946 GeoJSON. Default target: the input path with a
                       .geojson extension; '-' writes the GeoJSON to stdout (the report then
                       goes to stderr).
  --csv [path|-]       Also export RFC 4180 CSV (with OGC WKT geometries). Default target: the
                       input path with a .csv extension; '-' writes the CSV to stdout (the
                       report then goes to stderr).
  -o, --output <path>  Same as --geojson <path>.
  --pretty             Indent the GeoJSON.
  --no-schematron      Skip the built-in business rules (XSD validation only).
  --official-schematron
                       Run the complete official WMO Schematron (all 165 rules, SaxonCS-HE)
                       instead of the built-in subset. Cannot be combined with --no-schematron.
  --no-color           Never use ANSI colours (also off when output is redirected or NO_COLOR is set).
  -h, --help           Show the help.
```

The input file must be the first argument. `--geojson` takes the next argument as its path unless it starts with `-` (except a lone `-`), so put it after the input file.

Examples:

```bash
Iwxxm.Net.Demo taf-A5-1.xml
Iwxxm.Net.Demo sigmet-A6-2-TC.xml --geojson                  # writes sigmet-A6-2-TC.geojson
Iwxxm.Net.Demo sigmet-A6-2-TC.xml --csv                      # writes sigmet-A6-2-TC.csv
Iwxxm.Net.Demo spacewx-A7-4.xml -o out/swx.geojson --pretty
Iwxxm.Net.Demo WAFS-Example.xml --geojson - > wafs.geojson   # report on stderr, GeoJSON on stdout
Iwxxm.Net.Demo bulletin.xml --no-schematron
Iwxxm.Net.Demo sigmet-A6-1a-TS.xml --official-schematron      # all 165 official rules
```

## Exit codes

| Code | Meaning |
| :---: | :--- |
| 0 | Parsed (warnings may be present) and, if requested, exported |
| 1 | Parse or validation errors, or the GeoJSON export failed, or the official Schematron engine could not be initialised (`SCHEMATRON-UNAVAILABLE` / `SCHEMATRON-EXECUTION-ERROR`, or an exception while loading Saxon, reported as a one-line message) |
| 2 | Usage error (unknown option, missing or extra input, GeoJSON target equal to the input) |
| 3 | I/O error (input missing or unreadable, output not writable) |

Bad input never throws: the library returns a failed `Result<T>`, and the demo maps every `ErrorCategory.Io` error (`FILE-NOT-FOUND`, `ACCESS-DENIED`, `FILE-READ-ERROR`, ...) to exit code 3.

## Official Schematron

By default the demo runs the built-in XSLT 1.0 subset (11 of the 165 official rules), and the report header says so (`Validation:`). `--official-schematron` builds the parser with `new IwxxmParserBuilder().UseOfficialSchematron()`, which runs the unmodified `iwxxm.sch` with SaxonCS-HE. Its findings are printed like any other: official rule id as code, severity, category and an absolute XPath as location. Costs to expect: the first run compiles the stylesheet (about 1 to 1.5 s for a small file; the report header prints the measured parse-and-validate time), and the published image and output folder grow by the SaxonCS-HE assemblies.

## Sample output

```text
== Iwxxm.Net demo ========================================================
Input:                    /samples/wmo-examples/metar-A3-1.xml
Validation:               XSD + built-in business rules
Document type:            Metar
Id:                       uuid.510df5de-fefb-4406-bafd-faab35333ec0
Status:                   Normal
Permissible usage:        Operational
Issue time (UTC):         2012-08-22T16:30:00Z
Aerodrome:                YUDO (DONLON/INTERNATIONAL)
Surface wind:             240 deg at 4 m/s (~7.8 kt)
Visibility:               600 m
...
OK: Metar parsed, 0 warning(s).
```
