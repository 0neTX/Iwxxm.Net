using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Iwxxm.Net.Domain.Common;

namespace Iwxxm.Net.Domain.Aggregates
{
    /// <summary>
    /// Domain Aggregate Root representing a WMO/SWIM Collective Bulletin (<c>collect:MeteorologicalBulletin</c>,
    /// <c>http://def.wmo.int/collect/2014</c>) that bundles one or more IWXXM 2025-2 reports.
    /// Supports both aggregate access to valid reports (<see cref="Reports"/>) and individual per-report
    /// validation results (<see cref="ReportResults"/>).
    /// </summary>
    public sealed class MeteorologicalBulletin : IIwxxmReport
    {
        public string Id { get; }
        public IwxxmReportType ReportType => IwxxmReportType.MeteorologicalBulletin;
        public string BulletinIdentifier { get; }
        public DateTimeOffset IssueTimeUtc { get; }
        public string ReportStatus => "NORMAL";
        public string PermissibleUsage => "OPERATIONAL";
        public bool IsTranslationFailed => false;
        public string? TranslationFailedTac => null;

        /// <summary>
        /// Gets all successfully parsed IWXXM 2025-2 reports contained inside this collective bulletin.
        /// </summary>
        public IReadOnlyList<IIwxxmReport> Reports { get; }

        /// <summary>
        /// Gets the individual <see cref="Result{IIwxxmReport}"/> outcomes for every <c>collect:meteorologicalInformation</c>
        /// entry in the bulletin, enabling partial-success processing on AMHS/SWIM feeds.
        /// </summary>
        public IReadOnlyList<Result<IIwxxmReport>> ReportResults { get; }

        private MeteorologicalBulletin(
            string id,
            string bulletinIdentifier,
            DateTimeOffset issueTimeUtc,
            IReadOnlyList<IIwxxmReport> reports,
            IReadOnlyList<Result<IIwxxmReport>> reportResults)
        {
            Id = id;
            BulletinIdentifier = bulletinIdentifier;
            IssueTimeUtc = issueTimeUtc;
            Reports = reports;
            ReportResults = reportResults;
        }

        public static Result<MeteorologicalBulletin> Create(
            string id,
            string bulletinIdentifier,
            IEnumerable<Result<IIwxxmReport>> reportResults)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return Result<MeteorologicalBulletin>.Failure(
                    "DOMAIN-BULLETIN-ID",
                    "The MeteorologicalBulletin gml:id identifier is required.",
                    ErrorCategory.DomainMapping);
            }

            if (string.IsNullOrWhiteSpace(bulletinIdentifier))
            {
                return Result<MeteorologicalBulletin>.Failure(
                    "DOMAIN-BULLETIN-IDENTIFIER",
                    "The collect:bulletinIdentifier is required.",
                    ErrorCategory.DomainMapping,
                    id);
            }

            var resultsList = reportResults?.ToList() ?? new List<Result<IIwxxmReport>>(0);
            var validReports = resultsList
                .Where(r => r.IsSuccess)
                .Select(r => r.Value)
                .ToList();

            DateTimeOffset issueTime = validReports.Count > 0
                ? validReports[0].IssueTimeUtc
                : DateTimeOffset.MinValue;

            return Result<MeteorologicalBulletin>.Success(new MeteorologicalBulletin(
                id,
                bulletinIdentifier.Trim(),
                issueTime,
                new ReadOnlyCollection<IIwxxmReport>(validReports),
                new ReadOnlyCollection<Result<IIwxxmReport>>(resultsList)));
        }
    }
}
