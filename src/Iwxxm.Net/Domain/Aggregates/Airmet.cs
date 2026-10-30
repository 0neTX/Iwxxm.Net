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
        public override bool IsTranslationFailed { get; }
        public override string? TranslationFailedTac { get; }

        /// <inheritdoc />
        public override TResult Accept<TResult>(IIwxxmDocumentVisitor<TResult> visitor) =>
            (visitor ?? throw new ArgumentNullException(nameof(visitor))).Visit(this);

        public IReadOnlyList<EnRoutePhenomenonVolume> EvolvingConditions { get; }

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
            bool isTranslationFailed,
            string? translationFailedTac,
            IReadOnlyList<EnRoutePhenomenonVolume> evolvingConditions)
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
            IEnumerable<EnRoutePhenomenonVolume>? evolvingConditions = null,
            PermissibleUsageReason? permissibleUsageReason = null,
            string? permissibleUsageSupplementary = null)
        {
            var evolving = evolvingConditions?.ToList() ?? new List<EnRoutePhenomenonVolume>(0);

            var invariants = new DomainInvariants(id);
            invariants.RequireText(id, "DOMAIN-AIRMET-ID", "The AIRMET report gml:id identifier is required.");
            invariants.RequireText(atsUnitDesignator, "DOMAIN-AIRMET-ATS-UNIT", "The issuing Air Traffic Services unit designator is required.");
            invariants.RequireMetadata(reportStatus, permissibleUsage, permissibleUsageReason);
            invariants.RequireIssueTime(issueTimeUtc);
            invariants.RequireOrderedPeriod(validFromUtc, validUntilUtc, "DOMAIN-AIRMET-VALID-PERIOD", "AIRMET validity period");
            foreach (EnRoutePhenomenonVolume volume in evolving)
            {
                invariants.Check(volume);
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
                isTranslationFailed,
                translationFailedTac,
                new ReadOnlyCollection<EnRoutePhenomenonVolume>(evolving)));
        }
    }
}
