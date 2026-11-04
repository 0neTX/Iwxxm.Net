# Security Policy

Iwxxm.Net parses XML that usually arrives from networks (aeronautical meteorological messages, SWIM bulletins), so we treat parsing robustness as a security matter. Thank you for reporting problems responsibly.

## Supported versions

The project is **pre-release** and has no long-term-support branches. Only the following receive security fixes:

| Version | Supported |
| :--- | :---: |
| Latest `main` | Yes |
| Latest published `1.0.0` prerelease (currently `1.0.0-rc.1`) | Yes |
| Any older prerelease or fork | No |

Fixes are released as a new prerelease (and, once it exists, a new stable version); we do not backport them. If you depend on an older build, update to the latest one first.

## Reporting a vulnerability

**Please do not open a public issue, discussion or pull request for a vulnerability.**

Report it privately with GitHub private vulnerability reporting:

<https://github.com/0neTX/Iwxxm.Net/security/advisories/new>

(or the **Security** tab of the repository, then **Report a vulnerability**).

Please include, as far as you can:

- the affected package (`Iwxxm.Net` or `Iwxxm.Net.Validation.Saxon`), version or commit, and target framework;
- a minimal input document or code snippet that reproduces the problem (if the input is large, say how to generate it);
- what you expected and what happened (exception, memory or CPU growth, wrong output, file or network access);
- your assessment of the impact, if you have one.

What to expect (best effort, this is a volunteer-maintained project):

- we aim to **acknowledge your report within about 7 days**;
- we will tell you whether we consider it a vulnerability, ask for details if needed, and keep you informed while we work on a fix;
- disclosure is **coordinated**: we publish the fix and a GitHub security advisory together, credit you if you wish, and ask you to keep the details private until then. If a report stays unresolved for a long time we will agree a disclosure date with you instead of leaving it open indefinitely.

We cannot offer a bug bounty or a guaranteed fix time.

## Scope

In scope (issues in code or data of this repository):

- **XML parsing hardening**: XXE and DTD handling, entity expansion ("billion laughs"), and resource exhaustion through large or deeply nested documents, or through documents that bypass the size and character limits;
- **Resource resolution**: any way for an input document or a stylesheet to make the library read a file, call the network or reach a resource that is not embedded (the XSD, XSLT and Schematron resolvers are meant to be air-gapped);
- **Output injection**: JSON or GeoJSON output that can be broken out of its structure by input content, and `ValidationError.Location` values that can be turned into XPath injection when evaluated;
- **Supply chain of the embedded official files**: the vendored WMO schemas, code lists, `iwxxm.sch` and SchXslt files must stay byte-identical to their upstream sources; a modified or substituted copy, or a packaging step that alters them, is in scope;
- the packages published from this repository.

Out of scope:

- vulnerabilities in third-party packages, in particular **SaxonCS-HE** (Saxonica) used by `Iwxxm.Net.Validation.Saxon`, and in the .NET runtime: please report them to their maintainers. If you believe **our usage** of such a component is affected or makes it exploitable, do tell us;
- findings that need an application to disable the built-in protections or to give the library a non-default custom adapter that is itself unsafe;
- invalid or malicious IWXXM content that is rejected with a proper `Result<T>` error (that is the intended behaviour);
- denial of service that needs input larger than the configured limits.

## Built-in defenses

The details and defaults are in the [guide](docs/GUIDE.md#6-options-and-security-limits) and the [reference](docs/REFERENCE.md#official-wmo-schematron):

- DTDs are always prohibited and no external resource is ever resolved (`XML-MALFORMED`); the XSD, XSLT and Schematron resolvers only serve resources embedded in the assemblies, so there is no network or file-system access during validation;
- configurable limits in `IwxxmParserOptions`: `MaxDocumentBytes` (50 MiB = 52,428,800 bytes), `MaxCharactersInDocument` (52,428,800), `MaxCharactersFromEntities` (1,024), `MaxValidationErrors` (100) and `MaxRuleReportBytes` (4 MiB, `SCHEMATRON-REPORT-SIZE-LIMIT`); the input is read once with these limits before any adapter sees it. The two size defaults are the same number on purpose: a character takes at least one byte of UTF-8 (two of UTF-16), so a document that passes the byte limit never exceeds the character quota whatever its encoding. `MaxElementDepth` (128, root element = depth 1, `XML-DEPTH-LIMIT`) bounds the element nesting: a streaming check in that same single read stops at the first element over the limit, before any tree is built or validated. Without it a small, schema-valid document can crash the host process: a `collect:MeteorologicalBulletin` may contain itself, and about 25,000 levels (about 5 MB, far below the size limit) overflow the stack of the .NET tree validation, an uncatchable `StackOverflowException` (24,000 levels survived in the measurement, and `XDocument.Load` alone is super-linear: 3.2 s at 50,000 levels, 110 s at 200,000). Real products nest at most about 20 levels (the 14 MB Met Office bulletin 16, the official examples 20), so the default leaves a margin of more than six times and the crash threshold is about 190 times the default. The check costs nothing measurable (it is a forwarding reader inside the one read, with no second pass; measured within run-to-run noise: loading the 14 MB bulletin takes about 0.17 s with and without it, a 50 MiB flat document about 1.5 s with and without it); a 100,000-level payload (about 2.2 MB) and a 1,000,000-level payload (about 11 MB) are rejected in under 0.1 s. A legitimate deeper document needs a conscious `new IwxxmParserOptions(maxElementDepth: ...)`; keep it far below 24,000;
- the core package `Iwxxm.Net` has **zero runtime dependencies**; the optional Saxon package only requests Saxon-HE capabilities and runs air-gapped;
- the official schemas, code lists and Schematron are embedded byte for byte and checked by tests (for example the SHA-256 of `iwxxm.sch`);
- expected failures (malformed input, limits, rule violations) are returned as `Result<T>` values with stable error codes, and GeoJSON strings are escaped by a dedicated writer.

### Memory cost of the default size limit

The default limit (50 MiB) is sized for large operational products (the Met Office WAFS SIGWX bulletin is 14.2 MB and a global product can be several times larger). The numbers below were measured in a container (.NET 10, 2 cores, one process, indicative, not a benchmark) and show what one request can cost at the cap:

| Stage | Cost at the cap |
| :--- | :--- |
| Buffering a non-seekable 50 MiB stream (`BoundedDocumentBuffer`: a growing `MemoryStream` of up to 64 MiB, then the final array) | peak about 160 MiB above the payload source, up to about 3 times the payload while it is read; a seekable stream longer than the limit is refused without reading it |
| Full pipeline (XSD, built-in Schematron, `XDocument` DOM, mapping) of a valid 49.7 MiB bulletin | about 3.8 s, about 350 MiB above the process baseline (peak working set about 545 MiB, about 1.2 GiB allocated), roughly 11 times the input |
| The same plus the GeoJSON export (18 MB of JSON) | peak working set about 670 MiB |
| 14.2 MB real bulletin, full pipeline | about 1.5 s, about 155 MiB above baseline |
| SVRL report of the Schematron stage | bounded by `MaxRuleReportBytes` (4 MiB) and `MaxValidationErrors`, independent of the input size |

Plan roughly 12 times the largest accepted document per concurrent request (about 600 MB at 50 MiB, plus about 25 percent when you export GeoJSON), or lower `MaxDocumentBytes` and `MaxCharactersInDocument` together to what your feeds need. The DOM and the buffered payload are held for the duration of the call only.

## Secure usage guidance

- Treat every input document as **untrusted**, including documents from "internal" feeds.
- Keep the default `IwxxmParserOptions` limits, or lower them for your traffic (the default size cap is already 50 MiB); do not raise them without a reason, and still bound the size of what you read from the network before you hand it to the parser.
- **Never disable the DTD prohibition** and do not replace the built-in adapters with ones that resolve external resources or relax the reader settings.
- Run the parser with the least privileges your platform allows, with a request timeout and a memory limit around it.
- Do not evaluate `ValidationError.Location` or other values taken from input in contexts that interpret them (for example concatenating them into a query) without escaping.
- Pin the exact package versions you deploy (and, if you use them, lock files and package-source mapping), review updates, and verify the hash of the packages you restore.
- Subscribe to the repository's security advisories (**Watch**, then **Custom**, then **Security alerts**) to hear about fixes.
