# Iwxxm.Net reference

The companion to the [guide](GUIDE.md): the guide teaches, this page looks things up. It holds one example per product family, the GeoPackage table layout, the composition root and its ports, the full error-code table, the value-object catalogue and the namespace map.

Every C# snippet is compiled against the packaged library. As in the guide, paths such as `samples/metar-A3-1.xml` refer to the official example messages copied next to your project (see [Before you start](GUIDE.md#1-before-you-start)).

**Contents**

1. [One example per product family](#one-example-per-product-family)
2. [GeoJSON export in detail](#geojson-export-in-detail)
3. [GeoPackage tables and columns](#geopackage-tables-and-columns)
4. [Composition root and dependency injection](#composition-root-and-dependency-injection)
5. [Official WMO Schematron](#official-wmo-schematron)
6. [Error codes](#error-codes)
7. [Domain value objects and spatial primitives](#domain-value-objects-and-spatial-primitives)
8. [Namespaces and layers](#namespaces-and-layers)

---

## One example per product family

### METAR and SPECI

`AerodromeObservation` is the common base of `Metar` and `Speci`.

```csharp
using System;
using Iwxxm.Net;
using Iwxxm.Net.Reports;

// ParseFileAs<Metar> or ParseFileAs<Speci> for a specific kind; AerodromeObservation accepts both.
Result<AerodromeObservation> metarResult =
    IwxxmParser.ParseFileAs<AerodromeObservation>("samples/metar-A3-1.xml");

if (metarResult.IsFailure)
{
    return;
}

AerodromeObservation metar = metarResult.Value;

Console.WriteLine($"Report ID       : {metar.Id}");
Console.WriteLine($"Type            : {metar.ReportType}");
Console.WriteLine($"Aerodrome       : {metar.AerodromeDesignator} ({metar.AerodromeName})");
Console.WriteLine($"ARP coordinate  : {metar.AerodromePosition}");
Console.WriteLine($"Observation UTC : {metar.ObservationTimeUtc:O}"); // null when given only by an unresolved xlink:href
Console.WriteLine($"Status / usage  : {metar.ReportStatus} / {metar.PermissibleUsage}");
Console.WriteLine($"Automated (AUTO): {metar.IsAutomatedStation}");
Console.WriteLine($"CAVOK           : {metar.CloudAndVisibilityOk}");
Console.WriteLine($"Temperature     : {metar.AirTemperatureCelsius} C (dew point {metar.DewpointTemperatureCelsius} C)");
Console.WriteLine($"Altimeter (QNH) : {metar.QnhHpa} hPa");
Console.WriteLine($"Visibility      : {metar.PrevailingVisibilityMeters} m");

if (metar.SurfaceWind != null)
{
    // Speeds keep the reported unit (m/s or [kn_i]); convert explicitly when a fixed unit is needed.
    Console.WriteLine($"Surface wind    : {metar.SurfaceWind.MeanDirectionDegrees} deg at {metar.SurfaceWind.MeanSpeed} " +
                      $"({metar.SurfaceWind.MeanSpeed?.ToKnots():F0} kt, gust: {metar.SurfaceWind.GustSpeed?.ToString() ?? "none"}, " +
                      $"variable: {metar.SurfaceWind.IsVariableDirection})");
}

foreach (RunwayVisualRange rvr in metar.RunwayVisualRanges)
{
    Console.WriteLine($"RVR runway {rvr.RunwayDesignator}: {rvr.MeanRvrMeters} m (tendency: {rvr.PastTendency})");
}

foreach (AerodromeWeather weather in metar.PresentWeather)
{
    Console.WriteLine($"Present weather : {weather.Notation} (official code: {weather.IsKnown})");
}

foreach (AerodromeCloudLayer layer in metar.CloudLayers)
{
    // Amount and CloudType are code-list values; Base keeps the reported unit, e.g. "1000 [ft_i]".
    Console.WriteLine($"Cloud layer     : amount {layer.Amount?.Code}, base {layer.Base} ({layer.Base?.ToMeters():F0} m), type {layer.CloudType?.Code}");
}

foreach (AerodromeForecastGroup trend in metar.TrendForecasts)
{
    Console.WriteLine($"TREND ({trend.ChangeIndicator}): visibility {trend.PrevailingVisibilityMeters} m");
}
```
```output
Report ID       : uuid.510df5de-fefb-4406-bafd-faab35333ec0
Type            : Metar
Aerodrome       : YUDO (DONLON/INTERNATIONAL)
...
Altimeter (QNH) : 1018 hPa
Visibility      : 600 m
...
```

### TAF

Base forecast, `BECOMING`, `TEMPORARY_FLUCTUATIONS`, `FROM`, and the cancel and NIL shapes. A fully worked TAF, including the temperature groups, is in the [guide](GUIDE.md#a-taf-base-forecast-change-groups-tx-and-tn).

```csharp
using System;
using System.Linq;
using Iwxxm.Net;
using Iwxxm.Net.Reports;

Result<Taf> tafResult = IwxxmParser.ParseFileAs<Taf>("samples/taf-A5-1.xml");

if (tafResult.IsFailure)
{
    return;
}

Taf taf = tafResult.Value;

Console.WriteLine($"TAF for {taf.AerodromeDesignator} ({taf.ValidFromUtc:O} -> {taf.ValidUntilUtc:O})");
Console.WriteLine($"Status: {taf.ReportStatus}, IsCancelReport: {taf.IsCancelReport}, IsNil: {taf.IsNilForecast}");

if (taf.BaseForecast != null)
{
    Console.WriteLine($"Base visibility : {taf.BaseForecast.PrevailingVisibilityMeters} m");
    Console.WriteLine($"Base wind       : {taf.BaseForecast.SurfaceWind?.MeanDirectionDegrees} deg / {taf.BaseForecast.SurfaceWind?.MeanSpeed}");
}

foreach (AerodromeForecastGroup change in taf.ChangeForecasts)
{
    Console.WriteLine($"Change [{change.ChangeIndicator}] ({change.ValidFromUtc:HH:mm}Z - {change.ValidUntilUtc:HH:mm}Z): " +
                      $"vis={change.PrevailingVisibilityMeters} m, weather={string.Join(", ", change.Weather.Select(w => w.Notation))}");
}
```
```output
TAF for YUDO (2012-08-16T00:00:00.0000000+00:00 -> 2012-08-16T18:00:00.0000000+00:00)
Status: Normal, IsCancelReport: False, IsNil: False
...
```

### SIGMET, volcanic ash and tropical cyclone SIGMET, AIRMET

`Sigmet` unifies `SIGMET`, `VolcanicAshSIGMET` and `TropicalCycloneSIGMET`; `ReportType` keeps the variant. A walk-through with circles and cancellations is in the [guide](GUIDE.md#13-worked-example-sigmet).

```csharp
using System;
using Iwxxm.Net;
using Iwxxm.Net.Reports;

Sigmet sigmet = IwxxmParser.ParseFileAs<Sigmet>("samples/sigmet-VA-EGGX.xml").Value;

Console.WriteLine($"SIGMET variant  : {sigmet.ReportType}");
Console.WriteLine($"ATS unit / MWO  : {sigmet.AtsUnitDesignator} ({sigmet.AtsUnitName}) / MWO {sigmet.MwoDesignator}");
Console.WriteLine($"FIR             : {sigmet.FirDesignator} ({sigmet.FirName})");
Console.WriteLine($"Sequence number : {sigmet.SequenceNumber}");
Console.WriteLine($"Validity        : {sigmet.ValidFromUtc:O} to {sigmet.ValidUntilUtc:O}");
if (sigmet.PermissibleUsage == PermissibleUsage.NonOperational)
{
    Console.WriteLine($"NON-OPERATIONAL ({sigmet.PermissibleUsageReason}): {sigmet.PermissibleUsageSupplementary}");
}
Console.WriteLine($"Phenomenon      : {sigmet.Phenomenon?.Code} ({sigmet.Phenomenon?.Notation})");

if (sigmet.EruptingVolcano != null)
{
    Console.WriteLine($"Erupting volcano: {sigmet.EruptingVolcano.Name} at {sigmet.EruptingVolcano.Position}");
}

foreach (EnRoutePhenomenonVolume volume in sigmet.EvolvingConditions)
{
    Console.WriteLine($"Evolving volume {volume.Id}:");
    // VerticalLimit keeps value, unit and reference: "FL250 STD", "15000 M MSL", "GND SFC", "NIL(unknown)".
    Console.WriteLine($"  vertical extent : {volume.LowerLimit?.ToString() ?? "(not reported)"} to {volume.UpperLimit}");
    Console.WriteLine($"  upper as FL     : {volume.UpperLimit?.AsFlightLevel()?.ToString() ?? "n/a (not an STD flight level)"}");
    Console.WriteLine($"  intensity change: {volume.IntensityChange}");
    Console.WriteLine($"  motion          : {volume.DirectionOfMotionDegrees} deg at {volume.SpeedOfMotion}");
    Console.WriteLine($"  time context    : {volume.TimeIndicator} at {volume.PhenomenonTimeUtc:O}");
    // The area is a closed union: GeoPolygon or GeoCircle ("WI 250NM OF TC CENTRE").
    string area = volume.Area?.Match(
        polygon => $"polygon, {polygon.ExteriorRing.Count} vertices",
        circle => $"circle, {circle.Radius.Amount} {circle.Radius.UnitOfMeasure} around {circle.Centre}") ?? "(none)";
    Console.WriteLine($"  area            : {area}");
}

foreach (EnRoutePhenomenonVolume forecast in sigmet.ForecastPositions)
{
    int vertices = forecast.Area is GeoPolygon polygon ? polygon.ExteriorRing.Count : 0;
    Console.WriteLine($"Forecast volume {forecast.Id}: {vertices} vertices at {forecast.PhenomenonTimeUtc:O}");
}
```
```output
SIGMET variant  : VolcanicAshSigmet
ATS unit / MWO  : EGGX (SHANWICK OCEANIC AREA CONTROL CENTRE) / MWO EGRR
...
Phenomenon      : VolcanicAsh (VA)
...
```

An AIRMET cancellation is distinguishable and never describes an active hazard; the AIRMET-specific conditions (cloud base and top, surface visibility, surface wind) sit next to the shared volume and are `null` when the message does not carry them:

```csharp
using System;
using Iwxxm.Net;
using Iwxxm.Net.Reports;

Airmet airmet = IwxxmParser.ParseFileAs<Airmet>("samples/airmet-A6-1a-TS.xml").Value;

Console.WriteLine($"AIRMET #{airmet.SequenceNumber} for {airmet.FirDesignator}, {airmet.Phenomenon?.Code} ({airmet.Phenomenon?.Notation})");
Console.WriteLine($"  valid {airmet.ValidFromUtc:O} to {airmet.ValidUntilUtc:O}");

if (airmet.IsCancelReport)
{
    Console.WriteLine($"  cancels AIRMET {airmet.CancelledReportSequenceNumber} ({airmet.CancelledReportValidFromUtc:O} - {airmet.CancelledReportValidUntilUtc:O})");
}

foreach (AirmetEvolvingCondition condition in airmet.EvolvingConditions)
{
    EnRoutePhenomenonVolume volume = condition.Volume;
    Console.WriteLine($"  {volume.TimeIndicator}: {volume.LowerLimit?.ToString() ?? "no lower limit"} to {volume.UpperLimit}");
    Console.WriteLine($"    cloud base/top : {condition.CloudBase?.ToString() ?? "-"} / {condition.CloudTop?.ToString() ?? "-"}");
    Console.WriteLine($"    surface vis    : {condition.SurfaceVisibility?.ToString() ?? "-"}");
    Console.WriteLine($"    surface wind   : {condition.SurfaceWindDirectionDegrees?.ToString() ?? "-"} deg / {condition.SurfaceWindSpeed?.ToString() ?? "-"}");
}
```
```output
...
```

### WAFS significant weather (SIGWX)

`IwxxmParser.ParseFileAs<Sigwx>` is the entry point: the general parser reads the official `iwxxm:WAFSSignificantWeatherForecast`, the compact `iwxxm:SIGWXReport` and bulletins that carry them (`ParseBulletin`). `IwxxmSigwxParser.ParseFile` gives the same model through an optional narrow facade: it returns `Result<Sigwx>` without the `ParseAs` cast and refuses any other root element before mapping (`XSD-UNEXPECTED-ROOT-ELEMENT`), bulletins included. The guide has worked examples for [jet streams](GUIDE.md#10-worked-example-jet-streams-sigwx) and [clouds](GUIDE.md#11-worked-example-clouds-sigwx). The nine phenomenon families:

```csharp
using System;
using Iwxxm.Net;
using Iwxxm.Net.Reports;

Result<Sigwx> result = IwxxmParser.ParseFileAs<Sigwx>("samples/WAFS-Example.xml");

if (result.IsFailure)
{
    return;
}

Sigwx sigwx = result.Value;

Console.WriteLine($"WAFC issuing centre : {sigwx.IssuingCentre}");
Console.WriteLine($"Validity period     : {sigwx.ValidFromUtc:O} -> {sigwx.ValidUntilUtc:O}");
Console.WriteLine($"Vertical envelope   : {sigwx.LowerLimit} -> {sigwx.UpperLimit}");

// 1. Jet streams (axis trajectory plus wind symbol core points)
foreach (JetStream js in sigwx.JetStreams)
{
    Console.WriteLine($"JetStream: max {js.MaxWindSpeed} ({js.MaxWindSpeed.ToKnots():F1} kt) at {js.CoreLevel} ({js.AxisTrajectory.Count} axis points, {js.CorePoints.Count} core symbols)");
}

// 2. Clouds (WMO BUFR 0-20-008 distribution and 0-20-012 cloud type)
foreach (Cloud cloud in sigwx.Clouds)
{
    Console.WriteLine($"Cloud: {cloud.Distribution?.Code} {cloud.CloudType?.Code} ({cloud.Base?.ToString() ?? "no base"} to {cloud.Top})");
}

// 3. Turbulence (BUFR 0-11-030)
foreach (Turbulence turb in sigwx.Turbulences)
{
    Console.WriteLine($"Turbulence: {turb.Degree?.Code} ({turb.Base} to {turb.Top})"); // "10363.2 M MSL to 12192 M MSL", never converted to FL340-FL400
}

// 4. Airframe icing (BUFR 0-20-041)
foreach (AirframeIcing icing in sigwx.AirframeIcings)
{
    Console.WriteLine($"Icing: {icing.Degree?.Code} ({icing.Base} to {icing.Top})");
}

// 5. Tropopause: a contour is one polygon with ONE level; spot heights are isolated points with their own level
foreach (Tropopause tropo in sigwx.Tropopauses)
{
    Console.WriteLine(tropo.ContourPolygon != null
        ? $"Tropopause contour at {tropo.ContourLevel}"
        : $"Tropopause: {tropo.SpotHeights.Count} spot heights");
}

// 6. Point phenomena: volcanoes, tropical cyclones, sandstorms, radiation incidents
foreach (Volcano v in sigwx.Volcanoes)
{
    Console.WriteLine($"Volcano {v.Name} at {v.Position}");
}
foreach (TropicalCyclone tc in sigwx.TropicalCyclones)
{
    Console.WriteLine($"Tropical cyclone {tc.Name} at {tc.Position}");
}
Console.WriteLine($"Sandstorms: {sigwx.Sandstorms.Count}, radiation incidents: {sigwx.RadiationIncidents.Count}");
```
```output
WAFC issuing centre : London
...
JetStream: max 61.8 m/s (120.1 kt) at 15240 M MSL (11 axis points, 5 core symbols)
...
```

### QVACI: ash contours with holes

IWXXM 2025-2 introduced `QuantitativeVolcanicAshConcentrationInformation`; concentration contours can contain hollow interior rings (`gml:interior`):

```csharp
using System;
using Iwxxm.Net;
using Iwxxm.Net.Reports;

Qvaci qvaci = IwxxmParser.ParseFileAs<Qvaci>("samples/qvaci-Example.xml").Value;
Console.WriteLine($"Originating centre: {qvaci.OriginatingCentreName} (model initialisation: {qvaci.DispersionModelInitialisationTimeUtc:O})");

foreach (VolcanicAshConcentrationCloud cloud in qvaci.AshConcentrationClouds)
{
    Console.WriteLine($"Ash contour {cloud.Id}:");
    Console.WriteLine($"  density >=       : {cloud.AshDensityEqualsOrExceedsMgPerM3} mg/m3");
    Console.WriteLine($"  vertical layer   : {cloud.LowerLimit} to {cloud.UpperLimit}");
    Console.WriteLine($"  exterior vertices: {cloud.Area.ExteriorRing.Count}");
    Console.WriteLine($"  interior holes   : {cloud.Area.InteriorRings.Count}");
}
```
```output
...
```

### VAA, TCA and Space Weather advisories

```csharp
using System;
using Iwxxm.Net;
using Iwxxm.Net.Reports;

Vaa vaa = IwxxmParser.ParseFileAs<Vaa>("samples/va-advisory-A7-2.xml").Value;
Console.WriteLine($"VAAC {vaa.IssuingVaacName} #{vaa.AdvisoryNumber}: {vaa.ObservedAshClouds.Count} observed clouds at {vaa.Observation?.PhenomenonTimeUtc:O}");
foreach (VolcanicAshConditions forecast in vaa.Forecasts) // one entry per +6/+12/+18 HR offset
{
    Console.WriteLine($"  FCST {forecast.PhenomenonTimeUtc:O}: {forecast.Status} ({forecast.AshClouds.Count} clouds)");
}

Tca tca = IwxxmParser.ParseFileAs<Tca>("samples/tc-advisory-A2-2.xml").Value;
Console.WriteLine($"TCAC {tca.IssuingTcacDesignator} cyclone {tca.CycloneName}: observed pressure {tca.Observation?.CentralPressureHpa} hPa, " +
                  $"{tca.Forecasts.Count} forecast track points, {tca.CumulonimbusCloudLocations.Count} CB extents");

SpaceWx swx = IwxxmParser.ParseFileAs<SpaceWx>("samples/spacewx-A7-3.xml").Value;
Console.WriteLine($"SWXC {swx.IssuingSwxcName} #{swx.AdvisoryNumber}: effects [{string.Join(", ", swx.Effects)}]");
foreach (SpaceWeatherAnalysis analysis in swx.Analyses) // the observation and each forecast offset
{
    Console.WriteLine(analysis.IsNothingOfOperationalSignificance
        ? $"  {analysis.TimeIndicator} {analysis.PhenomenonTimeUtc:O}: NO SWX EXP"
        : $"  {analysis.TimeIndicator} {analysis.PhenomenonTimeUtc:O}: {analysis.IntensityAndRegions.Count} intensity/region groups");
    foreach (SpaceWeatherIntensityAndRegion group in analysis.IntensityAndRegions)
        foreach (SpaceWeatherRegion region in group.Regions) // each region pairs its own indicators with its own extent
            Console.WriteLine($"    {group.Intensity}: [{string.Join(", ", region.LocationIndicators)}] {region.Areas.Count} area(s) {region.LowerLimit}");
}
```
```output
VAAC TOKYO #2024/4: 2 observed clouds at 2024-09-23T01:00:00.0000000+00:00
...
```

### Bulletins

Partial-result behaviour, `ReportResults` and `ItemErrors` are explained with a runnable example in the [guide](GUIDE.md#bulletins-many-reports-one-envelope). A bulletin succeeds as long as one report maps; each skipped report is also a warning in `Result.Warnings`. The same applies per feature to WAFS charts (`Sigwx.FeatureErrors`). Schema and rule validation remain document-level.

### Large-scale sample: synthetic WAFS SIGWX

`tests/Iwxxm.Net.Tests/Samples/external/metoffice/example-iwxxm-2025-2-sigwx-non-operational.xml` holds a synthetic, anonymized 14.1 MB `collect:MeteorologicalBulletin` (`NON-OPERATIONAL`, `TEST`) with 15 `iwxxm:WAFSSignificantWeatherForecast` reports and 4252 features. It is below the default 50 MiB size limit, so the default parser processes it (and emits one aggregated `ACL-MEASURE-UOM-NON-UCUM-ALIAS` warning for the legacy `kn_i` unit on wind symbols):

```csharp
using Iwxxm.Net;
using Iwxxm.Net.Reports;

string path = "samples/example-iwxxm-2025-2-sigwx-non-operational.xml";

Result<MeteorologicalBulletin> result = IwxxmParser.ParseBulletinFile(path);
Console.WriteLine($"success: {result.IsSuccess}, warnings: {result.Warnings.Count}");
```

```output
success: True, warnings: 1
```

What the file shows (official XSD, built-in Schematron subset, `Iwxxm.Net.Validation.Saxon`):

- **The XSD and the official Schematron (165 rules) both pass** and the bulletin maps all 15 reports with no item errors: 574 jet streams (1850 core points), 733 clouds, 585 turbulence areas, 1903 airframe icing areas, 301 tropopause contours, 135 volcanoes and 21 tropical cyclones. `RADIATION` is listed in `phenomenaList` but the bulletin contains no radiation feature.
- **Each chart is an instant** (`gml:TimePeriod` with `beginPosition == endPosition`, equal to `phenomenonTime`). The schema allows it and so does the SIGWX domain (`ValidFromUtc == ValidUntilUtc`); only an end before the start is `DOMAIN-SIGWX-VALID-PERIOD`. Forecast groups of aerodrome reports accept an instant too; the validity period of a SIGMET, AIRMET or TAF must still end after it starts.
- **The wind speed unit is `kn_i`, not the UCUM `[kn_i]`.** Neither the XSD nor the official Schematron checks it. The unit readers accept `kn_i` as a recognised legacy alias of `[kn_i]` (knots) and always report it: the whole bulletin gets one `ACL-MEASURE-UOM-NON-UCUM-ALIAS` warning in `Result.Warnings` that names the field (`windSpeed`), the alias, the UCUM code and the count (1850). All 574 jet streams map.
- **GeoJSON**: 6,102 features (4252 phenomena and 1850 jet stream core points), valid JSON; positions are `[longitude, latitude]`, rings are closed and counter-clockwise, and the antimeridian crossings of the source are split at 180 without losing a vertex (the three south polar tropopause contours whose ring has a lobe across 180 keep all their vertices). Each tropopause contour is one polygon that carries its level; the `bbox` of this global collection is `[-180, south, 180, north]`.
- **Cost** (container with 2 cores, one process, indicative; not a benchmark): parsing and mapping about 0.5 s, the XSD about 0.4 s and the built-in Schematron about 0.3 s on top, the GeoJSON export about 0.3 s; about 1.5 s for the first complete run and 0.7 to 1.0 s once warm, about 155 MiB above the process baseline at peak. The official Schematron through `Iwxxm.Net.Validation.Saxon` needs about 4.5 s for the first run and 1.7 s warm (about 1.7 GB allocated). The deepest element nesting is 16 levels, far below the default `MaxElementDepth` of 128 (AUD-N1). A synthetic 49.7 MiB bulletin built from the same content (the largest document the default accepts) takes about 3.8 s and peaks at about 545 MiB of working set; see [SECURITY.md](../SECURITY.md#memory-cost-of-the-default-size-limit).

---

## GeoJSON export in detail

`IwxxmParser.ParseFileToGeoJson` and `IwxxmGeoJsonExporter.ToGeoJson` convert any `IwxxmDocument` into an RFC 7946 `FeatureCollection`. Both return a `Result<string>`: an exporter failure is a `GEOJSON-EXPORT-ERROR` value (located at the document id), like every other failure; only a `null` document passed to `IwxxmGeoJsonExporter` throws (`ArgumentNullException`). The `*ToGeoJson` members of the parser reuse the exporter facade, so there is one definition of an export failure:

- GML `[latitude, longitude]` is swapped to RFC 7946 section 3.1.1 `[longitude, latitude]`.
- Polygon interior holes (`GeoPolygon.InteriorRings`) are written as additional linear rings.
- The global `bbox` is `[west, south, east, north]`. When a geometry crosses the antimeridian the longitude extent is the smallest arc that contains every position, so `west` is greater than `east` (section 5.2), but only when that arc is at most 180 degrees wide. Features spread over the whole longitude range (a global chart, a polar cap, features on both sides of the antimeridian with no narrower wrapping arc) have no meaningful smaller box and a wrapping box that spans almost 360 degrees looks empty to a client that expects `west <= east`, so the plain extent is written (`-180` and `180` when the content reaches both sides). The latitudes are always the plain minimum and maximum of the positions: a pole (`-90` or `90`) appears only when a position reaches it.
- Exterior rings are counter-clockwise and holes clockwise (right-hand rule); geometries that cross +-180 degrees are split into `MultiLineString` or `MultiPolygon` (section 3.1.9); rings around a pole become polar caps (the ring is repeated every 360 degrees, clipped to [-180, 180] and closed along the antimeridian and the pole, so a ring with a lobe that leaves the strip and returns keeps every vertex and may give several polygons; a ring that crosses the antimeridian more than twice is clipped the same way, giving separate pieces instead of pieces joined by a zero-width bridge): interior rings entirely inside a cap are kept as its holes, and an interior ring that cannot be represented (it crosses the antimeridian or is not inside the cap) is left out and counted in the feature property `droppedInteriorRings` (omitted when 0), never dropped silently. For a polygon that does not enclose a pole and crosses the antimeridian, an interior ring that reaches +-180 (it crosses it, or has an edge on it) is cut out of the pieces of the exterior: each piece gets a notch instead of a hole that shares an edge with its shell, so the area is the exterior minus the interior ring and every ring stays valid for GEOS and PostGIS; an interior ring entirely inside one piece stays a hole of that piece (the piece is chosen from several of its vertices that are not on +-180, not from its first vertex); one that fits no piece, or that is not inside the exterior (entirely outside it, with a vertex outside it, or crossing its edge) is left out and counted once in `droppedInteriorRings`, even if it crosses several strips. The containment test looks at the vertices of the interior ring, not at its edges: a ring whose vertices are all inside a concave exterior but whose edges leave it is accepted and can still produce a world-sized ring, and the cost of the test is per interior ring (bounded by 4,000,000 edge tests each), not per document, so an untrusted document with a very large number of interior rings crossing a strip border can be slow (both need invalid or hostile GML). A line vertex exactly on +-180 is the cut position itself: it is not repeated, and a line that only starts or ends on the antimeridian stays a single `LineString` (on the side it lies on), never a `MultiLineString` with a one-position part. A surface with several `gml:PolygonPatch` is a `MultiPolygon` with the property `patchCount` (space weather regions); the other report types hold one surface and report `ACL-GML-MULTI-PATCH-UNSUPPORTED` for several patches. The domain model keeps the source order.
- Circles (`GeoCircle`) are written as a 64-segment polygon (geodesic points on the IUGG mean sphere) plus the properties `center` (`[lon, lat]`), `radius`, `radiusUom` and `circleSegments`, from which a consumer can rebuild the exact circle.
- The IAVCEI volcano number (`Volcano.IavceiNumber`) is written as the property `iavceiNumber` on volcano features (VONA, QVACI, WAFS and compact SIGWX) when the source gives a number; the property is omitted when the value is null: always for SIGMET volcanoes (their `metce:Volcano` never carries it) and for any other volcano whose number is absent or nil. Earlier previews wrote it as `wmoNumber`.
- VONA and QVACI `VOLCANO` features also carry the volcano `activityStatus` (the enum name, e.g. `DecreasedActivity`) and `sourceElevation` with its `sourceElevationUom` / `sourceElevationReference` companions, when the source reports them; `Volcano.ActivityStatus` and `Volcano.SourceElevation` are filled for WAFS SIGWX, VONA and QVACI volcanoes (never for SIGMET and VAA, whose `metce:Volcano` has only a name and a position).
- QVACI features: the `ASH_CONCENTRATION_CLOUD` feature carries `ashDensityEqualsOrExceedsMgPerM3` (the official `ashDensityEqualsOrExceeds`, a lower bound: the density within the cloud equals or exceeds it, higher values are possible) and the `VOLCANO` feature carries `originatingCentreName` (`aixm:name` of the originating centre, or its designator when it has no name). These were `ashDensityMgPerM3` and `originatingCentre` in earlier previews.
- `NaN` and `Infinity` are never written; a non-finite property is written as `null`.
- A `Tca` gets a synthesized `TC_TRACK` `LineString` joining the observed and forecast cyclone positions.
- Measures are emitted **as reported**: a value plus a sibling `...Uom` property with the UCUM unit (`"windSpeed": 25, "windSpeedUom": "[kn_i]"`, `"maxWindSpeed"` / `"maxWindSpeedUom"`, `"speedOfMotion"` / `"speedOfMotionUom"`, `"sourceElevation"` / `"sourceElevationUom"`). WMO BUFR codes are written both as the enum name and as the official number (`distributionCode` and `distributionNumericCode`, `typeCode` and `typeNumericCode`, `degree` and `degreeNumericCode`).

```csharp
using System;
using Iwxxm.Net;
using Iwxxm.Net.Reports;

// Option A: one step, XML to validated domain object to GeoJSON.
Result<string> direct = IwxxmParser.ParseFileToGeoJson("samples/qvaci-Example.xml", new GeoJsonExportOptions(indented: true, includeBoundingBox: true));
Console.WriteLine(direct.IsSuccess ? $"qvaci: {direct.Value.Length} characters" : direct.Errors[0].ToString());

// Option B: export a document you already parsed.
Result<IwxxmDocument> parsed = IwxxmParser.ParseFile("samples/WAFS-Example.xml");
if (parsed.IsSuccess)
{
    Result<string> pretty = IwxxmGeoJsonExporter.ToGeoJson(parsed.Value, GeoJsonExportOptions.Pretty);
    if (pretty.IsFailure)
    {
        Console.WriteLine(pretty.Errors[0]);
        return;
    }

    string[] lines = pretty.Value.Split('\n');
    for (int i = 0; i < 31; i++)
    {
        string line = lines[i].TrimEnd('\r');
        Console.WriteLine(line.Length > 80 ? line.Substring(0, 80) + " ..." : line);
    }
}
```
```output
qvaci: 7080 characters
{
  "type": "FeatureCollection",
  "bbox": [-158.734129, -48.19933, 15, 55.3],
  "metadata": {
    "id": "uuid.ce3b61b9-d360-4da1-83b9-67e668406869",
    "reportType": "SignificantWeather",
    "issueTimeUtc": "2001-01-30T21:00:00.0000000+00:00",
    "reportStatus": "NORMAL",
    "permissibleUsage": "OPERATIONAL"
  },
  "features": [
    {
      "type": "Feature",
      "id": "uuid.8c3d3467-e398-4627-ab23-f9e57c2215b5",
      "geometry": {
        "type": "LineString",
        "coordinates": [[-104.7, 35.4], [-101.6, 36.1], [-99.6, 36.6], [-97.7, 3 ...
      },
      "properties": {
        "featureType": "JETSTREAM",
        "reportId": "uuid.ce3b61b9-d360-4da1-83b9-67e668406869",
        "reportType": "SignificantWeather",
        "issueTimeUtc": "2001-01-30T21:00:00.0000000+00:00",
        "coreLevel": 15240,
        "coreLevelUom": "M",
        "coreLevelReference": "MSL",
        "maxWindSpeed": 61.8,
        "maxWindSpeedUom": "m/s"
      }
    },
```

---

## GeoPackage tables and columns

The optional package `Iwxxm.Net.Export.GeoPackage` writes an OGC GeoPackage (see [section 8B of the guide](GUIDE.md#8b-geopackage-export) for the API, the options and the notes for .NET Framework). This section is the layout of the file. Every table is in WGS 84 (EPSG:4326, x = longitude, y = latitude), and rows are written in the order of the features of the GeoJSON export, so `fid` follows that order (1, 2, 3, ...) within a table.

### File header and mandatory tables

| Item | Value |
| :--- | :--- |
| `application_id` | `0x47504B47` (`GPKG`) |
| `user_version` | `10400` for `GeoPackageVersion.Version14` (default), `10300` for `Version13` |
| `gpkg_spatial_ref_sys` | Three rows: `-1` (undefined Cartesian), `0` (undefined geographic) and `4326` (WGS 84, `EPSG`, the WKT 1 definition) |
| `gpkg_contents` | One row per data table. `identifier` is the table name. Features tables: `data_type` `features`, `srs_id` 4326, `min_x`, `min_y`, `max_x`, `max_y` the plain minimum and maximum of the stored coordinates. Attributes tables: `data_type` `attributes`, `srs_id` 0, extent `NULL`. `last_change` is `yyyy-MM-ddTHH:mm:ss.fffZ` in UTC: the `LastChangeUtc` option, else the issue time of the document, else `1970-01-01T00:00:00.000Z`. |
| `gpkg_geometry_columns` | One row per features table: column `geom`, the geometry type name of the table, `srs_id` 4326, `z` 0, `m` 0 |

Not written: `gpkg_extensions`, `gpkg_metadata` and its reference table, `gpkg_ogr_contents`, triggers, R-tree index tables, tile and raster tables.

### Data tables

| Table | `gpkg_contents.data_type` | Geometry column | Rows |
| :--- | :--- | :--- | :--- |
| `iwxxm_points` | `features` | `geom POINT NOT NULL` | Features with a point geometry. Created only when it has rows. |
| `iwxxm_lines` | `features` | `geom MULTILINESTRING NOT NULL` | Features with a line geometry (a `LineString` is stored as a `MultiLineString` of one member). Created only when it has rows. |
| `iwxxm_polygons` | `features` | `geom MULTIPOLYGON NOT NULL` | Features with a polygon geometry (a `Polygon` is stored as a `MultiPolygon` of one member; circles are the densified polygons of the GeoJSON export). Created only when it has rows. |
| `iwxxm_attributes` | `attributes` | none | Features that have no geometry. Always created, possibly empty. |
| `iwxxm_reports` | `attributes` | none | One row per report: a standalone report gives one row, a bulletin one per contained report. |
| `iwxxm_metadata` | `attributes` | none | Key/value rows of the file. |

### Columns of the feature and attribute tables

`iwxxm_points`, `iwxxm_lines`, `iwxxm_polygons` and `iwxxm_attributes` share these columns (`iwxxm_attributes` has no `geom`):

| Column | Type | Content |
| :--- | :--- | :--- |
| `fid` | `INTEGER PRIMARY KEY NOT NULL` | Row id, in feature order. |
| `geom` | `POINT`, `MULTILINESTRING` or `MULTIPOLYGON`, `NOT NULL` | `StandardGeoPackageBinary`: header `GP`, version 0, flags `0x01` for a point (little-endian, no envelope) or `0x03` for lines and polygons (little-endian, envelope `[minx, maxx, miny, maxy]`), SRS id 4326, then little-endian WKB. 2D only. |
| `feature_id` | `TEXT` | The GeoJSON feature `id`; `NULL` when the feature has none. |
| `feature_type` | `TEXT` | The `featureType` property (for example `METAR_OBSERVATION`, `SIGMET_EVOLVING_VOLUME`, `JETSTREAM`). |
| `report_id` | `TEXT` | The `reportId` property: the id of the report the feature belongs to. |
| `report_type` | `TEXT` | The `reportType` property: the name of `IwxxmReportType` (for example `Metar`, `TropicalCycloneSigmet`). |
| `bulletin_identifier` | `TEXT` | The bulletin identifier for a feature of a report inside a bulletin; `NULL` otherwise. |
| `issue_time_utc` | `DATETIME` | The `issueTimeUtc` property as `yyyy-MM-ddTHH:mm:ss.fffZ`; `NULL` when absent. |
| `properties` | `TEXT NOT NULL` | The GeoJSON `properties` object as compact JSON, the same text and key order as the GeoJSON export. Properties whose value is null are omitted, and a non-finite number is written as `null`, as there. |

The columns `feature_type`, `report_id`, `report_type`, `bulletin_identifier` and `issue_time_utc` are read from the same properties object that is written to `properties`, so the column and the JSON cannot disagree. A feature that lacks one of those properties leaves its column `NULL`.

### `iwxxm_reports`

| Column | Type | Content |
| :--- | :--- | :--- |
| `fid` | `INTEGER PRIMARY KEY NOT NULL` | Row id, in report order. |
| `report_id` | `TEXT NOT NULL` | The report id. |
| `report_type` | `TEXT NOT NULL` | The `IwxxmReportType` name. |
| `report_status` | `TEXT NOT NULL` | The report status code (for example `NORMAL`). |
| `permissible_usage` | `TEXT NOT NULL` | The permissible usage code (for example `OPERATIONAL`). |
| `issue_time_utc` | `DATETIME NOT NULL` | The issue time, `yyyy-MM-ddTHH:mm:ss.fffZ`. |
| `bulletin_identifier` | `TEXT` | The bulletin identifier, for a report inside a bulletin; `NULL` otherwise. |

### `iwxxm_metadata`

| Column | Type | Content |
| :--- | :--- | :--- |
| `fid` | `INTEGER PRIMARY KEY NOT NULL` | Row id. |
| `key` | `TEXT NOT NULL UNIQUE` | One of the keys below. |
| `value` | `TEXT NOT NULL` | The value. |

| Key | Value |
| :--- | :--- |
| `geopackage_version` | `1.3` or `1.4` |
| `library_version` | The version of `Iwxxm.Net.Export.GeoPackage` that wrote the file |
| `source_document_id` | The id of the exported document |
| `source_document_type` | The `IwxxmReportType` name of the document (`MeteorologicalBulletin` for a bulletin) |
| `bulletin_identifier` | Only for a bulletin |
| `report_count` | The number of rows of `iwxxm_reports` |

The rows are written in the order of the table above: five rows, or six for a bulletin.

### Compatibility checks

Files written by the package were opened with GDAL (`ogrinfo -al`) and checked with GDAL's GeoPackage validator (`validate_gpkg.py --extra --warning-as-error`), with `PRAGMA integrity_check` and `foreign_key_check`. The repository keeps those checks as `tools/validate-geopackage-file.sh` (every sample, through a pinned GDAL container image) and `tools/validate-geopackage-geometry.sh` (the geometry blobs against GDAL's reading of them); they are not part of the unit tests and need podman.

---

## Composition root and dependency injection

There are two ways to get a facade; their constructors are `internal`. For the common case use the default instances (`IwxxmParser.Default`, `IwxxmGeoJsonExporter.Default` and the optional `IwxxmSigwxParser.Default`) or the static `Parse*` / `ToGeoJson` helpers, which are thin wrappers over them. When you need options or a different Schematron engine, `IwxxmParserBuilder` is the composition root: it selects the options and the business-rule engine, connects the built-in internal adapters and returns immutable, thread-safe facades. The Schematron engine can be replaced through the public `ISchematronValidator` in `Iwxxm.Net.Extensibility`, or switched off; the other adapters (schema validator, mappers, GeoJSON exporter) are internal.

A custom Schematron engine is small. This one implements the Schematron port with a rule of its own: it works on the raw bytes and reports a *warning* (the document stays valid) when a message is unusually large:

```csharp
using System;
using System.Collections.Generic;
using Iwxxm.Net;
using Iwxxm.Net.Extensibility;

public sealed class MyRuleEngine : ISchematronValidator
{
    public IReadOnlyList<ValidationError> Validate(IwxxmDocumentContext document)
    {
        // document.OpenRead() gives the raw bytes if you need them; the facade has already parsed them safely once.
        if (document.Length <= 5_000)
        {
            return Array.Empty<ValidationError>();
        }

        return new[]
        {
            new ValidationError(
                "MY-RULE-001",
                $"Unusually large message ({document.Length} bytes).",
                ErrorCategory.BusinessRuleViolation,
                ValidationSeverity.Warning),
        };
    }
}
```

Wiring it, and registering the result with Microsoft's container:

```csharp
using System;
using Iwxxm.Net;
using Iwxxm.Net.Extensibility;
using Iwxxm.Net.Reports;
using Microsoft.Extensions.DependencyInjection;

var builder = new IwxxmParserBuilder()
    .WithOptions(IwxxmParserOptions.Default)
    .UseSchematronValidator(new MyRuleEngine());   // an ISchematronValidator adapter
    // .WithoutSchematron()                        // or: XSD validation only
    // .UseOfficialSchematron()                    // or: all 165 official rules (Iwxxm.Net.Validation.Saxon)

var services = new ServiceCollection();
services.AddSingleton(builder.Build());                // IwxxmParser (convenience facade)
services.AddSingleton(builder.BuildSigwxParser());     // IwxxmSigwxParser
services.AddSingleton(builder.BuildGeoJsonExporter()); // IwxxmGeoJsonExporter


using ServiceProvider provider = services.BuildServiceProvider();
IwxxmParser parser = provider.GetRequiredService<IwxxmParser>();
Result<IwxxmDocument> result = parser.Process(System.IO.File.OpenRead("samples/metar-A3-1.xml"));
Console.WriteLine($"custom rule engine in the pipeline: success = {result.IsSuccess}, warnings = {result.Warnings.Count}");
foreach (ValidationError warning in result.Warnings)
{
    Console.WriteLine($"  {warning.Severity} {warning.Code}: {warning.Message}");
}
```
```output
custom rule engine in the pipeline: success = True, warnings = 1
  Warning MY-RULE-001: Unusually large message (6854 bytes).
```

| Port | Kind | Visibility | Default adapter |
| :--- | :--- | :--- | :--- |
| `ISchematronValidator` | outbound | public, `Iwxxm.Net.Extensibility` | built-in XSLT 1.0 rule subset (or `UseOfficialSchematron()`) |
| `ISchemaValidator` | outbound | internal | embedded official WMO XSDs |
| `IIwxxmDocumentMapper` | outbound | internal | built-in anti-corruption-layer mapper |
| `ISigwxDocumentMapper` | outbound | internal | built-in SIGWX mapper |
| `IGeoJsonExporter` | outbound | internal | built-in RFC 7946 writer |

Only the Schematron port is an extension point because it is the only one with more than one adapter (the built-in subset and the Saxon package). The other ports have a single implementation and stay internal; making a type public later is a compatible change, closing it is not.

Outbound ports receive an **`IwxxmDocumentContext`**: the facade has already bounded the size, converted text input to UTF-8 and parsed the document once with the hardened reader settings (DTD prohibited, no resolver, character quotas), so a malformed or DTD-bearing document never reaches an adapter (`XML-MALFORMED`). `OpenRead()` returns a read-only stream of the document bytes for adapters that need raw XML; the built-in adapters share the parsed tree. You can call an adapter directly with `new IwxxmDocumentContext(bytes, options)`.

Validators return `IReadOnlyList<ValidationError>`: findings with `ValidationSeverity.Error` stop the pipeline; `Warning` findings come back as `Result<T>.Warnings` of the successful result (as the example above shows). Each validator reports at most `IwxxmParserOptions.MaxValidationErrors` findings (then a `...-TOO-MANY-ERRORS` sentinel), and Schematron reports are bounded by `MaxRuleReportBytes` (`SCHEMATRON-REPORT-SIZE-LIMIT`).

---

## Official WMO Schematron

The official `iwxxm.sch` declares `queryBinding="xslt2"`, which the .NET base library cannot execute. The optional package `Iwxxm.Net.Validation.Saxon` runs it with SaxonCS-HE (Saxonica's Home Edition for .NET):

- The **byte-identical official `iwxxm.sch`** (165 rule patterns, no rewriting) is compiled once per process to XSLT with the vendored SchXslt 1.10.1 compiler (MIT) and cached as an immutable, thread-safe Saxon executable. The first compilation takes roughly a second.
- It is **air-gapped**: the `document('codes.wmo.int-*.rdf')` code-list lookups resolve to the official WMO RDF files embedded in `Iwxxm.Net`; every other URI (network, file system) is refused.
- SVRL `failed-assert` and `successful-report` findings become `ValidationError`s with `ErrorCategory.BusinessRuleViolation`, the **official rule (pattern) id** as `Code` (for example `METAR_SPECI.MeteorologicalAerodromeObservation-5`), the official message, an absolute XPath as `Location` (see [Locations of findings](#locations-of-findings)), and a `Severity` taken from `@role` / `@flag` (the official rules declare none, so every failed assertion is an `Error`; a fired `successful-report` without an error role is a `Warning`). The SVRL report is streamed from a size-bounded buffer and findings are capped.
- All 29 official WMO examples pass. The official rules are not exhaustive: for example, an ordinary TAF whose `iwxxm:baseForecast` is entirely absent is not reported by `TAF.TAF-5` (its test compares an empty attribute sequence).
- It needs a `net10.0` application. No Saxonica licence key is needed: the package only requests Saxon-HE capabilities (`new Processor(false)`), which the package tests verify in CI.

```csharp
using System;
using Iwxxm.Net;
using Iwxxm.Net.Reports;
using Iwxxm.Net.Validation.Saxon;

TimeSpan compile = OfficialSchematronValidator.Warmup();   // optional: compile eagerly at start-up
Console.WriteLine($"rules compiled: {compile.TotalMilliseconds >= 0}");

IwxxmParser parser = new IwxxmParserBuilder()
    .UseOfficialSchematron()   // extension method from Iwxxm.Net.Validation.Saxon
    .Build();

Result<IwxxmDocument> result = parser.ProcessFile("samples/metar-A3-1.xml");
Console.WriteLine($"official rules accept the sample METAR: {result.IsSuccess}");
```
```output
rules compiled: True
official rules accept the sample METAR: True
```

---

## Error codes

`ValidationError.Code` strings are the stable contract; messages may be reworded; `ErrorCategory` names stay technology-agnostic. Only the first findings of a validator are reported (`MaxValidationErrors`).

**What is stable.** Every code listed below, written exactly as shown, is part of the contract: it is not renamed or removed without a CHANGELOG entry. Three kinds of code are *families* rather than a closed list:

- **Official Schematron rule ids** (`Code` of a `BusinessRuleViolation`, for example `METAR_SPECI.MeteorologicalAerodromeObservation-5`): defined by the WMO rules, one per failed assertion.
- **`GML-*` coordinate codes**: raised by the GML position readers; the codes listed in [Geometry](#geometry) are the current members, and new `GML-*` members may appear when a new malformed-coordinate shape is rejected.
- **`ACL-*` and `DOMAIN-*` codes**: new mandatory-content or invariant checks add new members; the `ACL-` / `DOMAIN-` prefix and the `DomainMapping` category are stable.

`tests/Iwxxm.Net.Tests/ErrorCodeDocumentationTests.cs` fails when a code literal emitted by `src/` is missing from the tables below, or when a table row names a code that no longer exists, so this list cannot drift from the code. Where the same condition can be reached from two entry points (the SIGWX and the general parser), both report the same code (for example `STREAM-EMPTY`).

### Error categories

`ValidationError.Category` groups the codes by the stage where the problem arose. The numeric values are stable: `Io` was appended (5), the others keep their numbers.

| Category | Value | Meaning | Usual reaction |
| :--- | :---: | :--- | :--- |
| `MalformedInput` | 0 | The input was read but is empty, over a size or depth limit, or not well-formed XML. | Reject the message: the sender's content is unusable. |
| `SchemaViolation` | 1 | Well-formed XML that breaks the official schema (also a failing schema validator adapter). | Reject the message. |
| `BusinessRuleViolation` | 2 | A Schematron or code-list rule failed (also a failing rule validator adapter). | Reject or quarantine. |
| `Deserialization` | 3 | A valid document of a shape the requested entry point or mapper does not handle. | Routing error: use another entry point. |
| `DomainMapping` | 4 | Valid XML whose content cannot become a safe domain object, or a pipeline or export adapter failed. | Log and investigate. |
| `Io` | 5 | The input could not be read at all: missing or inaccessible file, unreadable or failing stream. The message was never seen, so the cause is the environment, not the content. | Fix the path or permissions, or retry. |

`Io` is deliberately narrow. An empty stream (`STREAM-EMPTY`), an empty string (`XML-EMPTY-STRING`) and a size limit are `MalformedInput`: the read succeeded and the *content* is what is wrong.

### Constants for the codes you branch on

`IwxxmErrorCodes` (namespace `Iwxxm.Net`) holds the codes a caller realistically branches on as `public const string`, so a comparison cannot drift from the code and the constants work in `switch` statements:

```csharp
using System;
using System.Linq;
using Iwxxm.Net;
using Iwxxm.Net.Reports;

Result<IwxxmDocument> result = IwxxmParser.ParseFile("samples/does-not-exist.xml");

if (result.Errors.Any(e => e.Code == IwxxmErrorCodes.FileNotFound))
{
    Console.WriteLine($"file missing ({result.Errors[0].Category}): check the path");
}
```
```output
file missing (Io): check the path
```

The library emits exactly these strings and refers to them through the constants only (a test fails on a second literal). The several hundred `ACL-*`, `DOMAIN-*`, `GML-*` and `GEO-*` mapping codes, the `SCHEMATRON-*` engine codes and the official rule ids are not constants: they describe document content or engine internals, may gain members, and are matched by prefix. They are listed in the tables below. A test checks that this table and the class contain the same constants with the same values.

| Constant | Code | Category | Meaning |
| :--- | :--- | :--- | :--- |
| `IwxxmErrorCodes.FileNotFound` | `FILE-NOT-FOUND` | `Io` | The file or its directory does not exist. |
| `IwxxmErrorCodes.AccessDenied` | `ACCESS-DENIED` | `Io` | Permission denied, or the path is a directory. |
| `IwxxmErrorCodes.FileReadError` | `FILE-READ-ERROR` | `Io` | Sharing violation, path too long, malformed path or another I/O error on a file. |
| `IwxxmErrorCodes.StreamUnreadable` | `STREAM-UNREADABLE` | `Io` | The stream does not support reading. |
| `IwxxmErrorCodes.StreamReadError` | `STREAM-READ-ERROR` | `Io` | Reading the stream or text reader threw an I/O error. |
| `IwxxmErrorCodes.StreamEmpty` | `STREAM-EMPTY` | `MalformedInput` | The stream was read and has zero bytes. |
| `IwxxmErrorCodes.XmlEmptyString` | `XML-EMPTY-STRING` | `MalformedInput` | The XML string, reader or file content is null, empty or whitespace. |
| `IwxxmErrorCodes.XmlSizeLimit` | `XML-SIZE-LIMIT` | `MalformedInput` | The input exceeds `MaxDocumentBytes`. |
| `IwxxmErrorCodes.XmlDepthLimit` | `XML-DEPTH-LIMIT` | `MalformedInput` | An element is nested deeper than `MaxElementDepth`. |
| `IwxxmErrorCodes.XmlMalformed` | `XML-MALFORMED` | `MalformedInput` | Not well-formed XML, or a DTD was found. |
| `IwxxmErrorCodes.XmlNoRootElement` | `XML-NO-ROOT-ELEMENT` | `MalformedInput` | The XML has no root element. |
| `IwxxmErrorCodes.XsdSchemaViolation` | `XSD-SCHEMA-VIOLATION` | `SchemaViolation` | The document violates the official XSD (one finding per violation). |
| `IwxxmErrorCodes.XsdUnknownRootElement` | `XSD-UNKNOWN-ROOT-ELEMENT` | `SchemaViolation` | The root element is not declared by the IWXXM / COLLECT schemas. |
| `IwxxmErrorCodes.XsdUnexpectedRootElement` | `XSD-UNEXPECTED-ROOT-ELEMENT` | `SchemaViolation` | `IwxxmSigwxParser` received a root element that is not a SIGWX document. |
| `IwxxmErrorCodes.SchemaValidatorError` | `SCHEMA-VALIDATOR-ERROR` | `SchemaViolation` | A schema validator threw or returned nothing. |
| `IwxxmErrorCodes.RuleValidatorError` | `RULE-VALIDATOR-ERROR` | `BusinessRuleViolation` | A business-rule validator threw or returned nothing. |
| `IwxxmErrorCodes.NotABulletin` | `DISPATCH-NOT-A-BULLETIN` | `Deserialization` | `ProcessBulletin` received a document that is not a `collect:MeteorologicalBulletin`. |
| `IwxxmErrorCodes.XmlUnsupportedNamespace` | `XML-UNSUPPORTED-NAMESPACE` | `Deserialization` | The root element is not in the IWXXM 2025-2 namespace. |
| `IwxxmErrorCodes.XmlUnsupportedReport` | `XML-UNSUPPORTED-REPORT` | `Deserialization` | The IWXXM root element is not a supported 2025-2 report. |
| `IwxxmErrorCodes.CollectNestedBulletin` | `COLLECT-NESTED-BULLETIN` | `Deserialization` | A bulletin entry is itself a bulletin (an item error). |
| `IwxxmErrorCodes.UnexpectedReportType` | `IWXXM-UNEXPECTED-REPORT-TYPE` | `DomainMapping` | `ParseAs<T>` parsed a different document type than `T`. |
| `IwxxmErrorCodes.MapperError` | `MAPPER-ERROR` | `DomainMapping` | A document or report mapper threw or returned nothing (for a bulletin: the item error of that entry). |
| `IwxxmErrorCodes.BulletinEmpty` | `DOMAIN-BULLETIN-EMPTY` | `DomainMapping` | No report of the bulletin could be mapped. |
| `IwxxmErrorCodes.GeoJsonExportError` | `GEOJSON-EXPORT-ERROR` | `DomainMapping` | The GeoJSON exporter threw or returned nothing. |
| `IwxxmErrorCodes.CsvExportError` | `CSV-EXPORT-ERROR` | `DomainMapping` | The CSV exporter threw or returned nothing. |

### Input, files and streams (`Io`, `MalformedInput`)

| Code | Category | Meaning | Typical cause and remediation |
| :--- | :--- | :--- | :--- |
| `FILE-NOT-FOUND` | `Io` | The file or its directory does not exist. | Wrong path; check the path and working directory. |
| `ACCESS-DENIED` | `Io` | Permission denied (or the path is a directory). | Fix file permissions or pass a file, not a directory. |
| `FILE-READ-ERROR` | `Io` | Sharing violation, path too long, malformed path or other I/O error. | Close the other process holding the file, shorten the path. |
| `XML-EMPTY-STRING` | `MalformedInput` | The XML string, reader or file content is null, empty or whitespace. | Nothing was read upstream; check the producer. |
| `STREAM-EMPTY` | `MalformedInput` | The input stream has zero bytes. | The upstream read returned nothing, or the stream was already consumed (rewind it). |
| `STREAM-UNREADABLE` | `Io` | The stream does not support reading. | Pass a readable stream (not a write-only one). |
| `STREAM-READ-ERROR` | `Io` | Reading the stream threw an I/O error. | Transient transport failure; retry. An `OutOfMemoryException` is never converted into this code. |
| `XML-SIZE-LIMIT` | `MalformedInput` | The input exceeds `IwxxmParserOptions.MaxDocumentBytes` (default 50 MiB = 52,428,800 bytes). | Raise the limit deliberately (mind the memory cost in [SECURITY.md](../SECURITY.md)) or reject the oversized message. |
| `XML-DEPTH-LIMIT` | `MalformedInput` | An element is nested deeper than `IwxxmParserOptions.MaxElementDepth` (default 128, root element = depth 1). The message gives the limit and the line and position of the first element over it; the check runs in the single read, before any tree is built or validated. | Real IWXXM products nest at most about 20 levels (the .NET tree validation overflows its stack at about 25,000), so this is a malformed or hostile document: reject it. Raise the limit only for a legitimate deeper document (see [SECURITY.md](../SECURITY.md)). |
| `XML-MALFORMED` | `MalformedInput` | Not well-formed XML, or a DTD was found (DTDs are always refused). | Fix the producer; do not enable DTD processing. |
| `XML-NO-ROOT-ELEMENT` | `MalformedInput` | The XML has no root element. | An empty or truncated document. |

### Schema validation (`SchemaViolation`)

| Code | Meaning | Typical cause and remediation |
| :--- | :--- | :--- |
| `XSD-SCHEMA-VIOLATION` | The document violates the official IWXXM / COLLECT XSD (one finding per violation, with line and position). | Fix the document at the reported position. |
| `XSD-FATAL-ERROR` | The XSD validator stopped with a fatal schema exception. | Usually a structurally broken instance; see the message. |
| `XSD-UNKNOWN-ROOT-ELEMENT` | The root element is not declared by the official IWXXM / COLLECT schemas. | Wrong namespace or element name. |
| `XSD-UNEXPECTED-ROOT-ELEMENT` | The optional SIGWX facade (`IwxxmSigwxParser`) received a root element that is not a SIGWX document (a bulletin included). | Use `IwxxmParser`, which reads SIGWX, every other report and bulletins. |
| `XSD-SCHEMA-SET-UNAVAILABLE` | The embedded schema set could not be compiled (retried on the next validation). | Resource pressure (for example memory); retry. |
| `XSD-TOO-MANY-ERRORS` | More findings than `MaxValidationErrors`; only the first are reported. | Fix the first findings, then re-validate. |
| `SCHEMA-VALIDATOR-ERROR` | A custom or built-in schema validator threw or returned nothing. | Bug in a custom `IIwxxmSchemaValidator` adapter. |

### Business rules (`BusinessRuleViolation`)

| Code | Meaning | Typical cause and remediation |
| :--- | :--- | :--- |
| official rule ids | A failed assertion of the official IWXXM Schematron (or of the built-in XSLT 1.0 subset / code-list rules); `Code` is the rule id. | Fix the content named by `Message` and `Location`. |
| `SCHEMATRON-ASSERT-FAILED` | A failed assertion that carries no rule id (fallback code). | Read `Message` and `Location`. |
| `SCHEMATRON-TOO-MANY-ERRORS` | More findings than `MaxValidationErrors`; only the first are reported. | Fix the first findings, then re-validate. |
| `SCHEMATRON-REPORT-SIZE-LIMIT` | The SVRL report exceeds `MaxRuleReportBytes`. | Heavily non-conforming document; fix it or raise the limit. |
| `SCHEMATRON-REPORT-INVALID` | The SVRL report produced by the rules could not be read. | Internal rule failure; report it as a bug. |
| `SCHEMATRON-EXECUTION-ERROR` | The rule transform failed while running. | See `Message`; retried on the next validation. |
| `SCHEMATRON-UNAVAILABLE` | The rules could not be compiled (retried on the next validation). | Resource pressure; for the Saxon package, check the `net10.0` runtime. |
| `RULE-VALIDATOR-ERROR` | A custom or built-in rule validator threw or returned nothing. | Bug in a custom `IIwxxmRuleValidator` adapter. |

### Routing and deserialization (`Deserialization`)

| Code | Meaning | Typical cause and remediation |
| :--- | :--- | :--- |
| `DISPATCH-NOT-A-BULLETIN` | `ProcessBulletin` received a document that is not a `collect:MeteorologicalBulletin`. | Use `Process` for single reports. |
| `XML-UNSUPPORTED-ROOT` | The SIGWX entry point cannot deserialize this root element. | Not a SIGWX document. |
| `XML-UNSUPPORTED-REPORT` | The IWXXM root element is not a supported 2025-2 report. | Another IWXXM version, or an element that is not a report. |
| `XML-UNSUPPORTED-NAMESPACE` | The root element is not in the IWXXM 2025-2 namespace. | Wrong namespace (for example IWXXM 3.0 or 2023-1). |
| `COLLECT-NESTED-BULLETIN` | A bulletin entry is itself a bulletin. | Entries must be IWXXM reports. |
| `XML-DESERIALIZATION-ERROR` | The SIGWX transport model could not be deserialized. | Structure that passed the schema but not the transport model; see `Message`. |

### Pipeline and facade (`DomainMapping`)

| Code | Meaning | Typical cause and remediation |
| :--- | :--- | :--- |
| `IWXXM-UNEXPECTED-REPORT-TYPE` | `ParseAs<T>` parsed a different document type than `T`. | Ask for the right type or use `Parse`. |
| `MAPPER-ERROR` | A document mapper threw or returned nothing. For a bulletin, the code is reported as the item error of that one entry and the other entries are kept. | Bug in a custom `ISigwxDocumentMapper` or report mapper. |
| `GEOJSON-EXPORT-ERROR` | The GeoJSON exporter threw or returned nothing (`*ToGeoJson` members of the parser, `IwxxmGeoJsonExporter.Export` and `ToGeoJson`). The error is located at the document id. | Bug in a custom exporter. |
| `CSV-EXPORT-ERROR` | The CSV exporter threw or returned nothing (`*ToCsv` members of the parser, `IwxxmCsvExporter.Export` and `ToCsv`). The error is located at the document id. | Bug in a custom exporter. |
| `GEOPACKAGE-EXPORT-ERROR` | The GeoPackage file could not be written (`Iwxxm.Net.Export.GeoPackage` package: `IwxxmGeoPackageExporter.Export`, `ExportToBytes`, `ToGeoPackage`, `ToGeoPackageBytes`, and the `ProcessToGeoPackage`, `ProcessFileToGeoPackage`, `ParseToGeoPackage` and `ParseFileToGeoPackage` members for the parser). The path is empty or invalid, its directory does not exist, it is a directory or is not writable, or the exporter failed. The error is located at the document id, and the target is left as it was before the call. The value is the constant `GeoPackageErrorCodes.ExportError` of that package. | Fix the path or its permissions; the message names the cause. |
| `ACL-EMPTY-ENVELOPE` | The deserialized SIGWX envelope holds no report instance. | Empty `iwxxm:report` wrapper. |

### Metadata, time and measures (`DomainMapping`)

| Code | Meaning | Typical cause and remediation |
| :--- | :--- | :--- |
| `ACL-REPORT-STATUS-MISSING`, `ACL-REPORT-STATUS-INVALID` | `reportStatus` absent or not an official code (never defaulted to `NORMAL`). | Producer omitted or misspelled the status. |
| `ACL-PERMISSIBLE-USAGE-MISSING`, `ACL-PERMISSIBLE-USAGE-INVALID`, `ACL-PERMISSIBLE-USAGE-REASON-INVALID` | Permissible usage (or its reason) absent or not an official code (never defaulted to `OPERATIONAL`). | Producer omitted or misspelled it. |
| `ACL-ISSUE-TIME-MISSING`, `ACL-OBSERVATION-TIME-MISSING`, `ACL-VALID-PERIOD-MISSING` | Mandatory time absent (an `issueTime` given only by `xlink:href` is also reported). | Supply the time inline. |
| `ACL-TIME-INVALID`, `ACL-TIME-INDICATOR-INVALID` | A time value, or the time indicator, cannot be parsed. | Use ISO 8601 / the official indicator codes. |
| `ACL-MISSING-TIME-INSTANT`, `ACL-MISSING-TIME-PERIOD` | A mandatory SIGWX time instant / validity period is missing. | Supply the element. |
| `ACL-MEASURE-UOM-NON-UCUM-ALIAS` | Warning, not an error: a speed carries the legacy spelling `kn_i` instead of the UCUM code `[kn_i]`. It is read as knots, and one warning per document and field reports the alias, the UCUM code and the number of values. | Ask the producer to write the UCUM code. |
| `ACL-MEASURE-UOM-MISSING`, `ACL-MEASURE-UOM-UNSUPPORTED`, `ACL-MEASURE-UOM-MISMATCH`, `ACL-MEASURE-UOM-NOT-ALLOWED` | A quantity has no `@uom`, an unknown one, a unit other than its single allowed unit (`hPa`, `Cel`, `deg`, `m`...), or a unit outside the allowed subset. | Correct the unit; no unit is assumed. |
| `ACL-MEASURE-VALUE-INVALID` | A measure value is not a finite number. | Correct the value. |
| `ACL-VERTICAL-UOM-UNSUPPORTED`, `ACL-VERTICAL-REFERENCE-UNSUPPORTED` | Vertical unit or reference outside the AIXM / UCUM codes. | Use `FL`, `M`, `[ft_i]`... with `STD`, `MSL`, `SFC`... |
| `ACL-OPERATOR-INVALID` | A relational operator other than `ABOVE` / `BELOW`. | Correct the value. |
| `ACL-RVR-TENDENCY-INVALID` | An RVR tendency that is not an official code. | Correct the value. |
| `ACL-ASH-DENSITY-MISSING`, `ACL-VOLCANO-NAME-MISSING` | Mandatory QVACI density / volcano name absent. | Supply the element. |
| `ACL-VAA-STATUS-INVALID`, `ACL-TCA-VALUE-INVALID`, `ACL-ENROUTE-INTENSITY-CHANGE-INVALID` | A VAA conditions status / TCA trend value / SIGMET or AIRMET `intensityChange` outside the official set (`NO_CHANGE`, `WEAKEN`, `INTENSIFY`); the value is left `null`. | Correct the value. |
| `DOMAIN-ISSUE-TIME-MISSING`, `DOMAIN-TIME-NOT-UTC` | A default issue time was supplied / a `*Utc` value has a non-zero offset. | Pass a real UTC time. |
| `DOMAIN-REPORT-STATUS-INVALID`, `DOMAIN-PERMISSIBLE-USAGE-INVALID`, `DOMAIN-PERMISSIBLE-USAGE-REASON-INVALID` | An undefined enumeration value reached an aggregate factory. | Use the defined enum members. |

### Geometry

| Code | Meaning | Typical cause and remediation |
| :--- | :--- | :--- |
| `GML-EMPTY-POS`, `GML-EMPTY-POSLIST`, `GML-POINT-EMPTY`, `GML-POSLIST-EMPTY` | A `gml:pos` / `gml:posList` / point is empty. | Supply coordinates. |
| `GML-INVALID-COORD-NUMBER` | A coordinate is not a valid number. | Correct the token. |
| `GML-UNBALANCED-POSLIST` | The number of values is not a multiple of the dimension. | Correct the list. |
| `GML-INVALID-POINT` | A point does not have the expected number of values. | Correct the position. |
| `GML-UNSUPPORTED-SRSDIM` | An `srsDimension` other than 2 or 3. | Use 2 or 3. |
| `ACL-CRS-UNSUPPORTED`, `ACL-CRS-AXIS-LABELS-UNSUPPORTED`, `ACL-CRS-DIMENSION-UNSUPPORTED` | A CRS, axis-label set or dimension other than the supported WGS84 forms; axes are never guessed. | Use `CRS84` / `EPSG:4326` with a supported axis order. |
| `ACL-CIRCLE-RADIUS-MISSING` | A `gml:CircleByCenterPoint` has no radius. | Supply `gml:radius`. |
| `ACL-GEOMETRY-SEGMENT-UNSUPPORTED` | A curve segment that is not a sequence of positions (for example an arc) in a ring; it is not approximated. | Use `LineStringSegment`. |
| `ACL-GML-MULTI-PATCH-UNSUPPORTED` | A surface with several `gml:PolygonPatch` where the report holds a single surface (en-route volumes, ash cloud contours). The first patch is not silently used. Space weather regions keep every patch. | Send one patch per volume. |
| `ACL-GML-INTERIOR-RING-UNREADABLE` | A `gml:interior` ring without readable positions; the hole is not dropped silently. | Correct or remove the ring. |
| `ACL-RING-EMPTY`, `ACL-CURVE-EMPTY`, `ACL-CURVE-NO-SEGMENTS`, `ACL-VOLUME-NO-PATCH` | A SIGWX ring, curve or volume without geometry. | Supply the geometry. |
| `GEO-INVALID-LAT`, `GEO-INVALID-LON` | A coordinate outside [-90, 90] / [-180, 180] or not finite. | Correct the value. |
| `GEO-POLYGON-MIN-VERTICES`, `GEO-POLYGON-DEGENERATE`, `GEO-POLYGON-HOLE-DEGENERATE`, `GEO-CIRCLE-RADIUS-INVALID` | Degenerate polygon, hole or circle. | Provide at least 4 closed positions per ring and a positive radius. |

### SIGWX / WAFS content (`DomainMapping`)

| Code | Meaning | Typical cause and remediation |
| :--- | :--- | :--- |
| `ACL-SIGWX-FEATURE-BY-REFERENCE` | A feature given only by a reference to an external resource was skipped (warning). | Inline the feature. |
| `ACL-SIGWX-PHENOMENON-UNSUPPORTED` | A phenomenon type the SIGWX mapper does not know (warning, feature skipped). | Not an IWXXM 2025-2 WAFS phenomenon. |
| `ACL-MISSING-FLIGHT-LEVEL`, `ACL-MISSING-WIND-SPEED` | A required vertical limit / wind speed is missing. | Supply the element. |
| `ACL-JETSTREAM-MISSING-FL`, `ACL-JS-CORE-FL`, `ACL-WAFS-JS-MISSING-ELEVATION`, `ACL-WAFS-JS-NO-WIND-SYMBOLS` | A jet stream, its core point or a wind symbol misses a level, elevation or wind symbols. | Supply the elements. |
| `ACL-TROPOPAUSE-MISSING-FL`, `ACL-WAFS-TROPO-MISSING-LEVEL` | A tropopause spot height / feature misses its level. | Supply the level. |
| `ACL-WAFS-MISSING-VOLUME`, `ACL-WAFS-MISSING-UPPER-ELEV`, `ACL-CLOUD-MISSING-TOP` | A feature misses its volume, the mandatory upper elevation, or a cloud its top. | Supply the elements. |
| `ACL-VOLCANO-ACTIVITY-INVALID` | A volcano `activityStatus` outside the official set. | Correct the value. |
| `DOMAIN-SIGWX-ID`, `DOMAIN-SIGWX-CENTRE`, `DOMAIN-SIGWX-VALID-PERIOD`, `DOMAIN-SIGWX-VERTICAL-LIMITS`, `DOMAIN-SIGWX-NO-FEATURE` | The chart misses its identifier or centre, its validity ends before it starts (an instant, `ValidFromUtc == ValidUntilUtc`, is valid), its vertical limits are not ordered, or it has no mapped feature. | Supply the content. |
| `DOMAIN-CLOUD-ID`, `DOMAIN-CLOUD-AREA`, `DOMAIN-CLOUD-VERTICAL-EXTENT` | Cloud feature without id / area, or whose top is not above its base. | Correct the feature. |
| `DOMAIN-ICING-ID`, `DOMAIN-ICING-AREA`, `DOMAIN-ICING-VERTICAL-EXTENT` | Same checks for icing. | Correct the feature. |
| `DOMAIN-TURBULENCE-ID`, `DOMAIN-TURBULENCE-AREA`, `DOMAIN-TURBULENCE-VERTICAL-EXTENT` | Same checks for turbulence. | Correct the feature. |
| `DOMAIN-JETSTREAM-ID`, `DOMAIN-JETSTREAM-TRAJECTORY`, `DOMAIN-JETSTREAM-SPEED` | Jet stream without id / trajectory, or below the 80 kt ICAO threshold. | Correct the feature. |
| `DOMAIN-TROPOPAUSE-ID`, `DOMAIN-TROPOPAUSE-EMPTY`, `DOMAIN-TROPOPAUSE-CONTOUR-LEVEL` | Tropopause feature without id, without a contour or spot height, or a contour without its level. | Correct the feature. |
| `DOMAIN-RADIATION-ID`, `DOMAIN-SANDSTORM-ID` | Point feature without id. | Supply `gml:id`. |

### Aggregates and value objects (`DomainMapping`)

| Code | Meaning | Typical cause and remediation |
| :--- | :--- | :--- |
| `DOMAIN-METAR-ID`, `DOMAIN-METAR-AERODROME`, `DOMAIN-TAF-ID`, `DOMAIN-TAF-AERODROME` | Aerodrome report without id or aerodrome designator. | Supply the element. |
| `DOMAIN-TAF-VALID-PERIOD`, `DOMAIN-TAF-CANCELLED-VALID-PERIOD`, `DOMAIN-FORECAST-GROUP-PERIOD` | End of a period not strictly after its start (for a forecast group, before its start: an instant is accepted). | Correct the period. |
| `DOMAIN-SIGMET-ID`, `DOMAIN-SIGMET-ATS-UNIT`, `DOMAIN-SIGMET-VALID-PERIOD`, `DOMAIN-SIGMET-CANCELLED-VALID-PERIOD` | SIGMET without id / ATS unit, or with an invalid period. | Correct the report. |
| `DOMAIN-AIRMET-ID`, `DOMAIN-AIRMET-ATS-UNIT`, `DOMAIN-AIRMET-VALID-PERIOD`, `DOMAIN-AIRMET-CANCELLED-VALID-PERIOD`, `DOMAIN-AIRMET-CANCEL-CONTENT`, `DOMAIN-AIRMET-CONDITION-NULL` | AIRMET without id / ATS unit, with an invalid period, content in a cancellation, or a null condition. | Correct the report. |
| `DOMAIN-VAA-ID`, `DOMAIN-VAA-CONDITIONS-NULL`, `DOMAIN-VOLCANO-ID`, `DOMAIN-VONA-ID`, `DOMAIN-QVACI-ID` | Volcanic ash products without id or with null conditions. | Correct the report. |
| `DOMAIN-TC-ID`, `DOMAIN-TC-NAME`, `DOMAIN-TCA-ID`, `DOMAIN-SWX-ID` | Tropical cyclone / space weather products without id or name. | Correct the report. |
| `DOMAIN-BULLETIN-ID`, `DOMAIN-BULLETIN-IDENTIFIER`, `DOMAIN-BULLETIN-EMPTY` | Bulletin without `gml:id` / `bulletinIdentifier`, or with no successfully mapped report. | Correct the envelope; the entry errors are returned together with this code. |
| `DOMAIN-DIRECTION-OUT-OF-RANGE`, `DOMAIN-NEGATIVE-SPEED`, `DOMAIN-NEGATIVE-VISIBILITY`, `DOMAIN-NEGATIVE-RVR` | Physical ranges: direction within [0, 360] degrees, non-negative magnitudes. | Correct the value. |
| `DOMAIN-FL-INVALID-UOM`, `DOMAIN-FL-MISSING-UOM`, `DOMAIN-FL-OUT-OF-RANGE`, `DOMAIN-VERTICAL-LIMIT-INVALID` | Flight level or vertical limit with an invalid unit or value (non-finite, negative flight level...). | Correct the value. |

### Locations of findings

`ValidationError.Location` is `null` when a finding has no position. When it is set, it has one of these forms:

| Source | Form | Example |
| :--- | :--- | :--- |
| Malformed input (`XML-MALFORMED`) | none; `LineNumber` / `LinePosition` | |
| XSD findings | `/` plus the local name of the root element for root-element findings (`XSD-UNKNOWN-ROOT-ELEMENT`); otherwise the source URI of the schema exception when there is one (usually none for in-memory input: use `LineNumber` / `LinePosition`) | `/Unknown` |
| Built-in rules (XSLT 1.0 subset) | absolute XPath that uses **the prefixes of the instance**, one positional step per element | `/iwxxm:METAR[1]/iwxxm:observation[1]/iwxxm:MeteorologicalAerodromeObservation[1]/iwxxm:presentWeather[1]` |
| Official Schematron (`Iwxxm.Net.Validation.Saxon`) | absolute **namespace-independent XPath 1.0**, one positional step per element | see below |

The official Schematron form does not depend on the prefixes or the default namespace of the document, so it can be evaluated by any XPath engine without namespace bindings:

```text
/*[namespace-uri()='http://def.wmo.int/collect/2014' and local-name()='MeteorologicalBulletin'][1]/*[namespace-uri()='http://def.wmo.int/collect/2014' and local-name()='meteorologicalInformation'][2]/*[namespace-uri()='http://icao.int/iwxxm/2025-2' and local-name()='METAR'][1]/...
```

The `[n]` predicate is the position among the siblings with the same expanded name (the second `collect:meteorologicalInformation` above). Attribute steps look like `@*[namespace-uri()='ns' and local-name()='name']`. Namespace URIs come from the input, so they are always emitted as correctly quoted XPath literals.

The compiled official rules (SchXslt 1.10.1, vendored unchanged) write the same path as `/Q{http://icao.int/iwxxm/2025-2}METAR[1]/...`, the XPath 3.0 braced-URI syntax, which `System.Xml.XPath` and other XPath 1.0 / 2.0 engines cannot parse. The adapter therefore rewrites each `Q{ns}name[n]` step while it reads the SVRL report (`Internal/SvrlLocation.cs`); the rewrite is purely syntactic and the positions keep their meaning. Evaluating a location against the instance selects exactly the offending element:

```csharp
using System;
using System.Linq;
using System.Xml.Linq;
using System.Xml.XPath;
using Iwxxm.Net;
using Iwxxm.Net.Reports;
using Iwxxm.Net.Validation.Saxon;

string xml = System.IO.File.ReadAllText("samples/metar-A3-1.xml")
    .Replace("<iwxxm:qnh uom=\"hPa\">", "<iwxxm:qnh uom=\"mbar\">");

Result<IwxxmDocument> result = new IwxxmParserBuilder().UseOfficialSchematron().Build().Process(xml);
ValidationError error = result.Errors.Single();

XElement offending = XDocument.Parse(xml).XPathSelectElements(error.Location!).Single();
Console.WriteLine(error.Code);
Console.WriteLine(offending.Name.LocalName);
```
```output
METAR_SPECI.MeteorologicalAerodromeObservation-5
MeteorologicalAerodromeObservation
```

---

## Performance notes

Indicative numbers, not guarantees. They were measured with the BenchmarkDotNet project `tests/Iwxxm.Net.Benchmarks` (`--job short`, three iterations, so the error margins are wide) inside the repository's container on an AMD Ryzen 5 6600H limited to 2 logical cores, .NET 10.0.14, Linux. Expect different absolute values on other hardware; compare the ratios and the allocated bytes, which are stable.

| Benchmark | Mean | Allocated | What it shows |
| :--- | ---: | ---: | :--- |
| SIGWX report, standalone (`IwxxmSigwxParser`) | 1.48 ms | 613 KB | Baseline. |
| The same SIGWX report inside a SWIM bulletin (`ParseBulletin`) | 1.49 ms | 652 KB | Within noise of the baseline (BenchmarkDotNet ratio 1.05, RatioSD 0.38; +6% memory): the embedded report is mapped from the already parsed element, it is not serialized and parsed again. |
| METAR, repeated call through the default facade | 0.33 ms | 210 KB | Steady state: compiled schemas and rules, cached code lists. |
| Code list read from the shared resolver | 31 ns | 40 B | One cached `XPathDocument` per code list and process. |
| The same code list parsed on every call | 3.0 ms | 1.1 MB | What a per-transform parse would cost: about nine METAR parses for one code list. |
| First METAR parse in a fresh process | 307 ms | n/a | One-time cost: XSD set and rule compilation plus the first code-list parses (five launches, 288 to 325 ms confidence interval). Call a parser once at start-up to move it out of the first request. |
| All 29 official examples through the default facade | 16 ms | 6.2 MB | Baseline for the whole pipeline (about 0.55 ms per document). |

Reproduce them with `dotnet run --project tests/Iwxxm.Net.Benchmarks -c Release -- --filter "*SigwxInBulletin*" --job short` (and `*ValidationCache*`, `*FullSuiteBaseline*`; `*FirstCall*` must run without `--job`). The unit test `RuleCacheReuseTests` guards the code-list cache without timing: the number of parses is bounded by the number of embedded lists, not by the number of validations.

---

## Domain value objects and spatial primitives

| Value object | Description |
| :--- | :--- |
| **`GeoCoordinate`** | Immutable `readonly struct` enforcing WGS84 bounds (latitude in [-90, 90], longitude in [-180, 180]). Values are quantised once at construction to 7 decimal degrees (`GeoCoordinate.Precision` = 1e-7, about 1.1 cm) and compared exactly, so `Equals` and `GetHashCode` agree and equality is transitive. |
| **`GeoSurface`** | Closed union of horizontal surfaces: `GeoPolygon` or `GeoCircle`. Two consumption styles: `Match(polygon => ..., circle => ...)` with two functions (a value) or two actions (statements), which forces a handler per case; or pattern matching (`switch (area) { case GeoPolygon p: ...; case GeoCircle c: ...; }`) on the public sealed subtypes, which the compiler does not check for completeness. Used by `EnRoutePhenomenonVolume.Area`, `SpaceWeatherRegion.Areas` and the SIGWX `Cloud`, `Turbulence` and `AirframeIcing` areas. |
| **`GeoCircle`** | Circle from `gml:CircleByCenterPoint`: `Centre` and `Radius` (a `Length`, amount and unit as reported, strictly positive). |
| **`GeoPolygon`** | Immutable polygon with at least 3 distinct vertices, automatic ring closure and optional `InteriorRings` for hollow contours. Equality and hash code cover every exterior and interior vertex. |
| **`VerticalLimit`** | Immutable `readonly struct` keeping a limit exactly as reported: `Amount` (`double?`; `null` for special values and nil limits), `Unit` (`FlightLevel`, `Feet`, `Meters`, `Kilometers`, `StandardMeters`), `Reference` (`Sfc`, `Msl`, `W84`, `Std`, `Other`; `null` when not reported), `SpecialValue` (`GND`, `UNL`, `FLOOR`, `CEILING`) and `NilReason`. "15 km AMSL" is not FL492 and `GND SFC` is not FL000. `TryCompareTo` compares only commensurable limits. |
| **`FlightLevel`** | Derived view (`Number` = hundreds of feet): `VerticalLimit.AsFlightLevel()` returns a flight level only for STD limits in `FL` (or exact hundreds of `FT`). |
| **`ReportStatus`, `PermissibleUsage`, `PermissibleUsageReason`** | Typed report metadata (`common.xsd`). Missing or unknown required values are mapping errors; a report is never presented as operational by default. |
| **`Speed`** | Reported speed (`Amount`) and `SpeedUnit` (`MetersPerSecond`, `Knots`, `KilometersPerHour`); exact conversions `ToMetersPerSecond()`, `ToKnots()`, `ToKilometersPerHour()` (1 kt = 1852/3600 m/s). Equality is exact on `Amount` and unit. |
| **`Length`** | Reported length (`Amount`) and `LengthUnit` (`Meters`, `Feet`, `Kilometers`, `NauticalMiles`, `StatuteMiles`); exact conversions `ToMeters()`, `ToFeet()`, ... (1 ft = 0.3048 m). |
| **BUFR codes** | `Cloud.Distribution`, `Cloud.CloudType`, `Turbulence.Degree` and `AirframeIcing.Degree` are `CodeListValue<CloudDistributionCode / CloudTypeCode / TurbulenceDegreeCode / IcingDegreeCode>`, translated from the WMO BUFR code tables (`0-20-008`, `0-20-012`, `0-11-030`, `0-20-041`), bare codes and TAC abbreviations. Each member's numeric value equals the official code; `Notation` keeps the code as reported. |
| **`CodeListValue<NilReason>`** | `@nilReason` translated against the official register `http://codes.wmo.int/common/nil`: distinguishes `NSW` (`AerodromeForecastGroup.IsNoSignificantWeather`), `NSC` / `NCD` (`AerodromeCloud.IsNoSignificantCloud` / `IsNoCloudDetected`), `VV///`, `//` and `SKC` from content that is not reported. Every nil reason of the model has this type: `AerodromeCloud.NilReason` / `VerticalVisibilityNilReason`, `AerodromeCloudLayer.AmountNilReason` / `BaseNilReason` / `NilReason`, `AerodromeForecastGroup.WeatherNilReason`, `AerodromeObservation.PresentWeatherNilReason` / `ObservationNilReason` (nil `observation`), `Taf.BaseForecastNilReason` (nil `baseForecast`), `SpaceWeatherAnalysis.NilReason` and `VerticalLimit.NilReason`. A known code maps to its member with `Notation` = the code; anything else (an unlisted register code, a case variant, a foreign URI, a bare token such as `unknown`) is `Code = Unknown` with the reference as written in `Notation`, without a warning; the GeoJSON `...NilReason` / `nilReason` properties carry the reason as reported. |
| **`RelationalOperator`** | TAC `P` / `M` qualifiers on wind and gust speeds, RVR and prevailing visibility (`SurfaceWind.MeanSpeedOperator`, `RunwayVisualRange.MeanRvrOperator`, `AerodromeHorizontalVisibility.PrevailingOperator`). |
| **`TranslationMetadata`** | `IwxxmReport.Translation`: translated bulletin id, reception time, translation centre designator and name, and translation time (`null` when the report was not translated from TAC). |

---

## Namespaces and layers

### Public namespaces (consumer view)

Consumers never need a layer name in a `using` directive:

| Namespace | Contents |
| :--- | :--- |
| `Iwxxm.Net` | Facades (`IwxxmParser`, `IwxxmSigwxParser`, `IwxxmGeoJsonExporter`, `IwxxmCsvExporter`), composition root (`IwxxmParserBuilder`), options (`IwxxmParserOptions`, `GeoJsonExportOptions`, `CsvExportOptions`), `Result<T>`, `ValidationError`, `ErrorCategory`, `IwxxmErrorCodes`, `ValidationSeverity`, and the `UseOfficialSchematron()` extension of the optional Saxon package |
| `Iwxxm.Net.Reports` | Domain model: the closed hierarchy (`IwxxmDocument`, `IwxxmReport`, `MeteorologicalBulletin`, `IIwxxmDocumentVisitor<TResult>`, `AerodromeObservation`, `Metar`, `Speci`, `Taf`, `Sigmet`, `Airmet`, `Vona`, `Qvaci`, `Vaa`, `Tca`, `SpaceWx`, `Sigwx`) and the value objects |
| `Iwxxm.Net.Extensibility` | The `ISchematronValidator` port for custom business-rule engines and `IwxxmDocumentContext` |
| `Iwxxm.Net.Validation.Saxon` | `OfficialSchematronValidator` (optional package) |
| `Iwxxm.Net.Export.GeoPackage` | `IwxxmGeoPackageExporter`, `GeoPackageExportOptions`, `GeoPackageVersion`, `GeoPackageErrorCodes` (optional package). Its parser members (`ProcessToGeoPackage`, `ProcessFileToGeoPackage`, `ParseToGeoPackage`, `ParseFileToGeoPackage`) are in `IwxxmParserGeoPackageExtensions`, namespace `Iwxxm.Net`. |

Everything else (`Iwxxm.Net.Application.*`, `Iwxxm.Net.Infrastructure.*`, `Iwxxm20252.Generated`) is `internal`.

### Layers (source view)

Folders express the hexagonal layers; IL-level tests (`HexagonalArchitectureTests`, NetArchTest and Mono.Cecil) enforce the dependency rules on the compiled assembly.

```text
src/Iwxxm.Net/
├── Domain/                     # Depends on nothing (no System.Xml, System.IO, Application or Infrastructure)
│   ├── Common/                 # Result<T>, ValidationError, ErrorCategory, IwxxmErrorCodes, ValidationSeverity (ns Iwxxm.Net)
│   ├── ValueObjects/           # GeoCoordinate, GeoPolygon, VerticalLimit, SurfaceWind, ..., CodeLists/ (ns Iwxxm.Net.Reports)
│   └── Aggregates/             # IwxxmDocument, IwxxmReport, IIwxxmDocumentVisitor<TResult>, aggregates (ns Iwxxm.Net.Reports)
├── Application/                # Depends only on Domain (no System.Xml)
│   ├── Options/                # IwxxmParserOptions, GeoJsonExportOptions                            (ns Iwxxm.Net)
│   ├── Ports/Out/              # Outbound ports (driven adapters)                                    (ns Iwxxm.Net.Extensibility: ISchematronValidator, IwxxmDocumentContext; others internal, ns Iwxxm.Net.Application.Ports.Out)
│   └── UseCases/               # BoundedDocumentBuffer, ValidationPipeline, PipelineGuard (internal helpers)
├── Infrastructure/             # Driven adapters, all internal
│   ├── Generated/              # SIGWX transport model (hand-maintained, namespace Iwxxm20252.Generated)
│   ├── Resources/              # EmbeddedResourceLoader and EmbeddedXmlUrlResolver (air-gapped XSD, XSLT, RDF)
│   ├── Validation/             # XsdValidator, XsltSchematronValidator, DisabledSchematronValidator
│   ├── Serialization/          # SigwxXmlDeserializer + AttributeDrivenXmlReader
│   ├── Mapping/                # IwxxmSuiteMapper (composite), SigwxDocumentMapper, UomMeasureReader, GmlCoordinateTokenizer
│   │   ├── Reports/            # One mapper per report family + IwxxmReportMapperRegistry (dispatch by root QName)
│   │   ├── Xml/                # Shared LINQ to XML readers: GML time and geometry, AIXM features, text, error codes
│   │   ├── CodeLists/          # WMO code-list anti-corruption layer (tables checked against the embedded RDFs)
│   │   └── Wafs/               # WAFS and compact SIGWX mappers, one extractor per phenomenon family
│   └── GeoJson/                # Rfc7946GeoJsonExporter + GeoJsonFeatureCollector (an IIwxxmDocumentVisitor)
│       ├── Features/           # One feature builder per report family
│       └── Writing/            # RFC 8259 JSON writer and FeatureCollection writer
├── Composition/                # IwxxmParserBuilder: the only place that references Infrastructure  (ns Iwxxm.Net)
└── Presentation/               # Facades running the pipeline over outbound ports                   (ns Iwxxm.Net)

src/Iwxxm.Net.Validation.Saxon/ # Optional driven adapter package (net10.0): ISchematronValidator over SaxonCS-HE
src/Iwxxm.Net.Export.GeoPackage/ # Optional export package (netstandard2.0): GeoPackage writer over Microsoft.Data.Sqlite, reads the features of the core GeoJSON collector
```

The rules checked by `HexagonalArchitectureTests`: Domain depends on nothing outside the Domain; Application depends only on Domain; Presentation never depends on Infrastructure or `System.Xml`; Infrastructure is reached only through the composition root and never calls facades; `Iwxxm20252.Generated` and Infrastructure types are not public; public types live only in the documented namespaces; every root-namespace type is assigned to a layer; and `Iwxxm.Net` has zero third-party runtime dependencies. See [CONTRIBUTING.md](../CONTRIBUTING.md#hexagonal-architecture-rules) for the contributor view.
