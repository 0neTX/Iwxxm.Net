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
            var features = new SigwxFeatureSet();
            var featureErrors = new List<ItemMappingError>();
            int inlineFeatureCount = 0;
            if (dto.Features != null)
            {
                for (int index = 0; index < dto.Features.Count; index++)
                {
                    // An external xlink:href feature reference without inline payload is skipped.
                    MeteorologicalFeatureType? feature = dto.Features[index].MeteorologicalFeature;
                    if (feature == null)
                        continue;

                    inlineFeatureCount++;
                    var itemErrors = new List<ValidationError>();
                    Extract(feature, features, itemErrors);
                    if (itemErrors.Count > 0)
                    {
                        featureErrors.Add(new ItemMappingError(index, feature.Id, itemErrors));
                    }
                }
            }

            if (inlineFeatureCount > 0 && featureErrors.Count == inlineFeatureCount)
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

        /// <summary>Dispatches a feature to the first extractor whose suffix matches; unknown phenomena are ignored.</summary>
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
        }

        private static KeyValuePair<string, FeatureExtraction> Entry(string suffix, FeatureExtraction extraction) =>
            new KeyValuePair<string, FeatureExtraction>(suffix, extraction);
    }
}
