using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using Iwxxm.Net.Extensibility;
using Iwxxm.Net.Reports;

namespace Iwxxm.Net.Infrastructure.Mapping
{
    /// <summary>
    /// Anti-Corruption Layer (ACL) mapper and polymorphic dispatcher for the full ICAO/WMO IWXXM 2025-2
    /// report suite (<c>SIGWX</c>, <c>METAR</c>, <c>SPECI</c>, <c>TAF</c>, <c>SIGMET</c>, <c>AIRMET</c>,
    /// <c>VONA</c>, <c>QVACI</c>, <c>VolcanicAshAdvisory</c>, <c>TropicalCycloneAdvisory</c>,
    /// <c>SpaceWeatherAdvisory</c>) and SWIM <c>collect:MeteorologicalBulletin</c> envelopes.
    /// </summary>
    internal sealed class IwxxmSuiteMapper : IIwxxmDocumentMapper
    {
        private static readonly XNamespace IwxxmNs = "http://icao.int/iwxxm/2025-2";
        private static readonly XNamespace GmlNs = "http://www.opengis.net/gml/3.2";
        private static readonly XNamespace AixmNs = "http://www.aixm.aero/schema/5.1.1";
        private static readonly XNamespace MetceNs = "http://def.wmo.int/metce/2013";
        private static readonly XNamespace CollectNs = "http://def.wmo.int/collect/2014";
        private static readonly XNamespace XlinkNs = "http://www.w3.org/1999/xlink";
        private static readonly XNamespace XsiNs = "http://www.w3.org/2001/XMLSchema-instance";

        // Mapping error codes for mandatory content that is absent or cannot be interpreted (never defaulted)
        internal const string IssueTimeMissingCode = "ACL-ISSUE-TIME-MISSING";
        internal const string ObservationTimeMissingCode = "ACL-OBSERVATION-TIME-MISSING";
        internal const string ValidPeriodMissingCode = "ACL-VALID-PERIOD-MISSING";
        internal const string TimeInvalidCode = "ACL-TIME-INVALID";
        internal const string AshDensityMissingCode = "ACL-ASH-DENSITY-MISSING";
        internal const string VolcanoNameMissingCode = "ACL-VOLCANO-NAME-MISSING";

        // UCUM units required by the official IWXXM 2025-2 rules for single-unit quantities
        private const string CelsiusUom = "Cel";
        private const string HectopascalUom = "hPa";
        private const string DegreeUom = "deg";
        private const string MeterUom = "m";
        private const string MilligramPerCubicMeterUom = "mg/m3";

        private readonly ISigwxDocumentMapper _sigwxMapper;
        private readonly IwxxmParserOptions _options;

        public IwxxmSuiteMapper(IwxxmParserOptions? options = null)
            : this(new SigwxDocumentMapper(options), options)
        {
        }

        public IwxxmSuiteMapper(ISigwxDocumentMapper sigwxMapper, IwxxmParserOptions? options = null)
        {
            _sigwxMapper = sigwxMapper ?? throw new ArgumentNullException(nameof(sigwxMapper));
            _options = options ?? IwxxmParserOptions.Default;
        }

        public Result<IIwxxmReport> Map(Stream xmlStream)
        {
            if (xmlStream == null)
                throw new ArgumentNullException(nameof(xmlStream));

            var readerSettings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                MaxCharactersInDocument = _options.MaxCharactersInDocument,
                MaxCharactersFromEntities = _options.MaxCharactersFromEntities,
                XmlResolver = null
            };

            XDocument doc;
            try
            {
                xmlStream.Position = 0;
                using (XmlReader reader = XmlReader.Create(xmlStream, readerSettings))
                {
                    doc = XDocument.Load(reader, LoadOptions.SetLineInfo);
                }
            }
            catch (XmlException ex)
            {
                return Result<IIwxxmReport>.Failure(
                    "XML-MALFORMED",
                    ex.Message,
                    ErrorCategory.MalformedInput);
            }

            XElement? root = doc.Root;
            if (root == null)
            {
                return Result<IIwxxmReport>.Failure(
                    "XML-EMPTY-ROOT",
                    "The XML document does not contain a root element.",
                    ErrorCategory.MalformedInput);
            }

            // Delegate SIGWX/WAFS directly to the dedicated SIGWX mapper (transport model + ACL)
            if (root.Name == IwxxmNs + "WAFSSignificantWeatherForecast" ||
                root.Name == IwxxmNs + "SIGWXReport")
            {
                xmlStream.Position = 0;
                var sigwxResult = _sigwxMapper.Map(xmlStream);
                return sigwxResult.IsSuccess
                    ? Result<IIwxxmReport>.Success(sigwxResult.Value)
                    : Result<IIwxxmReport>.Failure(sigwxResult.Errors);
            }

            return MapElementToReport(root);
        }

        private Result<IIwxxmReport> MapElementToReport(XElement root)
        {
            if (root.Name == CollectNs + "MeteorologicalBulletin")
            {
                return MapMeteorologicalBulletin(root);
            }

            if (root.Name.Namespace != IwxxmNs)
            {
                return Result<IIwxxmReport>.Failure(
                    "XML-UNSUPPORTED-NAMESPACE",
                    $"Unsupported root namespace '{root.Name.NamespaceName}' on element '{root.Name.LocalName}'.",
                    ErrorCategory.Deserialization);
            }

            switch (root.Name.LocalName)
            {
                case "WAFSSignificantWeatherForecast":
                case "SIGWXReport":
                {
                    using (var ms = new MemoryStream())
                    {
                        root.Save(ms);
                        ms.Position = 0;
                        var sigwxResult = _sigwxMapper.Map(ms);
                        return sigwxResult.IsSuccess
                            ? Result<IIwxxmReport>.Success(sigwxResult.Value)
                            : Result<IIwxxmReport>.Failure(sigwxResult.Errors);
                    }
                }
                case "METAR":
                    return WrapResult(MapMetarOrSpeci(root, IwxxmReportType.Metar));
                case "SPECI":
                    return WrapResult(MapMetarOrSpeci(root, IwxxmReportType.Speci));
                case "TAF":
                    return WrapResult(MapTaf(root));
                case "SIGMET":
                    return WrapResult(MapSigmet(root, IwxxmReportType.Sigmet));
                case "VolcanicAshSIGMET":
                    return WrapResult(MapSigmet(root, IwxxmReportType.VolcanicAshSigmet));
                case "TropicalCycloneSIGMET":
                    return WrapResult(MapSigmet(root, IwxxmReportType.TropicalCycloneSigmet));
                case "AIRMET":
                    return WrapResult(MapAirmet(root));
                case "VolcanoObservatoryNoticeForAviation":
                    return WrapResult(MapVona(root));
                case "QuantitativeVolcanicAshConcentrationInformation":
                    return WrapResult(MapQvaci(root));
                case "VolcanicAshAdvisory":
                    return WrapResult(MapVolcanicAshAdvisory(root));
                case "TropicalCycloneAdvisory":
                    return WrapResult(MapTropicalCycloneAdvisory(root));
                case "SpaceWeatherAdvisory":
                    return WrapResult(MapSpaceWeatherAdvisory(root));
                default:
                    return Result<IIwxxmReport>.Failure(
                        "XML-UNSUPPORTED-REPORT",
                        $"Unsupported IWXXM 2025-2 root element '{root.Name.LocalName}'.",
                        ErrorCategory.Deserialization);
            }
        }

        private static Result<IIwxxmReport> WrapResult<TReport>(Result<TReport> typedResult)
            where TReport : class, IIwxxmReport
        {
            return typedResult.IsSuccess
                ? Result<IIwxxmReport>.Success(typedResult.Value)
                : Result<IIwxxmReport>.Failure(typedResult.Errors);
        }

        // =====================================================================
        // 1. SWIM Collect MeteorologicalBulletin Mapper (TD-204)
        // =====================================================================

        private Result<IIwxxmReport> MapMeteorologicalBulletin(XElement bulletinElem)
        {
            string id = GetGmlId(bulletinElem);
            string bulletinIdentifier = bulletinElem.Element(CollectNs + "bulletinIdentifier")?.Value ?? string.Empty;

            var childResults = new List<Result<IIwxxmReport>>();
            var errors = new List<ValidationError>();

            foreach (XElement metInfo in bulletinElem.Elements(CollectNs + "meteorologicalInformation"))
            {
                XElement? reportElem = metInfo.Elements().FirstOrDefault();
                if (reportElem == null)
                    continue;

                Result<IIwxxmReport> childResult = MapElementToReport(reportElem);
                childResults.Add(childResult);
                if (childResult.IsFailure)
                {
                    errors.AddRange(childResult.Errors);
                }
            }

            if (errors.Count > 0)
            {
                return Result<IIwxxmReport>.Failure(errors);
            }

            var bulletinResult = MeteorologicalBulletin.Create(id, bulletinIdentifier, childResults);
            return WrapResult(bulletinResult);
        }

        // =====================================================================
        // 2. Aerodrome Reports Mappers: METAR, SPECI, TAF (TD-203)
        // =====================================================================

        private static Result<AerodromeWeatherReport> MapMetarOrSpeci(XElement root, IwxxmReportType reportType)
        {
            var errors = new List<ValidationError>();
            string id = GetGmlId(root);
            ReportMetadataCodes.Metadata metadata = ReportMetadataCodes.Read(root, id, errors);
            bool isAutomated = ParseBoolAttribute(root, "automatedStation");
            string? failedTac = root.Attribute("translationFailedTAC")?.Value;
            bool isTranslationFailed = failedTac != null || root.Attribute("translatedBulletinID") != null && root.Element(IwxxmNs + "observation") == null;

            DateTimeOffset issueTime = ReadIssueTime(root, id, errors);

            // observationTime is mandatory (metarSpeci.xsd): absent -> mapping error; xlink:href only -> null (unresolved).
            DateTimeOffset? obsTime = ReadRequiredInstant(
                root.Element(IwxxmNs + "observationTime"), "observationTime", ObservationTimeMissingCode, id, errors);

            ExtractAerodromeInfo(
                root.Element(IwxxmNs + "aerodrome"),
                errors,
                out string designator,
                out string? name,
                out GeoCoordinate? position);

            XElement? obsProp = root.Element(IwxxmNs + "observation");
            string? obsNilReason = obsProp?.Attribute("nilReason")?.Value;
            XElement? obsElem = obsProp?.Element(IwxxmNs + "MeteorologicalAerodromeObservation");
            bool isNilObs = obsElem == null && !string.IsNullOrWhiteSpace(obsNilReason);

            bool cavok = false;
            double? airTemp = null;
            double? dewpoint = null;
            double? qnh = null;
            SurfaceWindInfo? surfaceWind = null;
            double? visibility = null;
            var rvrs = new List<RunwayVisualRangeInfo>();
            var presentWeather = new List<string>();
            var cloudLayers = new List<AerodromeCloudLayerInfo>();

            if (obsElem != null)
            {
                cavok = ParseBoolAttribute(obsElem, "cloudAndVisibilityOK");
                airTemp = UomMeasureReader.ReadFixedUnit(obsElem.Element(IwxxmNs + "airTemperature"), CelsiusUom, id, errors);
                dewpoint = UomMeasureReader.ReadFixedUnit(obsElem.Element(IwxxmNs + "dewpointTemperature"), CelsiusUom, id, errors);
                qnh = UomMeasureReader.ReadFixedUnit(obsElem.Element(IwxxmNs + "qnh"), HectopascalUom, id, errors);
                surfaceWind = ParseSurfaceWind(obsElem.Element(IwxxmNs + "surfaceWind"), id, errors);
                visibility = UomMeasureReader.ReadFixedUnit(
                    obsElem.Element(IwxxmNs + "visibility")
                        ?.Element(IwxxmNs + "AerodromeHorizontalVisibility")
                        ?.Element(IwxxmNs + "prevailingVisibility"),
                    MeterUom,
                    id,
                    errors);

                foreach (XElement rvrProp in obsElem.Elements(IwxxmNs + "rvr"))
                {
                    XElement? rvrElem = rvrProp.Element(IwxxmNs + "AerodromeRunwayVisualRange");
                    if (rvrElem == null) continue;

                    // The runway may be given only by an xlink:href reference (not resolved): the designator is then null.
                    string? rwyDesig = GetText(rvrElem
                        .Element(IwxxmNs + "runway")
                        ?.Element(AixmNs + "RunwayDirection")
                        ?.Element(AixmNs + "timeSlice")
                        ?.Element(AixmNs + "RunwayDirectionTimeSlice")
                        ?.Element(AixmNs + "designator"));
                    double? meanRvr = UomMeasureReader.ReadFixedUnit(rvrElem.Element(IwxxmNs + "meanRVR"), MeterUom, id, errors);
                    string? tendency = rvrElem.Attribute("pastTendency")?.Value;
                    rvrs.Add(new RunwayVisualRangeInfo(rwyDesig, meanRvr, tendency));
                }

                foreach (XElement wxElem in obsElem.Elements(IwxxmNs + "presentWeather"))
                {
                    string? href = wxElem.Attribute(XlinkNs + "href")?.Value;
                    if (!string.IsNullOrWhiteSpace(href))
                    {
                        presentWeather.Add(href!);
                    }
                }

                ParseCloudLayers(obsElem.Element(IwxxmNs + "cloud"), cloudLayers, id, errors);
            }

            var trends = new List<AerodromeForecastGroupInfo>();
            foreach (XElement trendProp in root.Elements(IwxxmNs + "trendForecast"))
            {
                XElement? trendElem = trendProp.Element(IwxxmNs + "MeteorologicalAerodromeTrendForecast");
                if (trendElem != null)
                {
                    trends.Add(ParseForecastGroup(trendElem, errors));
                }
            }

            if (errors.Count > 0)
            {
                return Result<AerodromeWeatherReport>.Failure(errors);
            }

            return AerodromeWeatherReport.Create(
                id,
                reportType,
                issueTime,
                obsTime,
                metadata.Status,
                metadata.Usage,
                isAutomated,
                designator,
                name,
                position,
                isNilObs,
                obsNilReason,
                isTranslationFailed,
                failedTac,
                cavok,
                airTemp,
                dewpoint,
                qnh,
                surfaceWind,
                visibility,
                rvrs,
                presentWeather,
                cloudLayers,
                trends,
                metadata.Reason,
                metadata.Supplementary);
        }

        private static Result<TafReport> MapTaf(XElement root)
        {
            var errors = new List<ValidationError>();
            string id = GetGmlId(root);
            ReportMetadataCodes.Metadata metadata = ReportMetadataCodes.Read(root, id, errors);
            bool isCancel = ParseBoolAttribute(root, "isCancelReport");
            string? failedTac = root.Attribute("translationFailedTAC")?.Value;
            bool isTranslationFailed = failedTac != null;

            DateTimeOffset issueTime = ReadIssueTime(root, id, errors);

            ExtractAerodromeInfo(
                root.Element(IwxxmNs + "aerodrome"),
                errors,
                out string designator,
                out string? name,
                out GeoCoordinate? position);

            // validPeriod and cancelledReportValidPeriod are minOccurs="0" (taf.xsd): optional.
            ReadPeriod(root.Element(IwxxmNs + "validPeriod"), "validPeriod", null, id, errors, out DateTimeOffset? validFrom, out DateTimeOffset? validUntil);
            ReadPeriod(root.Element(IwxxmNs + "cancelledReportValidPeriod"), "cancelledReportValidPeriod", null, id, errors, out DateTimeOffset? cancelFrom, out DateTimeOffset? cancelUntil);

            XElement? baseProp = root.Element(IwxxmNs + "baseForecast");
            string? baseNilReason = baseProp?.Attribute("nilReason")?.Value;
            XElement? baseElem = baseProp?.Element(IwxxmNs + "MeteorologicalAerodromeForecast");
            bool isNilForecast = baseElem == null && !string.IsNullOrWhiteSpace(baseNilReason);

            AerodromeForecastGroupInfo? baseForecast = baseElem != null ? ParseForecastGroup(baseElem, errors) : null;

            var changes = new List<AerodromeForecastGroupInfo>();
            foreach (XElement changeProp in root.Elements(IwxxmNs + "changeForecast"))
            {
                XElement? changeElem = changeProp.Element(IwxxmNs + "MeteorologicalAerodromeForecast");
                if (changeElem != null)
                {
                    changes.Add(ParseForecastGroup(changeElem, errors));
                }
            }

            if (errors.Count > 0)
            {
                return Result<TafReport>.Failure(errors);
            }

            return TafReport.Create(
                id,
                issueTime,
                metadata.Status,
                metadata.Usage,
                designator,
                name,
                position,
                validFrom,
                validUntil,
                isCancel,
                cancelFrom,
                cancelUntil,
                isNilForecast,
                baseNilReason,
                isTranslationFailed,
                failedTac,
                baseForecast,
                changes,
                metadata.Reason,
                metadata.Supplementary);
        }

        // =====================================================================
        // 3. En-Route Hazard Mappers: SIGMET, VA/TC SIGMET, AIRMET (TD-202)
        // =====================================================================

        private static Result<SigmetReport> MapSigmet(XElement root, IwxxmReportType reportType)
        {
            var errors = new List<ValidationError>();
            string id = GetGmlId(root);
            ReportMetadataCodes.Metadata metadata = ReportMetadataCodes.Read(root, id, errors);
            bool isCancel = ParseBoolAttribute(root, "isCancelReport");
            string? failedTac = root.Attribute("translationFailedTAC")?.Value;
            bool isTranslationFailed = failedTac != null;

            DateTimeOffset issueTime = ReadIssueTime(root, id, errors);
            ExtractUnitInfo(root.Element(IwxxmNs + "issuingAirTrafficServicesUnit"), out string? atsDesig, out string? atsName);
            ExtractUnitInfo(root.Element(IwxxmNs + "originatingMeteorologicalWatchOffice"), out string? mwoDesig, out _);
            ExtractAirspaceInfo(root.Element(IwxxmNs + "issuingAirTrafficServicesRegion"), out string? firDesig, out string? firName);

            string? seqNumber = GetText(root.Element(IwxxmNs + "sequenceNumber"));
            string? cancelSeqNumber = GetText(root.Element(IwxxmNs + "cancelledReportSequenceNumber"));

            // validPeriod is mandatory (sigmet.xsd): absent -> mapping error; xlink:href only -> null (unresolved).
            ReadPeriod(root.Element(IwxxmNs + "validPeriod"), "validPeriod", ValidPeriodMissingCode, id, errors, out DateTimeOffset? validFrom, out DateTimeOffset? validUntil);
            string? phenomenonUri = root.Element(IwxxmNs + "phenomenon")?.Attribute(XlinkNs + "href")?.Value;

            var evolvingConditions = new List<EnRoutePhenomenonVolume>();
            var forecastPositions = new List<EnRoutePhenomenonVolume>();

            foreach (XElement analysisCol in root.Elements(IwxxmNs + "analysisCollection"))
            {
                foreach (XElement posAnalysis in analysisCol.Elements(IwxxmNs + "analysisAndForecastPositionAnalysis"))
                {
                    XElement? evolvingCol = posAnalysis
                        .Element(IwxxmNs + "analysis")
                        ?.Element(IwxxmNs + "SIGMETEvolvingConditionCollection");

                    if (evolvingCol != null)
                    {
                        foreach (XElement member in evolvingCol.Elements(IwxxmNs + "member"))
                        {
                            XElement? cond = member.Element(IwxxmNs + "SIGMETEvolvingCondition");
                            if (cond != null)
                            {
                                evolvingConditions.Add(ParseEnRouteConditionVolume(cond, errors));
                            }
                        }
                    }

                    XElement? forecastCol = posAnalysis
                        .Element(IwxxmNs + "forecastPositionAnalysis")
                        ?.Element(IwxxmNs + "SIGMETPositionCollection");

                    if (forecastCol != null)
                    {
                        foreach (XElement member in forecastCol.Elements(IwxxmNs + "member"))
                        {
                            XElement? pos = member.Element(IwxxmNs + "SIGMETPosition");
                            if (pos != null)
                            {
                                forecastPositions.Add(ParseEnRouteConditionVolume(pos, errors));
                            }
                        }
                    }
                }
            }

            Volcano? eruptingVolcano = ParseMetceVolcano(root.Element(IwxxmNs + "eruptingVolcano"), errors);
            string? tcName = root
                .Element(IwxxmNs + "tropicalCyclone")
                ?.Element(MetceNs + "TropicalCyclone")
                ?.Element(MetceNs + "name")?.Value;

            if (errors.Count > 0)
            {
                return Result<SigmetReport>.Failure(errors);
            }

            return SigmetReport.Create(
                id,
                reportType,
                issueTime,
                metadata.Status,
                metadata.Usage,
                atsDesig ?? string.Empty,
                atsName,
                mwoDesig,
                firDesig,
                firName,
                seqNumber,
                validFrom,
                validUntil,
                phenomenonUri,
                isCancel,
                cancelSeqNumber,
                isTranslationFailed,
                failedTac,
                eruptingVolcano,
                tcName,
                evolvingConditions,
                forecastPositions,
                metadata.Reason,
                metadata.Supplementary);
        }

        private static Result<AirmetReport> MapAirmet(XElement root)
        {
            var errors = new List<ValidationError>();
            string id = GetGmlId(root);
            ReportMetadataCodes.Metadata metadata = ReportMetadataCodes.Read(root, id, errors);
            string? failedTac = root.Attribute("translationFailedTAC")?.Value;
            bool isTranslationFailed = failedTac != null;

            DateTimeOffset issueTime = ReadIssueTime(root, id, errors);
            ExtractUnitInfo(root.Element(IwxxmNs + "issuingAirTrafficServicesUnit"), out string? atsDesig, out string? atsName);
            ExtractUnitInfo(root.Element(IwxxmNs + "originatingMeteorologicalWatchOffice"), out string? mwoDesig, out _);
            ExtractAirspaceInfo(root.Element(IwxxmNs + "issuingAirTrafficServicesRegion"), out string? firDesig, out string? firName);

            string? seqNumber = GetText(root.Element(IwxxmNs + "sequenceNumber"));

            // validPeriod is mandatory (airmet.xsd): absent -> mapping error; xlink:href only -> null (unresolved).
            ReadPeriod(root.Element(IwxxmNs + "validPeriod"), "validPeriod", ValidPeriodMissingCode, id, errors, out DateTimeOffset? validFrom, out DateTimeOffset? validUntil);
            string? phenomenonUri = root.Element(IwxxmNs + "phenomenon")?.Attribute(XlinkNs + "href")?.Value;

            var evolvingConditions = new List<EnRoutePhenomenonVolume>();
            XElement? evolvingCol = root
                .Element(IwxxmNs + "analysis")
                ?.Element(IwxxmNs + "AIRMETEvolvingConditionCollection");

            if (evolvingCol != null)
            {
                foreach (XElement member in evolvingCol.Elements(IwxxmNs + "member"))
                {
                    XElement? cond = member.Element(IwxxmNs + "AIRMETEvolvingCondition");
                    if (cond != null)
                    {
                        evolvingConditions.Add(ParseEnRouteConditionVolume(cond, errors));
                    }
                }
            }

            if (errors.Count > 0)
            {
                return Result<AirmetReport>.Failure(errors);
            }

            return AirmetReport.Create(
                id,
                issueTime,
                metadata.Status,
                metadata.Usage,
                atsDesig ?? string.Empty,
                atsName,
                mwoDesig,
                firDesig,
                firName,
                seqNumber,
                validFrom,
                validUntil,
                phenomenonUri,
                isTranslationFailed,
                failedTac,
                evolvingConditions,
                metadata.Reason,
                metadata.Supplementary);
        }

        // =====================================================================
        // 4. Volcanic & Space Weather Mappers: VONA, QVACI, VAA, TCA, SWX (TD-202)
        // =====================================================================

        private static Result<VonaReport> MapVona(XElement root)
        {
            var errors = new List<ValidationError>();
            string id = GetGmlId(root);
            ReportMetadataCodes.Metadata metadata = ReportMetadataCodes.Read(root, id, errors);
            string? failedTac = root.Attribute("translationFailedTAC")?.Value;
            bool isTranslationFailed = failedTac != null;

            DateTimeOffset issueTime = ReadIssueTime(root, id, errors);
            ExtractUnitInfo(root.Element(IwxxmNs + "originatingCentre"), out string? centreDesig, out string? centreName);

            var volcanoes = new List<Volcano>();
            VerticalLimit? ashLevel = null;

            foreach (XElement featProp in root.Elements(IwxxmNs + "feature"))
            {
                XElement? feat = featProp.Element(IwxxmNs + "MeteorologicalFeature");
                if (feat == null) continue;

                string featId = GetGmlId(feat);
                string? phenHref = feat.Element(IwxxmNs + "phenomenon")?.Attribute(XlinkNs + "href")?.Value;

                if (phenHref != null && phenHref.EndsWith("/VOLCANO", StringComparison.OrdinalIgnoreCase))
                {
                    XElement? ptElem = feat.Element(IwxxmNs + "phenomenonGeometry")?.Element(GmlNs + "Point");
                    GeoCoordinate? pos = ParseGmlPointElement(ptElem, featId, errors);
                    XElement? volElem = feat.Element(IwxxmNs + "phenomenonProperty")?.Element(IwxxmNs + "Volcano");
                    if (pos.HasValue && volElem != null)
                    {
                        AddIwxxmVolcano(volElem, featId, pos.Value, volcanoes, errors);
                    }
                }
                else if (phenHref != null && phenHref.EndsWith("/VOLCANIC_ASH", StringComparison.OrdinalIgnoreCase))
                {
                    XElement? levelElem = feat
                        .Element(IwxxmNs + "phenomenonGeometry")
                        ?.Element(IwxxmNs + "ElevatedLevel");

                    ashLevel = VerticalLimitReader.Read(
                        levelElem?.Element(IwxxmNs + "elevation"),
                        levelElem?.Element(IwxxmNs + "verticalReference"),
                        featId,
                        errors);
                }
            }

            if (errors.Count > 0)
            {
                return Result<VonaReport>.Failure(errors);
            }

            return VonaReport.Create(
                id,
                issueTime,
                metadata.Status,
                metadata.Usage,
                centreName ?? centreDesig ?? string.Empty,
                centreDesig,
                GetText(root.Element(IwxxmNs + "stateOrRegion")),
                GetText(root.Element(IwxxmNs + "noticeNumber")),
                root.Element(IwxxmNs + "currentColourCode")?.Attribute(XlinkNs + "href")?.Value,
                root.Element(IwxxmNs + "previousColourCode")?.Attribute(XlinkNs + "href")?.Value,
                GetText(root.Element(IwxxmNs + "contacts")),
                GetText(root.Element(IwxxmNs + "remarks")),
                GetText(root.Element(IwxxmNs + "nextNotice")),
                isTranslationFailed,
                failedTac,
                volcanoes,
                ashLevel,
                metadata.Reason,
                metadata.Supplementary);
        }

        private static Result<QvaciReport> MapQvaci(XElement root)
        {
            var errors = new List<ValidationError>();
            string id = GetGmlId(root);
            ReportMetadataCodes.Metadata metadata = ReportMetadataCodes.Read(root, id, errors);
            string? failedTac = root.Attribute("translationFailedTAC")?.Value;
            bool isTranslationFailed = failedTac != null;

            DateTimeOffset issueTime = ReadIssueTime(root, id, errors);
            ExtractUnitInfo(root.Element(IwxxmNs + "originatingCentre"), out string? centreDesig, out string? centreName);

            var volcanoes = new List<Volcano>();
            var ashClouds = new List<VolcanicAshConcentrationCloud>();

            foreach (XElement featProp in root.Elements(IwxxmNs + "feature"))
            {
                XElement? feat = featProp.Element(IwxxmNs + "MeteorologicalFeature");
                if (feat == null) continue;

                string featId = GetGmlId(feat);
                string? phenHref = feat.Element(IwxxmNs + "phenomenon")?.Attribute(XlinkNs + "href")?.Value;

                if (phenHref != null && phenHref.EndsWith("/VOLCANO", StringComparison.OrdinalIgnoreCase))
                {
                    XElement? ptElem = feat.Element(IwxxmNs + "phenomenonGeometry")?.Element(GmlNs + "Point");
                    GeoCoordinate? pos = ParseGmlPointElement(ptElem, featId, errors);
                    XElement? volElem = feat.Element(IwxxmNs + "phenomenonProperty")?.Element(IwxxmNs + "Volcano");
                    if (pos.HasValue && volElem != null)
                    {
                        AddIwxxmVolcano(volElem, featId, pos.Value, volcanoes, errors);
                    }
                }
                else if (phenHref != null && phenHref.EndsWith("/VOLCANIC_ASH", StringComparison.OrdinalIgnoreCase))
                {
                    XElement? volGeom = feat.Element(IwxxmNs + "phenomenonGeometry")?.Element(IwxxmNs + "ElevatedVolume");
                    XElement? concProp = feat
                        .Element(IwxxmNs + "phenomenonProperty")
                        ?.Element(IwxxmNs + "VolcanicAshCloudConcentration");

                    if (volGeom != null && concProp != null)
                    {
                        GeoPolygon? poly = ParseGmlPatchesPolygon(volGeom.Element(GmlNs + "patches"), featId, errors);
                        VerticalLimit? lower = VerticalLimitReader.Read(
                            volGeom.Element(IwxxmNs + "lowerElevation"), volGeom.Element(IwxxmNs + "lowerVerticalReference"), featId, errors);
                        VerticalLimit? upper = VerticalLimitReader.Read(
                            volGeom.Element(IwxxmNs + "upperElevation"), volGeom.Element(IwxxmNs + "upperVerticalReference"), featId, errors);

                        // ashDensityEqualsOrExceeds is a mandatory gml:MeasureType (qvaci.xsd): never defaulted to 0.
                        double? density = UomMeasureReader.ReadFixedUnit(
                            concProp.Element(IwxxmNs + "ashDensityEqualsOrExceeds"), MilligramPerCubicMeterUom, featId, errors);
                        if (!density.HasValue)
                        {
                            errors.Add(MappingError(
                                AshDensityMissingCode,
                                $"The mandatory ashDensityEqualsOrExceeds of ash cloud '{featId}' is missing or nil; no density is assumed.",
                                featId));
                        }
                        else if (poly != null)
                        {
                            ashClouds.Add(new VolcanicAshConcentrationCloud(featId, poly, lower, upper, density.Value));
                        }
                    }
                }
            }

            DateTimeOffset? modelInitTime = ReadOptionalInstant(root.Element(IwxxmNs + "dispersionModelInitialisationTime"), "dispersionModelInitialisationTime", id, errors);

            if (errors.Count > 0)
            {
                return Result<QvaciReport>.Failure(errors);
            }

            return QvaciReport.Create(
                id,
                issueTime,
                metadata.Status,
                metadata.Usage,
                centreName ?? centreDesig ?? string.Empty,
                centreDesig,
                modelInitTime,
                GetText(root.Element(IwxxmNs + "remarks")),
                isTranslationFailed,
                failedTac,
                volcanoes,
                ashClouds,
                metadata.Reason,
                metadata.Supplementary);
        }

        private static Result<VolcanicAshAdvisoryReport> MapVolcanicAshAdvisory(XElement root)
        {
            var errors = new List<ValidationError>();
            string id = GetGmlId(root);
            ReportMetadataCodes.Metadata metadata = ReportMetadataCodes.Read(root, id, errors);
            string? failedTac = root.Attribute("translationFailedTAC")?.Value;
            bool isTranslationFailed = failedTac != null;

            DateTimeOffset issueTime = ReadIssueTime(root, id, errors);
            ExtractUnitInfo(root.Element(IwxxmNs + "issuingVolcanicAshAdvisoryCentre"), out string? vaacDesig, out string? vaacName);

            Volcano? volcano = ParseMetceVolcano(root.Element(IwxxmNs + "volcano"), errors);
            Length? sourceElev = UomMeasureReader.ReadLength(root.Element(IwxxmNs + "sourceElevationAMSL"), id, errors);

            var observedClouds = new List<EnRoutePhenomenonVolume>();
            XElement? obsCond = root
                .Element(IwxxmNs + "observation")
                ?.Element(IwxxmNs + "VolcanicAshObservedOrEstimatedConditions");

            if (obsCond != null)
            {
                foreach (XElement ashProp in obsCond.Elements(IwxxmNs + "ashCloud"))
                {
                    XElement? ashElem = ashProp.Element(IwxxmNs + "VolcanicAshCloudObservedOrEstimated");
                    if (ashElem != null)
                    {
                        observedClouds.Add(ParseAshCloudVolume(ashElem, errors));
                    }
                }
            }

            var forecastClouds = new List<EnRoutePhenomenonVolume>();
            foreach (XElement fcstProp in root.Elements(IwxxmNs + "forecast"))
            {
                XElement? fcstCond = fcstProp.Element(IwxxmNs + "VolcanicAshForecastConditions");
                if (fcstCond == null) continue;

                foreach (XElement ashProp in fcstCond.Elements(IwxxmNs + "ashCloud"))
                {
                    XElement? ashElem = ashProp.Element(IwxxmNs + "VolcanicAshCloudForecast");
                    if (ashElem != null)
                    {
                        forecastClouds.Add(ParseAshCloudVolume(ashElem, errors));
                    }
                }
            }

            DateTimeOffset? nextAdvisory = ReadOptionalInstant(root.Element(IwxxmNs + "nextAdvisoryTime"), "nextAdvisoryTime", id, errors);

            if (errors.Count > 0)
            {
                return Result<VolcanicAshAdvisoryReport>.Failure(errors);
            }

            return VolcanicAshAdvisoryReport.Create(
                id,
                issueTime,
                metadata.Status,
                metadata.Usage,
                vaacName ?? vaacDesig ?? string.Empty,
                volcano,
                GetText(root.Element(IwxxmNs + "stateOrRegion")),
                sourceElev,
                GetText(root.Element(IwxxmNs + "advisoryNumber")),
                GetText(root.Element(IwxxmNs + "informationSource")),
                GetText(root.Element(IwxxmNs + "eruptionDetails")),
                GetText(root.Element(IwxxmNs + "remarks")),
                nextAdvisory,
                isTranslationFailed,
                failedTac,
                observedClouds,
                forecastClouds,
                metadata.Reason,
                metadata.Supplementary);
        }

        private static Result<TropicalCycloneAdvisoryReport> MapTropicalCycloneAdvisory(XElement root)
        {
            var errors = new List<ValidationError>();
            string id = GetGmlId(root);
            ReportMetadataCodes.Metadata metadata = ReportMetadataCodes.Read(root, id, errors);
            string? failedTac = root.Attribute("translationFailedTAC")?.Value;
            bool isTranslationFailed = failedTac != null;

            DateTimeOffset issueTime = ReadIssueTime(root, id, errors);
            ExtractUnitInfo(root.Element(IwxxmNs + "issuingTropicalCycloneAdvisoryCentre"), out string? tcacDesig, out string? tcacName);

            string? cycloneName = root
                .Element(IwxxmNs + "tropicalCycloneName")
                ?.Element(MetceNs + "TropicalCyclone")
                ?.Element(MetceNs + "name")?.Value;

            TropicalCycloneTrackPoint? obsPoint = null;
            XElement? obsElem = root
                .Element(IwxxmNs + "observation")
                ?.Element(IwxxmNs + "TropicalCycloneObservedConditions");

            if (obsElem != null)
            {
                DateTimeOffset? obsTime = ReadOptionalInstant(obsElem.Element(IwxxmNs + "phenomenonTime"), "phenomenonTime", id, errors);
                GeoCoordinate? pos = ParseGmlPointElement(
                    obsElem.Element(IwxxmNs + "tropicalCyclonePosition")?.Element(GmlNs + "Point"),
                    GetGmlId(obsElem),
                    errors);
                Speed? maxWind = UomMeasureReader.ReadSpeed(obsElem.Element(IwxxmNs + "maximumSurfaceWindSpeed"), id, errors);
                double? pressure = UomMeasureReader.ReadFixedUnit(obsElem.Element(IwxxmNs + "centralPressure"), HectopascalUom, id, errors);

                if (pos.HasValue)
                {
                    obsPoint = new TropicalCycloneTrackPoint(obsTime, pos.Value, maxWind, pressure);
                }
            }

            var forecasts = new List<TropicalCycloneTrackPoint>();
            foreach (XElement fcstProp in root.Elements(IwxxmNs + "forecast"))
            {
                XElement? fcstElem = fcstProp.Element(IwxxmNs + "TropicalCycloneForecastConditions");
                if (fcstElem == null) continue;

                DateTimeOffset? fcstTime = ReadOptionalInstant(fcstElem.Element(IwxxmNs + "phenomenonTime"), "phenomenonTime", id, errors);
                GeoCoordinate? pos = ParseGmlPointElement(
                    fcstElem.Element(IwxxmNs + "tropicalCyclonePosition")?.Element(GmlNs + "Point"),
                    GetGmlId(fcstElem),
                    errors);
                Speed? maxWind = UomMeasureReader.ReadSpeed(fcstElem.Element(IwxxmNs + "maximumSurfaceWindSpeed"), id, errors);

                if (pos.HasValue)
                {
                    forecasts.Add(new TropicalCycloneTrackPoint(fcstTime, pos.Value, maxWind));
                }
            }

            DateTimeOffset? nextAdvisory = ReadOptionalInstant(root.Element(IwxxmNs + "nextAdvisoryTime"), "nextAdvisoryTime", id, errors);

            if (errors.Count > 0)
            {
                return Result<TropicalCycloneAdvisoryReport>.Failure(errors);
            }

            return TropicalCycloneAdvisoryReport.Create(
                id,
                issueTime,
                metadata.Status,
                metadata.Usage,
                tcacDesig ?? tcacName ?? string.Empty,
                cycloneName,
                GetText(root.Element(IwxxmNs + "advisoryNumber")),
                obsPoint,
                forecasts,
                GetText(root.Element(IwxxmNs + "remarks")),
                nextAdvisory,
                isTranslationFailed,
                failedTac,
                metadata.Reason,
                metadata.Supplementary);
        }

        private static Result<SpaceWeatherAdvisoryReport> MapSpaceWeatherAdvisory(XElement root)
        {
            var errors = new List<ValidationError>();
            string id = GetGmlId(root);
            ReportMetadataCodes.Metadata metadata = ReportMetadataCodes.Read(root, id, errors);
            string? failedTac = root.Attribute("translationFailedTAC")?.Value;
            bool isTranslationFailed = failedTac != null;

            DateTimeOffset issueTime = ReadIssueTime(root, id, errors);
            ExtractUnitInfo(root.Element(IwxxmNs + "issuingSpaceWeatherCentre"), out string? swxcDesig, out string? swxcName);

            var replacedNumbers = root
                .Elements(IwxxmNs + "replacedAdvisoryNumber")
                .Select(e => e.Value)
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .ToList();

            var effects = root
                .Elements(IwxxmNs + "effect")
                .Select(e => e.Value)
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .ToList();

            var analyses = new List<SpaceWeatherAnalysisRegion>();
            foreach (XElement analysisProp in root.Elements(IwxxmNs + "analysis"))
            {
                XElement? analysisElem = analysisProp.Element(IwxxmNs + "SpaceWeatherAnalysis");
                if (analysisElem == null) continue;

                string analysisId = GetGmlId(analysisElem);
                string? timeInd = analysisElem.Attribute("timeIndicator")?.Value;
                DateTimeOffset? phenTime = ReadOptionalInstant(analysisElem.Element(IwxxmNs + "phenomenonTime"), "phenomenonTime", analysisId, errors);

                foreach (XElement irProp in analysisElem.Elements(IwxxmNs + "intensityAndRegion"))
                {
                    XElement? irElem = irProp.Element(IwxxmNs + "SpaceWeatherIntensityAndRegion");
                    if (irElem == null) continue;

                    string? intensity = GetText(irElem.Element(IwxxmNs + "intensity"));
                    var locUris = new List<string>();
                    var polygons = new List<GeoPolygon>();

                    foreach (XElement regProp in irElem.Elements(IwxxmNs + "region"))
                    {
                        XElement? regElem = regProp.Element(IwxxmNs + "SpaceWeatherRegion");
                        if (regElem == null) continue;

                        foreach (XElement locInd in regElem.Elements(IwxxmNs + "locationIndicator"))
                        {
                            string? href = locInd.Attribute(XlinkNs + "href")?.Value;
                            if (!string.IsNullOrWhiteSpace(href))
                            {
                                locUris.Add(href!);
                            }
                        }

                        XElement? patches = regElem
                            .Element(IwxxmNs + "location")
                            ?.Element(AixmNs + "AirspaceVolume")
                            ?.Element(AixmNs + "horizontalProjection")
                            ?.Element(AixmNs + "Surface")
                            ?.Element(GmlNs + "patches");

                        if (patches != null)
                        {
                            GeoPolygon? poly = ParseGmlPatchesPolygon(patches, GetGmlId(regElem), errors);
                            if (poly != null)
                            {
                                polygons.Add(poly);
                            }
                        }
                    }

                    analyses.Add(new SpaceWeatherAnalysisRegion(analysisId, timeInd, phenTime, intensity, locUris, polygons));
                }
            }

            DateTimeOffset? nextAdvisory = ReadOptionalInstant(root.Element(IwxxmNs + "nextAdvisoryTime"), "nextAdvisoryTime", id, errors);

            if (errors.Count > 0)
            {
                return Result<SpaceWeatherAdvisoryReport>.Failure(errors);
            }

            return SpaceWeatherAdvisoryReport.Create(
                id,
                issueTime,
                metadata.Status,
                metadata.Usage,
                swxcName ?? swxcDesig ?? string.Empty,
                GetText(root.Element(IwxxmNs + "advisoryNumber")),
                replacedNumbers,
                effects,
                analyses,
                GetText(root.Element(IwxxmNs + "remarks")),
                nextAdvisory,
                isTranslationFailed,
                failedTac,
                metadata.Reason,
                metadata.Supplementary);
        }

        // =====================================================================
        // Shared XML & GML 3.2.1 Spatial Helper Methods
        // =====================================================================

        private static string GetGmlId(XElement elem)
        {
            return elem.Attribute(GmlNs + "id")?.Value ?? string.Empty;
        }

        /// <summary>
        /// Returns the trimmed text of an element, or <c>null</c> when the element is absent, <c>xsi:nil</c>, carries a
        /// <c>nilReason</c> without content, or is empty (a nil string is never turned into an empty string).
        /// </summary>
        private static string? GetText(XElement? elem)
        {
            if (elem == null || IsXsiNil(elem))
                return null;

            string value = elem.Value.Trim();
            return value.Length == 0 ? null : value;
        }

        private static bool IsXsiNil(XElement elem)
        {
            string? nil = elem.Attribute(XsiNs + "nil")?.Value;
            return string.Equals(nil, "true", StringComparison.Ordinal) || string.Equals(nil, "1", StringComparison.Ordinal);
        }

        private static bool ParseBoolAttribute(XElement elem, string attrName)
        {
            string? val = elem.Attribute(attrName)?.Value;
            return string.Equals(val, "true", StringComparison.OrdinalIgnoreCase) || val == "1";
        }

        private static ValidationError MappingError(string code, string message, string location) =>
            new ValidationError(code, message, ErrorCategory.DomainMapping, ValidationSeverity.Error, string.IsNullOrEmpty(location) ? null : location);

        /// <summary>
        /// Reads the mandatory <c>issueTime</c>. An absent element, or one given only by an <c>xlink:href</c> reference
        /// (not resolved by this version), is a mapping error: the issue time is never invented.
        /// </summary>
        private static DateTimeOffset ReadIssueTime(XElement root, string id, List<ValidationError> errors)
        {
            XElement? prop = root.Element(IwxxmNs + "issueTime");
            if (prop == null)
            {
                errors.Add(MappingError(IssueTimeMissingCode, "The mandatory element 'issueTime' is missing; no issue time is assumed.", id));
                return default;
            }

            XElement? instant = prop.Element(GmlNs + "TimeInstant");
            if (instant == null)
            {
                string? href = prop.Attribute(XlinkNs + "href")?.Value;
                errors.Add(MappingError(
                    IssueTimeMissingCode,
                    href != null
                        ? $"The mandatory element 'issueTime' is given only by the reference xlink:href='{href}', which is not resolved; no issue time is assumed."
                        : "The mandatory element 'issueTime' has no gml:TimeInstant; no issue time is assumed.",
                    id));
                return default;
            }

            return ParseTimePosition(instant.Element(GmlNs + "timePosition")?.Value, "issueTime", id, errors) ?? default;
        }

        /// <summary>
        /// Reads a mandatory time-instant property: absent -> mapping error <paramref name="missingCode"/>; given by an
        /// unresolved <c>xlink:href</c> or nil -> <c>null</c>; inline -> the parsed UTC instant.
        /// </summary>
        private static DateTimeOffset? ReadRequiredInstant(XElement? prop, string field, string missingCode, string id, List<ValidationError> errors)
        {
            if (prop == null)
            {
                errors.Add(MappingError(missingCode, $"The mandatory element '{field}' is missing; no value is assumed.", id));
                return null;
            }

            return ReadOptionalInstant(prop, field, id, errors);
        }

        /// <summary>Reads an optional time-instant property: absent, nil or <c>xlink:href</c>-only -> <c>null</c>.</summary>
        private static DateTimeOffset? ReadOptionalInstant(XElement? prop, string field, string id, List<ValidationError> errors)
        {
            XElement? instant = prop?.Element(GmlNs + "TimeInstant");
            if (instant == null)
                return null;

            return ParseTimePosition(instant.Element(GmlNs + "timePosition")?.Value, field, id, errors);
        }

        /// <summary>
        /// Reads a time-period property. When <paramref name="missingCode"/> is given the property is mandatory and its
        /// absence is a mapping error; an <c>xlink:href</c>-only or nil property yields <c>null</c> bounds.
        /// </summary>
        private static void ReadPeriod(
            XElement? prop,
            string field,
            string? missingCode,
            string id,
            List<ValidationError> errors,
            out DateTimeOffset? beginUtc,
            out DateTimeOffset? endUtc)
        {
            beginUtc = null;
            endUtc = null;

            if (prop == null)
            {
                if (missingCode != null)
                {
                    errors.Add(MappingError(missingCode, $"The mandatory element '{field}' is missing; no period is assumed.", id));
                }

                return;
            }

            XElement? periodElem = prop.Element(GmlNs + "TimePeriod");
            if (periodElem == null)
                return;

            beginUtc = ParseTimePosition(periodElem.Element(GmlNs + "beginPosition")?.Value, field + "/beginPosition", id, errors);
            endUtc = ParseTimePosition(periodElem.Element(GmlNs + "endPosition")?.Value, field + "/endPosition", id, errors);
        }

        private static DateTimeOffset? ParseTimePosition(string? text, string field, string id, List<ValidationError> errors)
        {
            if (string.IsNullOrWhiteSpace(text))
                return null;

            if (DateTimeOffset.TryParse(text!.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out DateTimeOffset dt))
                return dt.ToUniversalTime();

            errors.Add(MappingError(TimeInvalidCode, $"The time position '{text.Trim()}' of '{field}' is not a valid ISO 8601 date-time.", id));
            return null;
        }

        private static void ExtractAerodromeInfo(
            XElement? aerodromeProp,
            List<ValidationError> errors,
            out string designator,
            out string? name,
            out GeoCoordinate? position)
        {
            designator = string.Empty;
            name = null;
            position = null;

            XElement? slice = aerodromeProp
                ?.Element(AixmNs + "AirportHeliport")
                ?.Element(AixmNs + "timeSlice")
                ?.Element(AixmNs + "AirportHeliportTimeSlice");

            if (slice == null)
                return;

            designator = GetText(slice.Element(AixmNs + "designator"))
                         ?? GetText(slice.Element(AixmNs + "locationIndicatorICAO"))
                         ?? string.Empty;
            name = GetText(slice.Element(AixmNs + "name"));

            XElement? arpPt = slice
                .Element(AixmNs + "ARP")
                ?.Element(AixmNs + "ElevatedPoint");

            if (arpPt != null)
            {
                position = ParseGmlPointElement(arpPt, GetGmlId(arpPt), errors);
            }
        }

        /// <summary>
        /// Reads an <c>aixm:Unit</c> time slice. The designator is <c>null</c> when <c>aixm:designator</c> is absent
        /// (it is not replaced by the unit name).
        /// </summary>
        private static void ExtractUnitInfo(
            XElement? unitProp,
            out string? designator,
            out string? name)
        {
            designator = null;
            name = null;

            XElement? slice = unitProp
                ?.Element(AixmNs + "Unit")
                ?.Element(AixmNs + "timeSlice")
                ?.Element(AixmNs + "UnitTimeSlice");

            if (slice == null)
                return;

            name = GetText(slice.Element(AixmNs + "name"));
            designator = GetText(slice.Element(AixmNs + "designator"));
        }

        private static void ExtractAirspaceInfo(
            XElement? airspaceProp,
            out string? designator,
            out string? name)
        {
            designator = null;
            name = null;

            XElement? slice = airspaceProp
                ?.Element(AixmNs + "Airspace")
                ?.Element(AixmNs + "timeSlice")
                ?.Element(AixmNs + "AirspaceTimeSlice");

            if (slice == null)
                return;

            designator = GetText(slice.Element(AixmNs + "designator"));
            name = GetText(slice.Element(AixmNs + "name"));
        }

        private static SurfaceWindInfo? ParseSurfaceWind(XElement? surfaceWindProp, string context, List<ValidationError> errors)
        {
            XElement? windElem = surfaceWindProp?.Element(IwxxmNs + "AerodromeSurfaceWind")
                                 ?? surfaceWindProp?.Element(IwxxmNs + "AerodromeSurfaceWindForecast");
            if (windElem == null)
                return null;

            bool isVar = ParseBoolAttribute(windElem, "variableWindDirection");
            double? dir = UomMeasureReader.ReadFixedUnit(windElem.Element(IwxxmNs + "meanWindDirection"), DegreeUom, context, errors);
            Speed? spd = UomMeasureReader.ReadSpeed(windElem.Element(IwxxmNs + "meanWindSpeed"), context, errors);
            Speed? gust = UomMeasureReader.ReadSpeed(windElem.Element(IwxxmNs + "windGustSpeed"), context, errors);
            return new SurfaceWindInfo(isVar, dir, spd, gust);
        }

        private static void ParseCloudLayers(
            XElement? cloudProp,
            List<AerodromeCloudLayerInfo> target,
            string context,
            List<ValidationError> errors)
        {
            XElement? cloudElem = cloudProp?.Element(IwxxmNs + "AerodromeCloud")
                                  ?? cloudProp?.Element(IwxxmNs + "AerodromeCloudForecast");
            if (cloudElem == null)
                return;

            foreach (XElement layerProp in cloudElem.Elements(IwxxmNs + "layer"))
            {
                XElement? cl = layerProp.Element(IwxxmNs + "CloudLayer");
                if (cl == null) continue;

                string? amount = cl.Element(IwxxmNs + "amount")?.Attribute(XlinkNs + "href")?.Value;
                Length? cloudBase = UomMeasureReader.ReadLength(cl.Element(IwxxmNs + "base"), context, errors);
                string? cloudType = cl.Element(IwxxmNs + "cloudType")?.Attribute(XlinkNs + "href")?.Value;
                target.Add(new AerodromeCloudLayerInfo(amount, cloudBase, cloudType));
            }
        }

        private static AerodromeForecastGroupInfo ParseForecastGroup(XElement fcstElem, List<ValidationError> errors)
        {
            string id = GetGmlId(fcstElem);
            string? changeInd = fcstElem.Attribute("changeIndicator")?.Value;
            bool cavok = ParseBoolAttribute(fcstElem, "cloudAndVisibilityOK");

            // phenomenonTime may be a period, an instant, or an unresolved xlink:href reference (-> null bounds).
            XElement? phenTime = fcstElem.Element(IwxxmNs + "phenomenonTime");
            ReadPeriod(phenTime, "phenomenonTime", null, id, errors, out DateTimeOffset? validFrom, out DateTimeOffset? validUntil);
            if (!validFrom.HasValue && !validUntil.HasValue)
            {
                validFrom = ReadOptionalInstant(phenTime, "phenomenonTime", id, errors);
            }

            double? vis = UomMeasureReader.ReadFixedUnit(fcstElem.Element(IwxxmNs + "prevailingVisibility"), MeterUom, id, errors);
            string? visOp = GetText(fcstElem.Element(IwxxmNs + "prevailingVisibilityOperator"));
            SurfaceWindInfo? wind = ParseSurfaceWind(fcstElem.Element(IwxxmNs + "surfaceWind"), id, errors);

            var wxList = new List<string>();
            foreach (XElement wx in fcstElem.Elements(IwxxmNs + "weather"))
            {
                string? href = wx.Attribute(XlinkNs + "href")?.Value;
                if (!string.IsNullOrWhiteSpace(href))
                {
                    wxList.Add(href!);
                }
            }

            var layers = new List<AerodromeCloudLayerInfo>();
            ParseCloudLayers(fcstElem.Element(IwxxmNs + "cloud"), layers, id, errors);

            return new AerodromeForecastGroupInfo(
                id,
                changeInd,
                cavok,
                validFrom,
                validUntil,
                vis,
                visOp,
                wind,
                wxList,
                layers);
        }

        private static EnRoutePhenomenonVolume ParseEnRouteConditionVolume(XElement condElem, List<ValidationError> errors)
        {
            string id = GetGmlId(condElem);
            string? intensityChange = condElem.Attribute("intensityChange")?.Value;
            double? dir = UomMeasureReader.ReadFixedUnit(condElem.Element(IwxxmNs + "directionOfMotion"), DegreeUom, id, errors);
            Speed? spd = UomMeasureReader.ReadSpeed(condElem.Element(IwxxmNs + "speedOfMotion"), id, errors);

            GeoCoordinate? tcPos = ParseGmlPointElement(
                condElem.Element(IwxxmNs + "tropicalCyclonePosition")?.Element(GmlNs + "Point"),
                id,
                errors);

            XElement? volElem = condElem.Element(IwxxmNs + "geometry")?.Element(AixmNs + "AirspaceVolume");
            ReadAirspaceVolumeLimits(volElem, id, errors, out VerticalLimit? lower, out VerticalLimit? upper);
            XElement? patches = volElem
                ?.Element(AixmNs + "horizontalProjection")
                ?.Element(AixmNs + "Surface")
                ?.Element(GmlNs + "patches");

            GeoPolygon? area = patches != null ? ParseGmlPatchesPolygon(patches, id, errors) : null;

            return new EnRoutePhenomenonVolume(id, area, lower, upper, intensityChange, dir, spd, tcPos);
        }

        private static EnRoutePhenomenonVolume ParseAshCloudVolume(XElement ashElem, List<ValidationError> errors)
        {
            string id = GetGmlId(ashElem);
            double? dir = UomMeasureReader.ReadFixedUnit(ashElem.Element(IwxxmNs + "directionOfMotion"), DegreeUom, id, errors);
            Speed? spd = UomMeasureReader.ReadSpeed(ashElem.Element(IwxxmNs + "speedOfMotion"), id, errors);

            XElement? volElem = ashElem.Element(IwxxmNs + "ashCloudExtent")?.Element(AixmNs + "AirspaceVolume");
            ReadAirspaceVolumeLimits(volElem, id, errors, out VerticalLimit? lower, out VerticalLimit? upper);
            XElement? patches = volElem
                ?.Element(AixmNs + "horizontalProjection")
                ?.Element(AixmNs + "Surface")
                ?.Element(GmlNs + "patches");

            GeoPolygon? area = patches != null ? ParseGmlPatchesPolygon(patches, id, errors) : null;

            return new EnRoutePhenomenonVolume(id, area, lower, upper, null, dir, spd);
        }

        /// <summary>
        /// Reads <c>aixm:lowerLimit</c> / <c>aixm:lowerLimitReference</c> and <c>aixm:upperLimit</c> /
        /// <c>aixm:upperLimitReference</c> exactly as reported (no flight-level conversion, no assumed reference).
        /// </summary>
        private static void ReadAirspaceVolumeLimits(
            XElement? volElem,
            string context,
            List<ValidationError> errors,
            out VerticalLimit? lower,
            out VerticalLimit? upper)
        {
            lower = VerticalLimitReader.Read(volElem?.Element(AixmNs + "lowerLimit"), volElem?.Element(AixmNs + "lowerLimitReference"), context, errors);
            upper = VerticalLimitReader.Read(volElem?.Element(AixmNs + "upperLimit"), volElem?.Element(AixmNs + "upperLimitReference"), context, errors);
        }

        /// <summary>
        /// Maps an <c>iwxxm:Volcano</c> (VONA / QVACI). <c>iwxxm:name</c> is mandatory but nillable (<c>metFeature.xsd</c>):
        /// an absent name is a mapping error, a nil name maps to <c>null</c>.
        /// </summary>
        private static void AddIwxxmVolcano(XElement volElem, string featId, GeoCoordinate position, List<Volcano> volcanoes, List<ValidationError> errors)
        {
            XElement? nameElem = volElem.Element(IwxxmNs + "name");
            if (nameElem == null)
            {
                errors.Add(MappingError(VolcanoNameMissingCode, $"The mandatory element iwxxm:name of volcano '{featId}' is missing; no name is assumed.", featId));
                return;
            }

            var vRes = Volcano.Create(featId, GetText(nameElem), GetText(volElem.Element(IwxxmNs + "IAVCEINumber")), position);
            if (vRes.IsSuccess) volcanoes.Add(vRes.Value);
            else errors.AddRange(vRes.Errors);
        }

        /// <summary>Maps a <c>metce:Volcano</c>; <c>metce:name</c> is a mandatory, non-nillable string (<c>phenomena.xsd</c>).</summary>
        private static Volcano? ParseMetceVolcano(XElement? propElem, List<ValidationError> errors)
        {
            XElement? volElem = propElem?.Element(MetceNs + "Volcano")
                                ?? propElem?.Element(MetceNs + "EruptingVolcano");
            if (volElem == null)
                return null;

            string id = GetGmlId(volElem);
            string? name = GetText(volElem.Element(MetceNs + "name"));
            if (name == null)
            {
                errors.Add(MappingError(VolcanoNameMissingCode, $"The mandatory element metce:name of volcano '{id}' is missing or empty; no name is assumed.", id));
                return null;
            }

            XElement? ptElem = volElem.Element(MetceNs + "position")?.Element(GmlNs + "Point");
            GeoCoordinate? pos = ParseGmlPointElement(ptElem, id, errors);
            if (!pos.HasValue)
                return null;

            var res = Volcano.Create(id, name, null, pos.Value);
            if (res.IsFailure)
            {
                errors.AddRange(res.Errors);
                return null;
            }

            return res.Value;
        }

        private static GeoCoordinate? ParseGmlPointElement(XElement? pointElem, string contextLocation, List<ValidationError> errors)
        {
            string? posText = pointElem?.Element(GmlNs + "pos")?.Value;
            if (string.IsNullOrWhiteSpace(posText))
                return null;

            var coordRes = GmlCoordinateTokenizer.ParseSinglePoint(posText!, contextLocation);
            if (coordRes.IsFailure)
            {
                errors.AddRange(coordRes.Errors);
                return null;
            }

            return coordRes.Value;
        }

        private static GeoPolygon? ParseGmlPatchesPolygon(XElement? patchesElem, string contextLocation, List<ValidationError> errors)
        {
            XElement? patch = patchesElem?.Element(GmlNs + "PolygonPatch");
            if (patch == null)
                return null;

            XElement? extRing = patch.Element(GmlNs + "exterior");
            List<GeoCoordinate>? extCoords = ParseAbstractRingCoordinates(extRing, contextLocation, errors);
            if (extCoords == null || extCoords.Count == 0)
                return null;

            var interiorRings = new List<IReadOnlyList<GeoCoordinate>>();
            foreach (XElement intRing in patch.Elements(GmlNs + "interior"))
            {
                List<GeoCoordinate>? holeCoords = ParseAbstractRingCoordinates(intRing, contextLocation, errors);
                if (holeCoords != null && holeCoords.Count >= 3)
                {
                    interiorRings.Add(holeCoords);
                }
            }

            var polyRes = GeoPolygon.Create(extCoords, interiorRings, contextLocation);
            if (polyRes.IsFailure)
            {
                errors.AddRange(polyRes.Errors);
                return null;
            }

            return polyRes.Value;
        }

        private static List<GeoCoordinate>? ParseAbstractRingCoordinates(XElement? ringPropElem, string contextLocation, List<ValidationError> errors)
        {
            if (ringPropElem == null)
                return null;

            string? linearPosList = ringPropElem.Element(GmlNs + "LinearRing")?.Element(GmlNs + "posList")?.Value;
            if (!string.IsNullOrWhiteSpace(linearPosList))
            {
                var res = GmlCoordinateTokenizer.ParsePosList(linearPosList!, 2, contextLocation);
                if (res.IsFailure)
                {
                    errors.AddRange(res.Errors);
                    return null;
                }
                return res.Value as List<GeoCoordinate> ?? new List<GeoCoordinate>(res.Value);
            }

            XElement? ringElem = ringPropElem.Element(GmlNs + "Ring");
            if (ringElem != null)
            {
                var allCoords = new List<GeoCoordinate>();
                foreach (XElement curveMember in ringElem.Elements(GmlNs + "curveMember"))
                {
                    XElement? segments = curveMember.Element(GmlNs + "Curve")?.Element(GmlNs + "segments");
                    if (segments == null) continue;

                    foreach (XElement seg in segments.Elements())
                    {
                        string? segPosList = seg.Element(GmlNs + "posList")?.Value;
                        if (!string.IsNullOrWhiteSpace(segPosList))
                        {
                            var res = GmlCoordinateTokenizer.ParsePosList(segPosList!, 2, contextLocation);
                            if (res.IsFailure)
                            {
                                errors.AddRange(res.Errors);
                                return null;
                            }
                            allCoords.AddRange(res.Value);
                        }
                    }
                }

                return allCoords.Count > 0 ? allCoords : null;
            }

            return null;
        }
    }
}
