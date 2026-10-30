using System;

namespace Iwxxm.Net.Reports
{
    /// <summary>
    /// Closed root of every document the library maps: an ICAO/WMO IWXXM 2025-2 meteorological report
    /// (<see cref="IwxxmReport"/>) or a SWIM <c>collect:MeteorologicalBulletin</c> envelope (<see cref="MeteorologicalBulletin"/>).
    /// </summary>
    /// <remarks>
    /// <para>The hierarchy is closed: the constructor is not accessible outside this assembly, so the only branches are
    /// <see cref="IwxxmReport"/> (meteorological reports carrying their own status, usage and translation metadata) and
    /// the sealed <see cref="MeteorologicalBulletin"/> envelope (bulletin identifier, contained reports and metadata
    /// derived from them). Every concrete type has exactly one <see cref="IIwxxmDocumentVisitor{TResult}"/> overload, so
    /// visitors stay exhaustive: a new document type is a library change, never an external extension.</para>
    /// <para>The members below are the ones both branches can honestly provide: a bulletin derives its issue time,
    /// status and usage from its reports (see <see cref="MeteorologicalBulletin"/>), and carries no per-report fields
    /// such as the translation metadata.</para>
    /// </remarks>
    public abstract class IwxxmDocument
    {
        /// <summary>
        /// Initializes a new document. Only the types of this assembly can derive from this type.
        /// </summary>
        private protected IwxxmDocument()
        {
        }

        /// <summary>
        /// Gets the unique <c>gml:id</c> identifier of the document.
        /// </summary>
        public abstract string Id { get; }

        /// <summary>
        /// Gets the IWXXM 2025-2 classification of the document (it also distinguishes METAR from SPECI, the three SIGMET
        /// kinds and the COLLECT bulletin).
        /// </summary>
        public abstract IwxxmReportType ReportType { get; }

        /// <summary>
        /// Gets the UTC timestamp when the document was issued (derived from the contained reports for a bulletin).
        /// </summary>
        public abstract DateTimeOffset IssueTimeUtc { get; }

        /// <summary>
        /// Gets the report status (<c>reportStatus</c>: <c>NORMAL</c>, <c>AMENDMENT</c>, <c>CORRECTION</c>; derived for a bulletin).
        /// The attribute is required by the official schema; a missing or unknown value is a mapping error, never a default.
        /// </summary>
        public abstract ReportStatus ReportStatus { get; }

        /// <summary>
        /// Gets the permissible usage (<c>permissibleUsage</c>: <c>OPERATIONAL</c> or <c>NON-OPERATIONAL</c>; derived for a bulletin).
        /// The attribute is required by the official schema; a report without it is never presented as operational.
        /// </summary>
        public abstract PermissibleUsage PermissibleUsage { get; }

        /// <summary>
        /// Gets the optional reason for non-operational data (<c>permissibleUsageReason</c>: <c>TEST</c> or <c>EXERCISE</c>),
        /// or <c>null</c> when not reported (derived for a bulletin).
        /// </summary>
        public abstract PermissibleUsageReason? PermissibleUsageReason { get; }

        /// <summary>
        /// Dispatches to the <see cref="IIwxxmDocumentVisitor{TResult}"/> overload of this concrete document type.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="visitor"/> is <c>null</c>.</exception>
        public abstract TResult Accept<TResult>(IIwxxmDocumentVisitor<TResult> visitor);
    }
}
