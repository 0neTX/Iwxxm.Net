# Real-data run, 2026-11-14 bulletins (11 files)

> **Snapshot, not a golden master.** These results were produced on 2026-11-14 with the library at the commit that contains them (the demo application, built-in Schematron in pass A and the official Schematron in pass B). No test reads this folder and nothing compares against it, so it ages as the code changes. The `geojson/` folder is not kept because no GeoJSON was produced. Paths such as `/samples/<file>` are the container mount points.

Method: demo app `samples/Iwxxm.Net.Demo` built from a scratch copy of the repository (the repo itself was not touched) with the README podman command (SDK 10.0.403 image, build 12.5 s) and run in `mcr.microsoft.com/dotnet/runtime:10.0`. Pass A = default built-in Schematron, `--no-color --geojson`; pass B = `--official-schematron`, no GeoJSON. Logs: `logs/` and `logs-official/` (header line: file, size, exit code, seconds; seconds include the container start). Each run is one `podman run ... dotnet /app/Iwxxm.Net.Demo.dll /samples/<file> --no-color ...`.

## Headline

All 11 bulletins exit 1 in both passes, before any report is mapped. No GeoJSON was produced (`geojson/` is empty). The failing gate is XSD validation of the whole bulletin (`XSD-SCHEMA-VIOLATION`, plus `XSD-TOO-MANY-ERRORS` for the 5 METAR files). 0 warnings anywhere. Built-in and official Schematron findings never appear: the pipeline stops at the first failing gate, and `docs/REFERENCE.md` states "Schema and rule validation remain document-level" (only mapping is partial), so the Schematron was not evaluated on these files.

## Per-file table

Report counts: regex count of `<iwxxm:METAR|SPECI|TAF|SIGMET|AIRMET` opening tags with `python3 -I` (columns M/S/T/SIG/AIR/other; no other report root found). Mapped = reports mapped successfully (0 everywhere); per-item errors are 0 because mapping was never reached. Err/Warn: `+` = library caps XSD findings at 100 plus one `XSD-TOO-MANY-ERRORS`, so the true count is higher and unknown.

| File | Size | M/S/T/SIG/AIR/other | Mapped / in bulletin | Exit A | Exit B | GeoJSON features / size | Err / Warn A | Err / Warn B | Time s A / B |
| :--- | ---: | :--- | :--- | :-: | :-: | :--- | :--- | :--- | :--- |
| airsigmet_global | 16 KiB | 0/0/0/7/0/0 | 0 / 7 (not reached) | 1 | 1 | 0 / 0 B | 8 / 0 | 8 / 0 | 0.52 / 0.51 |
| metar_africa_mideast | 1103 KiB | 396/4/0/0/0/0 | 0 / 400 (not reached) | 1 | 1 | 0 / 0 B | 101+ / 0 | 101+ / 0 | 0.54 / 0.54 |
| metar_asia_pacific | 1192 KiB | 380/20/0/0/0/0 | 0 / 400 (not reached) | 1 | 1 | 0 / 0 B | 101+ / 0 | 101+ / 0 | 0.56 / 0.57 |
| metar_europe | 1146 KiB | 390/10/0/0/0/0 | 0 / 400 (not reached) | 1 | 1 | 0 / 0 B | 101+ / 0 | 101+ / 0 | 0.51 / 0.48 |
| metar_north_america | 1313 KiB | 313/87/0/0/0/0 | 0 / 400 (not reached) | 1 | 1 | 0 / 0 B | 101+ / 0 | 101+ / 0 | 0.56 / 0.54 |
| metar_south_america | 1187 KiB | 379/21/0/0/0/0 | 0 / 400 (not reached) | 1 | 1 | 0 / 0 B | 101+ / 0 | 101+ / 0 | 0.56 / 0.5 |
| taf_africa_mideast | 322 KiB | 0/0/69/0/0/0 | 0 / 69 (not reached) | 1 | 1 | 0 / 0 B | 2 / 0 | 2 / 0 | 0.49 / 0.46 |
| taf_asia_pacific | 641 KiB | 0/0/123/0/0/0 | 0 / 123 (not reached) | 1 | 1 | 0 / 0 B | 2 / 0 | 2 / 0 | 0.54 / 0.53 |
| taf_europe | 650 KiB | 0/0/117/0/0/0 | 0 / 117 (not reached) | 1 | 1 | 0 / 0 B | 2 / 0 | 2 / 0 | 0.51 / 0.48 |
| taf_north_america | 1753 KiB | 0/0/275/0/0/0 | 0 / 275 (not reached) | 1 | 1 | 0 / 0 B | 2 / 0 | 2 / 0 | 0.59 / 0.5 |
| taf_south_america | 272 KiB | 0/0/46/0/0/0 | 0 / 46 (not reached) | 1 | 1 | 0 / 0 B | 2 / 0 | 2 / 0 | 0.48 / 0.51 |

Totals (sum of per-file times, each a fresh container): pass A 5.86 s, pass B 5.62 s.

## Official Schematron: first-run time

Not measurable on these files: XSD fails in about 0.2 s ("Parse + validate: 0.2 s" in the logs), the Saxon stylesheet is never compiled, so pass B costs the same as pass A. The header text "the first run includes compiling the official rules" is generic. Control run (not one of the 11 files): `wmo-examples/metar-A3-1.xml` with `--official-schematron`, exit 0, 0 errors, 0 warnings, "Parse + validate: 1.6 s", container wall 1.82 to 1.88 s (two runs, each a new process, so the compile is paid every run). The cost of the official rules on a large bulletin remains unmeasured.

## Findings (grouped by code)

Pass A and pass B printed identical findings. Counts are findings printed in the logs (lower bounds for the METAR files because of the 100 cap). Positions are `line 1, pos N` character offsets (the 10 large files are single-line; the log location is `-`, no XPath).

| # | Code and message | Printed | Class |
| :-: | :--- | ---: | :--- |
| 1 | XSD-SCHEMA-VIOLATION: required `gml:id` missing | 450 | Producer data defect |
| 2 | XSD-SCHEMA-VIOLATION: `MeteorologicalBulletin` has invalid child `TAF` | 5 | Producer data defect |
| 3 | XSD-SCHEMA-VIOLATION: `aixm:name` fails `TextNameType` pattern | 14 | Producer data (non-ASCII names); schema strictness not verified |
| 4 | XSD-SCHEMA-VIOLATION: `iwxxm-us:Addendum` element not declared | 31 | Limitation (national extension schema not known to the validator) |
| 5 | XSD-SCHEMA-VIOLATION: `'station.<uuid>' is already used as an ID` | 6 | Producer data defect |
| 6 | XSD-SCHEMA-VIOLATION: empty `airTemperature` ("not a valid Double"), incomplete `MeteorologicalAerodromeObservation` | 3 + 1 | Producer data defect |
| 7 | XSD-SCHEMA-VIOLATION: `verticalVisibility` cannot contain `base`; missing `uom` | 1 + 1 | Producer data defect |
| 8 | XSD-SCHEMA-VIOLATION: SIGMET `issueTime`/`TimeInstant` structure, extra `SIGMET` in `meteorologicalInformation` | 5 | Producer data defect |
| 9 | XSD-TOO-MANY-ERRORS: "only the first 100 are reported" | 5 | Library limit by design |

Evidence per code:

1. Missing `gml:id`. Example `taf_europe_iwxxm.xml` pos 23, the bulletin root has none:
   `<collect:MeteorologicalBulletin xmlns:collect="http://def.wmo.int/collect/2014" xmlns:xsi=...>`
   In METAR files the first errors are the bulletin (pos 40 in `metar_europe`) and then the per-report elements. Regex counts of opening tags without `gml:id`: `gml:TimeInstant` 400 in each METAR file, 117 in `taf_europe`; `gml:TimePeriod` 426 and `aixm:ElevatedPoint` 117 in `taf_europe`, `aixm:ElevatedPoint` 400 in `metar_europe`. Example: `<gml:TimeInstant><gml:timePosition>2026-11-14T01:11:00.000Z</gml:timePosition></gml:TimeInstant>`. GML 3.2 requires `gml:id`; this is the producer.
2. TAF without wrapper (one per TAF file), pos 300: `<collect:MeteorologicalBulletin ...><iwxxm:TAF xmlns:iwxxm="http://icao.int/iwxxm/2025-2" ...>`. Count of `<collect:meteorologicalInformation>` is 0 in all 5 TAF files and 400 in each METAR file.
3. `metar_europe` pos 1585: `<aixm:name>SØRVÁGUR/VÁGAR ARPT</aixm:name>`; `metar_africa_mideast`: `IN AMÉNAS ARPT`. Whether the official XSD pattern is meant to reject these characters is not verified.
4. `metar_europe` pos 3409: `<iwxxm:extension><iwxxm-us:Addendum xmlns:iwxxm-us="http://www.weather.gov/iwxxm-us/3.0"><iwxxm-us:humanReadableText>OVC015/// WIND SKEID...`. 1204 `iwxxm-us:Addendum` in the 5 METAR files. I did not verify whether the extension point is `xs:any` with lax or strict processing in the schema.
5. `metar_asia_pacific` pos 17988: `<aixm:AirportHeliport gml:id="station.81ad5519-6ee2-4af7-8abd-36d6abaddbb3">`, already used earlier in the same file. Distinct repeated ids by regex: 66 / 124 / 119 / 41 / 49 (africa / asia / europe / north / south). The same aerodrome is embedded in many reports with the same id.
6. `metar_africa_mideast` pos 44607: `<iwxxm:MeteorologicalAerodromeObservation cloudAndVisibilityOK="false"><iwxxm:airTemperature uom="Cel"></iwxxm:airTemperature>...<iwxxm:qnh uom="hPa"></iwxxm:qnh>` (also no `surfaceWind`).
7. `metar_north_america` pos 47317: `<iwxxm:verticalVisibility><iwxxm:base uom="[ft_i]">100</iwxxm:base></iwxxm:verticalVisibility>`.
8. `airsigmet_global`: `<iwxxm:issueTime><gml:TimeInstant/><gml:timePosition>2026-11-13T23:04:51.549Z</gml:timePosition></iwxxm:issueTime>` (7 occurrences of `<gml:TimeInstant/>`); 7 `<iwxxm:SIGMET>` inside one `<collect:meteorologicalInformation>` (1 open tag); no `gml:id` on `aixm:Unit`, `gml:TimePeriod`, `aixm:AirspaceVolume`, `gml:LinearRing` (7 each). The "expected issuingAirTrafficServicesUnit" error is most likely a cascade of the malformed `issueTime`; not confirmed independently. Only 8 errors were printed, all about the first SIGMET; defects of SIGMETs 2 to 7 were not reported (the validator stopped descending), so their state is unknown.
9. Seen in the 5 METAR logs; the real XSD error count there is unknown.

## Library defect?

Nothing in this run demonstrates a library defect: each printed finding points at a concrete schema violation in the XML. Whole-bulletin rejection on a schema error is the documented design. Whether partial mapping should be offered for such bulletins is a product decision, not a verified bug. Untested: I did not check the library behaviour against the official XSD for findings 3 and 4.

## Surprises

- No report is mapped, none is silently dropped: all are rejected with exit 1. Report counts in the XML: METAR 1858 (396+380+390+313+379), SPECI 142 (4+20+10+87+21), TAF 630 (69+123+117+275+46), SIGMET 7, AIRMET 0.
- The cap hides most XSD errors in the 5 METAR files; the TAF files show only 2 errors because the XSD does not descend into the unexpected `TAF` element, so TAF content defects are unknown until the wrapper is fixed.
- The files declare `xsi:schemaLocation` `.../iwxxm/2025-2RC1/iwxxm.xsd` in the TAF/METAR reports (release candidate), the SIGMET file uses `.../2025-2/iwxxm.xsd`.

## Extra experiment (not requested; scratch copies outside the repo, results in `extra-fixed-ids/`)

For the 5 TAF files and `metar_europe` I added `gml:id` to the bulletin, every `gml:TimeInstant` and `gml:TimePeriod`, and wrapped TAFs in `collect:meteorologicalInformation`. All still exit 1 (101 findings, capped). Remaining: missing `gml:id` on `aixm:ElevatedPoint` (taf_europe 87 printed, metar_europe 92), the name pattern, the Addendum (metar), and in TAFs `iwxxm:layer` with invalid child `iwxxm:CloudLayer` (taf_europe 6, taf_north_america 25) and `MeteorologicalAerodromeForecast` with invalid child `weather` (taf_north_america 1). The `CloudLayer` and `weather` findings were not analysed further, so I do not classify them.
