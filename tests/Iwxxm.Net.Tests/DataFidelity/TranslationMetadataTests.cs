using System;
using System.IO;
using System.Linq;
using Iwxxm.Net.Reports;
using Xunit;

namespace Iwxxm.Net.Tests.DataFidelity
{
    /// <summary>TAC translation metadata of every report (AUD-U8), checked against the official *-translation-failed examples.</summary>
    public sealed class TranslationMetadataTests
    {
        private static readonly DateTimeOffset Reception = new DateTimeOffset(2014, 5, 15, 15, 29, 0, TimeSpan.Zero);
        private static readonly DateTimeOffset Translated = new DateTimeOffset(2014, 5, 15, 15, 30, 0, TimeSpan.Zero);

        [Theory]
        [InlineData("taf-translation-failed.xml")]
        [InlineData("airmet-translation-failed.xml")]
        [InlineData("va-advisory-translation-failed.xml")]
        public void TranslationFailedExamples_ExposeTheFullTranslationMetadata(string fileName)
        {
            IwxxmReport report = ParseReport(fileName);

            Assert.True(report.IsTranslationFailed);
            Assert.Equal(
                new TranslationMetadata("TTAAiiCCCYYGGgg", Reception, "YUZZ", "Fictional translation centre", Translated),
                report.Translation);
        }

        [Fact]
        public void TranslationMetadata_OfEveryTranslationFailedExample_IsComplete()
        {
            foreach (string path in Directory.GetFiles(Path.Combine("Samples", "wmo-examples"), "*translation-failed*.xml"))
            {
                Result<IwxxmDocument> result = IwxxmParser.ParseFile(path);
                Assert.True(result.IsSuccess, path + ": " + string.Join(" | ", result.Errors));

                IwxxmReport report = result.Value is MeteorologicalBulletin bulletin
                    ? Assert.Single(bulletin.Reports)
                    : Assert.IsAssignableFrom<IwxxmReport>(result.Value);
                TranslationMetadata translation = Assert.IsType<TranslationMetadata>(report.Translation);
                Assert.NotNull(translation.TranslatedBulletinId);
                Assert.NotNull(translation.TranslatedBulletinReceptionTimeUtc);
                Assert.Equal("YUZZ", translation.TranslationCentreDesignator);
                Assert.Equal("Fictional translation centre", translation.TranslationCentreName);
                Assert.Equal(TimeSpan.Zero, translation.TranslationTimeUtc!.Value.Offset);
            }
        }

        [Fact]
        public void SigmetInBulletin_KeepsItsOwnTranslationMetadata()
        {
            MeteorologicalBulletin bulletin = IwxxmParser.ParseBulletinFile(SampleDocuments.PathOf("sigmet-translation-failed-collect.xml")).Value;
            IwxxmReport sigmet = Assert.Single(bulletin.Reports);

            Assert.Equal("WSYU31 YUSO 101000", sigmet.Translation!.TranslatedBulletinId);
            Assert.Equal(new DateTimeOffset(2012, 8, 10, 10, 5, 0, TimeSpan.Zero), sigmet.Translation.TranslatedBulletinReceptionTimeUtc);
        }

        [Fact]
        public void ReportsWithoutTranslationAttributes_HaveNoTranslationMetadata()
        {
            Assert.Null(ParseReport("metar-A3-1.xml").Translation);
            Assert.Null(IwxxmSigwxParser.ParseFile(SampleDocuments.PathOf("WAFS-Example.xml")).Value.Translation);
        }

        [Fact]
        public void PartialTranslationMetadata_KeepsOnlyWhatIsReported()
        {
            string xml = SampleDocuments.Text("metar-A3-1.xml").Replace(
                "<iwxxm:METAR",
                "<iwxxm:METAR translationCentreDesignator=\"YUZZ\" translationTime=\"2012-08-22T16:31:00+02:00\"");

            IwxxmReport report = Assert.IsAssignableFrom<IwxxmReport>(IwxxmParser.Parse(xml).Value);

            Assert.Equal(new TranslationMetadata(null, null, "YUZZ", null, new DateTimeOffset(2012, 8, 22, 14, 31, 0, TimeSpan.Zero)), report.Translation);
            Assert.Equal(TimeSpan.Zero, report.Translation!.TranslationTimeUtc!.Value.Offset);
        }

        private static IwxxmReport ParseReport(string fileName)
        {
            Result<IwxxmDocument> result = IwxxmParser.ParseFile(SampleDocuments.PathOf(fileName));
            Assert.True(result.IsSuccess, string.Join(" | ", result.Errors));
            return Assert.IsAssignableFrom<IwxxmReport>(result.Value);
        }
    }
}
