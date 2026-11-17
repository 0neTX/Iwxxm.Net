## Summary

<!-- Describe what this pull request changes and why. Reference any related issue (e.g., Fixes #123). -->

## Type of change

- [ ] Bug fix (non-breaking change that fixes an issue)
- [ ] New feature (non-breaking change that adds functionality)
- [ ] Breaking change (fix or feature that would cause existing functionality to change)
- [ ] Documentation / build / CI update

## Checklist

- [ ] Code compiles cleanly in `Release` with zero warnings (`dotnet build Iwxxm.Net.slnx -c Release`)
- [ ] All unit, integration, and `HexagonalArchitectureTests` pass (`dotnet test Iwxxm.Net.slnx -c Release`)
- [ ] Added or updated tests covering the change (and `.NET Framework 4.8` compatibility preserved where applicable)
- [ ] Golden-master files under `tests/Iwxxm.Net.Tests/Golden/` are unchanged, or intentional changes are explained and updated
- [ ] Public members carry English XML documentation comments (`CS1591`) and runnable doc snippets remain valid
- [ ] Zero runtime `<PackageReference>` dependencies added to `src/Iwxxm.Net/Iwxxm.Net.csproj`
- [ ] Official WMO schemas (`Resources/Xsd/Official/`), code lists (`Resources/Codelists/`), and `iwxxm.sch` remain unmodified (or `THIRD-PARTY-NOTICES.md` updated if upgraded)
