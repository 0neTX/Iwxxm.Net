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
        /// <inheritdoc />
        public override string Id { get; }
        /// <inheritdoc />
        public override IwxxmReportType ReportType => IwxxmReportType.VolcanoObservatoryNotice;
        /// <inheritdoc />
        public override DateTimeOffset IssueTimeUtc { get; }
        /// <inheritdoc />
        public override ReportStatus ReportStatus { get; }
        /// <inheritdoc />
        public override PermissibleUsage PermissibleUsage { get; }
        /// <inheritdoc />
        public override PermissibleUsageReason? PermissibleUsageReason { get; }
        /// <inheritdoc />
        public override string? PermissibleUsageSupplementary { get; }
        /// <summary>Gets the name of the originating volcano observatory (<c>aixm:name</c> of <c>iwxxm:originatingCentre</c>, falling back to its designator); empty only when the document carries neither.</summary>
        public string OriginatingCentre { get; }
        /// <summary>Gets the designator of the originating volcano observatory (<c>aixm:designator</c> of <c>iwxxm:originatingCentre</c>), or <c>null</c> when not reported.</summary>
        public string? OriginatingCentreDesignator { get; }
        /// <summary>Gets the State or region involved (<c>iwxxm:stateOrRegion</c>), or <c>null</c> when absent or nil.</summary>
        public string? StateOrRegion { get; }
        /// <summary>Gets the VONA notice number (<c>iwxxm:noticeNumber</c>), or <c>null</c> when absent or nil.</summary>
        public string? NoticeNumber { get; }
        /// <summary>Current aviation colour code (WMO code list <c>AviationColourCode</c>), or <c>null</c> when absent or nil.</summary>
        public CodeListValue<AviationColourCode>? CurrentColourCode { get; }
        /// <summary>Previous aviation colour code (WMO code list <c>AviationColourCode</c>), or <c>null</c> when absent or nil.</summary>
        public CodeListValue<AviationColourCode>? PreviousColourCode { get; }
        /// <summary>Gets the contact details of the State Volcano Observatory (<c>iwxxm:contacts</c>), or <c>null</c> when absent or nil.</summary>
        public string? Contacts { get; }
        /// <summary>Gets the free-text remarks (<c>iwxxm:remarks</c>), or <c>null</c> when absent or nil.</summary>
        public string? Remarks { get; }
        /// <summary>Gets the free-text indication of when the next VONA will be issued (<c>iwxxm:nextNotice</c>), or <c>null</c> when absent or nil.</summary>
        public string? NextNotice { get; }
        /// <inheritdoc />
        public override bool IsTranslationFailed { get; }
        /// <inheritdoc />
        public override string? TranslationFailedTac { get; }

        /// <inheritdoc />
        public override TResult Accept<TResult>(IIwxxmDocumentVisitor<TResult> visitor) =>
            (visitor ?? throw new ArgumentNullException(nameof(visitor))).Visit(this);

        /// <summary>Gets the volcanoes the notice refers to (features whose phenomenon is <c>VOLCANO</c>), in document order; empty when none is reported.</summary>
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

        /// <summary>Creates a volcano observatory notice for aviation, enforcing the minimal domain invariants and collecting every violation in the returned <see cref="Result{T}"/>.</summary>
        /// <param name="id">The <c>gml:id</c> of the report root element; required (not blank).</param>
        /// <param name="issueTimeUtc">The <c>iwxxm:issueTime</c> instant; required (not <c>default</c>) and expressed in UTC (offset zero).</param>
        /// <param name="reportStatus">The <c>reportStatus</c> attribute (<c>NORMAL</c>, <c>AMENDMENT</c> or <c>CORRECTION</c>); must be a defined value.</param>
        /// <param name="permissibleUsage">The <c>permissibleUsage</c> attribute (<c>OPERATIONAL</c> or <c>NON-OPERATIONAL</c>); must be a defined value.</param>
        /// <param name="permissibleUsageReason">The optional <c>permissibleUsageReason</c> (<c>TEST</c> or <c>EXERCISE</c>), meaningful for non-operational data; <c>null</c> when not reported. When given it must be a defined value.</param>
        /// <param name="permissibleUsageSupplementary">The optional free-text <c>permissibleUsageSupplementary</c>; <c>null</c> when not reported.</param>
        /// <param name="originatingCentre">The name of the originating volcano observatory (the designator when no name is given).</param>
        /// <param name="originatingCentreDesignator">The designator of the originating observatory; <c>null</c> when not reported.</param>
        /// <param name="stateOrRegion">The State or region involved; <c>null</c> when absent.</param>
        /// <param name="noticeNumber">The notice number; <c>null</c> when absent.</param>
        /// <param name="currentColourCode">The current aviation colour code (volcano level of alert); <c>null</c> when absent or nil.</param>
        /// <param name="previousColourCode">The previous aviation colour code; <c>null</c> when absent or nil.</param>
        /// <param name="contacts">The contact details; <c>null</c> when absent.</param>
        /// <param name="remarks">The remarks text; <c>null</c> when absent.</param>
        /// <param name="nextNotice">The next-notice indication; <c>null</c> when absent.</param>
        /// <param name="isTranslationFailed">Whether the TAC-to-XML translation failed (<c>translationFailedTAC</c> present).</param>
        /// <param name="translationFailedTac">The original TAC text that could not be translated; <c>null</c> when the translation succeeded.</param>
        /// <param name="volcanoes">The volcanoes the notice refers to; <c>null</c> is treated as empty.</param>
        /// <param name="ashCloudElevation">The reported ash cloud elevation with its unit and vertical reference; <c>null</c> when not reported.</param>
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
