# Documentation index

Where to look, depending on what you want to do.

| I want to... | Read |
| :--- | :--- |
| **Learn the library step by step** (parse, validate, read jet streams and clouds, METAR, TAF, SIGMET) | [GUIDE.md](GUIDE.md) |
| **Look something up** (one example per product family, error codes, value objects, ports, namespaces) | [REFERENCE.md](REFERENCE.md) |
| **Install the packages and see what is supported** | [../README.md](../README.md) |
| **Understand why the code is shaped this way** (hexagonal-style layering, DDD naming, the debt catalogue, the 2026-11 cleanup) | [ARCHITECTURE_ANALYSIS.md](ARCHITECTURE_ANALYSIS.md) (in Spanish) |
| **See what is known, fixed and still open** | [../TECHNICAL_DEBT.md](../TECHNICAL_DEBT.md) |
| **Change the code** (setup, layer rules, adding a report family, golden masters, documentation checks) | [../CONTRIBUTING.md](../CONTRIBUTING.md) |
| **Report a vulnerability or harden a deployment** (supported versions, private reporting, scope, secure usage) | [../SECURITY.md](../SECURITY.md) |
| **See what changed between releases** | [../CHANGELOG.md](../CHANGELOG.md) |
| **Publish a release** (tag procedure, package contents, v1.0.0 checklist) | [RELEASING.md](RELEASING.md) |
| **Check licences of the embedded schemas, code lists and dependencies** | [../THIRD-PARTY-NOTICES.md](../THIRD-PARTY-NOTICES.md) |

## Illustrations

The diagrams in the guide are drawn by hand as small SVG files in [assets/](assets/), with an explicit neutral background so that they read well in both light and dark themes:

| File | Used in | Shows |
| :--- | :--- | :--- |
| [pipeline-overview.svg](assets/pipeline-overview.svg) | Guide, section 2 | The processing pipeline from XML to GeoJSON and its failure categories |
| [jet-stream-core-line.svg](assets/jet-stream-core-line.svg) | Guide, section 10 | A jet stream axis with core points, drawn from the official WAFS example |
| [cloud-layer-profile.svg](assets/cloud-layer-profile.svg) | Guide, section 11 | The vertical extents of a CB cloud, turbulence and icing, with a nil cloud base |
| [vertical-references.svg](assets/vertical-references.svg) | Guide, section 9 | Why SFC, MSL and STD vertical limits are different numbers |

Photographs and maps are never stored in this repository. The guide links to openly licensed or public-domain originals instead (see [Further reading and visual references](GUIDE.md#17-further-reading-and-visual-references)).

## How the examples are kept honest

Every C# snippet in the guide, the reference and the README is compiled and run against the packaged library, and the text shown in an `output` block after a snippet is what the snippet prints (long outputs are shortened with a line that only contains `...`). The procedure is in [CONTRIBUTING.md](../CONTRIBUTING.md#documentation).
