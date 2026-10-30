# Contributing to Iwxxm

Thank you for contributing to the **Iwxxm** .NET library! This document outlines the development setup, architectural guidelines, and build/packaging workflows.

---

## Prerequisites

- **.NET 10 SDK** (configured in [`global.json`](global.json); native `.slnx` solution support).
- `Iwxxm.Net` multi-targets **`netstandard2.0`** and **`netstandard2.1`**; the optional `Iwxxm.Net.Validation.Saxon` package and the test projects target **`net10.0`**.
- The reference build is the container pipeline: `podman build --target test -t iwxxm-net-test .` (or `docker build`).

---

## Local Development Setup

1. **Restore local .NET tools** (installs `dotnet-xscgen` from [`.config/dotnet-tools.json`](.config/dotnet-tools.json), used only by the optional reference scaffold described below):
   ```bash
   dotnet tool restore
   ```

2. **Restore & Build the Solution (`.slnx`)**:
   ```bash
   dotnet build Iwxxm.Net.slnx -c Release
   ```

3. **Run Unit, Integration & Architecture Tests**:
   ```bash
   dotnet test Iwxxm.Net.slnx -c Release
   ```

4. **Generate the NuGet & Symbol Packages (`.nupkg` and `.snupkg`)**:
   ```bash
   dotnet pack src/Iwxxm.Net/Iwxxm.Net.csproj -c Release -o ./artifacts
   dotnet pack src/Iwxxm.Net.Validation.Saxon/Iwxxm.Net.Validation.Saxon.csproj -c Release -o ./artifacts
   ```

---

## SIGWX Transport Model (hand-maintained)

The internal transport classes in `Iwxxm20252.Generated` (`src/Iwxxm.Net/Infrastructure/Generated/Iwxxm20252.Generated.cs`) follow the hand-written, NON-STANDARD legacy schema subset under `src/Iwxxm.Net/Resources/Xsd/Legacy/`, but they are **maintained by hand**: they contain code that no generator reproduces, and no build target regenerates them. When the legacy schemas change, edit the file by hand. To compare against a fresh generator scaffold, run:

```bash
./tools/generate-xsd-classes.sh
```

It writes the raw `xscgen` output to `artifacts/xscgen-reference/` (git-ignored) and never touches the hand-maintained file. The file name and namespace are historical; Roslyn still treats `*.Generated.cs` files as generated code, so analyzers skip it.

The official WMO IWXXM 2025-2 schemas and their imports (`src/Iwxxm.Net/Resources/Xsd/Official/`) and the official RDF code lists (`src/Iwxxm.Net/Resources/Codelists/`) are byte-identical copies of [wmo-im/iwxxm](https://github.com/wmo-im/iwxxm) (`IWXXM/`, `externalSchema/`, `IWXXM/rule/*.rdf`). Never edit them; to upgrade, copy the new upstream files unchanged, keeping their relative paths, and include only the files reachable from `iwxxm.xsd` / `iwxxm-collect.xsd`.

The same rule applies to `src/Iwxxm.Net.Validation.Saxon/Resources/Schematron/iwxxm.sch` (official `IWXXM/rule/iwxxm.sch`, SHA-256 checked by a test) and to the vendored SchXslt 1.10.1 files under `src/Iwxxm.Net.Validation.Saxon/Resources/SchXslt/` (keep `LICENSE`). The Saxon package reads the RDF code lists from the `Iwxxm.Net` assembly through their manifest names `Iwxxm.Net/Resources/Codelists/*.rdf`; renaming them is a breaking change for that package.

---

## Hexagonal Architecture Rules

All contributions are verified automatically by IL-level tests in [`HexagonalArchitectureTests`](tests/Iwxxm.Net.Tests/HexagonalArchitectureTests.cs) (NetArchTest.Rules / Mono.Cecil, test-only). Layers are identified by folder-aligned internal namespaces plus an explicit catalog of the public root-namespace types (`ArchitectureLayers`); a new public root type fails the build until it is assigned to a layer.

| Layer | Folder | Namespace(s) | May depend on |
| :--- | :--- | :--- | :--- |
| Domain | `Domain/` | `Iwxxm.Net.Reports`; `Result<T>`, `ValidationError`, `ErrorCategory`, `ValidationSeverity` in `Iwxxm.Net` | nothing (no `System.Xml`, `System.IO`, `System.Net`, `System.Reflection`) |
| Application | `Application/` (`Options`, `Ports/In`, `Ports/Out`, `UseCases`) | `Iwxxm.Net` (options, inbound ports), `Iwxxm.Net.Extensibility` (outbound ports), `Iwxxm.Net.Application.*` (internal use cases) | Domain (no `System.Xml`) |
| Infrastructure | `Infrastructure/` | `Iwxxm.Net.Infrastructure.*`, `Iwxxm20252.Generated` (all `internal`) | Domain, Application; never facades or the composition root |
| Composition root | `Composition/` | `IwxxmParserBuilder` in `Iwxxm.Net` | everything |
| Presentation | `Presentation/` | facades in `Iwxxm.Net` | Domain, Application options and inbound ports, composition root (default instances only); never Infrastructure, use cases or outbound ports |

Further rules:

1. **Zero Runtime Dependencies**: Do not add runtime `<PackageReference>` items to `src/Iwxxm.Net/Iwxxm.Net.csproj`. Only build-time packages with `PrivateAssets="all"` (SourceLink) are permitted. The pack stage fails if the `Iwxxm.Net` nuspec declares a dependency. Optional engines go to separate adapter packages (e.g. `Iwxxm.Net.Validation.Saxon`) that implement a public outbound port.
2. **Public API**: public types live only in `Iwxxm.Net`, `Iwxxm.Net.Reports` and `Iwxxm.Net.Extensibility`; consumers must never need a layer name in a `using` directive. Outbound ports use technology-neutral signatures (streams, `Result<T>`, `ValidationError`), never `System.Xml` types.
3. **Error contract**: `ValidationError.Code` strings are the stable contract; `ErrorCategory` names stay technology-agnostic.
4. **Coding Standards**: Follow [`.editorconfig`](.editorconfig) and write all XML documentation and code comments in **English**.

---

## Adding or Changing a Report Family

IWXXM 2025-2 defines a closed catalog of report types, so extension points are static (no reflection or plug-ins). Each report family is isolated in its own files so that a change to one family cannot regress another:

1. **Domain**: add the aggregate in `Domain/Aggregates` (namespace `Iwxxm.Net.Reports`) as a `sealed` class deriving from `IwxxmReport` (the hierarchy under the closed root `IwxxmDocument` is closed: every abstract type has a `private protected` constructor, so only this assembly can add document types; `MeteorologicalBulletin` is the only non-report branch), override the abstract members, implement `Accept<TResult>` as `visitor.Visit(this)`, and add a `Visit` overload to `IIwxxmDocumentVisitor<TResult>`. Use the operational name of the report (`Metar`, `Taf`, `Sigmet`, ...; no `Report` suffix) and put its value objects in `Domain/ValueObjects` with structural equality (`IEquatable<T>`, `==`/`!=`). Code-list references are translated in `Infrastructure/Mapping/CodeLists` into `CodeListValue<TCode>` (table checked against the embedded official RDF by `CodeListTranslationTests`), never stored as URIs. Every visitor (including `GeoJsonFeatureCollector`) then fails to compile until it handles the new type.
2. **Mapping (LINQ to XML)**: add `Infrastructure/Mapping/Reports/<Family>Mapper.cs` with a static `Map(XElement root)` that starts with `ReportHeader.Read(root, errors)` and reuses the shared readers in `Infrastructure/Mapping/Xml` (`GmlTimeReader`, `GmlGeometryReader`, `AixmFeatureReader`, `XmlValueReader`, `MappingErrors`, `UcumUnits`) and `UomMeasureReader` / `VerticalLimitReader`. Register the root element QName in `IwxxmReportMapperRegistry`; `IwxxmSuiteMapper` stays a thin composite (document loading, SIGWX and bulletin dispatch).
3. **SIGWX phenomena**: add or change the extractor of the phenomenon family in `Infrastructure/Mapping/Wafs` and, for WAFS features, its phenomenon suffix in the `WafsForecastMapper` table.
4. **GeoJSON**: add `Infrastructure/GeoJson/Features/<Family>FeatureBuilder.cs` (build properties with `GeoJsonProperties`, return `GeoJsonFeature` instances) and call it from the new `Visit` overload of `GeoJsonFeatureCollector`. The JSON writers in `Infrastructure/GeoJson/Writing` are shared and should not need changes.
5. Add an official example to `tests/Iwxxm.Net.Tests/Samples/wmo-examples` (byte-identical) and generate its golden files (below).

Keep each file to one responsibility (as a guide, under about 300 lines).

---

## Golden-Master (Characterization) Tests

[`GoldenMasterTests`](tests/Iwxxm.Net.Tests/GoldenMaster/GoldenMasterTests.cs) compare, for every official example in `Samples/wmo-examples` and both SIGWX samples, three outputs against committed files in `tests/Iwxxm.Net.Tests/Golden/{suite,sigwx}/`:

- `<sample>.domain.txt`: the full mapped Domain model, written by the test-only `CanonicalDomainDumper` (reflection over public properties in ordinal order, invariant culture, round-trip numbers, ISO 8601 instants);
- `<sample>.pretty.geojson`: GeoJSON with `GeoJsonExportOptions.Pretty`;
- `<sample>.compact.geojson`: compact GeoJSON without `bbox`.

A refactor must leave every golden file unchanged. If a behaviour change is intentional, document it in `CHANGELOG.md` and regenerate the goldens by running the tests with `IWXXM_UPDATE_GOLDEN=1` (files are written to the source `Golden/` directory, found by walking up from the test output directory to `Iwxxm.Net.Tests.csproj`), or with `IWXXM_UPDATE_GOLDEN=<directory>` to write them elsewhere. Then review the diff before committing:

```bash
IWXXM_UPDATE_GOLDEN=1 dotnet test tests/Iwxxm.Net.Tests/Iwxxm.Net.Tests.csproj -c Release --filter "FullyQualifiedName~GoldenMaster"
git diff --stat tests/Iwxxm.Net.Tests/Golden
```

With the container build, mount the golden directory and pass the variable:

```bash
podman build --target build -t iwxxm-net-build .
podman run --rm -v "$PWD/tests/Iwxxm.Net.Tests/Golden:/golden:Z" -e IWXXM_UPDATE_GOLDEN=/golden iwxxm-net-build \
  dotnet test tests/Iwxxm.Net.Tests/Iwxxm.Net.Tests.csproj -c Release --no-build --filter "FullyQualifiedName~GoldenMaster"
```
