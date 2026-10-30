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
        public override string Id { get; }
        public override IwxxmReportType ReportType => IwxxmReportType.Airmet;
        public override DateTimeOffset IssueTimeUtc { get; }
        public override ReportStatus ReportStatus { get; }
        public override PermissibleUsage PermissibleUsage { get; }
        public override PermissibleUsageReason? PermissibleUsageReason { get; }
        public override string? PermissibleUsageSupplementary { get; }
        public string AtsUnitDesignator { get; }
        public string? AtsUnitName { get; }
        public string? MwoDesignator { get; }
        public string? FirDesignator { get; }
        public string? FirName { get; }
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

        public override bool IsTranslationFailed { get; }
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
