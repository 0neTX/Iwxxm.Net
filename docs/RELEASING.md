# Releasing

How a version is built and published, what the packages contain, and what must be done before the repository becomes public at v1.0.0.

## Versioning and tags

- Versions follow [Semantic Versioning 2.0](https://semver.org/spec/v2.0.0.html). The version of both packages is the tag name without the leading `v`: tag `v1.0.0-rc.26` publishes `1.0.0-rc.26`.
- Only tags `vMAJOR.MINOR.PATCH[-PRERELEASE]` publish (for example `v1.0.0-rc.26`, `v1.0.0`). Any other tag name fails the version check of the workflow.
- Tag only commits that are on `main`. The publish job of `.github/workflows/ci-cd.yml` refuses a tagged commit that is not reachable from `origin/main`.
- Pushes to `main` and pull requests build and test `-ci.<run>.<attempt>` prereleases that are never published.
- The workflow pushes with `--skip-duplicate`: pushing a tag whose version already exists on the feed is not an error and publishes nothing new. To publish again, use a new version.
- The release candidates are numbered in sequence: `v1.0.0-rc.1`, `v1.0.0-rc.2`, ... The next candidate is the last one plus one; v1.0.0 follows the last release candidate.

Procedure:

```bash
git switch main && git pull --ff-only
git status                      # must be clean
podman build --target pack -t iwxxm-net-pack .   # local check: 0 warnings, all tests, package guards
git tag -a v1.0.0-rc.26 -m "v1.0.0-rc.26"
git push origin v1.0.0-rc.26    # the tag push starts the workflow, which tests, packs and publishes
```

Move the `## [Unreleased]` entries of `CHANGELOG.md` under a dated version heading in the commit that precedes the tag. The `Dockerfile` pack stage uses the default `PACKAGE_VERSION` for its local check; the real version is computed by the workflow from the tag.

## What the packages contain

| Package | Contents |
| :--- | :--- |
| `Iwxxm.Net` | `lib/netstandard2.0` and `lib/netstandard2.1` assemblies with the embedded official schemas, code lists and Schematron subset; `README.md`, `LICENSE`, `CHANGELOG.md`, `THIRD-PARTY-NOTICES.md`, `icon.png`; `schema-notices/` (upstream ReadMe and release notes of the official schemas). No NuGet dependencies. |
| `Iwxxm.Net.Validation.Saxon` | `lib/net10.0` assembly with the embedded `iwxxm.sch` and SchXslt; `README.md`, `LICENSE`, `CHANGELOG.md`, `THIRD-PARTY-NOTICES.md`, `icon.png`, `licenses/SchXslt/LICENSE`. Depends on `Iwxxm.Net` and `SaxonCS-HE`. |
| `.snupkg` symbol packages | PDBs with SourceLink maps to the GitHub sources. |

The `Dockerfile` pack stage fails the build if the core nuspec declares a dependency, if the symbols lack SourceLink, or if either package lacks `THIRD-PARTY-NOTICES.md` or `LICENSE`.

## API compatibility baseline

`Directory.Build.props` enables NuGet Package Validation (`EnablePackageValidation`) for the two packable projects, so `dotnet pack` checks that each package is consistent across its target frameworks (for example `netstandard2.0` against `netstandard2.1`). No baseline version is set yet: only release candidates exist and the API still changes.

After v1.0.0 is published, set the baseline in `Directory.Build.props` (a commented-out example is already there):

```xml
<PackageValidationBaselineVersion>1.0.0</PackageValidationBaselineVersion>
```

From then on `dotnet pack` fails on a breaking API change against 1.0.0, which keeps minor and patch releases compatible; a deliberate breaking change needs a new major version. A justified exception is recorded in a `CompatibilitySuppressions.xml` next to the project (generate it with `dotnet pack -p:GenerateCompatibilitySuppressionFile=true`), reviewed like any other change. Move the baseline forward after each stable release.

## Private phase (until v1.0.0)

- The repository is private. Packages published to GitHub Packages from a private repository are visible only to users with access to the repository. A consumer needs a token with the `read:packages` scope (see "Installation" in the [README](../README.md)).
- `PackageProjectUrl` and `RepositoryUrl` point to the GitHub repository on purpose. While it is private, those links, and the source links stored in the symbols (SourceLink), answer 404 to everyone without access. This is expected and needs no change; it disappears when the repository becomes public.
- Vulnerability reports go directly to the maintainer, not through GitHub private vulnerability reporting (see [SECURITY.md](../SECURITY.md)).

## Publication checklist for v1.0.0

1. Resolve or consciously accept each open point in [THIRD-PARTY-NOTICES.md](../THIRD-PARTY-NOTICES.md#12-open-points-for-the-maintainer); confirm the file is current for the versions being released.
2. Make the repository public.
4. Enable GitHub private vulnerability reporting (repository **Settings**, **Advanced Security**, **Private vulnerability reporting**), then check that <https://github.com/0neTX/Iwxxm.Net/security/advisories/new> works. [SECURITY.md](../SECURITY.md) already says to use it from v1.0.0.
5. Verify that the SourceLink and symbol links resolve for the public repository: download the `.snupkg`, or restore the package in a clean project and step into the library source from a debugger. They return 404 for outsiders while the repository is private.
6. Update the README badges and URLs if the project location or name changed; check the links in the package metadata (`PackageProjectUrl`, `RepositoryUrl`, release notes link).
7. Decide where to publish: GitHub Packages only (what the workflow does today) or nuget.org as well. This decision belongs to the maintainer. A move to nuget.org needs a workflow change, an API key kept as a repository secret, and a check that the package id is available; `.snupkg` symbols are accepted by nuget.org, not by GitHub Packages.
8. Create the GitHub Release for the tag with notes taken from `CHANGELOG.md`.
9. After publication, install the package from the feed in a clean project and run a smoke test (parse an official example).
