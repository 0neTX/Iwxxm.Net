# Contributing to Iwxxm

Thank you for contributing to the **Iwxxm** .NET library! This document outlines the development setup, architectural guidelines, and build/packaging workflows.

---

## Prerequisites

- **.NET 8.0 SDK** or later (configured in [`global.json`](global.json)).
- Compatible with both **.NET Framework 4.8** and **.NET 8+** consumers via `netstandard2.0`.

---

## Local Development Setup

1. **Restore local .NET tools** (installs `dotnet-xscgen` from [`.config/dotnet-tools.json`](.config/dotnet-tools.json)):
   ```bash
   dotnet tool restore
   ```

2. **Restore & Build the Solution**:
   ```bash
   dotnet build Iwxxm.Net.sln -c Release
   ```

3. **Run Unit, Integration & Architecture Tests**:
   ```bash
   dotnet test Iwxxm.Net.sln -c Release
   ```

4. **Generate the NuGet & Symbol Packages (`.nupkg` and `.snupkg`)**:
   ```bash
   dotnet pack src/Iwxxm.Net/Iwxxm.Net.csproj -c Release -o ./artifacts
   ```

---

## Regenerating XSD Classes

Whenever embedded XSD schemas under `src/Iwxxm.Net/Resources/Xsd/` are updated, regenerate the internal transport classes in `Iwxxm20252.Generated`:

```bash
./tools/generate-xsd-classes.sh
```
Or via MSBuild:
```bash
dotnet build src/Iwxxm.Net/Iwxxm.Net.csproj -p:RegenerateXsdClasses=true
```

---

## Clean Architecture Rules

All contributions are verified automatically by [`CleanArchitectureComplianceTests`](tests/Iwxxm.Net.Tests/CleanArchitectureComplianceTests.cs):

1. **Zero Runtime Dependencies**: Do not add runtime `<PackageReference>` items to `src/Iwxxm.Net/Iwxxm.Net.csproj`. Only build-time analyzers/SourceLink packages with `PrivateAssets="all"` are permitted.
2. **Domain Layer (`Iwxxm.Net.Domain.*`)**: Must remain 100% pure C#. Never reference `System.Xml`, `Application`, `Infrastructure`, `Presentation`, or `Iwxxm20252.Generated`.
3. **Application Layer (`Iwxxm.Net.Application.*`)**: Must depend only on `Iwxxm.Net.Domain`. Never reference `System.Xml`, `Infrastructure`, or `Iwxxm20252.Generated`.
4. **Infrastructure Layer (`Iwxxm.Net.Infrastructure.*` & `Iwxxm20252.Generated`)**: All classes must be `internal` (encapsulated within the assembly).
5. **Coding Standards**: Follow [`.editorconfig`](.editorconfig) and write all XML documentation and code comments in **English**.
