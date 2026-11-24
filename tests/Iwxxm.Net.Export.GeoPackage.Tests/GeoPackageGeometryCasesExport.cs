using System;
using System.IO;
using System.Linq;
using System.Text;
using Iwxxm.Net.Export.GeoPackage.Model;
using Xunit;
using static Iwxxm.Net.Export.GeoPackage.Tests.GeoPackageGeometryEncoderTests;
using static Iwxxm.Net.Export.GeoPackage.Tests.GeoPackageGeometryTests;

namespace Iwxxm.Net.Export.GeoPackage.Tests
{
    /// <summary>
    /// The input of <c>tools/validate-geopackage-geometry.sh</c>. It does nothing (and passes) unless the environment
    /// variable <c>IWXXM_GPKG_GEOMETRY_CASES_DIR</c> names a directory: then it writes <c>cases.tsv</c> there, one line per
    /// geometry with the columns <c>name</c>, <c>tags</c>, <c>blob (hex)</c> and <c>expected WKT</c>. No GDAL is involved here.
    /// </summary>
    public sealed class GeoPackageGeometryCasesExport
    {
        internal const string DirectoryVariable = "IWXXM_GPKG_GEOMETRY_CASES_DIR";

        [Fact]
        public void WriteCases_WhenAskedByTheValidationScript()
        {
            string? directory = Environment.GetEnvironmentVariable(DirectoryVariable);
            if (string.IsNullOrEmpty(directory))
                return;

            Directory.CreateDirectory(directory);
            var lines = new StringBuilder();

            Add(lines, "hand/point", "point", GeoPackageGeometry.ForPoint(new GeoPackagePosition(10.5, 45.25)));
            Add(lines, "hand/multilinestring", "multilinestring", GeoPackageGeometry.ForMultiLineString(new[] { Line((0, 0), (1, 1)), Line((2, 3), (4, 5)) }));
            Add(lines, "hand/multipolygon-hole", "multipolygon,hole", GeoPackageGeometry.ForMultiPolygon(new[]
            {
                Polygon(Ring((0, 0), (10, 0), (10, 10), (0, 10), (0, 0)), Ring((2, 2), (2, 4), (4, 4), (4, 2), (2, 2)))
            }));
            Add(lines, "hand/negative-zero", "point,exactness", GeoPackageGeometry.ForPoint(new GeoPackagePosition(-0.0, 1.0 / 3.0)));

            foreach (string path in GeoPackageGeometryWktTests.AllSamplePaths())
            {
                string sample = Path.GetFileName(path);
                int index = 0;
                foreach (GeoPackageFeature feature in ReadFeatures(path))
                {
                    if (feature.Geometry != null)
                    {
                        GeoPackageGeometry geometry = feature.Geometry;
                        var tags = new System.Collections.Generic.List<string> { geometry.Kind.ToString().ToLowerInvariant() };
                        if (feature.Properties.Any(p => p.Key == "circleSegments")) tags.Add("circle");
                        if (geometry.Kind == GeoPackageGeometryKind.MultiPolygon && GeoPackageGeometryWktTests.IsAntimeridianSplit(geometry)) tags.Add("antimeridian");
                        if (geometry.Kind == GeoPackageGeometryKind.MultiPolygon && GeoPackageGeometryWktTests.IsPolarCap(geometry)) tags.Add("polar-cap");
                        if (geometry.Kind == GeoPackageGeometryKind.MultiPolygon && geometry.Polygons!.Any(p => p.Count > 1)) tags.Add("hole");
                        Add(lines, sample + "/" + index, string.Join(",", tags), geometry);
                    }

                    index++;
                }
            }

            File.WriteAllText(Path.Combine(directory, "cases.tsv"), lines.ToString(), new UTF8Encoding(false));
        }

        private static void Add(StringBuilder lines, string name, string tags, GeoPackageGeometry geometry)
        {
            byte[] blob = GeoPackageGeometryEncoder.Encode(geometry, 4326);
            lines.Append(name).Append('\t').Append(tags).Append('\t')
                .Append(BitConverter.ToString(blob).Replace("-", string.Empty)).Append('\t')
                .Append(TestWkt.Format(geometry)).Append('\n');
        }
    }
}
