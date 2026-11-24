# Third-party notices

Iwxxm.Net is licensed under the [MIT License](LICENSE) (Copyright (c) 2026 0neTX). The three NuGet
packages, `Iwxxm.Net`, `Iwxxm.Net.Validation.Saxon` and `Iwxxm.Net.Export.GeoPackage`, and the repository also contain or depend on material from
other parties. This file lists it, the terms it comes with, and how this project meets them.

Rules used to write this file: every statement comes from a file in this repository, from the licence file or nuspec of
the NuGet package concerned, or from the publisher's own web page (URL and fetch date given). Where the terms could not
be established from those sources, the entry says so and is marked **To be confirmed by the maintainer**. Nothing here is
legal advice.

Facts checked on 2026-11-05. Web pages fetched on that date are marked "(fetched 2026-11-05)". Section 7A was written on
2026-11-24 from the nuspec files and package layouts on nuget.org; no web page was fetched for it.

## Contents

1. [Summary](#1-summary)
2. [Official XSD schemas (`Resources/Xsd/Official`)](#2-official-xsd-schemas)
   - [2.1 WMO IWXXM 2025-2](#21-wmo-iwxxm-2025-2-14-xsd-1-release-notes-file)
   - [2.2 WMO METCE, OPM and COLLECT 1.2](#22-wmo-metce-opm-and-collect-12-6-xsd-3-release-notes-files)
   - [2.3 OGC GML 3.2.1 / ISO 19136](#23-ogc-gml-321--iso-19136-28-xsd-1-readme)
   - [2.4 OGC Observations and Measurements 2.0](#24-ogc-observations-and-measurements-20-1-xsd)
   - [2.5 ISO/TC 211 ISO 19139 (gco, gmd, gsr, gss, gts)](#25-isotc-211-iso-19139-gco-gmd-gsr-gss-gts-26-xsd-6-readme-files)
   - [2.6 AIXM 5.1.1](#26-aixm-511-3-xsd)
   - [2.7 W3C XLink and XML namespace schemas](#27-w3c-xlink-and-xml-namespace-schemas-2-xsd)
   - [2.8 Hand-written schemas of this project](#28-hand-written-schemas-of-this-project-not-third-party)
3. [WMO code lists (RDF)](#3-wmo-code-lists-rdf)
4. [WMO example reports (test data)](#4-wmo-example-reports-test-data)
5. [Official WMO Schematron rules (`iwxxm.sch`)](#5-official-wmo-schematron-rules-iwxxmsch)
6. [SchXslt 1.10.1](#6-schxslt-1101)
7. [SaxonCS-HE 13.1.0 and its dependencies](#7-saxoncs-he-1310-and-its-dependencies)
   - [7A SQLite, Microsoft.Data.Sqlite and SQLitePCLRaw (GeoPackage package)](#7a-sqlite-microsoftdatasqlite-and-sqlitepclraw-geopackage-package)
8. [Test and benchmark dependencies (not redistributed)](#8-test-and-benchmark-dependencies-not-redistributed)
9. [Tooling that is not shipped](#9-tooling-that-is-not-shipped)
10. [Synthetic large-scale WAFS SIGWX sample (test data)](#10-synthetic-large-scale-wafs-sigwx-sample-test-data)
11. [Images linked from the documentation](#11-images-linked-from-the-documentation)
12. [Open points for the maintainer](#12-open-points-for-the-maintainer)
13. [How to update this file](#13-how-to-update-this-file)

## 1. Summary

| Component | Version | Redistributed in `Iwxxm.Net` | Redistributed in `Iwxxm.Net.Validation.Saxon` | Redistributed in `Iwxxm.Net.Export.GeoPackage` | Licence (as stated) |
| :--- | :--- | :---: | :---: | :---: | :--- |
| WMO IWXXM 2025-2 XSDs | 2025-2 | yes (embedded) | no | no | no licence file upstream; see 2.1 |
| WMO METCE / OPM / COLLECT XSDs | 1.2 | yes (embedded) | no | no | no licence file upstream; see 2.2 |
| OGC GML 3.2.1 XSDs | 3.2.1.2 | yes (embedded) | no | no | OGC copyright and notices in the files; see 2.3 |
| OGC O&M 2.0 XSD | 2.0.0 | yes (embedded) | no | no | OGC copyright notice in the file; see 2.4 |
| ISO 19139 XSDs (gco, gmd, gsr, gss, gts) | 2012-07-13 | yes (embedded) | no | no | no notice in the XSDs; ISO terms apply; see 2.5 |
| AIXM XSDs | 5.1.1 | yes (embedded) | no | no | BSD 3-clause text in the files; see 2.6 |
| W3C `xlink.xsd`, `xml.xsd` | as vendored | yes (embedded) | no | no | see 2.7 |
| WMO RDF code lists (20) | IWXXM 2025-2 | yes (embedded) | no (reads them from `Iwxxm.Net`) | no | no licence file upstream; see 3 |
| Official `iwxxm.sch` | IWXXM 2025-2 | no | yes (embedded) | no | no licence file upstream; see 5 |
| SchXslt | 1.10.1 | no | yes (embedded) | no | MIT |
| SaxonCS-HE | 13.1.0 | no | no (NuGet dependency, installed by the consumer) | no | MPL-2.0 |
| SQLite (native, in `SQLitePCLRaw.lib.e_sqlite3`) | 3.53.3 | no | no | no (NuGet dependency, installed by the consumer) | public domain (not verified from a primary source); see 7A |
| Microsoft.Data.Sqlite | 10.0.15 | no | no | no (NuGet dependency, installed by the consumer) | MIT (nuspec); see 7A |
| SQLitePCLRaw (bundle, core, providers, native lib) | 2.1.13 | no | no | no (NuGet dependency, installed by the consumer) | Apache-2.0 (nuspec); see 7A |
| Test and benchmark packages | various | no | no | no | various; see 8 |

Nothing third-party is redistributed in binary form other than the embedded resources above. The `Iwxxm.Net` package has
zero NuGet dependencies; `Iwxxm.Net.Validation.Saxon` declares dependencies on `Iwxxm.Net` and `SaxonCS-HE`;
`Iwxxm.Net.Export.GeoPackage` declares dependencies on `Iwxxm.Net` and `Microsoft.Data.Sqlite`, which brings the native
SQLite binaries through the consumer's NuGet restore.

What each package contains besides the assemblies: `README.md`, `LICENSE`, `CHANGELOG.md`, `THIRD-PARTY-NOTICES.md`, the
icon; `Iwxxm.Net` also `schema-notices/` (the upstream ReadMe and release-note files of the official schemas, 12 files);
`Iwxxm.Net.Validation.Saxon` also `licenses/SchXslt/LICENSE`; `Iwxxm.Net.Export.GeoPackage` only the common files.

## 2. Official XSD schemas

Location: `src/Iwxxm.Net/Resources/Xsd/Official/`. 80 `.xsd` files, byte-identical to the upstream copies
([wmo-im/iwxxm](https://github.com/wmo-im/iwxxm), `IWXXM/` and `externalSchema/`), compiled into the `Iwxxm.Net` assembly
as embedded resources (`.gitattributes` disables text conversion so the bytes stay identical). Only files reachable from
`iwxxm.xsd` and `iwxxm-collect.xsd` are included. Plus 12 upstream `.txt` notice files (packed as `schema-notices/`).

| Group | Path under `Resources/Xsd/Official/` | `.xsd` files |
| :--- | :--- | ---: |
| WMO IWXXM 2025-2 | `IWXXM/` | 14 |
| WMO METCE 1.2 | `externalSchema/schemas.wmo.int/metce/1.2/` | 3 |
| WMO OPM 1.2 | `externalSchema/schemas.wmo.int/opm/1.2/` | 2 |
| WMO COLLECT 1.2 | `externalSchema/schemas.wmo.int/collect/1.2/` | 1 |
| OGC GML 3.2.1 | `externalSchema/schemas.opengis.net/gml/3.2.1/` | 28 |
| OGC O&M 2.0 | `externalSchema/schemas.opengis.net/om/2.0/` | 1 |
| ISO 19139 gco | `externalSchema/schemas.opengis.net/iso/19139/20070417/gco/` | 3 |
| ISO 19139 gmd | `.../gmd/` | 17 |
| ISO 19139 gsr | `.../gsr/` | 2 |
| ISO 19139 gss | `.../gss/` | 2 |
| ISO 19139 gts | `.../gts/` | 2 |
| AIXM 5.1.1 | `externalSchema/aero/aixm/5.1.1/` | 3 |
| W3C XLink 1999 | `externalSchema/org/w3c/1999/xlink.xsd` | 1 |
| W3C XML namespace 2001 | `externalSchema/org/w3c/2001/xml.xsd` | 1 |
| **Total** | | **80** |

The ISO 19139 `gmx` schema is not included (not reachable from the IWXXM entry points).

Obligations common to the group: keep the files unmodified and keep their copyright and licence notices (done: the
files are never edited, and the notices travel inside them). `externalSchema/README.txt` states:
"The XML Schema and Schematron files in this directory are intended to be used as local copies for fast validation, and
at one point in time were faithful to the locations of schemas on the original web server."

### 2.1 WMO IWXXM 2025-2 (14 XSD, 1 release-notes file)

- What: the ICAO/WMO IWXXM 2025-2 XML schemas (`iwxxm.xsd`, `iwxxm-collect.xsd`, `common.xsd`, `metFeature.xsd`,
  `metarSpeci.xsd`, `taf.xsd`, `sigmet.xsd`, `airmet.xsd`, `vona.xsd`, `qvaci.xsd`, `WAFSSigWxFC.xsd`,
  `spaceWxAdvisory.xsd`, `volcanicAshAdvisory.xsd`, `tropicalCycloneAdvisory.xsd`), plus `IWXXM/ReleaseNotes-IWXXM.txt`.
  Used for XSD validation. Redistributed in `Iwxxm.Net` (embedded; release notes under `schema-notices/IWXXM/`).
- Copyright and licence notice in the files: none. The schemas carry no copyright or licence header
  (checked with a search for "copyright", "licence", "license" in `IWXXM/*.xsd`).
- Upstream: <https://github.com/wmo-im/iwxxm>. The repository top level lists no `LICENSE` file and its README has no
  licence section (fetched 2026-11-05).
- Licence: **not stated upstream.** This project does not claim one for these files.
- Compliance: files are unmodified, source and origin are named here and in `CONTRIBUTING.md`.
- **To be confirmed by the maintainer**: the redistribution terms of the WMO/ICAO-published schemas. Missing: a
  written statement or licence from WMO (for example in the WMO Codes Registry or in the IWXXM publication) allowing
  redistribution inside a library. This is a gap to close before the repository becomes public (see
  [12](#12-open-points-for-the-maintainer)).

### 2.2 WMO METCE, OPM and COLLECT 1.2 (6 XSD, 3 release-notes files)

- What: `metce.xsd`, `phenomena.xsd`, `procedure.xsd` (METCE, namespace `http://def.wmo.int/metce/2013`);
  `opm.xsd`, `observable-property.xsd` (OPM, `http://def.wmo.int/opm/2013`); `collect.xsd` (COLLECT,
  `http://def.wmo.int/collect/2014`); with `ReleaseNotes-METCE.txt`, `ReleaseNotes-OPM.txt`, `ReleaseNotes-COLLECT.txt`
  (packed under `schema-notices/`). Redistributed in `Iwxxm.Net`.
- Copyright and licence notice in the files: none.
- Upstream: same repository as 2.1 (`externalSchema/schemas.wmo.int/`).
- Licence: **not stated upstream.** **To be confirmed by the maintainer**: same missing item as 2.1.

### 2.3 OGC GML 3.2.1 / ISO 19136 (28 XSD, 1 ReadMe)

- What: the GML 3.2.1 application schema (`gml.xsd` and the 27 files it includes), plus `gml_3_2_1-ReadMe.txt`
  (packed under `schema-notices/`). Redistributed in `Iwxxm.Net`.
- Notice inside each XSD (for example `gml.xsd`):

  > GML is an OGC Standard.
  > Copyright (c) 2007,2010 Open Geospatial Consortium.
  > To obtain additional rights of use, visit http://www.opengeospatial.org/legal/ .

  The copyright line and the "To obtain additional rights of use" line are present in all 28 files (checked with a
  search).
- Notice in `gml_3_2_1-ReadMe.txt` (verbatim excerpt):

  > The Open Geospatial Consortium, Inc. official schema repository is at http://schemas.opengis.net/ .
  > Policies, Procedures, Terms, and Conditions of OGC(r) are available http://www.opengeospatial.org/ogc/policies/ .
  > Additional rights of use are described at http://www.opengeospatial.org/legal/ .
  > Copyright (c) 2007 Open Geospatial Consortium.

- Publisher terms (fetched 2026-11-05):
  - OGC "Copyright Notice and Disclaimers", <https://www.ogc.org/about/copyright-notice-and-disclaimers/>: material "may be
    downloaded or copied provided that ALL copies retain the copyright and any other proprietary notices"; "No material
    may be modified, edited or taken out of context"; "Mirroring of OGC content is currently disallowed."; "Specific
    notices do exist for OGC documents and software."
  - OGC "Document License Agreement for OGC Resources",
    <https://www.ogc.org/about/policies/document-license-agreement/>: permission "to deal in the Intellectual Property
    without restriction (except as set forth below)", including "the rights to implement, use, copy, modify, merge,
    publish, distribute, and/or sublicense copies", "provided that all copyright notices on the intellectual property are
    retained intact". The text does not mention schemas explicitly.
  - OGC software licences, <https://www.ogc.org/about/policies/software-licenses/>: lists "OGC Software License 1.0"
    (deprecated) and "Apache Software License 2.0"; the page does not say whether the schemas are covered.
- Licence: OGC copyright; terms as stated in the files and the OGC pages above. No SPDX identifier applies.
- Compliance: files unmodified; the copyright notices stay inside the files; the upstream ReadMe ships in the package.
- **To be confirmed by the maintainer**: which OGC licence formally covers the GML schemas, and whether embedding them
  in a package falls under "Mirroring of OGC content is currently disallowed" (the same files are redistributed by
  the WMO IWXXM repository). Missing: an OGC statement for `schemas.opengis.net`.

### 2.4 OGC Observations and Measurements 2.0 (1 XSD)

- What: `externalSchema/schemas.opengis.net/om/2.0/observation.xsd`. Redistributed in `Iwxxm.Net`.
- Notice in the file (verbatim):

  > Observations and Measurements - XML Implementation is an OGC Standard.
  > Copyright (c) [2010] Open Geospatial Consortium.
  > To obtain additional rights of use, visit http://www.opengeospatial.org/legal/.

- Terms and compliance: as 2.3. **To be confirmed by the maintainer**: as 2.3.

### 2.5 ISO/TC 211 ISO 19139 (gco, gmd, gsr, gss, gts) (26 XSD, 6 ReadMe files)

- What: the XML implementation of ISO/TS 19139:2007 (version 2012-07-13) as published by OGC and ISO, namespaces
  `http://www.isotc211.org/2005/{gco,gmd,gsr,gss,gts}`; 3 + 17 + 2 + 2 + 2 = 26 XSD files and a `ReadMe.txt` at the
  `20070417/` level and in each of the five folders (packed under `schema-notices/`). Redistributed in `Iwxxm.Net`.
- Copyright notice in the XSDs: none (searched). The ReadMe files carry only the heading "ISO(c)" and history notes
  (for example "2012-07-13 Nicolas Lesage on behalf of the ISO/TC 211 XML Maintenance Group"); they state that "The most
  current schemas are available at: http://standards.iso.org/ittf/PubliclyAvailableStandards/ISO_19139_Schemas/".
- Publisher terms (fetched 2026-11-05), <https://standards.iso.org/ittf/PubliclyAvailableStandards/ISO_19139_Schemas/>,
  "Licence Agreement for electronic inserts": "You are permitted to use the electronic insert(s) available on this
  site, in their original format without any modifications for the purposes specified in their respective ISO
  standard(s). When you download any electronic insert, you accept the ISO Customer Licence Agreement ("Licence
  Agreement"), clauses 1. ISO's Copyright, 7. Termination, 8. Limitations, and 9. Governing Law."
- Licence: ISO terms as above. No SPDX identifier applies.
- Compliance: files are used unmodified and in their original format, for the purpose of validating IWXXM documents.
- **To be confirmed by the maintainer**: whether re-publishing these files inside a NuGet package is covered by the
  ISO text above ("use ... for the purposes specified in their respective ISO standard(s)"). Missing: the full text of
  the ISO Customer Licence Agreement clauses cited, which this project has not reviewed.

### 2.6 AIXM 5.1.1 (3 XSD)

- What: `AIXM_AbstractGML_ObjectTypes.xsd`, `AIXM_DataTypes.xsd`, `AIXM_Features.xsd` (namespace
  `http://www.aixm.aero/schema/5.1.1`). Redistributed in `Iwxxm.Net`.
- Licence: BSD 3-clause (the files call it "an instance of the BSD license template"); the text below is in all three
  files and is reproduced verbatim:

  > Copyright (c) 2016, EUROCONTROL & FAA
  > =====================================
  > All rights reserved.
  > Redistribution and use in source and binary forms, with or without modification, are permitted provided that the
  > following conditions are met:
  > * Redistributions of source code must retain the above copyright notice, this list of conditions and the following
  >   disclaimer.
  > * Redistributions in binary form must reproduce the above copyright notice, this list of conditions and the
  >   following disclaimer in the documentation and/or other materials provided with the distribution.
  > * Neither the names of EUROCONTROL or FAA nor the names of their contributors may be used to endorse or promote
  >   products derived from this specification without specific prior written permission.
  >
  > THIS SPECIFICATION IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND ANY EXPRESS OR IMPLIED
  > WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR
  > PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT,
  > INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE
  > GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF
  > LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT
  > OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
  > ==========================================
  > Editorial note: this license is an instance of the BSD license template as provided by the Open Source Initiative:
  > http://www.opensource.org/licenses/bsd-license.php

  Line breaks above were re-flowed; the first comment of each file is the authoritative copy.
- Compliance: the files are redistributed in source form, unmodified, so the copyright notice, the conditions and the
  disclaimer remain inside them. This file repeats them for the binary package; the names EUROCONTROL and FAA are not
  used to endorse or promote this project.
- Upstream site: <https://www.aixm.aero/>. The pages fetched on 2026-11-05 contain no licence statement; the licence
  evidence is the text in the files themselves.

### 2.7 W3C XLink and XML namespace schemas (2 XSD)

- `externalSchema/org/w3c/1999/xlink.xsd` (XLink 1.0 schema document). Header in the file (verbatim):

  > (c) Copyright 2015 University Corporation for Atmospheric Research (UCAR). All Rights Reserved.

  This is the copy distributed with the WMO/OGC schema set; the current file served at
  <https://www.w3.org/1999/xlink.xsd> carries no copyright line (fetched 2026-11-05), so the two files differ.
  Licence of the UCAR copy: **not stated in the file.** **To be confirmed by the maintainer**: the licence of the
  UCAR-copyrighted copy. Missing: a licence statement from UCAR or from the OGC/WMO that distributes it.
- `externalSchema/org/w3c/2001/xml.xsd` (schema for the `xml:` namespace). The file contains no copyright or licence
  header; it states that the `xml:` names are defined by the W3C and describes how to reference the document.
  W3C publishes documents and software under the
  [W3C Software and Document License](https://www.w3.org/copyright/software-license-2023/) (fetched 2026-11-05: permission
  "to copy, modify, and distribute this work, with or without modification, for any purpose and without fee or
  royalty ... provided that you include ... the full text of this NOTICE in a location viewable to users of the
  redistributed or derivative work"). **To be confirmed by the maintainer**: that this licence applies to the vendored
  `xml.xsd`; the W3C licence has not been matched to this specific file.
- Both files are redistributed in `Iwxxm.Net`, unmodified.

### 2.8 Hand-written schemas of this project (not third-party)

`src/Iwxxm.Net/Resources/Xsd/Legacy/` (7 files: `aixm.xsd`, `collect.xsd`, `gml.xsd`, `iwxxm-2025-2.xsd`,
`iwxxm-sigwx.xsd`, `metce.xsd`, `xlink.xsd`) are hand-written by this project, are not official, and are covered by the
project licence (MIT). `iwxxm-sigwx.xsd` is marked in its header as "NON-STANDARD SCHEMA (Iwxxm.Net extension)". Four of
them are embedded (`xlink.xsd`, `gml.xsd`, `aixm.xsd`, `iwxxm-sigwx.xsd`).

## 3. WMO code lists (RDF)

- What: 20 RDF/SKOS files in `src/Iwxxm.Net/Resources/Codelists/` (`codes.wmo.int-49-2-*.rdf` x11,
  `codes.wmo.int-bufr4-codeflag-*.rdf` x5, `codes.wmo.int-common-nil.rdf`, `codes.wmo.int-iwxxm-AviationColourCode.rdf`,
  `codes.wmo.int-iwxxm-MeteorologicalFeature.rdf`, `codes.wmo.int-iwxxm-nil.rdf`), byte-identical to
  `IWXXM/rule/*.rdf` of [wmo-im/iwxxm](https://github.com/wmo-im/iwxxm). Embedded in `Iwxxm.Net`; the Saxon package reads
  them from there. Four of them are also copied under `tests/Iwxxm.Net.Tests/TestData/OfficialCodelists/` (test data,
  not packed).
- Origin: WMO Codes Registry, <https://codes.wmo.int/>.
- Notice in the files: none (no copyright or licence statement; the files only list concepts, labels and
  `owl:versionInfo`).
- Publisher statement: the footer of <https://codes.wmo.int/> says "Unless otherwise stated, all content is available
  under the Open Government Licence" and links to
  <https://www.nationalarchives.gov.uk/doc/open-government-licence/version/3/> (fetched 2026-11-05). No statement
  specific to the RDF files was found.
- **To be confirmed by the maintainer**: that the Open Government Licence statement of the registry website applies to
  these exported files (the registry is operated for WMO; the OGL is a UK licence), and the attribution wording WMO
  expects.

## 4. WMO example reports (test data)

- What: 29 official IWXXM 2025-2 example XML documents in `tests/Iwxxm.Net.Tests/Samples/wmo-examples/`, byte-identical
  to the examples published by WMO ([wmo-im/iwxxm](https://github.com/wmo-im/iwxxm)). Used as test data. Not packed in
  any NuGet package. Their golden outputs in `tests/Iwxxm.Net.Tests/Golden/` are derived from them by this project.
- Licence: no notice in the files; no licence file upstream (see 2.1). **To be confirmed by the maintainer**: as 2.1.

## 5. Official WMO Schematron rules (`iwxxm.sch`)

- What: `src/Iwxxm.Net.Validation.Saxon/Resources/Schematron/iwxxm.sch`, byte-identical copy of `IWXXM/rule/iwxxm.sch`
  (IWXXM 2025-2, `queryBinding="xslt2"`, 165 rule patterns; SHA-256 checked by a test). Embedded in
  `Iwxxm.Net.Validation.Saxon`.
- Notice in the file: none (no copyright or licence header). Upstream: <https://github.com/wmo-im/iwxxm>, no licence file
  (see 2.1).
- **To be confirmed by the maintainer**: as 2.1.

Not third-party: `src/Iwxxm.Net/Resources/Schematron/iwxxm-2025-2-sigwx.xslt` is the project's own XSLT 1.0 subset of the
rules (MIT).

## 6. SchXslt 1.10.1

- What: the SchXslt XSLT 2.0 Schematron-to-XSLT compiler, used to turn `iwxxm.sch` into an XSLT stylesheet at run time.
  Location: `src/Iwxxm.Net.Validation.Saxon/Resources/SchXslt/` (`2.0/**/*.xsl`, `LICENSE`, `README.md`); the 12 `.xsl`
  files are embedded in `Iwxxm.Net.Validation.Saxon`, and `LICENSE` is packed as `licenses/SchXslt/LICENSE`.
- Upstream: <https://github.com/schxslt/schxslt> (named in the vendored `README.md`; the page was not reachable on
  2026-11-05, so the licence below comes from the vendored `LICENSE` and `README.md`, which state: "SchXslt is copyright
  (c) by David Maus ... and released under the terms of the MIT license.").
- Licence: MIT (SPDX `MIT`). Copyright (c) 2018-2021 David Maus. Full text, as shipped in `LICENSE`:

```text
MIT License

Copyright (c) 2018-2021 David Maus

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

- Obligation: include the copyright notice and permission notice in copies. Compliance: the files are unmodified, the
  `LICENSE` file ships in the package under `licenses/SchXslt/`, and the text is repeated above.

## 7. SaxonCS-HE 13.1.0 and its dependencies

`Iwxxm.Net.Validation.Saxon` declares a NuGet dependency on `SaxonCS-HE` 13.1.0 (`Directory.Packages.props`). The
Saxon assemblies are **not** copied into this project's package: the consumer's NuGet restore downloads them from the
configured feed, together with the transitive packages below. The information is given because consumers redistribute
them with their applications.

### 7.1 SaxonCS-HE 13.1.0

- Publisher: Saxonica Ltd. nuspec: authors "Saxonica Ltd.", copyright "Copyright (c) 2021-2026 Saxonica, Ltd.", project
  <https://saxonica.com/>, license `LICENSE.txt` (type file).
- Licence file shipped in the package, first lines (verbatim): "SaxonCS-HE is created and distributed by Saxonica Ltd, as
  an "executable form" of the open-source SaxonJ-HE product, under the terms of the Mozilla Public License version
  2.0, which is reproduced below." Licence: **MPL-2.0** (SPDX `MPL-2.0`); the file reproduces the MPL 2.0 text, whose
  full text is also at <https://www.mozilla.org/MPL/2.0/>.
- Source code: MPL-2.0 section 3.2 requires that executable-form Covered Software be made available in source form and
  that recipients be told how to get it. Saxonica states: "Saxon-HE is an open-source product available under the
  Mozilla Public License version 2.0" at <https://github.com/Saxonica/Saxon-HE> (fetched 2026-11-05, "This repository
  contains all releases for the current actively maintained versions of Saxon-HE: 10, 11, 12, and 13."). The package
  README states that SaxonCS is built from the same source code as the Java product. The nuspec records the source
  commit `9d0b60cf2556658cfd7d44f265fa3efc729abbf4` (no repository URL is given in the nuspec).
- Compliance: this project does not modify or redistribute Saxon. It uses the public API (`new Processor(false)`, Home
  Edition capabilities, no licence key). The MPL file-level copyleft applies to Saxon's own files and does not reach
  this project's MIT code. Source location for recipients: the Saxonica repository above.
- **To be confirmed by the maintainer**: the exact location of the SaxonCS 13.1.0 source (the Saxonica repository
  page fetched lists releases; this project did not verify that it contains the C# conversion output for the nuspec
  commit).

### 7.2 Transitive dependencies of SaxonCS-HE 13.1.0 (net10.0 group)

Read from the nuspec files in the NuGet cache (restored in the build container). None of them is redistributed in this
project's packages.

| Package | Version | Licence (from the nuspec / package) | Copyright (from the nuspec) |
| :--- | :--- | :--- | :--- |
| AngleSharp | 1.5.2 | MIT (expression) | Copyright 2013-2026, AngleSharp. |
| DotNet.Bundle | 0.9.13 | **no licence, copyright or project URL in the nuspec** (description "Package Description"; MSBuild task assembly only) | none stated |
| J2N | 2.1.0 | Apache-2.0 (`LICENSE.txt` in the package is the Apache License 2.0) | Copyright (c) 2019 - 2024 J2N |
| Microsoft.AspNetCore.StaticFiles | 2.3.8 | licenseUrl <https://raw.githubusercontent.com/aspnet/AspNetCore/2.0.0/LICENSE.txt>, which starts "Licensed under the Apache License, Version 2.0" (fetched 2026-11-05) | Microsoft Corporation |
| Microsoft.Extensions.Configuration (+ .Json, .Xml) | 10.0.13 declared by Saxon (10.0.11 resolved here) | MIT (expression) | Microsoft Corporation |
| NSec.Cryptography | 25.4.0 | MIT (expression); `NOTICE` also contains notices for libsodium (ISC), RFC 6234 code (Simplified BSD), the .NET Runtime (MIT) and Steve Thomas's Hex/Base64 code (MIT) | (c) 2025 Klaus Hartke |
| Singulink.Numerics.BigDecimal | 3.4.0 | MIT (expression) | (c) Singulink |
| Singulink.Numerics.BigIntegerExtensions | 2.3.2 | MIT (expression) | (c) Singulink |
| System.IO.Hashing | 10.0.13 | MIT (expression) | Microsoft Corporation |
| XmlResolver | 6.1.1 | MIT (expression) | Copyright (c) 2021-2026 Norman Walsh. Portions Copyright (c) 2022-2026 Saxonica, Ltd. |
| XmlResolverData | 6.1.1 | MIT (expression) | none stated |

Their own dependencies seen in the restored cache: Singulink.Enums 2.3.1 (MIT), libsodium 1.0.20.1 (ISC, Frank Denis,
native library used by NSec), IndexRange 1.1.0 (MIT), NLog 6.0.7 (BSD-3-Clause), Microsoft.Bcl.Memory 10.0.7,
Microsoft.Extensions.* and System.IO.Packaging 10.0.10 (MIT), and the ASP.NET Core 2.3.x abstractions (Microsoft;
licenseUrl as for StaticFiles).

Open point: DotNet.Bundle publishes no licence information in its nuspec. **To be confirmed by the maintainer**:
the licence of DotNet.Bundle (a build-time MSBuild task referenced by SaxonCS-HE). It is a Saxonica dependency, not
redistributed here.

## 7A. SQLite, Microsoft.Data.Sqlite and SQLitePCLRaw (GeoPackage package)

`Iwxxm.Net.Export.GeoPackage` declares a NuGet dependency on `Microsoft.Data.Sqlite` 10.0.15 (`Directory.Packages.props`).
Like Saxon, none of it is copied into this project's package: the consumer's NuGet restore downloads it, together with
the packages below, and the consumer's application then redistributes it (about 19 MB, 95% of it the native binaries).
Facts in this section come from the nuspec files and the file layout of the packages on nuget.org (version index,
nuspec and `.nupkg` contents), read on 2026-11-24. **Not verified**: anything below that says so.

| Package | Version | Licence (nuspec expression) | Role |
| :--- | :--- | :--- | :--- |
| Microsoft.Data.Sqlite | 10.0.15 | MIT | metapackage: depends on `.Core` and on the SQLitePCLRaw bundle (no assembly of its own) |
| Microsoft.Data.Sqlite.Core | 10.0.15 | MIT | ADO.NET provider (`netstandard2.0` asset used by this package) |
| SQLitePCLRaw.bundle_e_sqlite3 | 2.1.13 | Apache-2.0 | selects the provider and native library per platform |
| SQLitePCLRaw.core | 2.1.13 | Apache-2.0 | raw SQLite binding |
| SQLitePCLRaw.provider.e_sqlite3 | 2.1.13 | Apache-2.0 | native loader (.NET Standard 2.0 and modern .NET) |
| SQLitePCLRaw.provider.dynamic_cdecl | 2.1.13 | Apache-2.0 | native loader on .NET Framework |
| SQLitePCLRaw.lib.e_sqlite3 | 2.1.13 | Apache-2.0 (nuspec expression only) | the native SQLite binaries (below) |
| System.Memory, System.Buffers, System.Numerics.Vectors, System.Runtime.CompilerServices.Unsafe | 4.5.3, 4.4.0, 4.4.0, 4.5.2 | **not read** | .NET Framework consumers only (transitive) |

- **Native SQLite**: `SQLitePCLRaw.lib.e_sqlite3` 2.1.13 carries `runtimes/<rid>/native/` builds of SQLite for Windows
  (`win-x86`, `win-x64`, `win-arm64`), Linux (glibc and musl, several CPU architectures), macOS and Mac Catalyst, and a
  WebAssembly build. The library embedded in the Linux builds reports SQLite **3.53.3**, compiled with, among others,
  `ENABLE_RTREE`, `ENABLE_FTS3/4/5`, `ENABLE_MATH_FUNCTIONS` and `DEFAULT_FOREIGN_KEYS`. SQLite itself is placed in the
  public domain by its authors (<https://www.sqlite.org/copyright.html>); **this statement was not fetched or checked
  for this file**: confirm it from that page. The package carries no separate
  licence file for the native code, only the nuspec expression Apache-2.0, which covers the SQLitePCLRaw packaging, not
  SQLite.
- Not verified: the SQLite version and compile options of the Windows and macOS binaries (only the Linux builds were
  run; the others are assumed to be built from the same source); the licence of the four `System.*` packages (they
  are Microsoft packages, but their nuspecs were not read for this file).
- Compliance: this project does not modify, copy or ship any of these files; it only references the packages. The
  MIT and Apache-2.0 terms (keep the copyright and licence notices when redistributing) apply to a consumer that ships
  them with an application; this file is packed into `Iwxxm.Net.Export.GeoPackage` so that the information travels with
  it.
- Version policy: `Microsoft.Data.Sqlite` 10.0.x is used because every 10.0.x release ships a `netstandard2.0` asset;
  11.0.0 targets `net10.0` only (and SQLitePCLRaw 3.x) and is not used.

**To be confirmed by the maintainer**: the public-domain status of SQLite (primary source above) and the licence of the
four `System.*` packages, before the GeoPackage package is published.

## 8. Test and benchmark dependencies (not redistributed)

Used only by `tests/` projects. They are not part of either NuGet package (no packable project references them) and are
listed for completeness. Licence data comes from the nuspec files in the build container's NuGet cache.

| Package | Version | Used by | Licence | Copyright (nuspec) |
| :--- | :--- | :--- | :--- | :--- |
| xunit (+ .core, .assert, .extensibility.*, .analyzers 1.18.0) | 2.9.3 | tests | Apache-2.0 | Copyright (C) .NET Foundation |
| xunit.abstractions | 2.0.3 | tests | licenseUrl <https://raw.githubusercontent.com/xunit/xunit/master/license.txt>; no SPDX expression | none |
| xunit.runner.visualstudio | 4.0.1 | tests | Apache-2.0 | Copyright (C) .NET Foundation |
| Microsoft.NET.Test.Sdk (+ Microsoft.TestPlatform.*, Microsoft.CodeCoverage) | 18.11.0 | tests | MIT | Microsoft Corporation |
| coverlet.collector | 10.1.0 | tests | MIT | (c) 2018 Toni Solarin-Sodara |
| NetArchTest.Rules | 1.3.2 | architecture tests | no licence in the nuspec; the upstream repository `LICENSE` (<https://raw.githubusercontent.com/BenMorris/NetArchTest/master/LICENSE>, fetched 2026-11-05) is the MIT License, "Copyright (c) 2018 Ben Morris" | Copyright (c) 2021 Ben Morris |
| Mono.Cecil (dependency of NetArchTest.Rules) | 0.11.3 | architecture tests | nuspec licenseUrl <http://opensource.org/licenses/mit-license.php>; upstream `LICENSE.txt` is MIT, "Copyright (c) 2008 - 2015 Jb Evain" and "Copyright (c) 2008 - 2011 Novell, Inc." (fetched 2026-11-05) | none in the nuspec |
| System.Text.Json | 9.0.10 | `net48` test target only | MIT | Microsoft Corporation |
| BenchmarkDotNet (+ .Annotations) | 0.15.8 | benchmarks | MIT | .NET Foundation and contributors |
| BenchmarkDotNet dependencies: Perfolizer 0.6.1, Pragmastat 3.2.4, Iced 1.21.0, Gee.External.Capstone 2.3.0, Microsoft.Diagnostics.Runtime 3.1.512801, Microsoft.Diagnostics.Tracing.TraceEvent 3.1.21, Microsoft.Diagnostics.NETCore.Client 0.2.410101, Microsoft.CodeAnalysis.* 4.14.0, System.Management 9.0.5 | various | benchmarks | MIT in every nuspec | various |
| BenchmarkDotNet dependency CommandLineParser | 2.9.1 | benchmarks | `License.md` file in the package | Copyright (c) 2005 - 2020 Giacomo Stelluti Scala & Contributors |
| BenchmarkDotNet dependency Microsoft.DotNet.PlatformAbstractions | 3.1.6 | benchmarks | `LICENSE.TXT` file in the package | Microsoft Corporation |

(`LICENSE.TXT`/`License.md` texts of the last two rows were not reproduced here; read them in the packages if needed.)

## 9. Tooling that is not shipped

- The .NET SDK container image (`mcr.microsoft.com/dotnet/sdk`) used by the `Dockerfile`; Mono and the xunit console
  runner (`xunit.runner.console` 2.9.3, pinned by SHA-256) in the optional `net48-test` stage; GitHub Actions in
  `.github/workflows/`. None is part of the packages or the source tree.
- `xscgen` (design-time helper, `.config/dotnet-tools.json`) is not restored by the build and writes only an optional
  reference scaffold.
- Mermaid diagrams in the Markdown files are rendered by GitHub; no Mermaid code is stored.

## 10. Synthetic large-scale WAFS SIGWX sample (test data)

- `tests/Iwxxm.Net.Tests/Samples/external/metoffice/example-iwxxm-2025-2-sigwx-non-operational.xml` is a synthetic,
  anonymized non-operational (`TEST`) WAFS SIGWX bulletin used for large-scale and antimeridian test coverage. Test
  data only; not packed in any NuGet package.

## 11. Images linked from the documentation

No third-party image is stored in the repository. `docs/GUIDE.md` (section 17, "Further reading and visual references")
loads or links pictures from Wikimedia Commons and records their licences:

| Picture | Author / source | Licence recorded in `docs/GUIDE.md` |
| :--- | :--- | :--- |
| US jet stream, wind speed at 500 mb, 2 March 2012 | NOAA Storm Prediction Center / NNVL | public domain |
| Four cumulonimbus incus clouds | Peter Broster | CC0 1.0 |
| Jet Stream diagram (SVG), linked only | Fred the Oyster | CC BY-SA 4.0 |
| Cumulonimbus incus cloud, linked only | Bidgee | CC BY 3.0 |
| Jet stream convergence, linked only | NASA | public domain |

The SVG diagrams in `docs/assets/` are drawn by hand for this project (MIT).

## 12. Open points for the maintainer

1. WMO/ICAO schemas, code lists, example reports and `iwxxm.sch` (2.1, 2.2, 3, 4, 5): upstream states no licence.
2. OGC schemas (2.3, 2.4): which OGC licence covers `schemas.opengis.net`, and the "mirroring" sentence.
3. ISO 19139 schemas (2.5): whether redistribution in a package is covered by the ISO electronic-insert terms.
4. UCAR-copyrighted `xlink.xsd` and the W3C `xml.xsd` (2.7).
5. DotNet.Bundle licence (7.2) and the location of the SaxonCS 13.1.0 source (7.1).
6. SQLite public-domain statement and the licence of the `System.*` packages brought in on .NET Framework (7A).

None of these blocks the MIT licence of the project's own code; they concern the redistributed third-party files.

## 13. How to update this file

1. Component versions: after changing `Directory.Packages.props`, a vendored folder or a schema release, update the
   version in the entry and in the summary table.
2. Re-read the notice of every changed component from the new files: the XSD headers
   (`rg -i 'copyright|licen[sc]e' src/Iwxxm.Net/Resources/Xsd/Official`), the vendored ReadMe files
   (`schema-notices/`), `src/Iwxxm.Net.Validation.Saxon/Resources/SchXslt/LICENSE`, and, for NuGet packages, the nuspec
   and licence file of the restored package (for example inside the container under `/root/.nuget/packages/<id>/<version>/`).
   Copy copyright lines verbatim; never write a licence that is not in one of those sources.
3. SaxonCS-HE: open `LICENSE.txt` and the nuspec of the new version; re-list the dependency group of the nuspec; check
   the Saxonica source location is still valid.
4. Count the files again (`fd -e xsd . src/Iwxxm.Net/Resources/Xsd/Official | wc -l` is 80 today; 20 RDF files; 29
   examples) and update the tables.
5. Keep the unresolved items in section 12 until the missing evidence is added to the repository or this file, then
   remove the "To be confirmed by the maintainer" marker.
6. Fetch each publisher URL again when a statement changes, and record the URL and the fetch date.
7. Build with `podman build --target pack -t iwxxm-net-pack .`: the pack stage fails if `THIRD-PARTY-NOTICES.md` is
   missing from either package.
