using System;
using System.Collections.Generic;
using Iwxxm.Net.Reports;
using Iwxxm20252.Generated;

namespace Iwxxm.Net.Infrastructure.Mapping.Wafs
{
    /// <summary>Maps the compact <c>SIGWXReport</c> format onto <see cref="Sigwx"/>.</summary>
    internal static class CompactSigwxReportMapper
    {
        public static Result<Sigwx> Map(SIGWXReportType dto, UomAliasCollector aliases)
        {
            var errors = new List<ValidationError>();

            ReportMetadataCodes.Metadata metadata = ReportMetadataCodes.Read(
                dto.ReportStatus, dto.PermissibleUsage, dto.PermissibleUsageReason, dto.PermissibleUsageSupplementary, dto.Id, errors);
            DateTimeOffset? issueTime = SigwxTimeReader.ReadRequiredInstant(dto.IssueTime, "issueTime", errors);
            (DateTimeOffset? validFrom, DateTimeOffset? validUntil) = SigwxTimeReader.ReadRequiredPeriod(dto.ValidPeriod, "validPeriod", errors);

            // The compact format reports no vertical reference: none is assumed.
            VerticalLimit? lowerLimit = SigwxMeasureReader.ReadRequiredVertical(dto.LowerLimit, null, "lowerLimit", errors);
            VerticalLimit? upperLimit = SigwxMeasureReader.ReadRequiredVertical(dto.UpperLimit, null, "upperLimit", errors);

            var features = new SigwxFeatureSet();
            CollectAll(dto.JetStreams, jetStream => JetStreamExtractor.FromCompact(jetStream, aliases), features.JetStreams, errors);
            CollectAll(dto.Clouds, CloudExtractor.FromCompact, features.Clouds, errors);
            CollectAll(dto.Tropopauses, TropopauseExtractor.FromCompact, features.Tropopauses, errors);
            CollectAll(dto.Volcanoes, PointPhenomenaExtractor.VolcanoFromCompact, features.Volcanoes, errors);

            if (errors.Count > 0)
            {
                return Result<Sigwx>.Failure(errors);
            }

            return Sigwx.Create(
                id: dto.Id,
                issuingCentre: dto.IssuingCentre,
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
                permissibleUsageReason: metadata.Reason,
                permissibleUsageSupplementary: metadata.Supplementary);
        }

        private static void CollectAll<TDto, TPhenomenon>(
            IEnumerable<TDto>? dtos,
            Func<TDto, Result<TPhenomenon>> extract,
            List<TPhenomenon> target,
            List<ValidationError> errors)
        {
            if (dtos == null)
                return;

            foreach (TDto dto in dtos)
            {
                SigwxFeatureSet.Collect(extract(dto), target, errors);
            }
        }
    }
}
