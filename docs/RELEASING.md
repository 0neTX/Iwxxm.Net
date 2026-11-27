# Releasing

How a version is built and published, what the packages contain, what the workflow does when a tag is pushed, and what must be checked before and after the first stable version, v1.0.0.

## Versioning and tags

- Versions follow [Semantic Versioning 2.0](https://semver.org/spec/v2.0.0.html). The version of both packages is the tag name without the leading `v`: tag `v1.0.0` publishes `1.0.0`; tag `v1.0.0-rc.3` publishes `1.0.0-rc.3`.
- A stable version has no prerelease suffix (`v1.0.0`). Only a tag with a suffix (`v1.0.0-rc.3`) publishes a prerelease. Consumers need `--prerelease` (or an explicit `--version`) in `dotnet add package` only for a prerelease; once `1.0.0` is on the feed, `dotnet add package Iwxxm.Net` resolves it without any flag.
- Only tags `vMAJOR.MINOR.PATCH[-PRERELEASE]` publish (for example `v1.0.0-rc.3`, `v1.0.0`). Any other tag name fails the version check of the workflow.
- Tag only commits that are on `main`. Both publish jobs of `.github/workflows/ci-cd.yml` refuse a tagged commit that is not reachable from `origin/main`.
- Pushes to `main` and pull requests build and test `-ci.<run>.<attempt>` prereleases that are never published.
- The release candidates are numbered in sequence (`v1.0.0-rc.1`, `v1.0.0-rc.2`, ...). The next candidate is the last one plus one; v1.0.0 follows the last release candidate.
- **A published version is immutable.** Neither feed lets you replace a version with different content, and NuGet.org does not allow deleting one (it can only be unlisted). Do not move or re-push a release tag; if something is wrong after publication, fix it and publish a new version (for example `1.0.1`).
- The workflow pushes with `--skip-duplicate`: pushing a tag whose version already exists on a feed is not an error and publishes nothing new on that feed.

## What the workflow does on a tag

Pushing a tag `v*` starts `.github/workflows/ci-cd.yml` (a manual run selected on a tag does the same):

1. **`build-and-test`** (Ubuntu and Windows): computes the package version from the tag, rejecting anything that is not `vMAJOR.MINOR.PATCH[-PRERELEASE]`; restores, builds in `Release` with warnings as errors, runs the three test projects (the GeoPackage tests on `net10.0` on both runners, and on `net48` as a 64-bit and a 32-bit process on Windows), packs the three `.nupkg` and `.snupkg` files on Ubuntu, checks that `Iwxxm.Net` declares no dependency and that `Iwxxm.Net.Export.GeoPackage` depends on `Iwxxm.Net` and `Microsoft.Data.Sqlite`, and uploads the packages as a workflow artifact.
2. **`publish-github-packages`** (after `build-and-test`): checks that the tagged commit is reachable from `origin/main`, then pushes the packages to GitHub Packages with the workflow `GITHUB_TOKEN`.
3. **`publish-nuget`** (after `build-and-test`, in parallel with the previous job): runs in the GitHub environment `nuget-release`, checks that the tagged commit is reachable from `origin/main`, obtains a short-lived NuGet API key with `NuGet/login` (Trusted Publishing, no stored secret) and pushes the packages to NuGet.org (`https://api.nuget.org/v3/index.json`).

Both publish jobs run only on a tag ref. If the environment `nuget-release` has protection rules, `publish-nuget` waits for approval and does not start until a required reviewer approves the deployment in the Actions run; the run stays pending until then. The two publish jobs are independent: one can succeed while the other fails, and a failed job can be re-run for the same tag (`--skip-duplicate` makes the push idempotent).

Confirm before tagging, because these live outside the repository and cannot be verified from it (to be confirmed by the maintainer):

- **Trusted Publishing policy on nuget.org**: a policy exists for the `0neTX` nuget.org account (the `user` given to `NuGet/login` in the workflow) that trusts this repository (`0neTX/Iwxxm.Net`), the workflow file `ci-cd.yml` and the environment `nuget-release`, and it is active for every package id that is published.
- **Package ids**: `Iwxxm.Net`, `Iwxxm.Net.Validation.Saxon` and `Iwxxm.Net.Export.GeoPackage` are owned by that account (the three were published to nuget.org as `1.1.0-rc.1` through the policy), and the policy covers them. A new id has to be added to the policy before it is published.
- **Environment `nuget-release`**: it exists under repository **Settings**, **Environments**; list who the required reviewers are (if any) and make sure one of them can approve the run.

## Procedure

```bash
git switch main && git pull --ff-only
git status                      # must be clean
podman build --target pack --build-arg PACKAGE_VERSION=1.0.0 -t iwxxm-net-pack .   # local check: 0 warnings, all tests, package guards
git tag -a v1.0.0 -m "v1.0.0"
git push origin v1.0.0          # the tag push starts the workflow, which tests, packs and publishes
```

Before tagging, `CHANGELOG.md` must already have the release under a dated version heading (for example `## [1.0.0] - YYYY-MM-DD`, the real publication date) above an empty `## [Unreleased]` section, in the commit that precedes the tag and that is on `main`. The package contains `CHANGELOG.md` as of the tagged commit. The `Dockerfile` pack stage uses `PACKAGE_VERSION` (default `1.0.0-rc.1`) only for its local check; the real version is computed by the workflow from the tag.

Optional dry run: before the stable tag, push a release candidate tag (for example `v1.0.0-rc.3`, the next number after the last existing candidate) from the same commit. It exercises the whole chain, including the environment approval and the Trusted Publishing login, and publishes only a prerelease that does not affect `1.0.0`. It is still a real publication and is immutable like any other version, so use a number that has not been used.

### What the first release candidate (1.1.0-rc.1) showed

Facts about the repository settings, read from the GitHub API for this document; they are outside the repository files, so check them again if the settings change.

- **`main` is protected** by the ruleset `Protect main`: no deletion, no force push, changes only through a pull request with 1 approving review (a new push dismisses a stale approval) and with the checks `Build & Test (ubuntu-latest)` and `Build & Test (windows-latest)` passing. Repository administrators can bypass it. All three merge methods are allowed.
- **The release went through a pull request.** The pull request (#4) was merged into `main` by squash, which creates a new commit on `main` (`c8d3d27`): tag that commit, not the head of the branch. The tag `v1.1.0-rc.1` was created on `main` after the CI run of that commit on `main` had passed, which is the same workflow that runs on a tag. `CHANGELOG.md` had the dated `1.1.0-rc.1` heading above an empty `Unreleased` section in the merged commit.
- **GitHub Packages publishes by itself** when the tag run reaches `publish-github-packages`. **NuGet.org waits**: `publish-nuget` runs in the environment `nuget-release`, whose required reviewer is the maintainer account (self-review is not prevented, administrators can bypass the rule) and which accepts deployments only from tags `v*`. The run stays pending until the deployment is approved in the Actions run.
- **The logs of the publish jobs could only be read after the whole run had finished**, and the run does not finish while `publish-nuget` waits for the approval.
- **The GitHub Release** of `v1.1.0-rc.1` exists (marked as a prerelease for a release candidate, with the three `.nupkg` files attached); the workflow does not create it (there is no step for it in `ci-cd.yml`).

## What the packages contain

The files bundled in every package come from `Directory.Build.targets` (README, `LICENSE`, `CHANGELOG.md`, `THIRD-PARTY-NOTICES.md` and `assets/icon.png`, packed as `icon.png`) and from the project files.

| Package | Contents |
| :--- | :--- |
| `Iwxxm.Net` | `lib/netstandard2.0` and `lib/netstandard2.1` assemblies with the embedded official schemas, code lists and Schematron subset; `README.md`, `LICENSE`, `CHANGELOG.md`, `THIRD-PARTY-NOTICES.md`, `icon.png`; `schema-notices/` (upstream ReadMe and release notes of the official schemas). No NuGet dependencies. |
| `Iwxxm.Net.Validation.Saxon` | `lib/net10.0` assembly with the embedded `iwxxm.sch` and SchXslt; `README.md`, `LICENSE`, `CHANGELOG.md`, `THIRD-PARTY-NOTICES.md`, `icon.png`, `licenses/SchXslt/LICENSE`. Depends on `Iwxxm.Net` and `SaxonCS-HE`. |
| `Iwxxm.Net.Export.GeoPackage` | `lib/netstandard2.0` assembly; `README.md`, `LICENSE`, `CHANGELOG.md`, `THIRD-PARTY-NOTICES.md`, `icon.png`. Depends on `Iwxxm.Net` and `Microsoft.Data.Sqlite` 10.0.15 (which brings SQLitePCLRaw 2.1.13 and the native SQLite binaries; about 19 MB on restore). Built, tested and packed by the `Dockerfile` and by the workflow, which publishes it with the others: see "The GeoPackage package". |
| `.snupkg` symbol packages | PDBs with SourceLink maps to the GitHub sources. |

The `Dockerfile` pack stage (the local check) fails the build if the core nuspec declares a dependency, if the core symbol package lacks SourceLink, if the GeoPackage nuspec does not depend on `Iwxxm.Net` and `Microsoft.Data.Sqlite`, or if any package lacks `THIRD-PARTY-NOTICES.md` or `LICENSE`. The workflow itself checks that the core package declares no dependency and that the GeoPackage package depends on `Iwxxm.Net` and `Microsoft.Data.Sqlite`; the other guards (SourceLink, notices and licence, the exact `Iwxxm.Net` version of the GeoPackage dependency) run in the container build, so run it before tagging.

## The GeoPackage package

`Iwxxm.Net.Export.GeoPackage` has its public API (`IwxxmGeoPackageExporter`, `GeoPackageExportOptions`, `GeoPackageVersion`, `GeoPackageContent`, the parser members in `IwxxmParserGeoPackageExtensions`, the error code `GEOPACKAGE-EXPORT-ERROR`). It was first released as **`1.1.0-rc.1`**, a prerelease, together with `Iwxxm.Net` and `Iwxxm.Net.Validation.Saxon` `1.1.0-rc.1` (tag `v1.1.0-rc.1` on commit `c8d3d27`; NuGet.org and GitHub Packages; the GitHub Release is marked as a prerelease and has the three `.nupkg` files attached). What runs where:

- **The `Dockerfile`** builds, tests (on `net10.0`, and on `net48` under Mono in the `net48-test` stage) and packs it, and the pack stage checks that its nuspec depends on `Iwxxm.Net` and `Microsoft.Data.Sqlite`, and that the declared version of the `Iwxxm.Net` dependency is the version packed with it. The container is the complete local check.
- **The GitHub Actions workflow** (the CI changes for the new package are in the repository) builds it, runs its tests on `net10.0` on Ubuntu and Windows and on `net48` on Windows (64-bit and 32-bit process, after checking that the native `e_sqlite3.dll` is copied for `win-x86` and `win-x64`), packs it with the other two packages and checks that its nuspec depends on `Iwxxm.Net` and `Microsoft.Data.Sqlite`. A tag therefore publishes it too. It does not check the version of the `Iwxxm.Net` dependency (the `Dockerfile` checks that it equals the version packed with it). Verified on a real run (run 45901356325, `windows-latest` and `ubuntu-latest`, all steps green): core `net10.0` 1414 and `net48` 1357, Saxon 96, GeoPackage `net10.0` 792 and `net48` 787 of 787 as a 64-bit and as a 32-bit process.
- **`tools/run-local-ci.sh --publish`** pushes every package of the pack stage, this one included. Do not use it for a release of the other packages alone.

What applies to every release of this package:

- **Release both packages with the same version.** `Iwxxm.Net.Export.GeoPackage` reads internals of `Iwxxm.Net` through `InternalsVisibleTo`, which `Iwxxm.Net` 1.0.0 does not grant. Its nuspec dependency on `Iwxxm.Net` is a **lower bound** (`>= 1.1.0-rc.1` for that release), not an exact range, so a version of `Iwxxm.Net` above it is not excluded by NuGet (not tested). Checked with the published `1.1.0-rc.1`: with `Iwxxm.Net` 1.0.0 NuGet restore refuses the combination (`NU1605`), and with that warning silenced an export returns `GEOPACKAGE-EXPORT-ERROR` with the message about the versions.
- **The Trusted Publishing policy covers the package id.** It covered `Iwxxm.Net.Export.GeoPackage` (and the other two ids) for `1.1.0-rc.1`: the NuGet.org publication worked. A new package id needs the policy first (see "What the workflow does on a tag").
- **Verified for `1.1.0-rc.1`** by CI run 45901356325: the Windows job runs the GeoPackage tests on `net48` x64 and x86 and passes (see "What has and has not been run"), and the Ubuntu job packs the package and checks its dependencies.
- **Verified from the published package** (a clean `net10.0` project that restores from nuget.org only): restoring the add-on `1.1.0-rc.1` brings `Iwxxm.Net` `1.1.0-rc.1`, `Microsoft.Data.Sqlite` 10.0.15 and SQLitePCLRaw 2.1.13; exports of 5 samples succeed; the Met Office SIGWX sample gives 15 distinct `valid_from_utc` values and 231 core points with `lowerIsotachLevel`; `GeoPackageContent.None` writes no `gpkg_data_columns`; two exports of the same document have the same canonical dump; failure paths return `GEOPACKAGE-EXPORT-ERROR`; GDAL 3.13.3 `ogrinfo` and `validate_gpkg.py --extra --warning-as-error` pass on 11 files. **Not verified from the published package:** `net48` at runtime (it was run only from the local build in CI), the symbol packages (`.snupkg`), the repository signature, Windows ARM64, macOS, ASP.NET and other shadow-copying hosts, the QGIS desktop application and ArcGIS.
- **Platforms.** The package targets `netstandard2.0` and depends on `Microsoft.Data.Sqlite`, whose SQLitePCLRaw bundle initializes the native SQLite on its own on modern .NET. Native files come from `SQLitePCLRaw.lib.e_sqlite3` under `runtimes/<rid>/native/` for Windows (x86, x64, ARM64), Linux (glibc and musl) and macOS; the Linux ones (x64, glibc and musl) and the Windows x64 and x86 ones (on .NET Framework 4.8 and, for x64, on `net10.0`) have been run, the Windows ARM64 and macOS ones are inferred from the package layout.
- **.NET Framework (net48) on Windows.** A project that uses `PackageReference` gets `e_sqlite3.dll` for `win-x86` and `win-x64` copied under `runtimes\` of its output; the SQLite loader looks for them there. Not covered, and documented for users in [section 8B of the guide](GUIDE.md#net-framework-and-native-sqlite): projects that use `packages.config` (the native copy is not applied, so `e_sqlite3.dll` must be copied by hand next to the executable) and .NET Framework on Windows ARM64 (no `win-arm64` copy rule for .NET Framework).
- **What has and has not been run.** The tests run on `net10.0` in the container (glibc, and a musl check was made once with the same package), on `net48` under Mono on Linux in the `net48-test` stage (which loads the Linux native library, after a `dllmap` for `libdl`; Mono is not .NET Framework), and on `windows-latest` in CI: `net10.0`, and the real .NET Framework 4.8 as a 64-bit and as a 32-bit process, which loaded `runtimes\win-x64\native\e_sqlite3.dll` and `runtimes\win-x86\native\e_sqlite3.dll` (SQLite 3.53.3, .NET Framework 4.8.9363.0). The test host has to run without shadow copy: the copy does not include `runtimes\<rid>\native`, so the SQLite tests failed with `Library e_sqlite3 not found` (417 of 787) until the GeoPackage test project got an `xunit.runner.json` with `shadowCopy` set to `false`, which the Visual Studio adapter honours. The x86 process is selected with `dotnet test -- RunConfiguration.TargetPlatform=x86`, which worked. **Not verified:** Windows ARM64, the macOS native binaries, `packages.config` projects, and hosts other than the xUnit runners (for example ASP.NET and other hosts that shadow-copy). If one fails, the fallback is a documented manual copy of `e_sqlite3.dll`.
- **Files.** The exporter opens its SQLite connections with `Pooling=False`, so the `.gpkg` file can be deleted or moved right after the export on Windows.
- **Versions.** `Microsoft.Data.Sqlite` stays on the 10.0.x line while `netstandard2.0` is a target (11.0.0 targets `net10.0` only). Updating it changes the SQLite version that writes the files, and the golden dumps compare content, not bytes, for that reason.
- **Validation tools.** Run `tools/validate-geopackage-file.sh` and `tools/validate-geopackage-geometry.sh` (podman, a pinned GDAL image) on the commit to be tagged; they are not part of the container build or of the workflow. The file check also covers the `gpkg_schema` extension (alias, comment and JSON type through GDAL, and the `*.plain.gpkg` files written with `GeoPackageContent.None`); `GeoPackageContent` is a public flags enumeration, so a new flag must be a new power of two, with the values already shipped never changed, and an overload of `GeoPackageExportOptions` rather than an optional parameter.
- **Package validation, no baseline.** `EnablePackageValidation` is on for every project (see "Package validation" below), so `dotnet pack` checks each package for consistency across its target frameworks. No `PackageValidationBaselineVersion` is set anywhere, by decision, so nothing compares the API with an earlier release: a breaking change against a published version is not detected by the build, and the release notes must say it.

## Publication checklist for v1.0.0

Before tagging:

1. Resolve or consciously accept each open point in [THIRD-PARTY-NOTICES.md](../THIRD-PARTY-NOTICES.md#12-open-points-for-the-maintainer); confirm the file is current for the versions being released.
2. Enable GitHub private vulnerability reporting (repository **Settings**, **Advanced Security**, **Private vulnerability reporting**), then check that <https://github.com/0neTX/Iwxxm.Net/security/advisories/new> works. [SECURITY.md](../SECURITY.md) tells reporters to use it.
3. Confirm the prerequisites outside the repository listed under "What the workflow does on a tag" (Trusted Publishing policy, package ids, reviewers of `nuget-release`).
4. Date the `## [1.0.0]` heading of `CHANGELOG.md` with the real publication date, and merge it to `main`.
5. Check the README badges and URLs, and the links in the package metadata (`PackageProjectUrl`, `RepositoryUrl`, release notes link in `Directory.Build.props`).
6. Optionally run the dry run with a release candidate tag (see "Procedure").

Tag and publish (see "Procedure"), approve the `nuget-release` deployment if it is waiting, then after publication:

7. Install both packages from the feed in a clean project and run a smoke test (parse an official example). Use nuget.org for the check: `dotnet add package Iwxxm.Net --version 1.0.0` (no `--prerelease` for a stable version). The GitHub Packages feed needs a token with the `read:packages` scope to be read (to be confirmed by the maintainer against the GitHub documentation).
8. Verify that the SourceLink and symbol links resolve now that the repository is public: download the `.snupkg`, or restore the package in a clean project and step into the library source from a debugger.
9. Create the GitHub Release for the tag (`v1.0.0`) with notes taken from the `## [1.0.0]` section of `CHANGELOG.md`. It is a stable release, so do not mark it as a prerelease; a release candidate tag would be marked as one.
10. Apply the follow-ups listed under "Other follow-ups" below.

## After v1.0.0 is published

### Package validation

`Directory.Build.props` sets `EnablePackageValidation` to `true` for all projects (the validation runs only as part of `dotnet pack`, so the test projects, the benchmarks and the demo are not affected). `dotnet pack` therefore checks that each package is consistent across its target frameworks, for example `netstandard2.0` against `netstandard2.1` for `Iwxxm.Net`; the container pack stage runs it for the three packages. The property was previously conditioned on `IsPackable`, which the projects set after `Directory.Build.props` is evaluated, so the condition was always false and the validation did not run before this was fixed (including for 1.0.0).

**No baseline is used, by decision.** `PackageValidationBaselineVersion` is not set in `Directory.Build.props` or in any project, so nothing compares the API of a package with an earlier release: API breaking changes against published versions are not checked by the build. Keep compatibility by review (the public API surface tests pin the shape of the GeoPackage package) and describe every breaking change under `BREAKING CHANGES` in `CHANGELOG.md`. If a baseline is wanted later, set `PackageValidationBaselineVersion` to a published version of the same package id (it applies to every packable project, so it needs a published version of each); a justified exception is recorded in a `CompatibilitySuppressions.xml` next to the project.

### Other follow-ups

- Review the default `VersionSuffix` (`rc.1`) in `Directory.Build.props` and the `PACKAGE_VERSION` default of the `Dockerfile` (`1.0.0-rc.1`): they apply only to local builds that do not pass a version, but they still describe a release candidate (to be decided by the maintainer).
- Update the README and the `docs/` examples if they still show a release candidate version.
