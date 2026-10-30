using System;

namespace Iwxxm.Net.Reports
{
    /// <summary>
    /// Enumeration of all aeronautical meteorological report types supported by the ICAO/WMO IWXXM 2025-2 suite
    /// and SWIM Collect 2014 envelopes.
    /// </summary>
    public enum IwxxmReportType
    {
        SignificantWeather = 1,
        Metar = 2,
        Speci = 3,
        Taf = 4,
        Sigmet = 5,
        VolcanicAshSigmet = 6,
        TropicalCycloneSigmet = 7,
        Airmet = 8,
        VolcanoObservatoryNotice = 9,
        QuantitativeVolcanicAshConcentration = 10,
        VolcanicAshAdvisory = 11,
        TropicalCycloneAdvisory = 12,
        SpaceWeatherAdvisory = 13,
        MeteorologicalBulletin = 14
    }

    /// <summary>
    /// Polymorphic Domain Aggregate Root base for all ICAO/WMO IWXXM 2025-2 reports
    /// and SWIM <c>collect:MeteorologicalBulletin</c> envelopes.
    /// </summary>
    /// <remarks>
    /// The hierarchy is closed: the constructor is not accessible outside this assembly, so the only subtypes are the
    /// sealed report aggregates of the IWXXM 2025-2 catalog, each with exactly one
    /// <see cref="IIwxxmReportVisitor{TResult}"/> overload. A new report type is a library change, never an external
    /// extension, which keeps visitors exhaustive.
    /// </remarks>
    public abstract class IwxxmReport
    {
        /// <summary>
        /// Initializes a new report. Only the report aggregates of this assembly can derive from this type.
        /// </summary>
        private protected IwxxmReport()
        {
        }

        /// <summary>
        /// Gets the unique <c>gml:id</c> identifier of the report.
        /// </summary>
        public abstract string Id { get; }

        /// <summary>
        /// Gets the specific IWXXM 2025-2 report classification.
        /// </summary>
        public abstract IwxxmReportType ReportType { get; }

        /// <summary>
        /// Gets the UTC timestamp when the report was issued.
        /// </summary>
        public abstract DateTimeOffset IssueTimeUtc { get; }

        /// <summary>
        /// Gets the report status (<c>reportStatus</c>: <c>NORMAL</c>, <c>AMENDMENT</c>, <c>CORRECTION</c>).
        /// The attribute is required by the official schema; a missing or unknown value is a mapping error, never a default.
        /// </summary>
        public abstract ReportStatus ReportStatus { get; }

        /// <summary>
        /// Gets the permissible usage (<c>permissibleUsage</c>: <c>OPERATIONAL</c> or <c>NON-OPERATIONAL</c>).
        /// The attribute is required by the official schema; a report without it is never presented as operational.
        /// </summary>
        public abstract PermissibleUsage PermissibleUsage { get; }

        /// <summary>
        /// Gets the optional reason for non-operational data (<c>permissibleUsageReason</c>: <c>TEST</c> or <c>EXERCISE</c>),
        /// or <c>null</c> when not reported.
        /// </summary>
        public abstract PermissibleUsageReason? PermissibleUsageReason { get; }

        /// <summary>
        /// Gets the optional human-readable <c>permissibleUsageSupplementary</c> text, or <c>null</c> when not reported.
        /// </summary>
        public abstract string? PermissibleUsageSupplementary { get; }

        /// <summary>
        /// Gets a value indicating whether TAC-to-XML translation failed (<c>translationFailedTAC</c> present).
        /// </summary>
        public abstract bool IsTranslationFailed { get; }

        /// <summary>
        /// Gets the raw Traditional Alphanumeric Code (TAC) text when <see cref="IsTranslationFailed"/> is <c>true</c>.
        /// </summary>
        public abstract string? TranslationFailedTac { get; }

        /// <summary>
        /// Dispatches to the <see cref="IIwxxmReportVisitor{TResult}"/> overload of this concrete report type.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="visitor"/> is <c>null</c>.</exception>
        public abstract TResult Accept<TResult>(IIwxxmReportVisitor<TResult> visitor);
    }
}
