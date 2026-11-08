# Iwxxm.Net.Demo

A small `net8.0` console application that consumes the library the way an application would. It parses one IWXXM 2025-2 file (any report type, a WAFS SIGWX forecast, or a `collect:MeteorologicalBulletin`), prints its structured data and, on request, exports it as GeoJSON.

It is a **sample**, not part of the product: it is not in `Iwxxm.Net.slnx`, the Dockerfile, the CI workflow or the NuGet packages, and `dotnet test` / `dotnet pack` of the repository never see it.

## What it demonstrates

| Topic | Where |
| :--- | :--- |
| Entry point: `IwxxmParser.Default` (or `IwxxmParserBuilder().WithoutSchematron().Build()` for `--no-schematron`) | `Program.cs` |
| `Result<T>`: errors and warnings (code, severity, category, location, line) are always printed | `Program.cs`, `ConsoleReport.cs` |
| The closed document hierarchy through `IIwxxmDocumentVisitor<TResult>` (adding a document type to the library breaks the build here) | `DocumentSummaryVisitor.cs` |
| Value objects: `Speed`/`Length` with `Amount` and unit, `VerticalLimit`, `CodeListValue<T>`, `GeoSurface.Match` (polygon or circle) | `FactSheet.cs` |
| `IwxxmGeoJsonExporter.ToGeoJson` on an already parsed document | `Program.cs` |

Values the message does not carry are shown as `-`; nothing is defaulted.

## Build and run

The repository pins SDK 10 in `global.json`; the sample targets `net8.0` and references the core library (`netstandard2.0`/`2.1`) by project. Building needs network access once, to restore the `net8.0` targeting pack.

With a local SDK 10:

```bash
dotnet run --project samples/Iwxxm.Net.Demo -c Release -- tests/Iwxxm.Net.Tests/Samples/wmo-examples/metar-A3-1.xml
```

With podman only (nothing installed on the host). Build with the SDK 10 image into a scratch directory, run on a real .NET 8 runtime:

```bash
# 1. Build (the repository is mounted read-write, so output goes to a separate directory)
mkdir -p /tmp/iwxxm-demo
podman run --rm -v "$PWD":/workspace -v /tmp/iwxxm-demo:/out -w /workspace \
  mcr.microsoft.com/dotnet/sdk:10.0.403 \
  dotnet build samples/Iwxxm.Net.Demo/Iwxxm.Net.Demo.csproj -c Release -o /out

# 2. Run on the .NET 8 runtime
podman run --rm \
  -v /tmp/iwxxm-demo:/app:ro \
  -v "$PWD/tests/Iwxxm.Net.Tests/Samples":/samples:ro \
  -v /tmp/iwxxm-geo:/geo \
  mcr.microsoft.com/dotnet/runtime:8.0 \
  dotnet /app/Iwxxm.Net.Demo.dll /samples/wmo-examples/metar-A3-1.xml
```

(`dotnet build` also writes `bin/` and `obj/` folders under `src/Iwxxm.Net` and `samples/`; they are git-ignored. Copy the repository to a scratch directory first if you want a pristine tree.)

## Usage

```text
Iwxxm.Net.Demo <file.xml> [options]

  --geojson [path|-]   Also export RFC 7946 GeoJSON. Default target: the input path with a
                       .geojson extension; '-' writes the GeoJSON to stdout (the report then
                       goes to stderr).
  -o, --output <path>  Same as --geojson <path>.
  --pretty             Indent the GeoJSON.
  --no-schematron      Skip the built-in business rules (XSD validation only).
  --no-color           Never use ANSI colours (also off when output is redirected or NO_COLOR is set).
  -h, --help           Show the help.
```

The input file must be the first argument. `--geojson` takes the next argument as its path unless it starts with `-` (except a lone `-`), so put it after the input file.

Examples:

```bash
Iwxxm.Net.Demo taf-A5-1.xml
Iwxxm.Net.Demo sigmet-A6-2-TC.xml --geojson                  # writes sigmet-A6-2-TC.geojson
Iwxxm.Net.Demo spacewx-A7-4.xml -o out/swx.geojson --pretty
Iwxxm.Net.Demo WAFS-Example.xml --geojson - > wafs.geojson   # report on stderr, GeoJSON on stdout
Iwxxm.Net.Demo bulletin.xml --no-schematron
```

## Exit codes

| Code | Meaning |
| :---: | :--- |
| 0 | Parsed (warnings may be present) and, if requested, exported |
| 1 | Parse or validation errors, or the GeoJSON export failed |
| 2 | Usage error (unknown option, missing or extra input, GeoJSON target equal to the input) |
| 3 | I/O error (input missing or unreadable, output not writable) |

Bad input never throws: the library returns a failed `Result<T>`, and the demo maps its `FILE-NOT-FOUND`, `ACCESS-DENIED` and `FILE-READ-ERROR` codes to exit code 3.

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
