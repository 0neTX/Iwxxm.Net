using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Iwxxm.Net.Reports
{
    /// <summary>
    /// Domain Aggregate Root representing an Airmen's Meteorological Information (<c>AIRMET</c>) report
    /// under ICAO/WMO IWXXM 2025-2.
    /// </summary>
    /// <remarks>
    /// A cancellation (<c>AIRMET ... CNL</c>, <c>@isCancelReport="true"</c>) carries <see cref="IsCancelReport"/>, the
    /// cancelled report sequence number and validity, and never a phenomenon or evolving condition: it does not describe
    /// an active hazard (<c>iwxxm.sch</c> AIRMET.AIRMET-3).
    /// </remarks>
    public sealed class Airmet : IwxxmReport
    {
        /// <inheritdoc />
        public override string Id { get; }
        /// <inheritdoc />
        public override IwxxmReportType ReportType => IwxxmReportType.Airmet;
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
        /// <summary>Gets the designator of the Air Traffic Services unit serving the FIR or CTA the report refers to (<c>aixm:designator</c> of <c>iwxxm:issuingAirTrafficServicesUnit</c>); never blank.</summary>
        public string AtsUnitDesignator { get; }
        /// <summary>Gets the name of the issuing ATS unit (<c>aixm:name</c> of <c>iwxxm:issuingAirTrafficServicesUnit</c>), or <c>null</c> when not reported.</summary>
        public string? AtsUnitName { get; }
        /// <summary>Gets the designator of the Meteorological Watch Office that originated the report (<c>aixm:designator</c> of <c>iwxxm:originatingMeteorologicalWatchOffice</c>), or <c>null</c> when not reported.</summary>
        public string? MwoDesignator { get; }
        /// <summary>Gets the designator of the FIR, UIR or CTA the report applies to (<c>aixm:designator</c> of <c>iwxxm:issuingAirTrafficServicesRegion</c>), or <c>null</c> when not reported.</summary>
        public string? FirDesignator { get; }
        /// <summary>Gets the name of the FIR, UIR or CTA the report applies to (<c>aixm:name</c> of <c>iwxxm:issuingAirTrafficServicesRegion</c>), or <c>null</c> when not reported.</summary>
        public string? FirName { get; }
        /// <summary>Gets the sequence number of the message (<c>iwxxm:sequenceNumber</c>, for example <c>5</c>, <c>A3</c> or <c>2</c>), kept as text; <c>null</c> when not reported.</summary>
        public string? SequenceNumber { get; }
        /// <summary>Start of <c>validPeriod</c> (mandatory in <c>airmet.xsd</c>); <c>null</c> only when given by an unresolved <c>xlink:href</c>.</summary>
        public DateTimeOffset? ValidFromUtc { get; }

        /// <summary>End of <c>validPeriod</c> (mandatory in <c>airmet.xsd</c>); <c>null</c> only when given by an unresolved <c>xlink:href</c>.</summary>
        public DateTimeOffset? ValidUntilUtc { get; }
        /// <summary>Reported phenomenon (WMO code list), or <c>null</c> when absent or nil; a non-official code is kept as <c>Unknown</c> with its raw notation.</summary>
        public CodeListValue<AirmetPhenomenon>? Phenomenon { get; }

        /// <summary><c>@isCancelReport</c>: <c>true</c> for a cancellation (<c>CNL</c>) of an earlier AIRMET.</summary>
        public bool IsCancelReport { get; }

        /// <summary><c>cancelledReportSequenceNumber</c> of a cancellation, or <c>null</c>.</summary>
        public string? CancelledReportSequenceNumber { get; }

        /// <summary>Start of <c>cancelledReportValidPeriod</c> of a cancellation, or <c>null</c>.</summary>
        public DateTimeOffset? CancelledReportValidFromUtc { get; }

        /// <summary>End of <c>cancelledReportValidPeriod</c> of a cancellation, or <c>null</c>.</summary>
        public DateTimeOffset? CancelledReportValidUntilUtc { get; }

        /// <summary>Gets a value indicating whether the report describes an active hazard (not a cancellation).</summary>
        public bool IsActiveHazard => !IsCancelReport;

        /// <inheritdoc />
        public override bool IsTranslationFailed { get; }
        /// <inheritdoc />
        public override string? TranslationFailedTac { get; }

        /// <inheritdoc />
        public override TResult Accept<TResult>(IIwxxmDocumentVisitor<TResult> visitor) =>
            (visitor ?? throw new ArgumentNullException(nameof(visitor))).Visit(this);

        /// <summary>
        /// <c>analysis/AIRMETEvolvingConditionCollection/member</c> conditions: volume plus AIRMET-specific content
        /// (cloud, surface visibility, surface wind). Always empty for a cancellation.
        /// </summary>
        public IReadOnlyList<AirmetEvolvingCondition> EvolvingConditions { get; }

        private Airmet(
            string id,
            DateTimeOffset issueTimeUtc,
            ReportStatus reportStatus,
            PermissibleUsage permissibleUsage,
            PermissibleUsageReason? permissibleUsageReason,
            string? permissibleUsageSupplementary,
            string atsUnitDesignator,
            string? atsUnitName,
            string? mwoDesignator,
            string? firDesignator,
            string? firName,
            string? sequenceNumber,
            DateTimeOffset? validFromUtc,
            DateTimeOffset? validUntilUtc,
            CodeListValue<AirmetPhenomenon>? phenomenon,
            bool isCancelReport,
            string? cancelledReportSequenceNumber,
            DateTimeOffset? cancelledReportValidFromUtc,
            DateTimeOffset? cancelledReportValidUntilUtc,
            bool isTranslationFailed,
            string? translationFailedTac,
            IReadOnlyList<AirmetEvolvingCondition> evolvingConditions)
        {
            Id = id;
            IssueTimeUtc = issueTimeUtc;
            ReportStatus = reportStatus;
            PermissibleUsage = permissibleUsage;
            PermissibleUsageReason = permissibleUsageReason;
            PermissibleUsageSupplementary = permissibleUsageSupplementary;
            AtsUnitDesignator = atsUnitDesignator;
            AtsUnitName = atsUnitName;
            MwoDesignator = mwoDesignator;
            FirDesignator = firDesignator;
            FirName = firName;
            SequenceNumber = sequenceNumber;
            ValidFromUtc = validFromUtc;
            ValidUntilUtc = validUntilUtc;
            Phenomenon = phenomenon;
            IsCancelReport = isCancelReport;
            CancelledReportSequenceNumber = cancelledReportSequenceNumber;
            CancelledReportValidFromUtc = cancelledReportValidFromUtc;
            CancelledReportValidUntilUtc = cancelledReportValidUntilUtc;
            IsTranslationFailed = isTranslationFailed;
            TranslationFailedTac = translationFailedTac;
            EvolvingConditions = evolvingConditions;
        }

        /// <summary>Creates an AIRMET, enforcing the minimal domain invariants (a cancellation carries no phenomenon or evolving condition) and collecting every violation in the returned <see cref="Result{T}"/>.</summary>
        /// <param name="id">The <c>gml:id</c> of the report root element; required (not blank).</param>
        /// <param name="issueTimeUtc">The <c>iwxxm:issueTime</c> instant; required (not <c>default</c>) and expressed in UTC (offset zero).</param>
        /// <param name="reportStatus">The <c>reportStatus</c> attribute (<c>NORMAL</c>, <c>AMENDMENT</c> or <c>CORRECTION</c>); must be a defined value.</param>
        /// <param name="permissibleUsage">The <c>permissibleUsage</c> attribute (<c>OPERATIONAL</c> or <c>NON-OPERATIONAL</c>); must be a defined value.</param>
        /// <param name="permissibleUsageReason">The optional <c>permissibleUsageReason</c> (<c>TEST</c> or <c>EXERCISE</c>), meaningful for non-operational data; <c>null</c> when not reported. When given it must be a defined value.</param>
        /// <param name="permissibleUsageSupplementary">The optional free-text <c>permissibleUsageSupplementary</c>; <c>null</c> when not reported.</param>
        /// <param name="atsUnitDesignator">The designator of the issuing ATS unit (<c>aixm:designator</c>); required (not blank).</param>
        /// <param name="atsUnitName">The name of the issuing ATS unit; <c>null</c> when not reported.</param>
        /// <param name="mwoDesignator">The designator of the originating Meteorological Watch Office; <c>null</c> when not reported.</param>
        /// <param name="firDesignator">The designator of the FIR, UIR or CTA concerned; <c>null</c> when not reported.</param>
        /// <param name="firName">The name of the FIR, UIR or CTA concerned; <c>null</c> when not reported.</param>
        /// <param name="sequenceNumber">The message sequence number; <c>null</c> when not reported.</param>
        /// <param name="validFromUtc">The start of <c>iwxxm:validPeriod</c> in UTC; <c>null</c> only when the period is given by an unresolved <c>xlink:href</c>.</param>
        /// <param name="validUntilUtc">The end of <c>iwxxm:validPeriod</c> in UTC; must be strictly after <paramref name="validFromUtc"/> when both are given.</param>
        /// <param name="isTranslationFailed">Whether the TAC-to-XML translation failed (<c>translationFailedTAC</c> present).</param>
        /// <param name="translationFailedTac">The original TAC text that could not be translated; <c>null</c> when the translation succeeded.</param>
        /// <param name="cancelledReportSequenceNumber">The sequence number of the cancelled report; <c>null</c> unless the report is a cancellation.</param>
        /// <param name="cancelledReportValidFromUtc">The start of <c>iwxxm:cancelledReportValidPeriod</c> in UTC; <c>null</c> when not reported.</param>
        /// <param name="cancelledReportValidUntilUtc">The end of <c>iwxxm:cancelledReportValidPeriod</c> in UTC; must be strictly after the start when both are given.</param>
        /// <param name="isCancelReport">Whether the report cancels an earlier one (<c>isCancelReport</c>).</param>
        /// <param name="phenomenon">The reported phenomenon from the WMO code list; <c>null</c> when absent or nil.</param>
        /// <param name="evolvingConditions">The observed or forecast conditions in document order; <c>null</c> is treated as empty. Must be empty for a cancellation.</param>
        public static Result<Airmet> Create(
            string id,
            DateTimeOffset issueTimeUtc,
            ReportStatus reportStatus,
            PermissibleUsage permissibleUsage,
            string atsUnitDesignator,
            string? atsUnitName,
            string? mwoDesignator,
            string? firDesignator,
            string? firName,
            string? sequenceNumber,
            DateTimeOffset? validFromUtc,
            DateTimeOffset? validUntilUtc,
            CodeListValue<AirmetPhenomenon>? phenomenon = null,
            bool isTranslationFailed = false,
            string? translationFailedTac = null,
            IEnumerable<AirmetEvolvingCondition>? evolvingConditions = null,
            PermissibleUsageReason? permissibleUsageReason = null,
            string? permissibleUsageSupplementary = null,
            bool isCancelReport = false,
            string? cancelledReportSequenceNumber = null,
            DateTimeOffset? cancelledReportValidFromUtc = null,
            DateTimeOffset? cancelledReportValidUntilUtc = null)
        {
            var evolving = evolvingConditions?.ToList() ?? new List<AirmetEvolvingCondition>(0);

            var invariants = new DomainInvariants(id);
            invariants.RequireText(id, "DOMAIN-AIRMET-ID", "The AIRMET report gml:id identifier is required.");
            invariants.RequireText(atsUnitDesignator, "DOMAIN-AIRMET-ATS-UNIT", "The issuing Air Traffic Services unit designator is required.");
            invariants.RequireMetadata(reportStatus, permissibleUsage, permissibleUsageReason);
            invariants.RequireIssueTime(issueTimeUtc);
            invariants.RequireOrderedPeriod(validFromUtc, validUntilUtc, "DOMAIN-AIRMET-VALID-PERIOD", "AIRMET validity period");
            invariants.RequireOrderedPeriod(cancelledReportValidFromUtc, cancelledReportValidUntilUtc, "DOMAIN-AIRMET-CANCELLED-VALID-PERIOD", "cancelled AIRMET validity period");
            if (isCancelReport && (phenomenon != null || evolving.Count > 0))
            {
                invariants.Add("DOMAIN-AIRMET-CANCEL-CONTENT",
                    "A cancellation AIRMET cannot carry a phenomenon or evolving conditions (iwxxm.sch AIRMET.AIRMET-3).");
            }

            foreach (AirmetEvolvingCondition condition in evolving)
            {
                if (condition == null)
                {
                    invariants.Add("DOMAIN-AIRMET-CONDITION-NULL", "An AIRMET evolving condition cannot be null.");
                    continue;
                }

                invariants.Check(condition);
            }

            if (invariants.HasErrors)
            {
                return Result<Airmet>.Failure(invariants.Errors);
            }

            return Result<Airmet>.Success(new Airmet(
                id,
                issueTimeUtc,
                reportStatus,
                permissibleUsage,
                permissibleUsageReason,
                permissibleUsageSupplementary,
                atsUnitDesignator.Trim(),
                atsUnitName,
                mwoDesignator,
                firDesignator,
                firName,
                sequenceNumber,
                validFromUtc,
                validUntilUtc,
                phenomenon,
                isCancelReport,
                cancelledReportSequenceNumber,
                cancelledReportValidFromUtc,
                cancelledReportValidUntilUtc,
                isTranslationFailed,
                translationFailedTac,
                new ReadOnlyCollection<AirmetEvolvingCondition>(evolving)));
        }
    }
}
