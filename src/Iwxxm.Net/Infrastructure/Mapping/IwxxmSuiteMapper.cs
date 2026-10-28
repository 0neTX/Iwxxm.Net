using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using Iwxxm.Net.Application.Ports;
using Iwxxm.Net.Domain.Aggregates;
using Iwxxm.Net.Domain.Common;
using Iwxxm.Net.Domain.ValueObjects;
using Iwxxm.Net.Infrastructure.Serialization;
using Iwxxm20252.Generated;

namespace Iwxxm.Net.Infrastructure.Mapping
{
    /// <summary>
    /// Anti-Corruption Layer (ACL) mapper and polymorphic dispatcher for the full ICAO/WMO IWXXM 2025-2
    /// report suite (<c>SIGWX</c>, <c>METAR</c>, <c>SPECI</c>, <c>TAF</c>, <c>SIGMET</c>, <c>AIRMET</c>,
    /// <c>VONA</c>, <c>QVACI</c>, <c>VolcanicAshAdvisory</c>, <c>TropicalCycloneAdvisory</c>,
    /// <c>SpaceWeatherAdvisory</c>) and SWIM <c>collect:MeteorologicalBulletin</c> envelopes.
    /// </summary>
    internal sealed class IwxxmSuiteMapper : IIwxxmSuiteDocumentMapper
    {
        private static readonly XNamespace IwxxmNs = "http://icao.int/iwxxm/2025-2";
        private static readonly XNamespace GmlNs = "http://www.opengis.net/gml/3.2";
        private static readonly XNamespace AixmNs = "http://www.aixm.aero/schema/5.1.1";
        private static readonly XNamespace MetceNs = "http://def.wmo.int/metce/2013";
        private static readonly XNamespace CollectNs = "http://def.wmo.int/collect/2014";
        private static readonly XNamespace XlinkNs = "http://www.w3.org/1999/xlink";

        private readonly ISigwxXmlDeserializer<SigwxDocumentEnvelope> _sigwxDeserializer;
        private readonly ISigwxAclMapper<SigwxDocumentEnvelope> _sigwxMapper;
        private readonly IwxxmParserOptions _options;

        public IwxxmSuiteMapper(IwxxmParserOptions? options = null)
            : this(new XmlSerializerSigwxDeserializer(options), new SigwxMapper(), options)
        {
        }

        public IwxxmSuiteMapper(
            ISigwxXmlDeserializer<SigwxDocumentEnvelope> sigwxDeserializer,
            ISigwxAclMapper<SigwxDocumentEnvelope> sigwxMapper,
            IwxxmParserOptions? options = null)
        {
            _sigwxDeserializer = sigwxDeserializer ?? throw new ArgumentNullException(nameof(sigwxDeserializer));
            _sigwxMapper = sigwxMapper ?? throw new ArgumentNullException(nameof(sigwxMapper));
            _options = options ?? IwxxmParserOptions.Default;
        }

        public Result<IIwxxmReport> MapValidatedStream(Stream xmlStream)
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
                    ErrorCategory.XmlWellFormedness);
            }

            XElement? root = doc.Root;
            if (root == null)
            {
                return Result<IIwxxmReport>.Failure(
                    "XML-EMPTY-ROOT",
                    "The XML document does not contain a root element.",
                    ErrorCategory.XmlWellFormedness);
            }

            // Delegate SIGWX/WAFS directly to the dedicated XmlSerializer + SigwxMapper pipeline
            if (root.Name == IwxxmNs + "WAFSSignificantWeatherForecast" ||
                root.Name == IwxxmNs + "SIGWXReport")
            {
                xmlStream.Position = 0;
                var dtoResult = _sigwxDeserializer.Deserialize(xmlStream);
                if (dtoResult.IsFailure)
                {
                    return Result<IIwxxmReport>.Failure(dtoResult.Errors);
                }

                var sigwxResult = _sigwxMapper.MapToDomain(dtoResult.Value);
                if (sigwxResult.IsFailure)
                {
                    return Result<IIwxxmReport>.Failure(sigwxResult.Errors);
                }

                return Result<IIwxxmReport>.Success(sigwxResult.Value);
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
                        var dtoResult = _sigwxDeserializer.Deserialize(ms);
                        if (dtoResult.IsFailure) return Result<IIwxxmReport>.Failure(dtoResult.Errors);
                        var sigwxResult = _sigwxMapper.MapToDomain(dtoResult.Value);
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
            string? reportStatus = root.Attribute("reportStatus")?.Value;
            string? permissibleUsage = root.Attribute("permissibleUsage")?.Value;
            bool isAutomated = ParseBoolAttribute(root, "automatedStation");
            string? failedTac = root.Attribute("translationFailedTAC")?.Value;
            bool isTranslationFailed = failedTac != null || root.Attribute("translatedBulletinID") != null && root.Element(IwxxmNs + "observation") == null;

            DateTimeOffset issueTime = ParseTimeInstantProperty(root.Element(IwxxmNs + "issueTime")) ?? DateTimeOffset.MinValue;
            DateTimeOffset obsTime = ParseTimeInstantProperty(root.Element(IwxxmNs + "observationTime")) ?? issueTime;

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
                airTemp = ParseOptionalDouble(obsElem.Element(IwxxmNs + "airTemperature"));
                dewpoint = ParseOptionalDouble(obsElem.Element(IwxxmNs + "dewpointTemperature"));
                qnh = ParseOptionalDouble(obsElem.Element(IwxxmNs + "qnh"));
                surfaceWind = ParseSurfaceWind(obsElem.Element(IwxxmNs + "surfaceWind"));
                visibility = ParseOptionalDouble(
                    obsElem.Element(IwxxmNs + "visibility")
                        ?.Element(IwxxmNs + "AerodromeHorizontalVisibility")
                        ?.Element(IwxxmNs + "prevailingVisibility"));

                foreach (XElement rvrProp in obsElem.Elements(IwxxmNs + "rvr"))
                {
                    XElement? rvrElem = rvrProp.Element(IwxxmNs + "AerodromeRunwayVisualRange");
                    if (rvrElem == null) continue;
                    string rwyDesig = rvrElem
                        .Element(IwxxmNs + "runway")
                        ?.Element(AixmNs + "RunwayDirection")
                        ?.Element(AixmNs + "timeSlice")
                        ?.Element(AixmNs + "RunwayDirectionTimeSlice")
                        ?.Element(AixmNs + "designator")?.Value ?? string.Empty;
                    double? meanRvr = ParseOptionalDouble(rvrElem.Element(IwxxmNs + "meanRVR"));
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

                ParseCloudLayers(obsElem.Element(IwxxmNs + "cloud"), cloudLayers);
            }

            var trends = new List<AerodromeForecastGroupInfo>();
            foreach (XElement trendProp in root.Elements(IwxxmNs + "trendForecast"))
            {
                XElement? trendElem = trendProp.Element(IwxxmNs + "MeteorologicalAerodromeTrendForecast");
                if (trendElem != null)
                {
                    trends.Add(ParseForecastGroup(trendElem));
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
                reportStatus,
                permissibleUsage,
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
                trends);
        }

        private static Result<TafReport> MapTaf(XElement root)
        {
            var errors = new List<ValidationError>();
            string id = GetGmlId(root);
            string? reportStatus = root.Attribute("reportStatus")?.Value;
            string? permissibleUsage = root.Attribute("permissibleUsage")?.Value;
            bool isCancel = ParseBoolAttribute(root, "isCancelReport");
            string? failedTac = root.Attribute("translationFailedTAC")?.Value;
            bool isTranslationFailed = failedTac != null;

            DateTimeOffset issueTime = ParseTimeInstantProperty(root.Element(IwxxmNs + "issueTime")) ?? DateTimeOffset.MinValue;

            ExtractAerodromeInfo(
                root.Element(IwxxmNs + "aerodrome"),
                errors,
                out string designator,
                out string? name,
                out GeoCoordinate? position);

            ParseTimePeriodProperty(root.Element(IwxxmNs + "validPeriod"), out DateTimeOffset? validFrom, out DateTimeOffset? validUntil);
            ParseTimePeriodProperty(root.Element(IwxxmNs + "cancelledReportValidPeriod"), out DateTimeOffset? cancelFrom, out DateTimeOffset? cancelUntil);

            XElement? baseProp = root.Element(IwxxmNs + "baseForecast");
            string? baseNilReason = baseProp?.Attribute("nilReason")?.Value;
            XElement? baseElem = baseProp?.Element(IwxxmNs + "MeteorologicalAerodromeForecast");
            bool isNilForecast = baseElem == null && !string.IsNullOrWhiteSpace(baseNilReason);

            AerodromeForecastGroupInfo? baseForecast = baseElem != null ? ParseForecastGroup(baseElem) : null;

            var changes = new List<AerodromeForecastGroupInfo>();
            foreach (XElement changeProp in root.Elements(IwxxmNs + "changeForecast"))
            {
                XElement? changeElem = changeProp.Element(IwxxmNs + "MeteorologicalAerodromeForecast");
                if (changeElem != null)
                {
                    changes.Add(ParseForecastGroup(changeElem));
                }
            }

            if (errors.Count > 0)
            {
                return Result<TafReport>.Failure(errors);
            }

            return TafReport.Create(
                id,
                issueTime,
                reportStatus,
                permissibleUsage,
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
                changes);
        }

        // =====================================================================
        // 3. En-Route Hazard Mappers: SIGMET, VA/TC SIGMET, AIRMET (TD-202)
        // =====================================================================

        private static Result<SigmetReport> MapSigmet(XElement root, IwxxmReportType reportType)
        {
            var errors = new List<ValidationError>();
            string id = GetGmlId(root);
            string? reportStatus = root.Attribute("reportStatus")?.Value;
            string? permissibleUsage = root.Attribute("permissibleUsage")?.Value;
            bool isCancel = ParseBoolAttribute(root, "isCancelReport");
            string? failedTac = root.Attribute("translationFailedTAC")?.Value;
            bool isTranslationFailed = failedTac != null;

            DateTimeOffset issueTime = ParseTimeInstantProperty(root.Element(IwxxmNs + "issueTime")) ?? DateTimeOffset.MinValue;
            ExtractUnitInfo(root.Element(IwxxmNs + "issuingAirTrafficServicesUnit"), out string atsDesig, out string? atsName);
            ExtractUnitInfo(root.Element(IwxxmNs + "originatingMeteorologicalWatchOffice"), out string mwoDesig, out _);
            ExtractAirspaceInfo(root.Element(IwxxmNs + "issuingAirTrafficServicesRegion"), out string? firDesig, out string? firName);

            string? seqNumber = root.Element(IwxxmNs + "sequenceNumber")?.Value;
            string? cancelSeqNumber = root.Element(IwxxmNs + "cancelledReportSequenceNumber")?.Value;
            ParseTimePeriodProperty(root.Element(IwxxmNs + "validPeriod"), out DateTimeOffset? validFrom, out DateTimeOffset? validUntil);
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
                reportStatus,
                permissibleUsage,
                atsDesig,
                atsName,
                string.IsNullOrWhiteSpace(mwoDesig) ? null : mwoDesig,
                firDesig,
                firName,
                seqNumber,
                validFrom ?? issueTime,
                validUntil ?? issueTime,
                phenomenonUri,
                isCancel,
                cancelSeqNumber,
                isTranslationFailed,
                failedTac,
                eruptingVolcano,
                tcName,
                evolvingConditions,
                forecastPositions);
        }

        private static Result<AirmetReport> MapAirmet(XElement root)
        {
            var errors = new List<ValidationError>();
            string id = GetGmlId(root);
            string? reportStatus = root.Attribute("reportStatus")?.Value;
            string? permissibleUsage = root.Attribute("permissibleUsage")?.Value;
            string? failedTac = root.Attribute("translationFailedTAC")?.Value;
            bool isTranslationFailed = failedTac != null;

            DateTimeOffset issueTime = ParseTimeInstantProperty(root.Element(IwxxmNs + "issueTime")) ?? DateTimeOffset.MinValue;
            ExtractUnitInfo(root.Element(IwxxmNs + "issuingAirTrafficServicesUnit"), out string atsDesig, out string? atsName);
            ExtractUnitInfo(root.Element(IwxxmNs + "originatingMeteorologicalWatchOffice"), out string mwoDesig, out _);
            ExtractAirspaceInfo(root.Element(IwxxmNs + "issuingAirTrafficServicesRegion"), out string? firDesig, out string? firName);

            string? seqNumber = root.Element(IwxxmNs + "sequenceNumber")?.Value;
            ParseTimePeriodProperty(root.Element(IwxxmNs + "validPeriod"), out DateTimeOffset? validFrom, out DateTimeOffset? validUntil);
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
                reportStatus,
                permissibleUsage,
                atsDesig,
                atsName,
                string.IsNullOrWhiteSpace(mwoDesig) ? null : mwoDesig,
                firDesig,
                firName,
                seqNumber,
                validFrom ?? issueTime,
                validUntil ?? issueTime,
                phenomenonUri,
                isTranslationFailed,
                failedTac,
                evolvingConditions);
        }

        // =====================================================================
        // 4. Volcanic & Space Weather Mappers: VONA, QVACI, VAA, TCA, SWX (TD-202)
        // =====================================================================

        private static Result<VonaReport> MapVona(XElement root)
        {
            var errors = new List<ValidationError>();
            string id = GetGmlId(root);
            string? reportStatus = root.Attribute("reportStatus")?.Value;
            string? permissibleUsage = root.Attribute("permissibleUsage")?.Value;
            string? failedTac = root.Attribute("translationFailedTAC")?.Value;
            bool isTranslationFailed = failedTac != null;

            DateTimeOffset issueTime = ParseTimeInstantProperty(root.Element(IwxxmNs + "issueTime")) ?? DateTimeOffset.MinValue;
            ExtractUnitInfo(root.Element(IwxxmNs + "originatingCentre"), out string centreDesig, out string? centreName);

            var volcanoes = new List<Volcano>();
            FlightLevel? ashLevel = null;

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
                        string vName = volElem.Element(IwxxmNs + "name")?.Value ?? "UNKNOWN";
                        string? iavcei = volElem.Element(IwxxmNs + "IAVCEINumber")?.Value;
                        var vRes = Volcano.Create(featId, vName, iavcei, pos.Value);
                        if (vRes.IsSuccess) volcanoes.Add(vRes.Value);
                        else errors.AddRange(vRes.Errors);
                    }
                }
                else if (phenHref != null && phenHref.EndsWith("/VOLCANIC_ASH", StringComparison.OrdinalIgnoreCase))
                {
                    XElement? elevElem = feat
                        .Element(IwxxmNs + "phenomenonGeometry")
                        ?.Element(IwxxmNs + "ElevatedLevel")
                        ?.Element(IwxxmNs + "elevation");

                    ashLevel = ParseOptionalFlightLevel(elevElem);
                }
            }

            if (errors.Count > 0)
            {
                return Result<VonaReport>.Failure(errors);
            }

            return VonaReport.Create(
                id,
                issueTime,
                reportStatus,
                permissibleUsage,
                centreName ?? centreDesig,
                centreDesig,
                root.Element(IwxxmNs + "stateOrRegion")?.Value,
                root.Element(IwxxmNs + "noticeNumber")?.Value,
                root.Element(IwxxmNs + "currentColourCode")?.Attribute(XlinkNs + "href")?.Value,
                root.Element(IwxxmNs + "previousColourCode")?.Attribute(XlinkNs + "href")?.Value,
                root.Element(IwxxmNs + "contacts")?.Value,
                root.Element(IwxxmNs + "remarks")?.Value,
                root.Element(IwxxmNs + "nextNotice")?.Value,
                isTranslationFailed,
                failedTac,
                volcanoes,
                ashLevel);
        }

        private static Result<QvaciReport> MapQvaci(XElement root)
        {
            var errors = new List<ValidationError>();
            string id = GetGmlId(root);
            string? reportStatus = root.Attribute("reportStatus")?.Value;
            string? permissibleUsage = root.Attribute("permissibleUsage")?.Value;
            string? failedTac = root.Attribute("translationFailedTAC")?.Value;
            bool isTranslationFailed = failedTac != null;

            DateTimeOffset issueTime = ParseTimeInstantProperty(root.Element(IwxxmNs + "issueTime")) ?? DateTimeOffset.MinValue;
            ExtractUnitInfo(root.Element(IwxxmNs + "originatingCentre"), out string centreDesig, out string? centreName);

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
                        string vName = volElem.Element(IwxxmNs + "name")?.Value ?? "UNKNOWN";
                        string? iavcei = volElem.Element(IwxxmNs + "IAVCEINumber")?.Value;
                        var vRes = Volcano.Create(featId, vName, iavcei, pos.Value);
                        if (vRes.IsSuccess) volcanoes.Add(vRes.Value);
                        else errors.AddRange(vRes.Errors);
                    }
                }
                else if (phenHref != null && phenHref.EndsWith("/VOLCANIC_ASH", StringComparison.OrdinalIgnoreCase))
                {
                    XElement? volGeom = feat.Element(IwxxmNs + "phenomenonGeometry")?.Element(IwxxmNs + "ElevatedVolume");
                    XElement? concElem = feat
                        .Element(IwxxmNs + "phenomenonProperty")
                        ?.Element(IwxxmNs + "VolcanicAshCloudConcentration")
                        ?.Element(IwxxmNs + "ashDensityEqualsOrExceeds");

                    if (volGeom != null && concElem != null)
                    {
                        GeoPolygon? poly = ParseGmlPatchesPolygon(volGeom.Element(GmlNs + "patches"), featId, errors);
                        FlightLevel? lowerFl = ParseOptionalFlightLevel(volGeom.Element(IwxxmNs + "lowerElevation"));
                        FlightLevel? upperFl = ParseOptionalFlightLevel(volGeom.Element(IwxxmNs + "upperElevation"));
                        double density = ParseOptionalDouble(concElem) ?? 0.0;

                        if (poly != null)
                        {
                            ashClouds.Add(new VolcanicAshConcentrationCloud(featId, poly, lowerFl, upperFl, density));
                        }
                    }
                }
            }

            if (errors.Count > 0)
            {
                return Result<QvaciReport>.Failure(errors);
            }

            DateTimeOffset? modelInitTime = ParseTimeInstantProperty(root.Element(IwxxmNs + "dispersionModelInitialisationTime"));

            return QvaciReport.Create(
                id,
                issueTime,
                reportStatus,
                permissibleUsage,
                centreName ?? centreDesig,
                centreDesig,
                modelInitTime,
                root.Element(IwxxmNs + "remarks")?.Value,
                isTranslationFailed,
                failedTac,
                volcanoes,
                ashClouds);
        }

        private static Result<VolcanicAshAdvisoryReport> MapVolcanicAshAdvisory(XElement root)
        {
            var errors = new List<ValidationError>();
            string id = GetGmlId(root);
            string? reportStatus = root.Attribute("reportStatus")?.Value;
            string? permissibleUsage = root.Attribute("permissibleUsage")?.Value;
            string? failedTac = root.Attribute("translationFailedTAC")?.Value;
            bool isTranslationFailed = failedTac != null;

            DateTimeOffset issueTime = ParseTimeInstantProperty(root.Element(IwxxmNs + "issueTime")) ?? DateTimeOffset.MinValue;
            ExtractUnitInfo(root.Element(IwxxmNs + "issuingVolcanicAshAdvisoryCentre"), out string vaacDesig, out string? vaacName);

            Volcano? volcano = ParseMetceVolcano(root.Element(IwxxmNs + "volcano"), errors);
            double? sourceElev = ParseOptionalDouble(root.Element(IwxxmNs + "sourceElevationAMSL"));

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

            if (errors.Count > 0)
            {
                return Result<VolcanicAshAdvisoryReport>.Failure(errors);
            }

            return VolcanicAshAdvisoryReport.Create(
                id,
                issueTime,
                reportStatus,
                permissibleUsage,
                vaacName ?? vaacDesig,
                volcano,
                root.Element(IwxxmNs + "stateOrRegion")?.Value,
                sourceElev,
                root.Element(IwxxmNs + "advisoryNumber")?.Value,
                root.Element(IwxxmNs + "informationSource")?.Value,
                root.Element(IwxxmNs + "eruptionDetails")?.Value,
                root.Element(IwxxmNs + "remarks")?.Value,
                ParseTimeInstantProperty(root.Element(IwxxmNs + "nextAdvisoryTime")),
                isTranslationFailed,
                failedTac,
                observedClouds,
                forecastClouds);
        }

        private static Result<TropicalCycloneAdvisoryReport> MapTropicalCycloneAdvisory(XElement root)
        {
            var errors = new List<ValidationError>();
            string id = GetGmlId(root);
            string? reportStatus = root.Attribute("reportStatus")?.Value;
            string? permissibleUsage = root.Attribute("permissibleUsage")?.Value;
            string? failedTac = root.Attribute("translationFailedTAC")?.Value;
            bool isTranslationFailed = failedTac != null;

            DateTimeOffset issueTime = ParseTimeInstantProperty(root.Element(IwxxmNs + "issueTime")) ?? DateTimeOffset.MinValue;
            ExtractUnitInfo(root.Element(IwxxmNs + "issuingTropicalCycloneAdvisoryCentre"), out string tcacDesig, out string? tcacName);

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
                DateTimeOffset? obsTime = ParseTimeInstantProperty(obsElem.Element(IwxxmNs + "phenomenonTime"));
                GeoCoordinate? pos = ParseGmlPointElement(
                    obsElem.Element(IwxxmNs + "tropicalCyclonePosition")?.Element(GmlNs + "Point"),
                    GetGmlId(obsElem),
                    errors);
                double? maxWind = ParseOptionalDouble(obsElem.Element(IwxxmNs + "maximumSurfaceWindSpeed"));
                double? pressure = ParseOptionalDouble(obsElem.Element(IwxxmNs + "centralPressure"));

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

                DateTimeOffset? fcstTime = ParseTimeInstantProperty(fcstElem.Element(IwxxmNs + "phenomenonTime"));
                GeoCoordinate? pos = ParseGmlPointElement(
                    fcstElem.Element(IwxxmNs + "tropicalCyclonePosition")?.Element(GmlNs + "Point"),
                    GetGmlId(fcstElem),
                    errors);
                double? maxWind = ParseOptionalDouble(fcstElem.Element(IwxxmNs + "maximumSurfaceWindSpeed"));

                if (pos.HasValue)
                {
                    forecasts.Add(new TropicalCycloneTrackPoint(fcstTime, pos.Value, maxWind));
                }
            }

            if (errors.Count > 0)
            {
                return Result<TropicalCycloneAdvisoryReport>.Failure(errors);
            }

            return TropicalCycloneAdvisoryReport.Create(
                id,
                issueTime,
                reportStatus,
                permissibleUsage,
                string.IsNullOrWhiteSpace(tcacDesig) ? (tcacName ?? string.Empty) : tcacDesig,
                cycloneName,
                root.Element(IwxxmNs + "advisoryNumber")?.Value,
                obsPoint,
                forecasts,
                root.Element(IwxxmNs + "remarks")?.Value,
                ParseTimeInstantProperty(root.Element(IwxxmNs + "nextAdvisoryTime")),
                isTranslationFailed,
                failedTac);
        }

        private static Result<SpaceWeatherAdvisoryReport> MapSpaceWeatherAdvisory(XElement root)
        {
            var errors = new List<ValidationError>();
            string id = GetGmlId(root);
            string? reportStatus = root.Attribute("reportStatus")?.Value;
            string? permissibleUsage = root.Attribute("permissibleUsage")?.Value;
            string? failedTac = root.Attribute("translationFailedTAC")?.Value;
            bool isTranslationFailed = failedTac != null;

            DateTimeOffset issueTime = ParseTimeInstantProperty(root.Element(IwxxmNs + "issueTime")) ?? DateTimeOffset.MinValue;
            ExtractUnitInfo(root.Element(IwxxmNs + "issuingSpaceWeatherCentre"), out string swxcDesig, out string? swxcName);

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
                DateTimeOffset? phenTime = ParseTimeInstantProperty(analysisElem.Element(IwxxmNs + "phenomenonTime"));

                foreach (XElement irProp in analysisElem.Elements(IwxxmNs + "intensityAndRegion"))
                {
                    XElement? irElem = irProp.Element(IwxxmNs + "SpaceWeatherIntensityAndRegion");
                    if (irElem == null) continue;

                    string? intensity = irElem.Element(IwxxmNs + "intensity")?.Value;
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

            if (errors.Count > 0)
            {
                return Result<SpaceWeatherAdvisoryReport>.Failure(errors);
            }

            return SpaceWeatherAdvisoryReport.Create(
                id,
                issueTime,
                reportStatus,
                permissibleUsage,
                swxcName ?? swxcDesig,
                root.Element(IwxxmNs + "advisoryNumber")?.Value,
                replacedNumbers,
                effects,
                analyses,
                root.Element(IwxxmNs + "remarks")?.Value,
                ParseTimeInstantProperty(root.Element(IwxxmNs + "nextAdvisoryTime")),
                isTranslationFailed,
                failedTac);
        }

        // =====================================================================
        // Shared XML & GML 3.2.1 Spatial Helper Methods
        // =====================================================================

        private static string GetGmlId(XElement elem)
        {
            return elem.Attribute(GmlNs + "id")?.Value ?? string.Empty;
        }

        private static bool ParseBoolAttribute(XElement elem, string attrName)
        {
            string? val = elem.Attribute(attrName)?.Value;
            return string.Equals(val, "true", StringComparison.OrdinalIgnoreCase) || val == "1";
        }

        private static double? ParseOptionalDouble(XElement? elem)
        {
            if (elem == null || string.IsNullOrWhiteSpace(elem.Value))
                return null;

            return double.TryParse(elem.Value.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double v)
                ? v
                : (double?)null;
        }

        private static FlightLevel? ParseOptionalFlightLevel(XElement? measureElem)
        {
            if (measureElem == null || string.IsNullOrWhiteSpace(measureElem.Value))
                return null;

            string rawVal = measureElem.Value.Trim();
            if (string.Equals(rawVal, "GND", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(rawVal, "SFC", StringComparison.OrdinalIgnoreCase))
            {
                return new FlightLevel(0);
            }

            if (!double.TryParse(rawVal, NumberStyles.Float, CultureInfo.InvariantCulture, out double numericVal))
                return null;

            string? uom = measureElem.Attribute("uom")?.Value;
            var res = FlightLevel.Create(numericVal, uom);
            return res.IsSuccess ? res.Value : (FlightLevel?)null;
        }

        private static DateTimeOffset? ParseTimeInstantProperty(XElement? propElem)
        {
            string? posText = propElem
                ?.Element(GmlNs + "TimeInstant")
                ?.Element(GmlNs + "timePosition")?.Value;

            if (string.IsNullOrWhiteSpace(posText))
                return null;

            return DateTimeOffset.TryParse(posText!.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out DateTimeOffset dt)
                ? dt.ToUniversalTime()
                : (DateTimeOffset?)null;
        }

        private static void ParseTimePeriodProperty(
            XElement? propElem,
            out DateTimeOffset? beginUtc,
            out DateTimeOffset? endUtc)
        {
            beginUtc = null;
            endUtc = null;

            XElement? periodElem = propElem?.Element(GmlNs + "TimePeriod");
            if (periodElem == null)
                return;

            string? beginText = periodElem.Element(GmlNs + "beginPosition")?.Value;
            string? endText = periodElem.Element(GmlNs + "endPosition")?.Value;

            if (!string.IsNullOrWhiteSpace(beginText) &&
                DateTimeOffset.TryParse(beginText!.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out DateTimeOffset b))
            {
                beginUtc = b.ToUniversalTime();
            }

            if (!string.IsNullOrWhiteSpace(endText) &&
                DateTimeOffset.TryParse(endText!.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out DateTimeOffset e))
            {
                endUtc = e.ToUniversalTime();
            }
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

            designator = slice.Element(AixmNs + "designator")?.Value
                         ?? slice.Element(AixmNs + "locationIndicatorICAO")?.Value
                         ?? string.Empty;
            name = slice.Element(AixmNs + "name")?.Value;

            XElement? arpPt = slice
                .Element(AixmNs + "ARP")
                ?.Element(AixmNs + "ElevatedPoint");

            if (arpPt != null)
            {
                position = ParseGmlPointElement(arpPt, GetGmlId(arpPt), errors);
            }
        }

        private static void ExtractUnitInfo(
            XElement? unitProp,
            out string designator,
            out string? name)
        {
            designator = string.Empty;
            name = null;

            XElement? slice = unitProp
                ?.Element(AixmNs + "Unit")
                ?.Element(AixmNs + "timeSlice")
                ?.Element(AixmNs + "UnitTimeSlice");

            if (slice == null)
                return;

            name = slice.Element(AixmNs + "name")?.Value;
            designator = slice.Element(AixmNs + "designator")?.Value ?? name ?? string.Empty;
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

            designator = slice.Element(AixmNs + "designator")?.Value;
            name = slice.Element(AixmNs + "name")?.Value;
        }

        private static SurfaceWindInfo? ParseSurfaceWind(XElement? surfaceWindProp)
        {
            XElement? windElem = surfaceWindProp?.Element(IwxxmNs + "AerodromeSurfaceWind")
                                 ?? surfaceWindProp?.Element(IwxxmNs + "AerodromeSurfaceWindForecast");
            if (windElem == null)
                return null;

            bool isVar = ParseBoolAttribute(windElem, "variableWindDirection");
            double? dir = ParseOptionalDouble(windElem.Element(IwxxmNs + "meanWindDirection"));
            double? spd = ParseOptionalDouble(windElem.Element(IwxxmNs + "meanWindSpeed"));
            double? gust = ParseOptionalDouble(windElem.Element(IwxxmNs + "windGustSpeed"));
            return new SurfaceWindInfo(isVar, dir, spd, gust);
        }

        private static void ParseCloudLayers(XElement? cloudProp, List<AerodromeCloudLayerInfo> target)
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
                double? baseFt = ParseOptionalDouble(cl.Element(IwxxmNs + "base"));
                string? cloudType = cl.Element(IwxxmNs + "cloudType")?.Attribute(XlinkNs + "href")?.Value;
                target.Add(new AerodromeCloudLayerInfo(amount, baseFt, cloudType));
            }
        }

        private static AerodromeForecastGroupInfo ParseForecastGroup(XElement fcstElem)
        {
            string id = GetGmlId(fcstElem);
            string? changeInd = fcstElem.Attribute("changeIndicator")?.Value;
            bool cavok = ParseBoolAttribute(fcstElem, "cloudAndVisibilityOK");

            XElement? phenTime = fcstElem.Element(IwxxmNs + "phenomenonTime");
            ParseTimePeriodProperty(phenTime, out DateTimeOffset? validFrom, out DateTimeOffset? validUntil);
            if (!validFrom.HasValue)
            {
                validFrom = ParseTimeInstantProperty(phenTime);
            }

            double? vis = ParseOptionalDouble(fcstElem.Element(IwxxmNs + "prevailingVisibility"));
            string? visOp = fcstElem.Element(IwxxmNs + "prevailingVisibilityOperator")?.Value;
            SurfaceWindInfo? wind = ParseSurfaceWind(fcstElem.Element(IwxxmNs + "surfaceWind"));

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
            ParseCloudLayers(fcstElem.Element(IwxxmNs + "cloud"), layers);

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
            double? dir = ParseOptionalDouble(condElem.Element(IwxxmNs + "directionOfMotion"));
            double? spd = ParseOptionalDouble(condElem.Element(IwxxmNs + "speedOfMotion"));

            GeoCoordinate? tcPos = ParseGmlPointElement(
                condElem.Element(IwxxmNs + "tropicalCyclonePosition")?.Element(GmlNs + "Point"),
                id,
                errors);

            XElement? volElem = condElem.Element(IwxxmNs + "geometry")?.Element(AixmNs + "AirspaceVolume");
            FlightLevel? lowerFl = ParseOptionalFlightLevel(volElem?.Element(AixmNs + "lowerLimit"));
            FlightLevel? upperFl = ParseOptionalFlightLevel(volElem?.Element(AixmNs + "upperLimit"));
            XElement? patches = volElem
                ?.Element(AixmNs + "horizontalProjection")
                ?.Element(AixmNs + "Surface")
                ?.Element(GmlNs + "patches");

            GeoPolygon? area = patches != null ? ParseGmlPatchesPolygon(patches, id, errors) : null;

            return new EnRoutePhenomenonVolume(id, area, lowerFl, upperFl, intensityChange, dir, spd, tcPos);
        }

        private static EnRoutePhenomenonVolume ParseAshCloudVolume(XElement ashElem, List<ValidationError> errors)
        {
            string id = GetGmlId(ashElem);
            double? dir = ParseOptionalDouble(ashElem.Element(IwxxmNs + "directionOfMotion"));
            double? spd = ParseOptionalDouble(ashElem.Element(IwxxmNs + "speedOfMotion"));

            XElement? volElem = ashElem.Element(IwxxmNs + "ashCloudExtent")?.Element(AixmNs + "AirspaceVolume");
            FlightLevel? lowerFl = ParseOptionalFlightLevel(volElem?.Element(AixmNs + "lowerLimit"));
            FlightLevel? upperFl = ParseOptionalFlightLevel(volElem?.Element(AixmNs + "upperLimit"));
            XElement? patches = volElem
                ?.Element(AixmNs + "horizontalProjection")
                ?.Element(AixmNs + "Surface")
                ?.Element(GmlNs + "patches");

            GeoPolygon? area = patches != null ? ParseGmlPatchesPolygon(patches, id, errors) : null;

            return new EnRoutePhenomenonVolume(id, area, lowerFl, upperFl, null, dir, spd);
        }

        private static Volcano? ParseMetceVolcano(XElement? propElem, List<ValidationError> errors)
        {
            XElement? volElem = propElem?.Element(MetceNs + "Volcano")
                                ?? propElem?.Element(MetceNs + "EruptingVolcano");
            if (volElem == null)
                return null;

            string id = GetGmlId(volElem);
            string name = volElem.Element(MetceNs + "name")?.Value ?? "UNKNOWN";
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
