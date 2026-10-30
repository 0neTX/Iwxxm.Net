using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Iwxxm.Net.Reports
{
    /// <summary>
    /// Domain Aggregate Root representing Quantitative Volcanic Ash Concentration Information (<c>QVACI</c>),
    /// introduced in ICAO/WMO IWXXM 2025-2.
    /// </summary>
    public sealed class Qvaci : IwxxmReport
    {
        public override string Id { get; }
        public override IwxxmReportType ReportType => IwxxmReportType.QuantitativeVolcanicAshConcentration;
        public override DateTimeOffset IssueTimeUtc { get; }
        public override ReportStatus ReportStatus { get; }
        public override PermissibleUsage PermissibleUsage { get; }
        public override PermissibleUsageReason? PermissibleUsageReason { get; }
        public override string? PermissibleUsageSupplementary { get; }
        public string OriginatingCentre { get; }
        public string? OriginatingCentreDesignator { get; }
        public DateTimeOffset? DispersionModelInitialisationTimeUtc { get; }
        public string? Remarks { get; }
        public override bool IsTranslationFailed { get; }
        public override string? TranslationFailedTac { get; }

        /// <inheritdoc />
        public override TResult Accept<TResult>(IIwxxmDocumentVisitor<TResult> visitor) =>
            (visitor ?? throw new ArgumentNullException(nameof(visitor))).Visit(this);

        public IReadOnlyList<Volcano> Volcanoes { get; }
        public IReadOnlyList<VolcanicAshConcentrationCloud> AshConcentrationClouds { get; }

        private Qvaci(
            string id,
            DateTimeOffset issueTimeUtc,
            ReportStatus reportStatus,
            PermissibleUsage permissibleUsage,
            PermissibleUsageReason? permissibleUsageReason,
            string? permissibleUsageSupplementary,
            string originatingCentre,
            string? originatingCentreDesignator,
            DateTimeOffset? dispersionModelInitialisationTimeUtc,
            string? remarks,
            bool isTranslationFailed,
            string? translationFailedTac,
            IReadOnlyList<Volcano> volcanoes,
            IReadOnlyList<VolcanicAshConcentrationCloud> ashConcentrationClouds)
        {
            Id = id;
            IssueTimeUtc = issueTimeUtc;
            ReportStatus = reportStatus;
            PermissibleUsage = permissibleUsage;
            PermissibleUsageReason = permissibleUsageReason;
            PermissibleUsageSupplementary = permissibleUsageSupplementary;
            OriginatingCentre = originatingCentre;
            OriginatingCentreDesignator = originatingCentreDesignator;
            DispersionModelInitialisationTimeUtc = dispersionModelInitialisationTimeUtc;
            Remarks = remarks;
            IsTranslationFailed = isTranslationFailed;
            TranslationFailedTac = translationFailedTac;
            Volcanoes = volcanoes;
            AshConcentrationClouds = ashConcentrationClouds;
        }

        public static Result<Qvaci> Create(
            string id,
            DateTimeOffset issueTimeUtc,
            ReportStatus reportStatus,
            PermissibleUsage permissibleUsage,
            string originatingCentre,
            string? originatingCentreDesignator = null,
            DateTimeOffset? dispersionModelInitialisationTimeUtc = null,
            string? remarks = null,
            bool isTranslationFailed = false,
            string? translationFailedTac = null,
            IEnumerable<Volcano>? volcanoes = null,
            IEnumerable<VolcanicAshConcentrationCloud>? ashConcentrationClouds = null,
            PermissibleUsageReason? permissibleUsageReason = null,
            string? permissibleUsageSupplementary = null)
        {
            var invariants = new DomainInvariants(id);
            invariants.RequireText(id, "DOMAIN-QVACI-ID", "The QVACI report gml:id identifier is required.");
            invariants.RequireMetadata(reportStatus, permissibleUsage, permissibleUsageReason);
            invariants.RequireIssueTime(issueTimeUtc);
            invariants.RequireUtc(dispersionModelInitialisationTimeUtc, "dispersion model initialisation time");

            if (invariants.HasErrors)
            {
                return Result<Qvaci>.Failure(invariants.Errors);
            }

            return Result<Qvaci>.Success(new Qvaci(
                id,
                issueTimeUtc,
                reportStatus,
                permissibleUsage,
                permissibleUsageReason,
                permissibleUsageSupplementary,
                originatingCentre ?? string.Empty,
                originatingCentreDesignator,
                dispersionModelInitialisationTimeUtc,
                remarks,
                isTranslationFailed,
                translationFailedTac,
                new ReadOnlyCollection<Volcano>(volcanoes?.ToList() ?? new List<Volcano>(0)),
                new ReadOnlyCollection<VolcanicAshConcentrationCloud>(ashConcentrationClouds?.ToList() ?? new List<VolcanicAshConcentrationCloud>(0))));
        }
    }
}
