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
        /// <inheritdoc />
        public override string Id { get; }
        /// <inheritdoc />
        public override IwxxmReportType ReportType { get; }
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

        /// <summary>Start of <c>validPeriod</c> (mandatory in <c>sigmet.xsd</c>); <c>null</c> only when given by an unresolved <c>xlink:href</c>.</summary>
        public DateTimeOffset? ValidFromUtc { get; }

        /// <summary>End of <c>validPeriod</c> (mandatory in <c>sigmet.xsd</c>); <c>null</c> only when given by an unresolved <c>xlink:href</c>.</summary>
        public DateTimeOffset? ValidUntilUtc { get; }
        /// <summary>Reported phenomenon (WMO code list), or <c>null</c> when absent or nil; a non-official code is kept as <c>Unknown</c> with the reference as written as notation.</summary>
        public CodeListValue<SigmetPhenomenon>? Phenomenon { get; }
        /// <summary>Gets a value indicating whether the SIGMET cancels a previously issued one (<c>isCancelReport="true"</c>); <c>false</c> when the attribute is false or absent.</summary>
        public bool IsCancelReport { get; }
        /// <summary>Gets the sequence number of the earlier report that this cancellation cancels (<c>iwxxm:cancelledReportSequenceNumber</c>), or <c>null</c> when the report is not a cancellation.</summary>
        public string? CancelledReportSequenceNumber { get; }

        /// <summary>Start of <c>cancelledReportValidPeriod</c> of a cancellation, or <c>null</c>.</summary>
        public DateTimeOffset? CancelledReportValidFromUtc { get; }

        /// <summary>End of <c>cancelledReportValidPeriod</c> of a cancellation, or <c>null</c>.</summary>
        public DateTimeOffset? CancelledReportValidUntilUtc { get; }
        /// <inheritdoc />
        public override bool IsTranslationFailed { get; }
        /// <inheritdoc />
        public override string? TranslationFailedTac { get; }

        /// <inheritdoc />
        public override TResult Accept<TResult>(IIwxxmDocumentVisitor<TResult> visitor) =>
            (visitor ?? throw new ArgumentNullException(nameof(visitor))).Visit(this);

        /// <summary>Gets the erupting volcano of a volcanic ash SIGMET (<c>iwxxm:eruptingVolcano</c>, required by the schema except in cancellations), or <c>null</c> for other SIGMET kinds and when not reported.</summary>
        public Volcano? EruptingVolcano { get; }
        /// <summary>Gets the name of the tropical cyclone of a tropical cyclone SIGMET (<c>metce:name</c> of <c>iwxxm:tropicalCyclone</c>; <c>NN</c> may be used when the cyclone is unnamed), or <c>null</c> for other SIGMET kinds and when not reported.</summary>
        public string? TropicalCycloneName { get; }
        /// <summary>Gets the observed or forecast conditions of the phenomenon (<c>SIGMETEvolvingCondition</c> members of <c>iwxxm:analysisCollection</c>), one volume per reported area, in document order; empty for a cancellation.</summary>
        public IReadOnlyList<EnRoutePhenomenonVolume> EvolvingConditions { get; }
        /// <summary>Gets the forecast positions of the phenomenon at an instant within the valid period (<c>SIGMETPosition</c> members of <c>iwxxm:forecastPositionAnalysis</c>), in document order; empty when none is reported.</summary>
        public IReadOnlyList<EnRoutePhenomenonVolume> ForecastPositions { get; }

        /// <summary>
        /// Evolving conditions of the TC SIGMET <c>supplementaryAnalysisCollection</c> entries (CB areas analysed in addition to
        /// the main analysis), in document order; empty otherwise.
        /// </summary>
        public IReadOnlyList<EnRoutePhenomenonVolume> SupplementaryEvolvingConditions { get; }

        /// <summary>Forecast positions of the TC SIGMET <c>supplementaryAnalysisCollection</c> entries; empty otherwise.</summary>
        public IReadOnlyList<EnRoutePhenomenonVolume> SupplementaryForecastPositions { get; }

        /// <summary>Designator of the FIR to which the volcanic ash has moved (<c>volcanicAshMovedToFIR</c>); reported only on SIGMET cancellation reports.</summary>
        public string? VolcanicAshMovedToFirDesignator { get; }

        /// <summary>Name of the FIR to which the volcanic ash has moved (<c>volcanicAshMovedToFIR</c>); reported only on SIGMET cancellation reports.</summary>
        public string? VolcanicAshMovedToFirName { get; }

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
            IReadOnlyList<EnRoutePhenomenonVolume> forecastPositions,
            IReadOnlyList<EnRoutePhenomenonVolume> supplementaryEvolvingConditions,
            IReadOnlyList<EnRoutePhenomenonVolume> supplementaryForecastPositions,
            string? volcanicAshMovedToFirDesignator,
            string? volcanicAshMovedToFirName)
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
            SupplementaryEvolvingConditions = supplementaryEvolvingConditions;
            SupplementaryForecastPositions = supplementaryForecastPositions;
            VolcanicAshMovedToFirDesignator = volcanicAshMovedToFirDesignator;
            VolcanicAshMovedToFirName = volcanicAshMovedToFirName;
        }

        /// <summary>Creates a SIGMET of any of the three kinds, enforcing the minimal domain invariants and collecting every violation in the returned <see cref="Result{T}"/>.</summary>
        /// <param name="id">The <c>gml:id</c> of the report root element; required (not blank).</param>
        /// <param name="issueTimeUtc">The <c>iwxxm:issueTime</c> instant; required (not <c>default</c>) and expressed in UTC (offset zero).</param>
        /// <param name="reportStatus">The <c>reportStatus</c> attribute (<c>NORMAL</c>, <c>AMENDMENT</c> or <c>CORRECTION</c>); must be a defined value.</param>
        /// <param name="permissibleUsage">The <c>permissibleUsage</c> attribute (<c>OPERATIONAL</c> or <c>NON-OPERATIONAL</c>); must be a defined value.</param>
        /// <param name="permissibleUsageReason">The optional <c>permissibleUsageReason</c> (<c>TEST</c> or <c>EXERCISE</c>), meaningful for non-operational data; <c>null</c> when not reported. When given it must be a defined value.</param>
        /// <param name="permissibleUsageSupplementary">The optional free-text <c>permissibleUsageSupplementary</c>; <c>null</c> when not reported.</param>
        /// <param name="reportType">Which SIGMET kind the report is: <see cref="IwxxmReportType.Sigmet"/>, <see cref="IwxxmReportType.VolcanicAshSigmet"/> or <see cref="IwxxmReportType.TropicalCycloneSigmet"/>.</param>
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
        /// <param name="eruptingVolcano">The erupting volcano of a volcanic ash SIGMET; <c>null</c> otherwise.</param>
        /// <param name="tropicalCycloneName">The tropical cyclone name of a tropical cyclone SIGMET; <c>null</c> otherwise.</param>
        /// <param name="evolvingConditions">The observed or forecast condition volumes in document order; <c>null</c> is treated as empty. Direction of motion and speed of each volume are checked.</param>
        /// <param name="forecastPositions">The forecast position volumes in document order; <c>null</c> is treated as empty. Each volume's invariants are checked.</param>
        /// <param name="supplementaryEvolvingConditions">The conditions of the supplementary analyses of a tropical cyclone SIGMET (<c>supplementaryAnalysisCollection</c>); <c>null</c> is treated as empty.</param>
        /// <param name="supplementaryForecastPositions">The forecast positions of the supplementary analyses; <c>null</c> is treated as empty.</param>
        /// <param name="volcanicAshMovedToFirDesignator">The designator of the FIR the volcanic ash has moved to (reported only in cancellations); <c>null</c> otherwise.</param>
        /// <param name="volcanicAshMovedToFirName">The name of the FIR the volcanic ash has moved to; <c>null</c> otherwise.</param>
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
            DateTimeOffset? cancelledReportValidUntilUtc = null,
            IEnumerable<EnRoutePhenomenonVolume>? supplementaryEvolvingConditions = null,
            IEnumerable<EnRoutePhenomenonVolume>? supplementaryForecastPositions = null,
            string? volcanicAshMovedToFirDesignator = null,
            string? volcanicAshMovedToFirName = null)
        {
            var evolving = evolvingConditions?.ToList() ?? new List<EnRoutePhenomenonVolume>(0);
            var positions = forecastPositions?.ToList() ?? new List<EnRoutePhenomenonVolume>(0);
            var supplementaryEvolving = supplementaryEvolvingConditions?.ToList() ?? new List<EnRoutePhenomenonVolume>(0);
            var supplementaryPositions = supplementaryForecastPositions?.ToList() ?? new List<EnRoutePhenomenonVolume>(0);

            var invariants = new DomainInvariants(id);
            invariants.RequireText(id, "DOMAIN-SIGMET-ID", "The SIGMET report gml:id identifier is required.");
            invariants.RequireText(atsUnitDesignator, "DOMAIN-SIGMET-ATS-UNIT", "The issuing Air Traffic Services unit designator is required.");
            invariants.RequireMetadata(reportStatus, permissibleUsage, permissibleUsageReason);
            invariants.RequireIssueTime(issueTimeUtc);
            invariants.RequireOrderedPeriod(validFromUtc, validUntilUtc, "DOMAIN-SIGMET-VALID-PERIOD", "SIGMET validity period");
            invariants.RequireOrderedPeriod(cancelledReportValidFromUtc, cancelledReportValidUntilUtc, "DOMAIN-SIGMET-CANCELLED-VALID-PERIOD", "cancelled SIGMET validity period");
            foreach (EnRoutePhenomenonVolume volume in evolving.Concat(positions).Concat(supplementaryEvolving).Concat(supplementaryPositions))
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
                new ReadOnlyCollection<EnRoutePhenomenonVolume>(positions),
                new ReadOnlyCollection<EnRoutePhenomenonVolume>(supplementaryEvolving),
                new ReadOnlyCollection<EnRoutePhenomenonVolume>(supplementaryPositions),
                volcanicAshMovedToFirDesignator,
                volcanicAshMovedToFirName));
        }
    }
}
