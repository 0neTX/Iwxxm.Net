using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Iwxxm.Net.Reports;
using static Iwxxm.Net.Infrastructure.Mapping.Xml.IwxxmXmlNames;
using static Iwxxm.Net.Infrastructure.Mapping.Xml.XmlValueReader;

namespace Iwxxm.Net.Infrastructure.Mapping.Reports
{
    /// <summary>
    /// Maps a SWIM <c>collect:MeteorologicalBulletin</c> envelope onto <see cref="MeteorologicalBulletin"/>, delegating
    /// each <c>collect:meteorologicalInformation</c> report to the composite dispatcher.
    /// </summary>
    internal static class BulletinMapper
    {
        public static Result<MeteorologicalBulletin> Map(XElement bulletinElem, Func<XElement, Result<IwxxmReport>> mapReport)
        {
            string id = GetGmlId(bulletinElem);
            string bulletinIdentifier = bulletinElem.Element(CollectNs + "bulletinIdentifier")?.Value ?? string.Empty;

            // Partial results (AUD-M12 / AUD-U4): a report that cannot be mapped does not fail the bulletin; it is
            // reported as an item error of the bulletin, which keeps every other report.
            var childResults = new List<Result<IwxxmReport>>();
            var itemIds = new List<string?>();

            foreach (XElement metInfo in bulletinElem.Elements(CollectNs + "meteorologicalInformation"))
            {
                XElement? reportElem = metInfo.Elements().FirstOrDefault();
                if (reportElem == null)
                    continue;

                string? itemId = reportElem.Attribute(GmlNs + "id")?.Value;
                childResults.Add(MapIsolated(mapReport, reportElem, itemIds.Count, itemId));
                itemIds.Add(itemId);
            }

            return MeteorologicalBulletin.Create(id, bulletinIdentifier, childResults, itemIds);
        }

        /// <summary>
        /// Maps one entry so that an exception thrown by a report mapper (including a custom one) becomes a
        /// <c>MAPPER-ERROR</c> of that entry only. <see cref="OutOfMemoryException"/> is not a mapping outcome and is rethrown.
        /// </summary>
        private static Result<IwxxmReport> MapIsolated(Func<XElement, Result<IwxxmReport>> mapReport, XElement reportElem, int index, string? itemId)
        {
            try
            {
                return mapReport(reportElem) ?? Fail(index, itemId, "The report mapper returned no result.");
            }
            catch (Exception ex) when (!(ex is OutOfMemoryException))
            {
                return Fail(index, itemId, "The report mapper threw " + ex.GetType().Name + ": " + ex.Message);
            }
        }

        private static Result<IwxxmReport> Fail(int index, string? itemId, string detail) =>
            Result<IwxxmReport>.Failure(
                IwxxmErrorCodes.MapperError,
                "The report of entry #" + index + (itemId == null ? string.Empty : " ('" + itemId + "')") + " could not be mapped. " + detail,
                ErrorCategory.DomainMapping,
                itemId);
    }
}
