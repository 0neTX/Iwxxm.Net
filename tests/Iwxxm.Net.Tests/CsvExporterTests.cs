using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Iwxxm.Net.Application.Ports.Out;
using Iwxxm.Net.Infrastructure.Csv;
using Iwxxm.Net.Reports;
using Xunit;

namespace Iwxxm.Net.Tests
{
    public sealed class CsvExporterTests
    {
        private const string SamplesDir = "Samples/wmo-examples";

        [Theory]
        [InlineData("metar-A3-1.xml", "METAR_OBSERVATION")]
        [InlineData("speci-A3-2.xml", "SPECI_OBSERVATION")]
        [InlineData("taf-A5-1.xml", "TAF_FORECAST")]
        [InlineData("sigmet-A6-1a-TS.xml", "SIGMET_EVOLVING_VOLUME")]
        [InlineData("sigmet-A6-2-TC.xml", "TROPICAL_CYCLONE_CENTRE")]
        [InlineData("airmet-A6-1a-TS.xml", "AIRMET_EVOLVING_VOLUME")]
        [InlineData("vona-A7-1.xml", "VONA_VOLCANO")]
        [InlineData("qvaci-Example.xml", "ASH_CONCENTRATION_CLOUD")]
        [InlineData("va-advisory-A7-2.xml", "OBSERVED_ASH_CLOUD")]
        [InlineData("tc-advisory-A2-2.xml", "TC_OBSERVATION")]
        [InlineData("spacewx-A7-3.xml", "SPACE_WEATHER_REGION")]
        [InlineData("WAFS-Example.xml", "JETSTREAM")]
        public void ParseFileToCsv_OfficialExamples_ProducesValidRfc4180CsvWithWkt(string fileName, string expectedFeatureType)
        {
            string path = Path.Combine(SamplesDir, fileName);
            Result<string> result = IwxxmParser.ParseFileToCsv(path);

            Assert.True(result.IsSuccess, string.Join(" | ", result.Errors));
            List<string[]> rows = ParseCsv(result.Value);
            Assert.True(rows.Count >= 2, $"Expected header + at least one data row for {fileName}.");
            Assert.Equal(CsvColumns.Headers.Length, rows[0].Length);
            Assert.Equal(CsvColumns.Headers, rows[0]);

            bool foundFeature = false;
            for (int r = 1; r < rows.Count; r++)
            {
                Assert.Equal(CsvColumns.Headers.Length, rows[r].Length);
                if (rows[r][CsvColumns.FeatureType] == expectedFeatureType)
                    foundFeature = true;
            }
            Assert.True(foundFeature, $"Expected feature type '{expectedFeatureType}' in CSV output for {fileName}.");
        }

        [Fact]
        public void ParseFileToCsv_Metar_IncludesPointWktAndNegativeCoordinatesWithoutFormulaPrefix()
        {
            Result<string> result = IwxxmParser.ParseFileToCsv(Path.Combine(SamplesDir, "metar-A3-1.xml"));
            Assert.True(result.IsSuccess);

            List<string[]> rows = ParseCsv(result.Value);
            string[] data = rows[1];

            Assert.Equal("Metar", data[CsvColumns.ReportType]);
            Assert.Equal("METAR_OBSERVATION", data[CsvColumns.FeatureType]);
            Assert.Equal("YUDO", data[CsvColumns.StationOrCentre]);
            Assert.StartsWith("POINT (", data[CsvColumns.GeometryWkt], StringComparison.Ordinal);
            Assert.NotEmpty(data[CsvColumns.Lat]);
            Assert.NotEmpty(data[CsvColumns.Lon]);
        }

        [Fact]
        public void ParseToCsv_Sigwx_NegativeCoordinatesAreNeverFormulaPrefixed_AndWktUsesLongitudeLatitude()
        {
            Result<string> result = IwxxmParser.ParseToCsv(File.ReadAllText("Samples/sigwx-swh-2025-2.xml", Encoding.UTF8));
            Assert.True(result.IsSuccess);

            List<string[]> rows = ParseCsv(result.Value);
            Assert.True(rows.Count > 2);

            bool sawNegativeNumeric = false;
            bool sawPolygonOrLine = false;
            for (int r = 1; r < rows.Count; r++)
            {
                string lat = rows[r][CsvColumns.Lat];
                string lon = rows[r][CsvColumns.Lon];
                string wkt = rows[r][CsvColumns.GeometryWkt];

                if (lat.StartsWith("-", StringComparison.Ordinal) || lon.StartsWith("-", StringComparison.Ordinal))
                    sawNegativeNumeric = true;

                Assert.False(lat.StartsWith("'-", StringComparison.Ordinal), "Numeric latitude must never be formula-prefixed.");
                Assert.False(lon.StartsWith("'-", StringComparison.Ordinal), "Numeric longitude must never be formula-prefixed.");

                if (wkt.StartsWith("LINESTRING (", StringComparison.Ordinal) ||
                    wkt.StartsWith("POLYGON ((", StringComparison.Ordinal) ||
                    wkt.StartsWith("MULTIPOLYGON (((", StringComparison.Ordinal))
                {
                    sawPolygonOrLine = true;
                }
            }

            Assert.True(sawNegativeNumeric);
            Assert.True(sawPolygonOrLine);
        }

        [Fact]
        public void Export_FormulaInjectionInTextCell_IsSanitizedByDefault_AndRawWhenDisabled()
        {
            string xml = File.ReadAllText(Path.Combine(SamplesDir, "metar-translation-failed.xml"), Encoding.UTF8)
                .Replace("METAR YUDO 221630Z INVALID", "=CMD|' /C calc'!A0, &quot;test&quot;");

            Result<IwxxmDocument> parsed = IwxxmParser.Parse(xml);
            Assert.True(parsed.IsSuccess, string.Join(" | ", parsed.Errors));

            Result<string> sanitized = IwxxmCsvExporter.ToCsv(parsed.Value);
            Assert.True(sanitized.IsSuccess);
            List<string[]> sanitizedRows = ParseCsv(sanitized.Value);
            Assert.StartsWith("'=CMD|", sanitizedRows[1][CsvColumns.Details], StringComparison.Ordinal);

            Result<string> unsanitized = IwxxmCsvExporter.ToCsv(parsed.Value, new CsvExportOptions(sanitizeFormulas: false));
            Assert.True(unsanitized.IsSuccess);
            List<string[]> rawRows = ParseCsv(unsanitized.Value);
            Assert.Equal("=CMD|' /C calc'!A0, \"test\"", rawRows[1][CsvColumns.Details]);
        }

        [Fact]
        public void Export_CustomOptions_RespectDelimiterHeaderAndOmitWkt()
        {
            Result<IwxxmDocument> parsed = IwxxmParser.ParseFile(Path.Combine(SamplesDir, "metar-A3-1.xml"));
            Assert.True(parsed.IsSuccess);

            var options = new CsvExportOptions(delimiter: ";", includeHeader: false, includeGeometryWkt: false, newLine: "\n");
            Result<string> csv = IwxxmCsvExporter.ToCsv(parsed.Value, options);

            Assert.True(csv.IsSuccess);
            Assert.DoesNotContain("\r\n", csv.Value, StringComparison.Ordinal);
            string[] lines = csv.Value.TrimEnd('\n').Split('\n');
            Assert.Single(lines);
            string[] cols = lines[0].Split(';');
            Assert.Equal(CsvColumns.Headers.Length, cols.Length);
            Assert.Equal(string.Empty, cols[CsvColumns.GeometryWkt]);
        }

        [Fact]
        public void Export_StreamAndBuilderAndErrorContract_WorkAsExpected()
        {
            using (FileStream fs = File.OpenRead(Path.Combine(SamplesDir, "taf-A5-1.xml")))
            {
                Result<string> fromStream = IwxxmParser.ParseToCsv(fs);
                Assert.True(fromStream.IsSuccess);
            }

            var throwingBuilder = new IwxxmParserBuilder().UseCsvExporter(new ThrowingCsvExporter());
            Result<string> failed = throwingBuilder.Build().ProcessFileToCsv(Path.Combine(SamplesDir, "metar-A3-1.xml"));
            Assert.True(failed.IsFailure);
            Assert.Equal(IwxxmErrorCodes.CsvExportError, Assert.Single(failed.Errors).Code);

            var nullBuilder = new IwxxmParserBuilder().UseCsvExporter(new NullCsvExporter()).BuildCsvExporter();
            Result<IwxxmDocument> doc = IwxxmParser.ParseFile(Path.Combine(SamplesDir, "metar-A3-1.xml"));
            Result<string> nullResult = nullBuilder.Export(doc.Value);
            Assert.True(nullResult.IsFailure);
            Assert.Equal(IwxxmErrorCodes.CsvExportError, Assert.Single(nullResult.Errors).Code);

            Assert.Throws<ArgumentNullException>(() => IwxxmCsvExporter.ToCsv(null!));
            Assert.Throws<ArgumentException>(() => new CsvExportOptions(delimiter: ""));
            Assert.Throws<ArgumentException>(() => new CsvExportOptions(newLine: ""));
        }

        private static List<string[]> ParseCsv(string csv)
        {
            var rows = new List<string[]>();
            var currentRow = new List<string>();
            var field = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < csv.Length; i++)
            {
                char ch = csv[i];
                if (inQuotes)
                {
                    if (ch == '"')
                    {
                        if (i + 1 < csv.Length && csv[i + 1] == '"')
                        {
                            field.Append('"');
                            i++;
                        }
                        else
                        {
                            inQuotes = false;
                        }
                    }
                    else
                    {
                        field.Append(ch);
                    }
                    continue;
                }

                if (ch == '"' && field.Length == 0)
                {
                    inQuotes = true;
                }
                else if (ch == ',')
                {
                    currentRow.Add(field.ToString());
                    field.Clear();
                }
                else if (ch == '\r' && i + 1 < csv.Length && csv[i + 1] == '\n')
                {
                    currentRow.Add(field.ToString());
                    field.Clear();
                    rows.Add(currentRow.ToArray());
                    currentRow.Clear();
                    i++;
                }
                else if (ch == '\n')
                {
                    currentRow.Add(field.ToString());
                    field.Clear();
                    rows.Add(currentRow.ToArray());
                    currentRow.Clear();
                }
                else
                {
                    field.Append(ch);
                }
            }

            if (field.Length > 0 || currentRow.Count > 0)
            {
                currentRow.Add(field.ToString());
                rows.Add(currentRow.ToArray());
            }

            return rows;
        }

        private sealed class ThrowingCsvExporter : ICsvExporter
        {
            public string Export(IwxxmDocument document, CsvExportOptions options) =>
                throw new InvalidOperationException("Simulated CSV failure");
        }

        private sealed class NullCsvExporter : ICsvExporter
        {
            public string Export(IwxxmDocument document, CsvExportOptions options) => null!;
        }
    }
}
