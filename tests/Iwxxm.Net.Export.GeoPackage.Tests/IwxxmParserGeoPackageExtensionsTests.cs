using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using Microsoft.Data.Sqlite;
using Xunit;
using static Iwxxm.Net.Export.GeoPackage.Tests.GeoPackageTestDatabase;

namespace Iwxxm.Net.Export.GeoPackage.Tests
{
    public sealed class IwxxmParserGeoPackageExtensionsTests : IDisposable
    {
        private static readonly IwxxmParser Parser = new IwxxmParserBuilder().WithoutSchematron().Build();

        private readonly string _directory = Path.Combine(Path.GetTempPath(), "iwxxm-gpkg-ext-" + Guid.NewGuid().ToString("N"));

        public IwxxmParserGeoPackageExtensionsTests() => Directory.CreateDirectory(_directory);

        public void Dispose() => Directory.Delete(_directory, recursive: true);

        private string PathOf(string name = "out.gpkg") => Path.Combine(_directory, name);

        private static string Xml(string sample = "metar-A3-1.xml") => File.ReadAllText(SampleFiles.PathOf(sample));

        private static string Dump(string path)
        {
            using (SqliteConnection connection = GeoPackageSqlite.Open(path))
                return CanonicalDump(connection);
        }

        private string[] FilesInDirectory() => Directory.GetFileSystemEntries(_directory).Select(Path.GetFileName).OrderBy(n => n, StringComparer.Ordinal).ToArray()!;

        private static readonly GeoPackageExportOptions Options = new GeoPackageExportOptions(GeoPackageVersion.Version14, new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero));

        // The reference: parse, then export the document with the exporter.
        private string Reference(string sample)
        {
            var parsed = IwxxmParser.ParseFile(SampleFiles.PathOf(sample));
            Assert.True(parsed.IsSuccess, string.Join(" | ", parsed.Errors));
            Assert.True(IwxxmGeoPackageExporter.Default.Export(parsed.Value, PathOf("reference.gpkg"), Options).IsSuccess);
            return Dump(PathOf("reference.gpkg"));
        }

        [Theory]
        [InlineData("metar-A3-1.xml")]
        [InlineData("metar-NIL-collect.xml")]
        public void EveryEntryPoint_WritesTheSameFileAsParsingThenExporting(string sample)
        {
            string expected = Reference(sample);

            Result<string> file = IwxxmParserGeoPackageExtensions.ParseFileToGeoPackage(SampleFiles.PathOf(sample), PathOf("file.gpkg"), Options);
            Result<string> text = IwxxmParserGeoPackageExtensions.ParseToGeoPackage(Xml(sample), PathOf("text.gpkg"), Options);
            Result<string> instanceFile = Parser.ProcessFileToGeoPackage(SampleFiles.PathOf(sample), PathOf("ifile.gpkg"), Options);
            Result<string> instanceText = Parser.ProcessToGeoPackage(Xml(sample), PathOf("itext.gpkg"), Options);
            Result<string> stream;
            Result<string> staticStream;
            using (var input = new MemoryStream(Encoding.UTF8.GetBytes(Xml(sample))))
                stream = Parser.ProcessToGeoPackage(input, PathOf("istream.gpkg"), Options);
            using (var input = new MemoryStream(Encoding.UTF8.GetBytes(Xml(sample))))
                staticStream = IwxxmParserGeoPackageExtensions.ParseToGeoPackage(input, PathOf("stream.gpkg"), Options);

            foreach (var (result, name) in new[] { (file, "file.gpkg"), (text, "text.gpkg"), (instanceFile, "ifile.gpkg"), (instanceText, "itext.gpkg"), (stream, "istream.gpkg"), (staticStream, "stream.gpkg") })
            {
                Assert.True(result.IsSuccess, name + ": " + string.Join(" | ", result.Errors));
                Assert.Equal(Path.GetFullPath(PathOf(name)), result.Value);
                Assert.Equal(expected, Dump(PathOf(name)));
            }
        }

        [Theory]
        [InlineData(GeoPackageContent.None)]
        [InlineData(GeoPackageContent.SchemaExtension)]
        public void EveryEntryPoint_PassesTheContentOptionOn(GeoPackageContent content)
        {
            const string sample = "metar-A3-1.xml";
            var options = new GeoPackageExportOptions(GeoPackageVersion.Version14, new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero), content);
            var parsed = IwxxmParser.ParseFile(SampleFiles.PathOf(sample));
            Assert.True(parsed.IsSuccess, string.Join(" | ", parsed.Errors));
            Assert.True(IwxxmGeoPackageExporter.Default.Export(parsed.Value, PathOf("reference.gpkg"), options).IsSuccess);
            string expected = Dump(PathOf("reference.gpkg"));

            var results = new System.Collections.Generic.List<Result<string>>
            {
                IwxxmParserGeoPackageExtensions.ParseFileToGeoPackage(SampleFiles.PathOf(sample), PathOf("file.gpkg"), options),
                IwxxmParserGeoPackageExtensions.ParseToGeoPackage(Xml(sample), PathOf("text.gpkg"), options),
                Parser.ProcessFileToGeoPackage(SampleFiles.PathOf(sample), PathOf("ifile.gpkg"), options),
                Parser.ProcessToGeoPackage(Xml(sample), PathOf("itext.gpkg"), options),
            };
            using (var input = new MemoryStream(Encoding.UTF8.GetBytes(Xml(sample))))
                results.Add(Parser.ProcessToGeoPackage(input, PathOf("istream.gpkg"), options));
            using (var input = new MemoryStream(Encoding.UTF8.GetBytes(Xml(sample))))
                results.Add(IwxxmParserGeoPackageExtensions.ParseToGeoPackage(input, PathOf("stream.gpkg"), options));

            Assert.Equal(6, results.Count);
            foreach (Result<string> result in results)
            {
                Assert.True(result.IsSuccess, string.Join(" | ", result.Errors));
                Assert.Equal(expected, Dump(result.Value));
                using (SqliteConnection connection = GeoPackageSqlite.Open(result.Value))
                    Assert.Equal(content == GeoPackageContent.None ? "0" : "3", Scalar(connection, "SELECT count(*) FROM sqlite_master WHERE type = 'table' AND name IN ('gpkg_extensions', 'gpkg_data_columns', 'gpkg_data_column_constraints')"));
            }
        }

        [Fact]
        public void ProcessToGeoPackage_WithoutOptions_UsesTheDefaults()
        {
            Assert.True(Parser.ProcessToGeoPackage(Xml(), PathOf()).IsSuccess);

            using (SqliteConnection connection = GeoPackageSqlite.Open(PathOf()))
                Assert.Equal("10400", Scalar(connection, "PRAGMA user_version"));
        }

        [Fact]
        public void ParseErrors_ArePropagatedAsFailures_AndNothingIsWritten()
        {
            Result<string> invalid = IwxxmParserGeoPackageExtensions.ParseToGeoPackage("<this is not xml", PathOf());
            Result<string> empty = IwxxmParserGeoPackageExtensions.ParseToGeoPackage("   ", PathOf());
            Result<string> notIwxxm = IwxxmParserGeoPackageExtensions.ParseToGeoPackage("<root/>", PathOf());
            Result<string> missingFile = IwxxmParserGeoPackageExtensions.ParseFileToGeoPackage(PathOf("missing.xml"), PathOf());
            Result<string> emptyStream;
            using (var input = new MemoryStream())
                emptyStream = IwxxmParserGeoPackageExtensions.ParseToGeoPackage(input, PathOf());

            foreach (Result<string> result in new[] { invalid, empty, notIwxxm, missingFile, emptyStream })
            {
                Assert.True(result.IsFailure);
                Assert.DoesNotContain(result.Errors, e => e.Code == GeoPackageErrorCodes.ExportError);
            }

            Assert.Equal("FILE-NOT-FOUND", missingFile.Errors.Single().Code);
            Assert.Empty(FilesInDirectory());
        }

        [Fact]
        public void ParseErrors_AreTheSameAsTheOnesOfProcess()
        {
            const string invalidXml = "<this is not xml";

            Result<string> export = Parser.ProcessToGeoPackage(invalidXml, PathOf());

            Assert.Equal(Parser.Process(invalidXml).Errors.Select(e => e.Code), export.Errors.Select(e => e.Code));
        }

        [Fact]
        public void ExportErrors_AreGeoPackageExportErrors_AfterASuccessfulParse()
        {
            Result<string> result = Parser.ProcessToGeoPackage(Xml(), PathOf("missing/out.gpkg"));

            Assert.True(result.IsFailure);
            ValidationError error = Assert.Single(result.Errors);
            Assert.Equal("GEOPACKAGE-EXPORT-ERROR", error.Code);
            Assert.Equal(ErrorCategory.DomainMapping, error.Category);
            Assert.Equal(Parser.Process(Xml()).Value.Id, error.Location);
            Assert.Empty(FilesInDirectory());
        }

        [Fact]
        public void Warnings_OfTheParse_AreKeptOnSuccess()
        {
            XDocument bulletin = XDocument.Parse(Xml("metar-NIL-collect.xml"));
            XNamespace iwxxm = bulletin.Descendants().First(e => e.Name.LocalName == "METAR").Name.Namespace;
            XNamespace collect = bulletin.Root!.Name.Namespace;
            XNamespace xlink = "http://www.w3.org/1999/xlink";
            XNamespace gml = "http://www.opengis.net/gml/3.2";

            // A second report whose issue time is unresolvable: the bulletin keeps the first report and warns about the second.
            XElement second = new XElement(XDocument.Parse(Xml("metar-A3-1.xml")).Root!);
            foreach (XElement e in second.DescendantsAndSelf().Where(e => e.Attribute(gml + "id") != null && e != second))
                e.SetAttributeValue(gml + "id", "b2-" + (string)e.Attribute(gml + "id")!);
            foreach (XAttribute href in second.Descendants().Attributes(xlink + "href").Where(a => a.Value.StartsWith("#", StringComparison.Ordinal)))
                href.Value = "#b2-" + href.Value.Substring(1);
            XElement issueTime = second.Element(iwxxm + "issueTime")!;
            issueTime.RemoveNodes();
            issueTime.SetAttributeValue(xlink + "href", "#uuid.not-in-this-bulletin");
            bulletin.Root.Elements(collect + "meteorologicalInformation").Last()
                .AddAfterSelf(new XElement(collect + "meteorologicalInformation", second));

            Result<string> result = Parser.ProcessToGeoPackage(bulletin.ToString(), PathOf(), Options);

            Assert.True(result.IsSuccess, string.Join(" | ", result.Errors));
            Assert.True(result.HasWarnings);
            Assert.Equal(Parser.Process(bulletin.ToString()).Warnings.Select(w => w.Code), result.Warnings.Select(w => w.Code));
        }

        [Fact]
        public void ArgumentErrors_Throw_LikeTheParserDoes()
        {
            Assert.Throws<ArgumentNullException>(() => IwxxmParserGeoPackageExtensions.ProcessToGeoPackage(null!, Xml(), PathOf()));
            Assert.Throws<ArgumentNullException>(() => IwxxmParserGeoPackageExtensions.ProcessFileToGeoPackage(null!, SampleFiles.PathOf("metar-A3-1.xml"), PathOf()));
            Assert.Throws<ArgumentNullException>(() => IwxxmParserGeoPackageExtensions.ParseToGeoPackage((Stream)null!, PathOf()));
            Assert.Throws<ArgumentException>(() => IwxxmParserGeoPackageExtensions.ParseFileToGeoPackage(" ", PathOf()));
        }
    }
}
