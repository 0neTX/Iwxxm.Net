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
    /// 1.3 file of one sample, and, for three samples, a file named <c>*.plain.gpkg</c> written with
    /// <see cref="GeoPackageContent.None"/>, that is, without the <c>gpkg_schema</c> extension) and <c>expected.tsv</c>, one line
    /// per table with the columns <c>file</c>, <c>table</c>, <c>row count</c> and the extent <c>minx maxx miny maxy</c> (<c>-</c> for a table without extent). No GDAL is involved here.
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

            // Without the schema extension (validate-geopackage-file.py reads the .plain.gpkg suffix): a bulletin, a SIGMET with
            // polygons and the small SIGWX sample with lines and polygons.
            WriteSample(directory, lines, SampleFiles.PathOf("metar-NIL-collect.xml"), "metar-NIL-collect.plain.gpkg", GeoPackageVersion.Version14, GeoPackageContent.None);
            WriteSample(directory, lines, SampleFiles.PathOf("sigmet-A6-2-TC.xml"), "sigmet-A6-2-TC.plain.gpkg", GeoPackageVersion.Version14, GeoPackageContent.None);
            WriteSample(directory, lines, SampleFiles.ExtraPathOf("sigwx-swh-2025-2.xml"), "sigwx-swh-2025-2.plain.gpkg", GeoPackageVersion.Version14, GeoPackageContent.None);

            File.WriteAllText(Path.Combine(directory, "expected.tsv"), lines.ToString(), new UTF8Encoding(false));
        }

        private static void WriteSample(string directory, StringBuilder lines, string samplePath, string fileName, GeoPackageVersion version, GeoPackageContent content = GeoPackageContent.SchemaExtension)
        {
            var result = IwxxmParser.ParseFile(samplePath);
            Assert.True(result.IsSuccess, string.Join(" | ", result.Errors));
            GeoPackageDocumentModel model = GeoPackageFeatureSource.Read(result.Value);
            GeoPackageWriter.Write(Path.Combine(directory, fileName), model, new GeoPackageWriteOptions(LastChange, version, content));

            foreach (var group in model.Features.Where(f => f.Geometry != null).GroupBy(f => GeoPackageDataTables.TableFor(f.Geometry!.Kind)).OrderBy(g => g.Key, StringComparer.Ordinal))
            {
                GeoPackageEnvelope[] envelopes = group.Select(f => f.Geometry!.Envelope()!.Value).ToArray();
                string extent = string.Join(" ", new[] { envelopes.Min(e => e.MinX), envelopes.Max(e => e.MaxX), envelopes.Min(e => e.MinY), envelopes.Max(e => e.MaxY) }
                    .Select(v => v.ToString("R", CultureInfo.InvariantCulture)));
                Add(lines, fileName, group.Key, group.Count(), extent);
            }

            Add(lines, fileName, GeoPackageDataTables.AttributesTable, model.Features.Count(f => f.Geometry == null), "-");
            Add(lines, fileName, GeoPackageDataTables.ReportsTable, model.Reports.Count, "-");
            // The rows the writer puts in the metadata table, whatever their keys are.
            Add(lines, fileName, GeoPackageDataTables.MetadataTable, GeoPackageDataTables.MetadataEntries(model, new GeoPackageWriteOptions(LastChange, version)).Count, "-");
        }

        private static void Add(StringBuilder lines, string file, string table, int count, string extent) =>
            lines.Append(file).Append('\t').Append(table).Append('\t').Append(count.ToString(CultureInfo.InvariantCulture)).Append('\t').Append(extent).Append('\n');
    }
}
