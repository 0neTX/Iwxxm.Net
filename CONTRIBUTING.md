# Contributing to Iwxxm

Thank you for contributing to the **Iwxxm** .NET library! This document outlines the development setup, architectural guidelines, and build/packaging workflows.

---

## Prerequisites

- **.NET 10 SDK** (configured in [`global.json`](global.json); native `.slnx` solution support).
- `Iwxxm.Net` multi-targets **`netstandard2.0`** and **`netstandard2.1`**; the optional `Iwxxm.Net.Validation.Saxon` package and the test projects target **`net10.0`**.
- The reference build is the container pipeline: `podman build --target test -t iwxxm-net-test .` (or `docker build`).

---

## Local Development Setup

1. **Restore local .NET tools** (installs `dotnet-xscgen` from [`.config/dotnet-tools.json`](.config/dotnet-tools.json)):
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

## Regenerating XSD Classes

The internal transport classes in `Iwxxm20252.Generated` are generated from the hand-written, NON-STANDARD legacy schema subset under `src/Iwxxm.Net/Resources/Xsd/Legacy/`. Regenerate them whenever those files change:

```bash
./tools/generate-xsd-classes.sh
```
Or via MSBuild:
```bash
dotnet build src/Iwxxm.Net/Iwxxm.Net.csproj -p:RegenerateXsdClasses=true
```

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
