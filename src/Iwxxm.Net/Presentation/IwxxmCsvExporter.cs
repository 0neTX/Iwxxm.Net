using System;
using Iwxxm.Net.Application.Ports.Out;
using Iwxxm.Net.Reports;

namespace Iwxxm.Net
{
    /// <summary>
    /// Public facade for exporting any parsed <see cref="IwxxmDocument"/> Domain Aggregate Root into an RFC 4180 CSV
    /// string with unified columns and OGC WKT geometries.
    /// </summary>
    /// <remarks>
    /// Instances are created by <see cref="IwxxmParserBuilder.BuildCsvExporter"/> and delegate to the outbound
    /// <see cref="ICsvExporter"/> port, applying <see cref="CsvExportOptions.Default"/> when no options are given.
    /// <see cref="ToCsv"/> is a convenience wrapper over <see cref="Default"/>. An exporter failure is a value:
    /// <see cref="Export"/> returns a <see cref="Result{T}"/> and throws only for a <c>null</c> document.
    /// </remarks>
    public sealed class IwxxmCsvExporter
    {
        private static readonly Lazy<IwxxmCsvExporter> DefaultInstance =
            new Lazy<IwxxmCsvExporter>(() => new IwxxmParserBuilder().BuildCsvExporter(), isThreadSafe: true);

        private readonly ICsvExporter _exporter;

        /// <summary>Initializes a facade over the given outbound exporter.</summary>
        internal IwxxmCsvExporter(ICsvExporter exporter)
        {
            _exporter = exporter ?? throw new ArgumentNullException(nameof(exporter));
        }

        /// <summary>Gets the shared instance built with the default RFC 4180 exporter.</summary>
        public static IwxxmCsvExporter Default => DefaultInstance.Value;

        /// <summary>
        /// Serializes <paramref name="document"/> to an RFC 4180 CSV string.
        /// </summary>
        /// <returns>
        /// The CSV text, or a failure with <see cref="IwxxmErrorCodes.CsvExportError"/>
        /// (<see cref="ErrorCategory.DomainMapping"/>, located at the document id) when the exporter threw or returned nothing.
        /// </returns>
        /// <exception cref="ArgumentNullException"><paramref name="document"/> is <c>null</c> (a programming error).</exception>
        public Result<string> Export(IwxxmDocument document, CsvExportOptions? options = null)
        {
            if (document == null)
                throw new ArgumentNullException(nameof(document));

            try
            {
                string csv = _exporter.Export(document, options ?? CsvExportOptions.Default);
                if (csv == null)
                {
                    return Result<string>.Failure(
                        IwxxmErrorCodes.CsvExportError,
                        "The CSV exporter returned no document.",
                        ErrorCategory.DomainMapping,
                        document.Id);
                }

                return Result<string>.Success(csv);
            }
            catch (Exception ex) when (!(ex is OutOfMemoryException))
            {
                return Result<string>.Failure(
                    IwxxmErrorCodes.CsvExportError,
                    $"The CSV export failed: {ex.Message}",
                    ErrorCategory.DomainMapping,
                    document.Id);
            }
        }

        /// <summary>Serializes <paramref name="document"/> to an RFC 4180 CSV string using <see cref="Default"/>.</summary>
        /// <returns>See <see cref="Export"/>: the CSV text or a <see cref="IwxxmErrorCodes.CsvExportError"/> failure.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="document"/> is <c>null</c>.</exception>
        public static Result<string> ToCsv(IwxxmDocument document, CsvExportOptions? options = null) =>
            Default.Export(document, options);
    }
}
