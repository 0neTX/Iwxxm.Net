using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using Iwxxm.Net.Export.GeoPackage.Model;
using Iwxxm.Net.Reports;
using Microsoft.Data.Sqlite;
using Xunit;
using static Iwxxm.Net.Export.GeoPackage.Tests.GeoPackageTestDatabase;

namespace Iwxxm.Net.Export.GeoPackage.Tests
{
    /// <summary>
    /// The validity period (<c>valid_from_utc</c>, <c>valid_until_utc</c>) of the feature tables and of <c>iwxxm_reports</c>:
    /// the values of SIGWX, SIGMET, AIRMET and TAF, NULL where the report states none, and the 15 charts of the Met Office
    /// bulletin told apart by it.
    /// </summary>
    public sealed class GeoPackageValidityTests
    {
        private static readonly DateTimeOffset T0 = new DateTimeOffset(2026, 3, 1, 6, 0, 0, TimeSpan.Zero);

        private static string WriteToTempFile(IwxxmDocument document)
        {
            GeoPackageDocumentModel model = GeoPackageFeatureSource.Read(document);
            string path = NewPath();
            try
            {
                var options = new GeoPackageWriteOptions(T0, GeoPackageVersion.Version14);
                using (SqliteConnection connection = GeoPackageSqlite.Open(path))
                using (SqliteTransaction transaction = connection.BeginTransaction())
                {
                    GeoPackageSchema.CreateBase(transaction, options.Version);
                    GeoPackageDataTables.Write(transaction, model, options, CancellationToken.None);
                    transaction.Commit();
                }

                return path;
            }
            catch
            {
                File.Delete(path);
                throw;
            }
        }

        private static GeoSurface Area() =>
            GeoPolygon.Create(new[] { new GeoCoordinate(0, 0), new GeoCoordinate(0, 10), new GeoCoordinate(10, 10), new GeoCoordinate(10, 0), new GeoCoordinate(0, 0) }).Value;

        private static Sigmet SigmetOf(DateTimeOffset? validFrom, DateTimeOffset? validUntil)
        {
            Result<Sigmet> result = Sigmet.Create(
                "s1", IwxxmReportType.Sigmet, T0, ReportStatus.Normal, PermissibleUsage.Operational,
                "YUDD", null, null, null, null, "1", validFrom, validUntil,
                evolvingConditions: new[] { new EnRoutePhenomenonVolume("e1", Area(), null, null) });
            Assert.True(result.IsSuccess, string.Join(" | ", result.Errors));
            return result.Value;
        }

        private static (string?[] Report, string?[] Feature) ValidityColumns(IwxxmDocument document)
        {
            string path = WriteToTempFile(document);
            try
            {
                using (SqliteConnection connection = GeoPackageSqlite.Open(path))
                {
                    string?[] report = Assert.Single(Rows(connection, "SELECT valid_from_utc, valid_until_utc FROM iwxxm_reports"));
                    string?[] feature = Assert.Single(Rows(connection, "SELECT valid_from_utc, valid_until_utc FROM iwxxm_polygons"));
                    return (report, feature);
                }
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public void AReportWithoutAValidityPeriod_HasNullColumns_InTheFeatureTableAndInTheReportRow()
        {
            (string?[] report, string?[] feature) = ValidityColumns(SigmetOf(null, null));

            Assert.Equal(new string?[] { null, null }, report);
            Assert.Equal(new string?[] { null, null }, feature);
        }

        [Fact]
        public void AReportWithAPartialPeriod_HasOnlyTheKnownBound()
        {
            (string?[] fromReport, string?[] fromFeature) = ValidityColumns(SigmetOf(T0, null));
            (string?[] untilReport, string?[] untilFeature) = ValidityColumns(SigmetOf(null, T0.AddHours(4)));

            Assert.Equal(new string?[] { "2026-03-01T06:00:00.000Z", null }, fromReport);
            Assert.Equal(new string?[] { "2026-03-01T06:00:00.000Z", null }, fromFeature);
            Assert.Equal(new string?[] { null, "2026-03-01T10:00:00.000Z" }, untilReport);
            Assert.Equal(new string?[] { null, "2026-03-01T10:00:00.000Z" }, untilFeature);
        }

        [Fact]
        public void AValidityPeriod_IsWrittenWithMilliseconds_AndFinerDigitsAreCutNotRounded()
        {
            DateTimeOffset from = T0.AddTicks(1234999); // 06:00:00.1234999
            (string?[] report, string?[] feature) = ValidityColumns(SigmetOf(from, from.AddHours(1)));

            Assert.Equal(new string?[] { "2026-03-01T06:00:00.123Z", "2026-03-01T07:00:00.123Z" }, report);
            Assert.Equal(report, feature);
        }

        [Fact]
        public void AReportWithoutAValidityPeriodInTheModel_HasNullColumns()
        {
            // METAR, SPECI, VAA, TCA, VONA, QVACI and space weather have no validity period: nothing is made up from their other times.
            foreach (string sample in new[] { "metar-A3-1.xml", "va-advisory-A7-2.xml", "tc-advisory-A2-2.xml", "vona-A7-1.xml", "qvaci-Example.xml", "spacewx-A7-5.xml" })
            {
                IwxxmDocument document = IwxxmParser.ParseFile(SampleFiles.PathOf(sample)).Value;
                string path = WriteToTempFile(document);
                try
                {
                    using (SqliteConnection connection = GeoPackageSqlite.Open(path))
                    {
                        Assert.Equal("0", Scalar(connection, "SELECT count(*) FROM iwxxm_reports WHERE valid_from_utc IS NOT NULL OR valid_until_utc IS NOT NULL"));
                        foreach (string table in TableNames(connection).Where(t => t.StartsWith("iwxxm_", StringComparison.Ordinal) && t != "iwxxm_reports" && t != "iwxxm_metadata"))
                            Assert.Equal("0", Scalar(connection, $"SELECT count(*) FROM {table} WHERE valid_from_utc IS NOT NULL OR valid_until_utc IS NOT NULL"));
                    }
                }
                finally
                {
                    File.Delete(path);
                }
            }
        }

        [Theory]
        [InlineData("sigmet-A6-1a-TS.xml")]
        [InlineData("sigmet-A6-2-TC.xml")]
        [InlineData("airmet-A6-1a-TS.xml")]
        [InlineData("taf-A5-1.xml")]
        [InlineData("WAFS-Example.xml")]
        public void ReportsThatStateAValidityPeriod_FillTheColumnsOfTheReportAndOfEveryFeature(string sample)
        {
            IwxxmDocument document = IwxxmParser.ParseFile(SampleFiles.PathOf(sample)).Value;
            (DateTimeOffset? from, DateTimeOffset? until) = GeoPackageGeoJsonParityTests.ValidityOf(document);
            Assert.NotNull(from);
            Assert.NotNull(until);

            string path = WriteToTempFile(document);
            try
            {
                using (SqliteConnection connection = GeoPackageSqlite.Open(path))
                {
                    Assert.Equal(new string?[] { GeoPackageGeoJsonParityTests.ExpectedColumn(from), GeoPackageGeoJsonParityTests.ExpectedColumn(until) },
                        Assert.Single(Rows(connection, "SELECT valid_from_utc, valid_until_utc FROM iwxxm_reports")));

                    int features = 0;
                    foreach (string table in TableNames(connection).Where(t => t.StartsWith("iwxxm_", StringComparison.Ordinal) && t != "iwxxm_reports" && t != "iwxxm_metadata"))
                    {
                        features += int.Parse(Scalar(connection, $"SELECT count(*) FROM {table}")!, CultureInfo.InvariantCulture);
                        Assert.Equal("0", Scalar(connection, $"SELECT count(*) FROM {table} WHERE valid_from_utc IS NOT {Quote(GeoPackageGeoJsonParityTests.ExpectedColumn(from))} OR valid_until_utc IS NOT {Quote(GeoPackageGeoJsonParityTests.ExpectedColumn(until))}"));
                    }

                    Assert.True(features > 0);
                }
            }
            finally
            {
                File.Delete(path);
            }
        }

        private static string Quote(string? text) => text == null ? "NULL" : "'" + text + "'";

        [Fact]
        public void TheFifteenChartsOfTheMetOfficeBulletin_AreToldApartByTheirValidityInstant()
        {
            string sample = SampleFiles.ExtraPathOf("example-iwxxm-2025-2-sigwx-non-operational.xml");
            string path = NewPath();
            try
            {
                Result<string> written = IwxxmParserGeoPackageExtensions.ParseFileToGeoPackage(sample, path);
                Assert.True(written.IsSuccess, string.Join(" | ", written.Errors));

                // T+06 .. T+48 from the 00:00Z base time of 2099-01-01, one chart every three hours (published as an instant).
                string[] expected = Enumerable.Range(0, 15)
                    .Select(i => new DateTimeOffset(2099, 1, 1, 6, 0, 0, TimeSpan.Zero).AddHours(3 * i).ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture))
                    .ToArray();

                using (SqliteConnection connection = GeoPackageSqlite.Open(path))
                {
                    List<string?[]> reports = Rows(connection, "SELECT report_id, valid_from_utc, valid_until_utc FROM iwxxm_reports ORDER BY fid");
                    Assert.Equal(15, reports.Count);
                    Assert.Equal(15, reports.Select(r => r[1]).Distinct().Count());
                    Assert.Equal(expected, reports.Select(r => r[1]).ToArray());
                    Assert.All(reports, r => Assert.Equal(r[1], r[2]));

                    // Filtering a feature table by the validity instant returns exactly the features of that chart, without reading the JSON.
                    foreach (string?[] report in reports)
                    {
                        foreach (string table in new[] { "iwxxm_points", "iwxxm_lines", "iwxxm_polygons" })
                        {
                            string byTime = Scalar(connection, $"SELECT count(*) FROM {table} WHERE valid_from_utc = '{report[1]}'")!;
                            string byReport = Scalar(connection, $"SELECT count(*) FROM {table} WHERE report_id = '{report[0]}'")!;
                            Assert.Equal(byReport, byTime);
                            Assert.Equal("0", Scalar(connection, $"SELECT count(*) FROM {table} WHERE report_id = '{report[0]}' AND (valid_from_utc IS NOT '{report[1]}' OR valid_until_utc IS NOT '{report[2]}')"));
                        }
                    }
                }
            }
            finally
            {
                File.Delete(path);
            }
        }
    }
}
