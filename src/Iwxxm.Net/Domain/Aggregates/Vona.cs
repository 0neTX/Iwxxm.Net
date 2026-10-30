using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Iwxxm.Net.Reports
{
    /// <summary>
    /// Domain Aggregate Root representing a Volcano Observatory Notice for Aviation (<c>VONA</c>)
    /// under ICAO/WMO IWXXM 2025-2.
    /// </summary>
    public sealed class Vona : IwxxmReport
    {
        public override string Id { get; }
        public override IwxxmReportType ReportType => IwxxmReportType.VolcanoObservatoryNotice;
        public override DateTimeOffset IssueTimeUtc { get; }
        public override ReportStatus ReportStatus { get; }
        public override PermissibleUsage PermissibleUsage { get; }
        public override PermissibleUsageReason? PermissibleUsageReason { get; }
        public override string? PermissibleUsageSupplementary { get; }
        public string OriginatingCentre { get; }
        public string? OriginatingCentreDesignator { get; }
        public string? StateOrRegion { get; }
        public string? NoticeNumber { get; }
        /// <summary>Current aviation colour code (WMO code list <c>AviationColourCode</c>), or <c>null</c> when absent or nil.</summary>
        public CodeListValue<AviationColourCode>? CurrentColourCode { get; }
        /// <summary>Previous aviation colour code (WMO code list <c>AviationColourCode</c>), or <c>null</c> when absent or nil.</summary>
        public CodeListValue<AviationColourCode>? PreviousColourCode { get; }
        public string? Contacts { get; }
        public string? Remarks { get; }
        public string? NextNotice { get; }
        public override bool IsTranslationFailed { get; }
        public override string? TranslationFailedTac { get; }

        /// <inheritdoc />
        public override TResult Accept<TResult>(IIwxxmDocumentVisitor<TResult> visitor) =>
            (visitor ?? throw new ArgumentNullException(nameof(visitor))).Visit(this);

        public IReadOnlyList<Volcano> Volcanoes { get; }

        /// <summary>Ash cloud elevation as reported (value, unit and vertical reference), or <c>null</c> when not reported.</summary>
        public VerticalLimit? AshCloudElevation { get; }

        private Vona(
            string id,
            DateTimeOffset issueTimeUtc,
            ReportStatus reportStatus,
            PermissibleUsage permissibleUsage,
            PermissibleUsageReason? permissibleUsageReason,
            string? permissibleUsageSupplementary,
            string originatingCentre,
            string? originatingCentreDesignator,
            string? stateOrRegion,
            string? noticeNumber,
            CodeListValue<AviationColourCode>? currentColourCode,
            CodeListValue<AviationColourCode>? previousColourCode,
            string? contacts,
            string? remarks,
            string? nextNotice,
            bool isTranslationFailed,
            string? translationFailedTac,
            IReadOnlyList<Volcano> volcanoes,
            VerticalLimit? ashCloudElevation)
        {
            Id = id;
            IssueTimeUtc = issueTimeUtc;
            ReportStatus = reportStatus;
            PermissibleUsage = permissibleUsage;
            PermissibleUsageReason = permissibleUsageReason;
            PermissibleUsageSupplementary = permissibleUsageSupplementary;
            OriginatingCentre = originatingCentre;
            OriginatingCentreDesignator = originatingCentreDesignator;
            StateOrRegion = stateOrRegion;
            NoticeNumber = noticeNumber;
            CurrentColourCode = currentColourCode;
            PreviousColourCode = previousColourCode;
            Contacts = contacts;
            Remarks = remarks;
            NextNotice = nextNotice;
            IsTranslationFailed = isTranslationFailed;
            TranslationFailedTac = translationFailedTac;
            Volcanoes = volcanoes;
            AshCloudElevation = ashCloudElevation;
        }

        public static Result<Vona> Create(
            string id,
            DateTimeOffset issueTimeUtc,
            ReportStatus reportStatus,
            PermissibleUsage permissibleUsage,
            string originatingCentre,
            string? originatingCentreDesignator = null,
            string? stateOrRegion = null,
            string? noticeNumber = null,
            CodeListValue<AviationColourCode>? currentColourCode = null,
            CodeListValue<AviationColourCode>? previousColourCode = null,
            string? contacts = null,
            string? remarks = null,
            string? nextNotice = null,
            bool isTranslationFailed = false,
            string? translationFailedTac = null,
            IEnumerable<Volcano>? volcanoes = null,
            VerticalLimit? ashCloudElevation = null,
            PermissibleUsageReason? permissibleUsageReason = null,
            string? permissibleUsageSupplementary = null)
        {
            var invariants = new DomainInvariants(id);
            invariants.RequireText(id, "DOMAIN-VONA-ID", "The VONA report gml:id identifier is required.");
            invariants.RequireMetadata(reportStatus, permissibleUsage, permissibleUsageReason);
            invariants.RequireIssueTime(issueTimeUtc);

            if (invariants.HasErrors)
            {
                return Result<Vona>.Failure(invariants.Errors);
            }

            return Result<Vona>.Success(new Vona(
                id,
                issueTimeUtc,
                reportStatus,
                permissibleUsage,
                permissibleUsageReason,
                permissibleUsageSupplementary,
                originatingCentre ?? string.Empty,
                originatingCentreDesignator,
                stateOrRegion,
                noticeNumber,
                currentColourCode,
                previousColourCode,
                contacts,
                remarks,
                nextNotice,
                isTranslationFailed,
                translationFailedTac,
                new ReadOnlyCollection<Volcano>(volcanoes?.ToList() ?? new List<Volcano>(0)),
                ashCloudElevation));
        }
    }
}
