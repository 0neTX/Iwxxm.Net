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

                childResults.Add(mapReport(reportElem));
                itemIds.Add(reportElem.Attribute(GmlNs + "id")?.Value);
            }

            return MeteorologicalBulletin.Create(id, bulletinIdentifier, childResults, itemIds);
        }
    }
}
