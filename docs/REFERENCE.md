# Iwxxm.Net reference

The companion to the [guide](GUIDE.md): the guide teaches, this page looks things up. It holds one example per product family, the composition root and its ports, the full error-code table, the value-object catalogue and the namespace map.

Every C# snippet is compiled against the packaged library. As in the guide, paths such as `samples/metar-A3-1.xml` refer to the official example messages copied next to your project (see [Before you start](GUIDE.md#1-before-you-start)).

**Contents**

1. [One example per product family](#one-example-per-product-family)
2. [GeoJSON export in detail](#geojson-export-in-detail)
3. [Composition root and dependency injection](#composition-root-and-dependency-injection)
4. [Official WMO Schematron](#official-wmo-schematron)
5. [Error codes](#error-codes)
6. [Domain value objects and spatial primitives](#domain-value-objects-and-spatial-primitives)
7. [Namespaces and layers](#namespaces-and-layers)

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
        circle => $"circle, {circle.Radius.Value} {circle.Radius.UnitOfMeasure} around {circle.Centre}") ?? "(none)";
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

`IwxxmSigwxParser.ParseFile` and `IwxxmParser.ParseFileAs<Sigwx>` give the same model. The guide has worked examples for [jet streams](GUIDE.md#10-worked-example-jet-streams-sigwx) and [clouds](GUIDE.md#11-worked-example-clouds-sigwx). The nine phenomenon families:

```csharp
using System;
using Iwxxm.Net;
using Iwxxm.Net.Reports;

Result<Sigwx> result = IwxxmSigwxParser.ParseFile("samples/WAFS-Example.xml");

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

// 5. Tropopause contours and spot heights
foreach (Tropopause tropo in sigwx.Tropopauses)
{
    Console.WriteLine($"Tropopause: {tropo.SpotHeights.Count} spot heights");
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
Console.WriteLine($"Originating centre: {qvaci.OriginatingCentre} (model initialisation: {qvaci.DispersionModelInitialisationTimeUtc:O})");

foreach (VolcanicAshConcentrationCloud cloud in qvaci.AshConcentrationClouds)
{
    Console.WriteLine($"Ash contour {cloud.Id}:");
    Console.WriteLine($"  density          : {cloud.AshDensityMgPerM3} mg/m3");
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
        : $"  {analysis.TimeIndicator} {analysis.PhenomenonTimeUtc:O}: {analysis.Regions.Count} intensity/region groups");
}
```
```output
VAAC TOKYO #2024/4: 2 observed clouds at 2024-09-23T01:00:00.0000000+00:00
...
```

### Bulletins

Partial-result behaviour, `ReportResults` and `ItemErrors` are explained with a runnable example in the [guide](GUIDE.md#bulletins-many-reports-one-envelope). A bulletin succeeds as long as one report maps; each skipped report is also a warning in `Result.Warnings`. The same applies per feature to WAFS charts (`Sigwx.FeatureErrors`). Schema and rule validation remain document-level.

---

## GeoJSON export in detail

`IwxxmParser.ParseFileToGeoJson` and `IwxxmGeoJsonExporter.ToGeoJson` convert any `IwxxmDocument` into an RFC 7946 `FeatureCollection`:

- GML `[latitude, longitude]` is swapped to RFC 7946 section 3.1.1 `[longitude, latitude]`.
- Polygon interior holes (`GeoPolygon.InteriorRings`) are written as additional linear rings.
- The global `bbox` is `[west, south, east, north]`; when a geometry crosses the antimeridian, `west` is greater than `east` (section 5.2).
- Exterior rings are counter-clockwise and holes clockwise (right-hand rule); geometries that cross +-180 degrees are split into `MultiLineString` or `MultiPolygon` (section 3.1.9); rings around a pole become polar caps. The domain model keeps the source order.
- Circles (`GeoCircle`) are written as a 64-segment polygon (geodesic points on the IUGG mean sphere) plus the properties `center` (`[lon, lat]`), `radius`, `radiusUom` and `circleSegments`, from which a consumer can rebuild the exact circle.
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
    string pretty = IwxxmGeoJsonExporter.ToGeoJson(parsed.Value, GeoJsonExportOptions.Pretty);
    string[] lines = pretty.Split('\n');
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

## Composition root and dependency injection

`IwxxmParserBuilder` is the composition root: it wires the driven adapters into the use cases and returns immutable, thread-safe facades (`IwxxmParser`, `IwxxmSigwxParser`, `IwxxmGeoJsonExporter`). Every adapter can be replaced through a public outbound port in `Iwxxm.Net.Extensibility`, and business rules can be switched off. The static `IwxxmParser.Parse*`, `IwxxmSigwxParser.Parse*` and `IwxxmGeoJsonExporter.ToGeoJson` members are thin wrappers over a default instance.

An outbound adapter is small. This one implements the Schematron port with a rule of its own: it works on the raw bytes and reports a *warning* (the document stays valid) when a message is unusually large:

```csharp
using System;
using System.Collections.Generic;
using Iwxxm.Net;
using Iwxxm.Net.Extensibility;

public sealed class MyRuleEngine : ISchematronValidator
{
    public IReadOnlyList<ValidationError> Validate(IwxxmDocumentContext document)
    {
        // document.OpenRead() gives the raw bytes if you need them; the use case has already parsed them safely once.
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
    // .UseSchemaValidator(...) / .UseDocumentMapper(...) / .UseSigwxDocumentMapper(...) / .UseGeoJsonExporter(...)

var services = new ServiceCollection();
services.AddSingleton(builder.Build());                // IwxxmParser (convenience facade)
services.AddSingleton(builder.BuildSigwxParser());     // IwxxmSigwxParser
services.AddSingleton(builder.BuildGeoJsonExporter()); // IwxxmGeoJsonExporter

// The facades do not implement the inbound ports; they expose the port they delegate to.
// Inject the ports where only the use-case contract is needed:
services.AddSingleton<IIwxxmReportDispatcher>(sp => sp.GetRequiredService<IwxxmParser>().Dispatcher);
services.AddSingleton<IIwxxmSigwxProcessor>(sp => sp.GetRequiredService<IwxxmSigwxParser>().Processor);

using ServiceProvider provider = services.BuildServiceProvider();
IIwxxmReportDispatcher dispatcher = provider.GetRequiredService<IIwxxmReportDispatcher>();
Result<IwxxmDocument> result = dispatcher.Process(System.IO.File.OpenRead("samples/metar-A3-1.xml"));
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

| Port | Kind | Namespace | Default adapter |
| :--- | :--- | :--- | :--- |
| `IIwxxmReportDispatcher` | inbound (use case) | `Iwxxm.Net` | full-suite validation and mapping pipeline |
| `IIwxxmSigwxProcessor` | inbound (use case) | `Iwxxm.Net` | SIGWX validation and mapping pipeline |
| `IGeoJsonExportService` | inbound (use case) | `Iwxxm.Net` | RFC 7946 export |
| `ISchemaValidator` | outbound | `Iwxxm.Net.Extensibility` | embedded official WMO XSDs |
| `ISchematronValidator` | outbound | `Iwxxm.Net.Extensibility` | built-in XSLT 1.0 rule subset (or `UseOfficialSchematron()`) |
| `IIwxxmDocumentMapper` | outbound | `Iwxxm.Net.Extensibility` | built-in anti-corruption-layer mapper |
| `ISigwxDocumentMapper` | outbound | `Iwxxm.Net.Extensibility` | built-in SIGWX mapper |
| `IGeoJsonExporter` | outbound | `Iwxxm.Net.Extensibility` | built-in RFC 7946 writer |

Outbound ports receive an **`IwxxmDocumentContext`**: the use case has already bounded the size, converted text input to UTF-8 and parsed the document once with the hardened reader settings (DTD prohibited, no resolver, character quotas), so a malformed or DTD-bearing document never reaches an adapter (`XML-MALFORMED`). `OpenRead()` returns a read-only stream of the document bytes for adapters that need raw XML; the built-in adapters share the parsed tree. You can call an adapter directly with `new IwxxmDocumentContext(bytes, options)`.

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

| Code | Category | Meaning |
| :--- | :--- | :--- |
| `FILE-NOT-FOUND` | `MalformedInput` | The file or its directory does not exist. |
| `ACCESS-DENIED` | `MalformedInput` | Permission denied (or the path is a directory). |
| `FILE-READ-ERROR` | `MalformedInput` | Sharing violation, path too long, malformed path or other I/O error. |
| `XML-EMPTY-STRING` | `MalformedInput` | The XML string is null, empty or whitespace. |
| `XML-SIZE-LIMIT`, `XML-MALFORMED`, `STREAM-*` | `MalformedInput` | Size limits, malformed XML or a DTD (checked once by the use case before any adapter), unreadable or empty stream. |
| `XSD-UNKNOWN-ROOT-ELEMENT` | `SchemaViolation` | The root element is not declared by the official IWXXM / COLLECT schemas. |
| `XSD-TOO-MANY-ERRORS`, `SCHEMATRON-TOO-MANY-ERRORS` | `SchemaViolation` / `BusinessRuleViolation` | More findings than `IwxxmParserOptions.MaxValidationErrors`; only the first are reported. |
| `SCHEMATRON-REPORT-SIZE-LIMIT`, `SCHEMATRON-UNAVAILABLE` | `BusinessRuleViolation` | The SVRL report exceeds `MaxRuleReportBytes` / the rules could not be compiled (retried on the next validation). |
| `XSD-*`, `SCHEMA-VALIDATOR-ERROR` | `SchemaViolation` | Schema violation / the schema validator threw or returned nothing. |
| official rule ids, `RULE-VALIDATOR-ERROR` | `BusinessRuleViolation` | Schematron or code-list rule / the rule validator threw or returned nothing. |
| `DISPATCH-NOT-A-BULLETIN`, `XML-UNSUPPORTED-ROOT`, `XML-UNSUPPORTED-REPORT`, `COLLECT-NESTED-BULLETIN`, `XML-DESERIALIZATION-ERROR` | `Deserialization` | The document cannot be routed to the requested entry point or a mapper. |
| `IWXXM-UNEXPECTED-REPORT-TYPE` | `DomainMapping` | `ParseAs<T>` parsed a different document type. |
| `MAPPER-ERROR` | `DomainMapping` | The document mapper threw or returned nothing. |
| `GEOJSON-EXPORT-ERROR` | `DomainMapping` | The GeoJSON exporter threw (`*ToGeoJson` members). |
| `ACL-REPORT-STATUS-MISSING` / `-INVALID`, `ACL-PERMISSIBLE-USAGE-MISSING` / `-INVALID`, `ACL-PERMISSIBLE-USAGE-REASON-INVALID` | `DomainMapping` | Required metadata absent or not an official code (never defaulted to `NORMAL` / `OPERATIONAL`). |
| `ACL-ISSUE-TIME-MISSING`, `ACL-OBSERVATION-TIME-MISSING`, `ACL-VALID-PERIOD-MISSING`, `ACL-TIME-INVALID` | `DomainMapping` | Mandatory time absent (an `issueTime` given only by `xlink:href` is also reported) or unparseable. |
| `ACL-MEASURE-UOM-MISMATCH` | `DomainMapping` | A single-unit quantity (`hPa`, `Cel`, `deg`, `m`...) reported in another unit; a missing or unknown `@uom` is also a mapping error. |
| `ACL-ASH-DENSITY-MISSING`, `ACL-VOLCANO-NAME-MISSING` | `DomainMapping` | Mandatory QVACI density / volcano name element absent. |
| `ACL-VERTICAL-UOM-UNSUPPORTED`, `ACL-VERTICAL-REFERENCE-UNSUPPORTED` | `DomainMapping` | Vertical unit or reference outside the AIXM / UCUM codes. |
| `ACL-SIGWX-FEATURE-BY-REFERENCE` | `DomainMapping` (warning) | A WAFS feature given only by a reference to an external resource was skipped. |
| `DOMAIN-<REPORT>-VALID-PERIOD`, `DOMAIN-TAF-CANCELLED-VALID-PERIOD`, `DOMAIN-FORECAST-GROUP-PERIOD` | `DomainMapping` | End of a period not strictly after its start. |
| `DOMAIN-TIME-NOT-UTC`, `DOMAIN-ISSUE-TIME-MISSING` | `DomainMapping` | A `*Utc` value with a non-zero offset / a default issue time. |
| `DOMAIN-DIRECTION-OUT-OF-RANGE`, `DOMAIN-NEGATIVE-SPEED`, `DOMAIN-NEGATIVE-VISIBILITY`, `DOMAIN-NEGATIVE-RVR` | `DomainMapping` | Physical ranges: direction within [0, 360] degrees, non-negative magnitudes. |
| `DOMAIN-JETSTREAM-SPEED` | `DomainMapping` | A WAFS jet stream below the 80 kt ICAO threshold. |
| `DOMAIN-CLOUD-VERTICAL-EXTENT` | `DomainMapping` | A cloud whose top is not above its base. |
| `DOMAIN-AIRMET-ATS-UNIT`, `DOMAIN-REPORT-STATUS-INVALID`, `DOMAIN-PERMISSIBLE-USAGE-INVALID`, `DOMAIN-BULLETIN-EMPTY`, `DOMAIN-VERTICAL-LIMIT-INVALID` | `DomainMapping` | Structural invariants of the aggregate factories. |
| `GEO-POLYGON-MIN-VERTICES`, `GEO-POLYGON-HOLE-DEGENERATE`, `GEO-CIRCLE-RADIUS-INVALID` | `DomainMapping` | Degenerate geometry. |

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
| **`GeoSurface`** | Closed union of horizontal surfaces: `GeoPolygon` or `GeoCircle` (`Match(polygon => ..., circle => ...)` or pattern matching). Used by `EnRoutePhenomenonVolume.Area`, `SpaceWeatherAnalysisRegion.Areas` and the SIGWX `Cloud`, `Turbulence` and `AirframeIcing` areas. |
| **`GeoCircle`** | Circle from `gml:CircleByCenterPoint`: `Centre` and `Radius` (a `Length`, value and unit as reported, strictly positive). |
| **`GeoPolygon`** | Immutable polygon with at least 3 distinct vertices, automatic ring closure and optional `InteriorRings` for hollow contours. Equality and hash code cover every exterior and interior vertex. |
| **`VerticalLimit`** | Immutable `readonly struct` keeping a limit exactly as reported: `Value`, `Unit` (`FlightLevel`, `Feet`, `Meters`, `Kilometers`, `StandardMeters`), `Reference` (`Sfc`, `Msl`, `W84`, `Std`, `Other`; `null` when not reported), `SpecialValue` (`GND`, `UNL`, `FLOOR`, `CEILING`) and `NilReason`. "15 km AMSL" is not FL492 and `GND SFC` is not FL000. `TryCompareTo` compares only commensurable limits. |
| **`FlightLevel`** | Derived view: `VerticalLimit.AsFlightLevel()` returns a flight level only for STD limits in `FL` (or exact hundreds of `FT`). |
| **`ReportStatus`, `PermissibleUsage`, `PermissibleUsageReason`** | Typed report metadata (`common.xsd`). Missing or unknown required values are mapping errors; a report is never presented as operational by default. |
| **`Speed`** | Reported speed value and `SpeedUnit` (`MetersPerSecond`, `Knots`, `KilometersPerHour`); exact conversions `ToMetersPerSecond()`, `ToKnots()`, `ToKilometersPerHour()` (1 kt = 1852/3600 m/s). Equality is exact on value and unit. |
| **`Length`** | Reported length and `LengthUnit` (`Meters`, `Feet`, `Kilometers`, `NauticalMiles`, `StatuteMiles`); exact conversions `ToMeters()`, `ToFeet()`, ... (1 ft = 0.3048 m). |
| **BUFR codes** | `Cloud.Distribution`, `Cloud.CloudType`, `Turbulence.Degree` and `AirframeIcing.Degree` are `CodeListValue<CloudDistributionCode / CloudTypeCode / TurbulenceDegreeCode / IcingDegreeCode>`, translated from the WMO BUFR code tables (`0-20-008`, `0-20-012`, `0-11-030`, `0-20-041`), bare codes and TAC abbreviations. Each member's numeric value equals the official code; `Notation` keeps the code as reported. |
| **`CodeListValue<NilReason>`** | `@nilReason` translated against the official register `http://codes.wmo.int/common/nil`: distinguishes `NSW` (`AerodromeForecastGroup.IsNoSignificantWeather`), `NSC` / `NCD` (`AerodromeCloud.IsNoSignificantCloud` / `IsNoCloudDetected`), `VV///`, `//` and `SKC` from content that is not reported. |
| **`RelationalOperator`** | TAC `P` / `M` qualifiers on wind and gust speeds, RVR and prevailing visibility (`SurfaceWind.MeanSpeedOperator`, `RunwayVisualRange.MeanRvrOperator`, `AerodromeHorizontalVisibility.PrevailingOperator`). |
| **`TranslationMetadata`** | `IwxxmReport.Translation`: translated bulletin id, reception time, translation centre designator and name, and translation time (`null` when the report was not translated from TAC). |

---

## Namespaces and layers

### Public namespaces (consumer view)

Consumers never need a layer name in a `using` directive:

| Namespace | Contents |
| :--- | :--- |
| `Iwxxm.Net` | Facades (`IwxxmParser`, `IwxxmSigwxParser`, `IwxxmGeoJsonExporter`), composition root (`IwxxmParserBuilder`), options (`IwxxmParserOptions`, `GeoJsonExportOptions`), `Result<T>`, `ValidationError`, `ErrorCategory`, `ValidationSeverity`, inbound ports (`IIwxxmReportDispatcher`, `IIwxxmSigwxProcessor`, `IGeoJsonExportService`) and the `UseOfficialSchematron()` extension of the optional Saxon package |
| `Iwxxm.Net.Reports` | Domain model: the closed hierarchy (`IwxxmDocument`, `IwxxmReport`, `MeteorologicalBulletin`, `IIwxxmDocumentVisitor<TResult>`, `AerodromeObservation`, `Metar`, `Speci`, `Taf`, `Sigmet`, `Airmet`, `Vona`, `Qvaci`, `Vaa`, `Tca`, `SpaceWx`, `Sigwx`) and the value objects |
| `Iwxxm.Net.Extensibility` | Outbound ports for custom adapters (`ISchemaValidator`, `ISchematronValidator`, `IIwxxmDocumentMapper`, `ISigwxDocumentMapper`, `IGeoJsonExporter`) and `IwxxmDocumentContext` |
| `Iwxxm.Net.Validation.Saxon` | `OfficialSchematronValidator` (optional package) |

Everything else (`Iwxxm.Net.Application.*`, `Iwxxm.Net.Infrastructure.*`, `Iwxxm20252.Generated`) is `internal`.

### Layers (source view)

Folders express the hexagonal layers; IL-level tests (`HexagonalArchitectureTests`, NetArchTest and Mono.Cecil) enforce the dependency rules on the compiled assembly.

```text
src/Iwxxm.Net/
├── Domain/                     # Depends on nothing (no System.Xml, System.IO, Application or Infrastructure)
│   ├── Common/                 # Result<T>, ValidationError, ErrorCategory, ValidationSeverity      (ns Iwxxm.Net)
│   ├── ValueObjects/           # GeoCoordinate, GeoPolygon, VerticalLimit, SurfaceWind, ..., CodeLists/ (ns Iwxxm.Net.Reports)
│   └── Aggregates/             # IwxxmDocument, IwxxmReport, IIwxxmDocumentVisitor<TResult>, aggregates (ns Iwxxm.Net.Reports)
├── Application/                # Depends only on Domain (no System.Xml)
│   ├── Options/                # IwxxmParserOptions, GeoJsonExportOptions                            (ns Iwxxm.Net)
│   ├── Ports/In/               # Inbound ports (use-case interfaces)                                 (ns Iwxxm.Net)
│   ├── Ports/Out/              # Outbound ports (driven adapters)                                    (ns Iwxxm.Net.Extensibility)
│   └── UseCases/               # DispatchIwxxmReportUseCase, ProcessSigwxReportUseCase, ExportGeoJsonUseCase (internal)
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
└── Presentation/               # Facades over inbound ports only                                     (ns Iwxxm.Net)

src/Iwxxm.Net.Validation.Saxon/ # Optional driven adapter package (net10.0): ISchematronValidator over SaxonCS-HE
```

The rules checked by `HexagonalArchitectureTests`: Domain depends on nothing outside the Domain; Application depends only on Domain; Presentation depends only on inbound ports (never on Infrastructure, use cases or outbound ports); Infrastructure is reached only through the composition root and never calls facades; `Iwxxm20252.Generated` and Infrastructure types are not public; public types live only in the documented namespaces; every root-namespace type is assigned to a layer; and `Iwxxm.Net` has zero third-party runtime dependencies. See [CONTRIBUTING.md](../CONTRIBUTING.md#hexagonal-architecture-rules) for the contributor view.
