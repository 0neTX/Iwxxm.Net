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
    /// Polymorphic Domain Aggregate Root contract for all ICAO/WMO IWXXM 2025-2 reports
    /// and SWIM <c>collect:MeteorologicalBulletin</c> envelopes.
    /// </summary>
    public interface IIwxxmReport
    {
        /// <summary>
        /// Gets the unique <c>gml:id</c> identifier of the report.
        /// </summary>
        string Id { get; }

        /// <summary>
        /// Gets the specific IWXXM 2025-2 report classification.
        /// </summary>
        IwxxmReportType ReportType { get; }

        /// <summary>
        /// Gets the UTC timestamp when the report was issued.
        /// </summary>
        DateTimeOffset IssueTimeUtc { get; }

        /// <summary>
        /// Gets the report status (e.g., <c>NORMAL</c>, <c>AMENDMENT</c>, <c>CORRECTION</c>).
        /// </summary>
        string ReportStatus { get; }

        /// <summary>
        /// Gets the operational usage indicator (<c>OPERATIONAL</c> or <c>NON-OPERATIONAL</c>).
        /// </summary>
        string PermissibleUsage { get; }

        /// <summary>
        /// Gets a value indicating whether TAC-to-XML translation failed (<c>translationFailedTAC</c> present).
        /// </summary>
        bool IsTranslationFailed { get; }

        /// <summary>
        /// Gets the raw Traditional Alphanumeric Code (TAC) text when <see cref="IsTranslationFailed"/> is <c>true</c>.
        /// </summary>
        string? TranslationFailedTac { get; }
    }
}
