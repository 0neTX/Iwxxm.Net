# Met Office WAFS SIGWX sample (external, unmodified)

`NEW(2025-2)SIGWX-260826T0600-06-48.xml` is a real operational WAFS significant weather (SIGWX) product in IWXXM 2025-2,
used as real-world test data. It is **not** an official WMO example and it is **not** written by this project.

| | |
| :--- | :--- |
| Origin | UK Met Office, World Area Forecast Centre (WAFC) London, WAFS SIGWX update package (zip with a `NEW(2025-2)` file in IWXXM 2025-2, a `PROD(2023-1)` file in IWXXM 2023-1 and a "Summary of Changes" PDF) |
| Download URL | not recorded by the maintainer who added the file |
| Product date | 2026-08-26, base time 06:00Z, charts for T+06 to T+48 (15 charts, one every three hours) |
| Content | one `collect:MeteorologicalBulletin` with 15 `iwxxm:WAFSSignificantWeatherForecast` reports and 4252 features (jet streams, clouds, turbulence, airframe icing, tropopause, volcanoes, tropical cyclones) |
| Size | 14,227,220 bytes, 162,098 lines |
| SHA-256 | `70d84973a7a2ef67b0f24eaa647ffa327ff786aff6f534d32f0aba3fdc6c6490` |

The file is a byte-identical copy of the extracted `NEW(2025-2)` member (`.gitattributes` disables any line-ending or text
conversion; `ExternalSigwxMetOfficeTests` asserts the length and the SHA-256). Do not reformat or edit it. The 2023-1 member
of the package is not supported by the library and is not included.

It is below the default `IwxxmParserOptions.MaxDocumentBytes` (50 MiB), so the default parser reads it (see "Real-world
sample: Met Office WAFS SIGWX" in `docs/REFERENCE.md`).

## Known data defects in the sample

- **Missing `gml:id`** on every `iwxxm:WAFSSignificantWeatherForecast` (15) and on every `gml:TimePeriod` (15). The mandatory
  attribute is required by the XSD, so the file as published does **not** validate and cannot pass the default pipeline
  unmodified. This is a defect of the data; the library keeps rejecting it (30 `XSD-SCHEMA-VIOLATION`s). The tests check the
  product as published and, separately, a copy in memory to which only the ids are added.
- **Non-UCUM wind speed unit `kn_i`** (UCUM is `[kn_i]`) on the 1,850 jet stream wind symbols. Neither the XSD nor the
  official Schematron checks it. The library accepts it as a legacy alias and reports one aggregated
  `ACL-MEASURE-UOM-NON-UCUM-ALIAS` warning.
- **Every chart is an instant**: each `gml:TimePeriod` has `beginPosition == endPosition` (equal to `phenomenonTime`). The
  schema allows it and the library accepts it.

**Licence and attribution:** the downloaded package contained no licence file. On 2026-11-05 the maintainer confirmed that this
data is redistributable, and it is included on that basis. The data is the work of the UK Met Office: please keep this
attribution when you reuse the file. This repository does not license the file; the MIT licence of the project does not
apply to it. If you are the rights holder and want it removed, please open an issue. See also
[THIRD-PARTY-NOTICES.md](../../../../../THIRD-PARTY-NOTICES.md#10-uk-met-office-wafs-sigwx-sample-test-data).
