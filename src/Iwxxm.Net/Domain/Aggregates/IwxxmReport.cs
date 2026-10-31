using System;

namespace Iwxxm.Net.Reports
{
    /// <summary>
    /// Closed branch of <see cref="IwxxmDocument"/> for the ICAO/WMO IWXXM 2025-2 meteorological reports: every report
    /// carries its own status, permissible usage and TAC translation metadata (<c>common.xsd</c> <c>ReportType</c>).
    /// </summary>
    /// <remarks>
    /// The constructor is not accessible outside this assembly: the only subtypes are the report aggregates of the
    /// IWXXM 2025-2 catalog. A SWIM <c>collect:MeteorologicalBulletin</c> is not a report (see <see cref="MeteorologicalBulletin"/>).
    /// </remarks>
    public abstract class IwxxmReport : IwxxmDocument
    {
        /// <summary>
        /// Initializes a new report. Only the report aggregates of this assembly can derive from this type.
        /// </summary>
        private protected IwxxmReport()
        {
        }

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
        /// Gets the TAC translation metadata (<c>translatedBulletinID</c>, <c>translatedBulletinReceptionTime</c>,
        /// <c>translationCentreDesignator</c>, <c>translationCentreName</c>, <c>translationTime</c>), or <c>null</c> when
        /// the report carries none of them (it was not translated from TAC).
        /// </summary>
        public TranslationMetadata? Translation { get; private set; }

        /// <summary>
        /// Attaches the translation metadata read by the Anti-Corruption Layer. Called once, before the aggregate is
        /// returned to any caller; the report is immutable from then on.
        /// </summary>
        /// <exception cref="InvalidOperationException">The metadata was already attached.</exception>
        internal void InitializeTranslation(TranslationMetadata? translation)
        {
            if (Translation != null)
                throw new InvalidOperationException("The translation metadata of a report can only be attached once.");

            Translation = translation;
        }
    }
}
