using System;
using System.Collections.Generic;
using Iwxxm.Net.Reports;
using Iwxxm20252.Generated;

namespace Iwxxm.Net.Infrastructure.Mapping.Sigwx
{
    /// <summary>
    /// Maps the official WMO IWXXM 2025-2 <c>WAFSSignificantWeatherForecast</c> onto <see cref="SignificantWeatherReport"/>,
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

        public static Result<SignificantWeatherReport> Map(WAFSSignificantWeatherForecastType dto)
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

            var features = new SigwxFeatureSet();
            if (dto.Features != null)
            {
                foreach (MeteorologicalFeaturePropertyType featureProp in dto.Features)
                {
                    // An external xlink:href feature reference without inline payload is skipped.
                    MeteorologicalFeatureType? feature = featureProp.MeteorologicalFeature;
                    if (feature != null)
                    {
                        Extract(feature, features, errors);
                    }
                }
            }

            if (errors.Count > 0)
            {
                return Result<SignificantWeatherReport>.Failure(errors);
            }

            return SignificantWeatherReport.Create(
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
                permissibleUsageSupplementary: metadata.Supplementary);
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
