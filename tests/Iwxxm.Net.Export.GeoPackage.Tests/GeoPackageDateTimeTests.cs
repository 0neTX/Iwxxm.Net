using System;
using System.Globalization;
using System.Linq;
using Iwxxm.Net.Export.GeoPackage.Model;
using Xunit;

namespace Iwxxm.Net.Export.GeoPackage.Tests
{
    public sealed class GeoPackageDateTimeTests
    {
        private static DateTimeOffset At(int year, int month, int day, int hour, int minute, int second, int offsetMinutes, long extraTicks = 0) =>
            new DateTimeOffset(year, month, day, hour, minute, second, TimeSpan.FromMinutes(offsetMinutes)).AddTicks(extraTicks);

        [Theory]
        [InlineData(2026, 11, 14, 1, 11, 0, 0, "2026-11-14T01:11:00.000Z")]
        [InlineData(2026, 11, 14, 6, 41, 0, 330, "2026-11-14T01:11:00.000Z")]
        [InlineData(2026, 11, 13, 17, 11, 0, -480, "2026-11-14T01:11:00.000Z")]
        [InlineData(2026, 1, 1, 0, 0, 0, 0, "2026-01-01T00:00:00.000Z")]
        [InlineData(2026, 1, 1, 0, 30, 0, 60, "2025-12-31T23:30:00.000Z")]
        [InlineData(2023, 12, 31, 23, 30, 0, -300, "2024-01-01T04:30:00.000Z")]
        [InlineData(2024, 2, 29, 23, 59, 59, 0, "2024-02-29T23:59:59.000Z")]
        [InlineData(2024, 2, 29, 0, 30, 0, 60, "2024-02-28T23:30:00.000Z")]
        [InlineData(2024, 2, 28, 23, 30, 0, -60, "2024-02-29T00:30:00.000Z")]
        [InlineData(2023, 2, 28, 23, 30, 0, -60, "2023-03-01T00:30:00.000Z")]
        [InlineData(1, 1, 2, 5, 0, 0, 0, "0001-01-02T05:00:00.000Z")]
        public void Format_ConvertsToUtcFirst(int y, int mo, int d, int h, int mi, int s, int offsetMinutes, string expected) =>
            Assert.Equal(expected, GeoPackageDateTime.Format(At(y, mo, d, h, mi, s, offsetMinutes)));

        [Theory]
        [InlineData(0, "2026-11-14T01:11:00.000Z")]
        [InlineData(1234567, "2026-11-14T01:11:00.123Z")]
        [InlineData(9999999, "2026-11-14T01:11:00.999Z")]
        [InlineData(9990000, "2026-11-14T01:11:00.999Z")]
        [InlineData(9999, "2026-11-14T01:11:00.000Z")]
        [InlineData(10000, "2026-11-14T01:11:00.001Z")]
        public void Format_TruncatesBelowTheMillisecond(long ticks, string expected) =>
            Assert.Equal(expected, GeoPackageDateTime.Format(At(2026, 11, 14, 1, 11, 0, 0, ticks)));

        [Fact]
        public void Format_UtcKind_IsTakenAsIs() =>
            Assert.Equal("2026-11-14T01:11:00.500Z", GeoPackageDateTime.Format(new DateTime(2026, 11, 14, 1, 11, 0, 500, DateTimeKind.Utc)));

        [Theory]
        [InlineData(DateTimeKind.Local)]
        [InlineData(DateTimeKind.Unspecified)]
        public void Format_LocalAndUnspecifiedKinds_AreTakenAsLocalTime(DateTimeKind kind)
        {
            var local = new DateTime(2026, 7, 1, 12, 0, 0, kind);
            string expected = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), TimeZoneInfo.Local)
                .ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);

            Assert.Equal(expected, GeoPackageDateTime.Format(local));
        }

        [Theory]
        [InlineData("tr-TR")]
        [InlineData("th-TH")]
        [InlineData("ar-SA")]
        [InlineData("fa-IR")]
        [InlineData("")]
        public void FormatAndParse_DoNotDependOnTheCurrentCulture(string cultureName)
        {
            CultureInfo previous = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = new CultureInfo(cultureName);
                DateTimeOffset value = At(2026, 11, 14, 6, 41, 5, 330, 1234567);

                Assert.Equal("2026-11-14T01:11:05.123Z", GeoPackageDateTime.Format(value));
                Assert.Equal("2026-11-14T01:11:05.123Z", GeoPackageDateTime.Normalize("2026-11-14T06:41:05.1234567+05:30"));
                Assert.Equal(value.UtcDateTime, GeoPackageDateTime.ParseIso("2026-11-14T06:41:05.1234567+05:30")!.Value.UtcDateTime);
            }
            finally
            {
                CultureInfo.CurrentCulture = previous;
            }
        }

        [Theory]
        [InlineData("2026-11-14T01:11:00.0000000+00:00", 0)]
        [InlineData("2026-11-14T01:11:00.0000000Z", 0)]
        [InlineData("2026-11-14T01:11:00Z", 0)]
        [InlineData("2026-11-14T01:11:00+00:00", 0)]
        [InlineData("2026-11-14T01:11:00.5Z", 5000000)]
        [InlineData("2026-11-14T01:11:00.123Z", 1230000)]
        [InlineData("2026-11-14T01:11:00.1234567Z", 1234567)]
        public void ParseIso_Utc(string text, long ticks)
        {
            DateTimeOffset? parsed = GeoPackageDateTime.ParseIso(text);

            Assert.NotNull(parsed);
            Assert.Equal(TimeSpan.Zero, parsed!.Value.Offset);
            Assert.Equal(new DateTime(2026, 11, 14, 1, 11, 0, DateTimeKind.Utc).AddTicks(ticks), parsed.Value.UtcDateTime);
        }

        [Theory]
        [InlineData("2026-11-14T06:41:00.0000000+05:30", 330)]
        [InlineData("2026-11-13T17:11:00.0000000-08:00", -480)]
        [InlineData("2026-11-14T02:11:00+01:00", 60)]
        public void ParseIso_Offsets_AreKeptAndDenoteTheSameInstant(string text, int offsetMinutes)
        {
            DateTimeOffset? parsed = GeoPackageDateTime.ParseIso(text);

            Assert.NotNull(parsed);
            Assert.Equal(TimeSpan.FromMinutes(offsetMinutes), parsed!.Value.Offset);
            Assert.Equal(new DateTime(2026, 11, 14, 1, 11, 0, DateTimeKind.Utc), parsed.Value.UtcDateTime);
        }

        [Fact]
        public void ParseIso_RoundTripText_IsTheInverseOfTheGeoJsonInstantFormat()
        {
            DateTimeOffset value = At(2024, 2, 29, 23, 59, 59, 120, 1234567);
            string text = value.ToString("O", CultureInfo.InvariantCulture);

            Assert.Equal(value, GeoPackageDateTime.ParseIso(text));
            Assert.Equal(value.Offset, GeoPackageDateTime.ParseIso(text)!.Value.Offset);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" ")]
        [InlineData("garbage")]
        [InlineData("2026-11-14")]
        [InlineData("2026-11-14T01:11:00")]
        [InlineData("2026-11-14T01:11Z")]
        [InlineData("2026-11-14 01:11:00Z")]
        [InlineData(" 2026-11-14T01:11:00Z")]
        [InlineData("2026-11-14T01:11:00Z ")]
        [InlineData("2026-13-14T01:11:00Z")]
        [InlineData("2026-02-30T01:11:00Z")]
        [InlineData("2023-02-29T01:11:00Z")]
        [InlineData("2026-11-14T24:00:00Z")]
        [InlineData("2026-11-14T01:60:00Z")]
        [InlineData("2026-11-14T23:59:60Z")]
        [InlineData("2026-11-14T01:11:00+25:00")]
        [InlineData("2026-11-14T01:11:00.Z")]
        [InlineData("2026-11-14T01:11:00.12345678Z")]
        public void ParseIso_InvalidText_IsNull(string? text)
        {
            Assert.Null(GeoPackageDateTime.ParseIso(text));
            Assert.Null(GeoPackageDateTime.Normalize(text));
        }

        [Fact]
        public void Normalize_ProducesTheGeoPackageText()
        {
            Assert.Equal("2026-11-14T01:11:00.000Z", GeoPackageDateTime.Normalize("2026-11-14T01:11:00.0000000+00:00"));
            Assert.Equal("2026-11-14T01:11:00.000Z", GeoPackageDateTime.Normalize("2026-11-14T03:11:00+02:00"));
            Assert.Equal("2024-02-29T00:00:00.999Z", GeoPackageDateTime.Normalize("2024-02-29T00:00:00.9999999Z"));
        }

        [Theory]
        [MemberData(nameof(Samples))]
        public void FeatureIssueTime_IsTheIssueTimeOfItsReport(string sample)
        {
            var parsed = IwxxmParser.ParseFile(SampleFiles.PathOf(sample));
            Assert.True(parsed.IsSuccess);
            GeoPackageDocumentModel model = GeoPackageFeatureSource.Read(parsed.Value);

            foreach (GeoPackageFeature feature in model.Features)
            {
                Assert.NotNull(feature.IssueTime);
                GeoPackageReportRecord report = model.Reports.Single(r => r.ReportId == feature.ReportId);
                Assert.Equal(report.IssueTime, feature.IssueTime!.Value);
                Assert.Equal(GeoPackageDateTime.Format(report.IssueTime), GeoPackageDateTime.Normalize(feature.IssueTimeText));
            }
        }

        public static System.Collections.Generic.IEnumerable<object[]> Samples() => SampleFiles.All();
    }
}
