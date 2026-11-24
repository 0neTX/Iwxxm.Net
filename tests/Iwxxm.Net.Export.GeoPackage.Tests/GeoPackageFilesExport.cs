using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Iwxxm.Net.Export.GeoPackage.Model;
using Xunit;

namespace Iwxxm.Net.Export.GeoPackage.Tests
{
    /// <summary>
    /// The input of <c>tools/validate-geopackage-file.sh</c>. It does nothing (and passes) unless the environment variable
    /// <c>IWXXM_GPKG_FILES_DIR</c> names a directory: then it writes one GeoPackage file per sample there (plus a GeoPackage
    /// 1.3 file of one sample) and <c>expected.tsv</c>, one line per table with the columns <c>file</c>, <c>table</c>,
    /// <c>row count</c> and the extent <c>minx maxx miny maxy</c> (<c>-</c> for a table without extent). No GDAL is involved here.
    /// </summary>
    public sealed class GeoPackageFilesExport
    {
        internal const string DirectoryVariable = "IWXXM_GPKG_FILES_DIR";

        private static readonly DateTimeOffset LastChange = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        [Fact]
        public void WriteFiles_WhenAskedByTheValidationScript()
        {
            string? directory = Environment.GetEnvironmentVariable(DirectoryVariable);
            if (string.IsNullOrEmpty(directory))
                return;

            Directory.CreateDirectory(directory);
            var lines = new StringBuilder();

            foreach (string path in SampleFiles.AllPaths())
                WriteSample(directory, lines, path, Path.GetFileNameWithoutExtension(path) + ".gpkg", GeoPackageVersion.Version14);

            WriteSample(directory, lines, SampleFiles.PathOf("metar-NIL-collect.xml"), "metar-NIL-collect.v13.gpkg", GeoPackageVersion.Version13);

            File.WriteAllText(Path.Combine(directory, "expected.tsv"), lines.ToString(), new UTF8Encoding(false));
        }

        private static void WriteSample(string directory, StringBuilder lines, string samplePath, string fileName, GeoPackageVersion version)
        {
            var result = IwxxmParser.ParseFile(samplePath);
            Assert.True(result.IsSuccess, string.Join(" | ", result.Errors));
            GeoPackageDocumentModel model = GeoPackageFeatureSource.Read(result.Value);
            GeoPackageWriter.Write(Path.Combine(directory, fileName), model, new GeoPackageWriteOptions(LastChange, version));

            foreach (var group in model.Features.Where(f => f.Geometry != null).GroupBy(f => TableOf(f.Geometry!.Kind)).OrderBy(g => g.Key, StringComparer.Ordinal))
            {
                GeoPackageEnvelope[] envelopes = group.Select(f => f.Geometry!.Envelope()!.Value).ToArray();
                string extent = string.Join(" ", new[] { envelopes.Min(e => e.MinX), envelopes.Max(e => e.MaxX), envelopes.Min(e => e.MinY), envelopes.Max(e => e.MaxY) }
                    .Select(v => v.ToString("R", CultureInfo.InvariantCulture)));
                Add(lines, fileName, group.Key, group.Count(), extent);
            }

            Add(lines, fileName, "iwxxm_attributes", model.Features.Count(f => f.Geometry == null), "-");
            Add(lines, fileName, "iwxxm_reports", model.Reports.Count, "-");
            // geopackage_version, library_version, source_document_id, source_document_type, report_count and, for a bulletin, its identifier.
            Add(lines, fileName, "iwxxm_metadata", model.BulletinIdentifier != null ? 6 : 5, "-");
        }

        private static string TableOf(GeoPackageGeometryKind kind) =>
            kind == GeoPackageGeometryKind.Point ? "iwxxm_points"
            : kind == GeoPackageGeometryKind.MultiLineString ? "iwxxm_lines"
            : "iwxxm_polygons";

        private static void Add(StringBuilder lines, string file, string table, int count, string extent) =>
            lines.Append(file).Append('\t').Append(table).Append('\t').Append(count.ToString(CultureInfo.InvariantCulture)).Append('\t').Append(extent).Append('\n');
    }
}
