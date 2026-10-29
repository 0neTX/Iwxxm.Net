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
        public static Result<MeteorologicalBulletin> Map(XElement bulletinElem, Func<XElement, Result<IIwxxmReport>> mapReport)
        {
            string id = GetGmlId(bulletinElem);
            string bulletinIdentifier = bulletinElem.Element(CollectNs + "bulletinIdentifier")?.Value ?? string.Empty;

            var childResults = new List<Result<IIwxxmReport>>();
            var errors = new List<ValidationError>();

            foreach (XElement metInfo in bulletinElem.Elements(CollectNs + "meteorologicalInformation"))
            {
                XElement? reportElem = metInfo.Elements().FirstOrDefault();
                if (reportElem == null)
                    continue;

                Result<IIwxxmReport> childResult = mapReport(reportElem);
                childResults.Add(childResult);
                if (childResult.IsFailure)
                {
                    errors.AddRange(childResult.Errors);
                }
            }

            if (errors.Count > 0)
            {
                return Result<MeteorologicalBulletin>.Failure(errors);
            }

            return MeteorologicalBulletin.Create(id, bulletinIdentifier, childResults);
        }
    }
}
