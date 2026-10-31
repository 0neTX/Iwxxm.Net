using System.Collections.Generic;
using System.Xml.Linq;
using Iwxxm.Net.Infrastructure.Mapping.Xml;
using Iwxxm.Net.Reports;

namespace Iwxxm.Net.Infrastructure.Mapping.Reports
{
    /// <summary>
    /// Reads the TAC translation attributes of a report root (<c>common.xsd</c> <c>ReportType</c>, AUD-U8) into a
    /// <see cref="TranslationMetadata"/>; <c>null</c> when none is present. Unparseable times are mapping errors
    /// (<c>ACL-TIME-INVALID</c>); no value is invented.
    /// </summary>
    internal static class ReportTranslationReader
    {
        public static TranslationMetadata? Read(XElement root, string id, List<ValidationError> errors)
        {
            string? bulletinId = Attribute(root, "translatedBulletinID");
            string? receptionTime = Attribute(root, "translatedBulletinReceptionTime");
            string? centreDesignator = Attribute(root, "translationCentreDesignator");
            string? centreName = Attribute(root, "translationCentreName");
            string? translationTime = Attribute(root, "translationTime");

            if (bulletinId == null && receptionTime == null && centreDesignator == null && centreName == null && translationTime == null)
                return null;

            return new TranslationMetadata(
                bulletinId,
                GmlTimeReader.ParseTimePosition(receptionTime, "translatedBulletinReceptionTime", id, errors),
                centreDesignator,
                centreName,
                GmlTimeReader.ParseTimePosition(translationTime, "translationTime", id, errors));
        }

        /// <summary>Reads the metadata of <paramref name="root"/> and attaches it to a successfully mapped report.</summary>
        public static Result<TReport> Attach<TReport>(XElement root, Result<TReport> mapped)
            where TReport : IwxxmReport
        {
            if (mapped.IsFailure)
                return mapped;

            var errors = new List<ValidationError>();
            TranslationMetadata? translation = Read(root, mapped.Value.Id, errors);
            if (errors.Count > 0)
                return Result<TReport>.Failure(errors);

            mapped.Value.InitializeTranslation(translation);
            return mapped;
        }

        private static string? Attribute(XElement root, string name)
        {
            string? value = root.Attribute(name)?.Value.Trim();
            return string.IsNullOrEmpty(value) ? null : value;
        }
    }
}
