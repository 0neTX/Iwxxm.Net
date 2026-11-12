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
        /// <inheritdoc />
        public override string Id { get; }
        /// <inheritdoc />
        public override IwxxmReportType ReportType => IwxxmReportType.QuantitativeVolcanicAshConcentration;
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
        /// <summary>Gets the name of the institution that created the information (<c>aixm:name</c> of <c>iwxxm:originatingCentre</c>, falling back to its designator); empty only when the document carries neither.</summary>
        public string OriginatingCentre { get; }
        /// <summary>Gets the designator of the originating centre (<c>aixm:designator</c> of <c>iwxxm:originatingCentre</c>), or <c>null</c> when not reported.</summary>
        public string? OriginatingCentreDesignator { get; }
        /// <summary>Gets the start of the dispersion model forecast integration (<c>iwxxm:dispersionModelInitialisationTime</c>), or <c>null</c> when not reported.</summary>
        public DateTimeOffset? DispersionModelInitialisationTimeUtc { get; }
        /// <summary>Gets the additional free-text information provided with the QVA-CI (<c>iwxxm:remarks</c>), or <c>null</c> when absent or nil.</summary>
        public string? Remarks { get; }
        /// <inheritdoc />
        public override bool IsTranslationFailed { get; }
        /// <inheritdoc />
        public override string? TranslationFailedTac { get; }

        /// <inheritdoc />
        public override TResult Accept<TResult>(IIwxxmDocumentVisitor<TResult> visitor) =>
            (visitor ?? throw new ArgumentNullException(nameof(visitor))).Visit(this);

        /// <summary>Gets the erupting volcanoes of the information (features whose phenomenon is <c>VOLCANO</c>), in document order; empty when none is reported.</summary>
        public IReadOnlyList<Volcano> Volcanoes { get; }
        /// <summary>Gets the modelled ash concentration areas (features whose phenomenon is <c>VOLCANIC_ASH</c>), each with its polygon, vertical limits, concentration and time, in document order; empty when none is reported.</summary>
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

        /// <summary>Creates a QVA-CI report, enforcing the minimal domain invariants and collecting every violation in the returned <see cref="Result{T}"/>.</summary>
        /// <param name="id">The <c>gml:id</c> of the report root element; required (not blank).</param>
        /// <param name="issueTimeUtc">The <c>iwxxm:issueTime</c> instant; required (not <c>default</c>) and expressed in UTC (offset zero).</param>
        /// <param name="reportStatus">The <c>reportStatus</c> attribute (<c>NORMAL</c>, <c>AMENDMENT</c> or <c>CORRECTION</c>); must be a defined value.</param>
        /// <param name="permissibleUsage">The <c>permissibleUsage</c> attribute (<c>OPERATIONAL</c> or <c>NON-OPERATIONAL</c>); must be a defined value.</param>
        /// <param name="permissibleUsageReason">The optional <c>permissibleUsageReason</c> (<c>TEST</c> or <c>EXERCISE</c>), meaningful for non-operational data; <c>null</c> when not reported. When given it must be a defined value.</param>
        /// <param name="permissibleUsageSupplementary">The optional free-text <c>permissibleUsageSupplementary</c>; <c>null</c> when not reported.</param>
        /// <param name="originatingCentre">The name of the originating centre (the designator when no name is given).</param>
        /// <param name="originatingCentreDesignator">The designator of the originating centre; <c>null</c> when not reported.</param>
        /// <param name="dispersionModelInitialisationTimeUtc">The dispersion model initialisation time in UTC; <c>null</c> when not reported.</param>
        /// <param name="remarks">Additional free-text information; <c>null</c> when absent.</param>
        /// <param name="isTranslationFailed">Whether the TAC-to-XML translation failed (<c>translationFailedTAC</c> present).</param>
        /// <param name="translationFailedTac">The original TAC text that could not be translated; <c>null</c> when the translation succeeded.</param>
        /// <param name="volcanoes">The erupting volcanoes; <c>null</c> is treated as empty.</param>
        /// <param name="ashConcentrationClouds">The ash concentration areas; <c>null</c> is treated as empty.</param>
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
