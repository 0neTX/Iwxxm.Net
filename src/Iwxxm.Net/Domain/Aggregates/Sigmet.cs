using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Iwxxm.Net.Reports
{
    /// <summary>
    /// Domain Aggregate Root representing an en-route Significant Meteorological Information report
    /// (<c>SIGMET</c>, <c>VolcanicAshSIGMET</c>, or <c>TropicalCycloneSIGMET</c>) under ICAO/WMO IWXXM 2025-2.
    /// </summary>
    public sealed class Sigmet : IwxxmReport
    {
        public override string Id { get; }
        public override IwxxmReportType ReportType { get; }
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

        /// <summary>Start of <c>validPeriod</c> (mandatory in <c>sigmet.xsd</c>); <c>null</c> only when given by an unresolved <c>xlink:href</c>.</summary>
        public DateTimeOffset? ValidFromUtc { get; }

        /// <summary>End of <c>validPeriod</c> (mandatory in <c>sigmet.xsd</c>); <c>null</c> only when given by an unresolved <c>xlink:href</c>.</summary>
        public DateTimeOffset? ValidUntilUtc { get; }
        /// <summary>Reported phenomenon (WMO code list), or <c>null</c> when absent or nil; a non-official code is kept as <c>Unknown</c> with its raw notation.</summary>
        public CodeListValue<SigmetPhenomenon>? Phenomenon { get; }
        public bool IsCancelReport { get; }
        public string? CancelledReportSequenceNumber { get; }

        /// <summary>Start of <c>cancelledReportValidPeriod</c> of a cancellation, or <c>null</c>.</summary>
        public DateTimeOffset? CancelledReportValidFromUtc { get; }

        /// <summary>End of <c>cancelledReportValidPeriod</c> of a cancellation, or <c>null</c>.</summary>
        public DateTimeOffset? CancelledReportValidUntilUtc { get; }
        public override bool IsTranslationFailed { get; }
        public override string? TranslationFailedTac { get; }

        /// <inheritdoc />
        public override TResult Accept<TResult>(IIwxxmDocumentVisitor<TResult> visitor) =>
            (visitor ?? throw new ArgumentNullException(nameof(visitor))).Visit(this);

        public Volcano? EruptingVolcano { get; }
        public string? TropicalCycloneName { get; }
        public IReadOnlyList<EnRoutePhenomenonVolume> EvolvingConditions { get; }
        public IReadOnlyList<EnRoutePhenomenonVolume> ForecastPositions { get; }

        private Sigmet(
            string id,
            IwxxmReportType reportType,
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
            CodeListValue<SigmetPhenomenon>? phenomenon,
            bool isCancelReport,
            string? cancelledReportSequenceNumber,
            DateTimeOffset? cancelledReportValidFromUtc,
            DateTimeOffset? cancelledReportValidUntilUtc,
            bool isTranslationFailed,
            string? translationFailedTac,
            Volcano? eruptingVolcano,
            string? tropicalCycloneName,
            IReadOnlyList<EnRoutePhenomenonVolume> evolvingConditions,
            IReadOnlyList<EnRoutePhenomenonVolume> forecastPositions)
        {
            Id = id;
            ReportType = reportType;
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
            EruptingVolcano = eruptingVolcano;
            TropicalCycloneName = tropicalCycloneName;
            EvolvingConditions = evolvingConditions;
            ForecastPositions = forecastPositions;
        }

        public static Result<Sigmet> Create(
            string id,
            IwxxmReportType reportType,
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
            CodeListValue<SigmetPhenomenon>? phenomenon = null,
            bool isCancelReport = false,
            string? cancelledReportSequenceNumber = null,
            bool isTranslationFailed = false,
            string? translationFailedTac = null,
            Volcano? eruptingVolcano = null,
            string? tropicalCycloneName = null,
            IEnumerable<EnRoutePhenomenonVolume>? evolvingConditions = null,
            IEnumerable<EnRoutePhenomenonVolume>? forecastPositions = null,
            PermissibleUsageReason? permissibleUsageReason = null,
            string? permissibleUsageSupplementary = null,
            DateTimeOffset? cancelledReportValidFromUtc = null,
            DateTimeOffset? cancelledReportValidUntilUtc = null)
        {
            var evolving = evolvingConditions?.ToList() ?? new List<EnRoutePhenomenonVolume>(0);
            var positions = forecastPositions?.ToList() ?? new List<EnRoutePhenomenonVolume>(0);

            var invariants = new DomainInvariants(id);
            invariants.RequireText(id, "DOMAIN-SIGMET-ID", "The SIGMET report gml:id identifier is required.");
            invariants.RequireText(atsUnitDesignator, "DOMAIN-SIGMET-ATS-UNIT", "The issuing Air Traffic Services unit designator is required.");
            invariants.RequireMetadata(reportStatus, permissibleUsage, permissibleUsageReason);
            invariants.RequireIssueTime(issueTimeUtc);
            invariants.RequireOrderedPeriod(validFromUtc, validUntilUtc, "DOMAIN-SIGMET-VALID-PERIOD", "SIGMET validity period");
            invariants.RequireOrderedPeriod(cancelledReportValidFromUtc, cancelledReportValidUntilUtc, "DOMAIN-SIGMET-CANCELLED-VALID-PERIOD", "cancelled SIGMET validity period");
            foreach (EnRoutePhenomenonVolume volume in evolving.Concat(positions))
            {
                invariants.Check(volume);
            }

            if (invariants.HasErrors)
            {
                return Result<Sigmet>.Failure(invariants.Errors);
            }

            return Result<Sigmet>.Success(new Sigmet(
                id,
                reportType,
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
                eruptingVolcano,
                tropicalCycloneName,
                new ReadOnlyCollection<EnRoutePhenomenonVolume>(evolving),
                new ReadOnlyCollection<EnRoutePhenomenonVolume>(positions)));
        }
    }
}
