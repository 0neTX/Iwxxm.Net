using System;
using System.Collections.Generic;
using System.Xml.Linq;
using Iwxxm.Net.Infrastructure.Mapping.Xml;

namespace Iwxxm.Net.Infrastructure.Mapping.Reports
{
    /// <summary>
    /// Content common to every IWXXM 2025-2 report root (<c>common.xsd</c> ReportType): <c>gml:id</c>, report metadata,
    /// <c>translationFailedTAC</c> and the mandatory <c>issueTime</c>. Errors are appended in that order.
    /// </summary>
    internal readonly struct ReportHeader
    {
        private ReportHeader(string id, ReportMetadataCodes.Metadata metadata, string? failedTac, DateTimeOffset issueTime)
        {
            Id = id;
            Metadata = metadata;
            FailedTac = failedTac;
            IssueTime = issueTime;
        }

        public string Id { get; }

        public ReportMetadataCodes.Metadata Metadata { get; }

        /// <summary>Gets the raw <c>translationFailedTAC</c> text, or <c>null</c> when absent.</summary>
        public string? FailedTac { get; }

        /// <summary>Gets a value indicating whether <c>translationFailedTAC</c> is present.</summary>
        public bool IsTranslationFailed => FailedTac != null;

        public DateTimeOffset IssueTime { get; }

        public static ReportHeader Read(XElement root, List<ValidationError> errors)
        {
            string id = XmlValueReader.GetGmlId(root);
            ReportMetadataCodes.Metadata metadata = ReportMetadataCodes.Read(root, id, errors);
            string? failedTac = root.Attribute("translationFailedTAC")?.Value;
            DateTimeOffset issueTime = GmlTimeReader.ReadIssueTime(root, id, errors);
            return new ReportHeader(id, metadata, failedTac, issueTime);
        }
    }
}
