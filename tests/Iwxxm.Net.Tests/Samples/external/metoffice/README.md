# Synthetic WAFS SIGWX bulletin sample (non-operational)

`example-iwxxm-2025-2-sigwx-non-operational.xml` is a synthetic, anonymized 15-chart WAFS significant weather (SIGWX) bulletin in IWXXM 2025-2 (`permissibleUsage="NON-OPERATIONAL"`, `permissibleUsageReason="TEST"`), used as large-scale test data.

| | |
| :--- | :--- |
| Type | Synthetic / anonymized IWXXM 2025-2 `collect:MeteorologicalBulletin` (`NON-OPERATIONAL`, `TEST`) |
| Product dates | Synthetic future dates (`2099-01-01T00:00:00Z` base time, T+06 to T+48 charts, one every three hours) |
| Content | 1 `collect:MeteorologicalBulletin` with 15 `iwxxm:WAFSSignificantWeatherForecast` reports and 4252 features (jet streams, clouds, turbulence, airframe icing, tropopause, volcanoes, tropical cyclones) |
| Size | 14,068,939 bytes, 162,099 lines |
| SHA-256 | `561b8884148eb715f8c0bb2f540c069ba2d7e0b1aa1cdc0134cb0a9d43caa30c` |

`.gitattributes` disables any line-ending or text conversion; `ExternalSigwxMetOfficeTests` asserts the length and the SHA-256. Do not reformat or edit it.

It is below the default `IwxxmParserOptions.MaxDocumentBytes` (50 MiB), so the default parser reads it (see "Large-scale sample: synthetic WAFS SIGWX" in `docs/REFERENCE.md`).

## Sample characteristics

- **Valid XSD structure**: includes sequential synthetic `gml:id` attributes on every `iwxxm:WAFSSignificantWeatherForecast` (15) and `gml:TimePeriod` (15), passing the official XSDs cleanly.
- **Non-UCUM wind speed unit `kn_i`** (UCUM is `[kn_i]`) on the 1,850 jet stream wind symbols. Neither the XSD nor the official Schematron checks it. The library accepts it as a legacy alias and reports one aggregated `ACL-MEASURE-UOM-NON-UCUM-ALIAS` warning.
- **Every chart is an instant**: each `gml:TimePeriod` has `beginPosition == endPosition` (equal to `phenomenonTime`). The schema allows it and the library accepts it.
