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

It is larger than the default `IwxxmParserOptions.MaxDocumentBytes` (10 MB), so it has to be processed with raised limits
(see "Real-world sample: Met Office WAFS SIGWX" in `docs/REFERENCE.md`).

As published, the file does not validate against the IWXXM 2025-2 XSD: the mandatory `gml:id` is missing on every report and
on every `gml:TimePeriod`. The tests therefore check the product as published (the XSD stage rejects it) and, separately, a
copy repaired in memory.

**Licence:** the redistribution terms of the Met Office data were NOT verified by the maintainers, and the package contained
no licence file. No licence is granted or implied by this repository for this file. If you are the rights holder and want it
removed, please open an issue.
