using System;
using System.Collections.Generic;
using System.Linq;
using Iwxxm.Net.Reports;
using Iwxxm20252.Generated;

namespace Iwxxm.Net.Infrastructure.Mapping.Wafs
{
    /// <summary>
    /// Maps the official WMO IWXXM 2025-2 <c>WAFSSignificantWeatherForecast</c> onto <see cref="Sigwx"/>,
    /// dispatching each <c>iwxxm:MeteorologicalFeature</c> to its phenomenon extractor by the phenomenon code suffix.
    /// </summary>
    internal static class WafsForecastMapper
    {
        internal const string FeatureByReferenceCode = "ACL-SIGWX-FEATURE-BY-REFERENCE";
        internal const string PhenomenonUnsupportedCode = "ACL-SIGWX-PHENOMENON-UNSUPPORTED";

        private delegate void FeatureExtraction(MeteorologicalFeatureType feature, SigwxFeatureSet set, List<ValidationError> errors);

        /// <summary>Phenomenon code suffix to extractor, evaluated in order (closed WAFS phenomenon catalog).</summary>
        private static readonly KeyValuePair<string, FeatureExtraction>[] Extractors =
        {
            Entry("/JETSTREAM", (f, s, e) => SigwxFeatureSet.Collect(JetStreamExtractor.FromWafsFeature(f), s.JetStreams, e)),
            Entry("/CLOUD", (f, s, e) => SigwxFeatureSet.Collect(CloudExtractor.FromWafsFeature(f), s.Clouds, e)),
            Entry("/TROPOPAUSE", (f, s, e) => SigwxFeatureSet.Collect(TropopauseExtractor.FromWafsFeature(f), s.Tropopauses, e)),
            Entry("/VOLCANO", (f, s, e) => SigwxFeatureSet.Collect(PointPhenomenaExtractor.VolcanoFromWafsFeature(f), s.Volcanoes, e)),
            Entry("/TURBULENCE", (f, s, e) => SigwxFeatureSet.Collect(TurbulenceExtractor.FromWafsFeature(f), s.Turbulences, e)),
            Entry("/AIRFRAME_ICING", (f, s, e) => SigwxFeatureSet.Collect(AirframeIcingExtractor.FromWafsFeature(f), s.AirframeIcings, e)),
            Entry("/TROPICAL_CYCLONE", (f, s, e) => SigwxFeatureSet.Collect(PointPhenomenaExtractor.TropicalCycloneFromWafsFeature(f), s.TropicalCyclones, e)),
            Entry("/SANDSTORM", (f, s, e) => SigwxFeatureSet.Collect(PointPhenomenaExtractor.SandstormFromWafsFeature(f), s.Sandstorms, e)),
            Entry("/RADIATION", (f, s, e) => SigwxFeatureSet.Collect(PointPhenomenaExtractor.RadiationIncidentFromWafsFeature(f), s.RadiationIncidents, e))
        };

        public static Result<Sigwx> Map(WAFSSignificantWeatherForecastType dto)
        {
            var errors = new List<ValidationError>();

            ReportMetadataCodes.Metadata metadata = ReportMetadataCodes.Read(
                dto.ReportStatus, dto.PermissibleUsage, dto.PermissibleUsageReason, dto.PermissibleUsageSupplementary, dto.Id, errors);
            DateTimeOffset? issueTime = SigwxTimeReader.ReadRequiredInstant(dto.IssueTime, "issueTime", errors);
            (DateTimeOffset? validFrom, DateTimeOffset? validUntil) = SigwxTimeReader.ReadRequiredPeriod(dto.BoundingPeriod, "boundingPeriod", errors);

            string issuingCentre = dto.OriginatingCentre?.WorldAreaForecastCentre ?? string.Empty;

            ElevatedEnvelopeType? envelope = dto.BoundingVolume?.ElevatedEnvelope;
            VerticalLimit? lowerLimit = SigwxMeasureReader.ReadRequiredVertical(
                envelope?.LowerElevation,
                envelope?.LowerVerticalReference,
                "boundingVolume/ElevatedEnvelope/lowerElevation",
                errors);
            VerticalLimit? upperLimit = SigwxMeasureReader.ReadRequiredVertical(
                envelope?.UpperElevation,
                envelope?.UpperVerticalReference,
                "boundingVolume/ElevatedEnvelope/upperElevation",
                errors);

            // Partial results (AUD-M12): a feature that cannot be mapped is reported as a feature error of the chart,
            // which keeps every other feature. Chart-level content (metadata, times, bounding volume) stays mandatory, and
            // a chart whose features all fail is a failure.
            // Nothing is skipped silently (AUD-L4): a feature given only by reference (not resolved by this version) and a
            // feature whose phenomenon is outside the WAFS catalog are feature errors too.
            var features = new SigwxFeatureSet();
            var featureErrors = new List<ItemMappingError>();
            int featureCount = dto.Features?.Count ?? 0;
            for (int index = 0; index < featureCount; index++)
            {
                MeteorologicalFeaturePropertyType entry = dto.Features![index];
                MeteorologicalFeatureType? feature = entry.MeteorologicalFeature;
                if (feature == null)
                {
                    featureErrors.Add(new ItemMappingError(index, null, new[]
                    {
                        new ValidationError(
                            FeatureByReferenceCode,
                            string.IsNullOrWhiteSpace(entry.Href)
                                ? "The feature has neither inline content nor a reference."
                                : $"The feature is given only by the reference '{entry.Href}', which this version does not resolve.",
                            ErrorCategory.DomainMapping,
                            ValidationSeverity.Error,
                            dto.Id)
                    }));
                    continue;
                }

                var itemErrors = new List<ValidationError>();
                Extract(feature, features, itemErrors);
                if (itemErrors.Count > 0)
                {
                    featureErrors.Add(new ItemMappingError(index, feature.Id, itemErrors));
                }
            }

            if (featureCount > 0 && featureErrors.Count == featureCount)
            {
                errors.AddRange(featureErrors.SelectMany(e => e.Errors));
                errors.Add(new ValidationError(
                    "DOMAIN-SIGWX-NO-FEATURE",
                    "No feature of the WAFS chart could be mapped.",
                    ErrorCategory.DomainMapping,
                    ValidationSeverity.Error,
                    dto.Id));
            }

            if (errors.Count > 0)
            {
                return Result<Sigwx>.Failure(errors);
            }

            return Sigwx.Create(
                id: dto.Id,
                issuingCentre: issuingCentre,
                reportStatus: metadata.Status,
                permissibleUsage: metadata.Usage,
                issueTimeUtc: issueTime!.Value,
                validFromUtc: validFrom!.Value,
                validUntilUtc: validUntil!.Value,
                lowerLimit: lowerLimit!.Value,
                upperLimit: upperLimit!.Value,
                jetStreams: features.JetStreams,
                clouds: features.Clouds,
                tropopauses: features.Tropopauses,
                volcanoes: features.Volcanoes,
                turbulences: features.Turbulences,
                airframeIcings: features.AirframeIcings,
                tropicalCyclones: features.TropicalCyclones,
                sandstorms: features.Sandstorms,
                radiationIncidents: features.RadiationIncidents,
                permissibleUsageReason: metadata.Reason,
                permissibleUsageSupplementary: metadata.Supplementary,
                featureErrors: featureErrors);
        }

        /// <summary>Dispatches a feature to the first extractor whose suffix matches; an unknown phenomenon is a feature error.</summary>
        private static void Extract(MeteorologicalFeatureType feature, SigwxFeatureSet features, List<ValidationError> errors)
        {
            string phenomenonHref = feature.Phenomenon?.Href ?? string.Empty;
            foreach (KeyValuePair<string, FeatureExtraction> extractor in Extractors)
            {
                if (phenomenonHref.EndsWith(extractor.Key, StringComparison.OrdinalIgnoreCase))
                {
                    extractor.Value(feature, features, errors);
                    return;
                }
            }

            errors.Add(new ValidationError(
                PhenomenonUnsupportedCode,
                phenomenonHref.Length == 0
                    ? $"Feature '{feature.Id}' reports no phenomenon."
                    : $"Feature '{feature.Id}' reports the phenomenon '{phenomenonHref}', which is not a WAFS SIGWX phenomenon supported by this version.",
                ErrorCategory.DomainMapping,
                ValidationSeverity.Error,
                feature.Id));
        }

        private static KeyValuePair<string, FeatureExtraction> Entry(string suffix, FeatureExtraction extraction) =>
            new KeyValuePair<string, FeatureExtraction>(suffix, extraction);
    }
}
