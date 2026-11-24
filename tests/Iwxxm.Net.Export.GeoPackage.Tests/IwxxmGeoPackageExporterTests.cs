using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Iwxxm.Net.Export.GeoPackage.Model;
using Iwxxm.Net.Reports;
using Microsoft.Data.Sqlite;
using Xunit;
using static Iwxxm.Net.Export.GeoPackage.Tests.GeoPackageTestDatabase;

namespace Iwxxm.Net.Export.GeoPackage.Tests
{
    public sealed class IwxxmGeoPackageExporterTests : IDisposable
    {
        private static readonly DateTimeOffset LastChange = new DateTimeOffset(2026, 3, 4, 5, 6, 7, 89, TimeSpan.Zero);

        private readonly string _directory = Path.Combine(Path.GetTempPath(), "iwxxm-gpkg-api-" + Guid.NewGuid().ToString("N"));

        public IwxxmGeoPackageExporterTests() => Directory.CreateDirectory(_directory);

        public void Dispose() => Directory.Delete(_directory, recursive: true);

        private string PathOf(string name = "out.gpkg") => Path.Combine(_directory, name);

        private static IwxxmDocument Parse(string path)
        {
            Result<IwxxmDocument> result = IwxxmParser.ParseFile(path);
            Assert.True(result.IsSuccess, string.Join(" | ", result.Errors));
            return result.Value;
        }

        private static string Dump(string path)
        {
            using (SqliteConnection connection = GeoPackageSqlite.Open(path))
                return CanonicalDump(connection);
        }

        private string[] FilesInDirectory() => Directory.GetFileSystemEntries(_directory).Select(Path.GetFileName).OrderBy(n => n, StringComparer.Ordinal).ToArray()!;

        public static IEnumerable<object[]> Samples() => SampleFiles.AllPaths().Select(p => new object[] { p });

        [Theory]
        [MemberData(nameof(Samples))]
        public void Export_WritesAReadableGeoPackage_ForEverySample(string sample)
        {
            IwxxmDocument document = Parse(sample);
            string path = PathOf();

            Result<string> result = IwxxmGeoPackageExporter.Default.Export(document, path);

            Assert.True(result.IsSuccess, string.Join(" | ", result.Errors));
            Assert.Equal(Path.GetFullPath(path), result.Value);
            Assert.Equal(new[] { "out.gpkg" }, FilesInDirectory());
            using (SqliteConnection connection = GeoPackageSqlite.Open(path))
            {
                Assert.Equal("ok", Scalar(connection, "PRAGMA integrity_check"));
                Assert.Empty(Rows(connection, "PRAGMA foreign_key_check"));
                Assert.Equal("1196444487", Scalar(connection, "PRAGMA application_id"));
                Assert.NotEqual("0", Scalar(connection, "SELECT count(*) FROM iwxxm_reports"));
            }
        }

        [Fact]
        public void Export_IsDeterministic_ForTheSameDocumentAndOptions()
        {
            IwxxmDocument document = Parse(SampleFiles.PathOf("metar-A3-1.xml"));

            Assert.True(IwxxmGeoPackageExporter.Default.Export(document, PathOf("a.gpkg")).IsSuccess);
            Assert.True(IwxxmGeoPackageExporter.Default.Export(document, PathOf("b.gpkg")).IsSuccess);

            Assert.Equal(Dump(PathOf("a.gpkg")), Dump(PathOf("b.gpkg")));
        }

        [Fact]
        public void Export_DefaultLastChange_IsTheIssueTimeOfTheDocument()
        {
            IwxxmDocument document = Parse(SampleFiles.PathOf("metar-A3-1.xml"));

            Assert.True(IwxxmGeoPackageExporter.Default.Export(document, PathOf()).IsSuccess);

            using (SqliteConnection connection = GeoPackageSqlite.Open(PathOf()))
            {
                string[] lastChanges = Rows(connection, "SELECT DISTINCT last_change FROM gpkg_contents").Select(r => r[0]!).ToArray();
                Assert.Equal(new[] { GeoPackageDateTime.Format(document.IssueTimeUtc) }, lastChanges);
            }
        }

        [Fact]
        public void Export_LastChangeUtc_OverridesTheIssueTime()
        {
            IwxxmDocument document = Parse(SampleFiles.PathOf("metar-A3-1.xml"));

            Assert.True(IwxxmGeoPackageExporter.Default.Export(document, PathOf(), new GeoPackageExportOptions(GeoPackageVersion.Version14, LastChange)).IsSuccess);

            using (SqliteConnection connection = GeoPackageSqlite.Open(PathOf()))
                Assert.Equal(new[] { "2026-03-04T05:06:07.089Z" }, Rows(connection, "SELECT DISTINCT last_change FROM gpkg_contents").Select(r => r[0]!).ToArray());
        }

        [Theory]
        [InlineData(GeoPackageVersion.Version14, "10400", "1.4")]
        [InlineData(GeoPackageVersion.Version13, "10300", "1.3")]
        public void Export_DeclaresTheRequestedVersion(GeoPackageVersion version, string userVersion, string text)
        {
            IwxxmDocument document = Parse(SampleFiles.PathOf("metar-A3-1.xml"));

            Assert.True(IwxxmGeoPackageExporter.Default.Export(document, PathOf(), new GeoPackageExportOptions(version)).IsSuccess);

            using (SqliteConnection connection = GeoPackageSqlite.Open(PathOf()))
            {
                Assert.Equal(userVersion, Scalar(connection, "PRAGMA user_version"));
                Assert.Equal(text, Scalar(connection, "SELECT value FROM iwxxm_metadata WHERE key = 'geopackage_version'"));
            }
        }

        [Fact]
        public void Export_NullOptions_AreTheDefaultOptions()
        {
            IwxxmDocument document = Parse(SampleFiles.PathOf("metar-A3-1.xml"));

            Assert.True(IwxxmGeoPackageExporter.Default.Export(document, PathOf("a.gpkg"), null).IsSuccess);
            Assert.True(IwxxmGeoPackageExporter.Default.Export(document, PathOf("b.gpkg"), GeoPackageExportOptions.Default).IsSuccess);

            Assert.Equal(Dump(PathOf("a.gpkg")), Dump(PathOf("b.gpkg")));
        }

        [Fact]
        public void Export_ReplacesAnExistingFile()
        {
            IwxxmDocument document = Parse(SampleFiles.PathOf("metar-A3-1.xml"));
            File.WriteAllText(PathOf(), "not a database");

            Assert.True(IwxxmGeoPackageExporter.Default.Export(document, PathOf()).IsSuccess);

            using (SqliteConnection connection = GeoPackageSqlite.Open(PathOf()))
                Assert.Equal("ok", Scalar(connection, "PRAGMA integrity_check"));
            Assert.Equal(new[] { "out.gpkg" }, FilesInDirectory());
        }

        [Fact]
        public void Export_NullDocument_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => IwxxmGeoPackageExporter.Default.Export(null!, PathOf()));
            Assert.Throws<ArgumentNullException>(() => IwxxmGeoPackageExporter.Default.ExportToBytes(null!));
            Assert.Throws<ArgumentNullException>(() => IwxxmGeoPackageExporter.ToGeoPackage(null!, PathOf()));
            Assert.Throws<ArgumentNullException>(() => IwxxmGeoPackageExporter.ToGeoPackageBytes(null!));
        }

        public static IEnumerable<object?[]> BadPaths()
        {
            yield return new object?[] { null };
            yield return new object?[] { string.Empty };
            yield return new object?[] { "   " };
            yield return new object?[] { "missing-directory/out.gpkg" };
            yield return new object?[] { "an-existing-directory" };
            yield return new object?[] { "a-file/below-a-file.gpkg" };
            yield return new object?[] { "bad\0name.gpkg" };
        }

        [Theory]
        [MemberData(nameof(BadPaths))]
        public void Export_UnusablePath_IsAFailureWithTheDocumentedCode_AndLeavesNothingBehind(string? relativePath)
        {
            IwxxmDocument document = Parse(SampleFiles.PathOf("metar-A3-1.xml"));
            Directory.CreateDirectory(PathOf("an-existing-directory"));
            File.WriteAllText(PathOf("a-file"), "content");
            string[] before = FilesInDirectory();
            string? path = relativePath == null || relativePath.Trim().Length == 0 ? relativePath : _directory + Path.DirectorySeparatorChar + relativePath;

            Result<string> result = IwxxmGeoPackageExporter.Default.Export(document, path!);

            Assert.True(result.IsFailure);
            ValidationError error = Assert.Single(result.Errors);
            Assert.Equal("GEOPACKAGE-EXPORT-ERROR", error.Code);
            Assert.Equal(GeoPackageErrorCodes.ExportError, error.Code);
            Assert.Equal(ErrorCategory.DomainMapping, error.Category);
            Assert.Equal(ValidationSeverity.Error, error.Severity);
            Assert.Equal(document.Id, error.Location);
            Assert.Equal(before, FilesInDirectory());
            Assert.Equal("content", File.ReadAllText(PathOf("a-file")));
        }

        [Fact]
        public void Export_OnAnExistingDirectory_DoesNotTouchItsContent()
        {
            IwxxmDocument document = Parse(SampleFiles.PathOf("metar-A3-1.xml"));
            Directory.CreateDirectory(PathOf("target"));
            File.WriteAllText(Path.Combine(PathOf("target"), "keep.txt"), "keep");

            Assert.True(IwxxmGeoPackageExporter.Default.Export(document, PathOf("target")).IsFailure);

            Assert.Equal("keep", File.ReadAllText(Path.Combine(PathOf("target"), "keep.txt")));
            Assert.Equal(new[] { "keep.txt" }, Directory.GetFileSystemEntries(PathOf("target")).Select(Path.GetFileName).ToArray());
        }

        [Fact]
        public void Export_ToAPathBelowAnExistingFile_FailsAndLeavesThatFileUntouched_WhenThePathIsInvalid()
        {
            IwxxmDocument document = Parse(SampleFiles.PathOf("metar-A3-1.xml"));
            Assert.True(IwxxmGeoPackageExporter.Default.Export(document, PathOf()).IsSuccess);
            string before = Dump(PathOf());
            byte[] bytesBefore = File.ReadAllBytes(PathOf());

            // The same file name below a regular file cannot be created: the export fails and the earlier file stays.
            Assert.True(IwxxmGeoPackageExporter.Default.Export(document, Path.Combine(PathOf(), "inner.gpkg")).IsFailure);

            Assert.Equal(bytesBefore, File.ReadAllBytes(PathOf()));
            Assert.Equal(before, Dump(PathOf()));
            Assert.Equal(new[] { "out.gpkg" }, FilesInDirectory());
        }

        // A model with a point that cannot be encoded (NaN): the writer is already inside the transaction when it throws.
        private static GeoPackageDocumentModel UnencodableModel(IwxxmDocument document)
        {
            GeoPackageDocumentModel model = GeoPackageFeatureSource.Read(document);
            GeoPackageFeature first = model.Features.First(f => f.Geometry != null);
            var bad = new GeoPackageFeature(first.Feature, GeoPackageGeometry.ForPoint(new GeoPackagePosition(double.NaN, 0)));
            return new GeoPackageDocumentModel(model.DocumentId, model.DocumentType, model.BulletinIdentifier, model.Reports, model.Features.Concat(new[] { bad }).ToList());
        }

        [Fact]
        public void Export_FailingAfterTheWriterStarted_OnThePathOfAValidGeoPackage_LeavesThatFileByteIdentical_AndNoTemporaryFile()
        {
            IwxxmDocument document = Parse(SampleFiles.PathOf("metar-A3-1.xml"));
            Assert.True(IwxxmGeoPackageExporter.Default.Export(document, PathOf()).IsSuccess);
            byte[] before = File.ReadAllBytes(PathOf());

            Result<string> result = IwxxmGeoPackageExporter.Default.ExportReadingModelWith(document, PathOf(), null, UnencodableModel);

            Assert.True(result.IsFailure);
            ValidationError error = Assert.Single(result.Errors);
            Assert.Equal("GEOPACKAGE-EXPORT-ERROR", error.Code);
            Assert.Equal(ErrorCategory.DomainMapping, error.Category);
            Assert.Equal(document.Id, error.Location);
            Assert.Equal(before, File.ReadAllBytes(PathOf()));
            Assert.Equal(new[] { "out.gpkg" }, FilesInDirectory());
            using (SqliteConnection connection = GeoPackageSqlite.Open(PathOf()))
                Assert.Equal("ok", Scalar(connection, "PRAGMA integrity_check"));
        }

        [Fact]
        public void Export_FailingAfterTheWriterStarted_OnANewPath_LeavesNoFile()
        {
            IwxxmDocument document = Parse(SampleFiles.PathOf("metar-A3-1.xml"));

            Result<string> result = IwxxmGeoPackageExporter.Default.ExportReadingModelWith(document, PathOf(), null, UnencodableModel);

            Assert.Equal("GEOPACKAGE-EXPORT-ERROR", Assert.Single(result.Errors).Code);
            Assert.Empty(FilesInDirectory());
        }

        [Fact]
        public void ExportToBytes_ReturnsTheFile_AndLeavesNoTemporaryFile()
        {
            IwxxmDocument document = Parse(SampleFiles.PathOf("metar-A3-1.xml"));
            string[] temporaryBefore = TemporaryFiles();

            Result<byte[]> result = IwxxmGeoPackageExporter.Default.ExportToBytes(document, new GeoPackageExportOptions(GeoPackageVersion.Version14, LastChange));

            Assert.True(result.IsSuccess, string.Join(" | ", result.Errors));
            Assert.Equal("SQLite format 3\0", System.Text.Encoding.ASCII.GetString(result.Value, 0, 16));
            File.WriteAllBytes(PathOf("bytes.gpkg"), result.Value);
            Assert.True(IwxxmGeoPackageExporter.Default.Export(document, PathOf("file.gpkg"), new GeoPackageExportOptions(GeoPackageVersion.Version14, LastChange)).IsSuccess);
            Assert.Equal(Dump(PathOf("file.gpkg")), Dump(PathOf("bytes.gpkg")));
            Assert.Equal(temporaryBefore, TemporaryFiles());
        }

        [Fact]
        public void StaticConvenienceMethods_UseTheDefaultExporter()
        {
            IwxxmDocument document = Parse(SampleFiles.PathOf("metar-A3-1.xml"));

            Result<string> file = IwxxmGeoPackageExporter.ToGeoPackage(document, PathOf());
            Result<byte[]> bytes = IwxxmGeoPackageExporter.ToGeoPackageBytes(document, new GeoPackageExportOptions(GeoPackageVersion.Version13));

            Assert.True(file.IsSuccess);
            Assert.True(bytes.IsSuccess);
            Assert.Same(IwxxmGeoPackageExporter.Default, IwxxmGeoPackageExporter.Default);
        }

        private static string[] TemporaryFiles() =>
            Directory.GetFiles(Path.GetTempPath(), "iwxxm-" + new string('?', 32) + ".gpkg*").OrderBy(n => n, StringComparer.Ordinal).ToArray();

        [Fact]
        public void Options_RejectAnUndefinedVersion_IncludingTheDefaultValueOfTheEnum()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new GeoPackageExportOptions((GeoPackageVersion)42));
            Assert.Throws<ArgumentOutOfRangeException>(() => new GeoPackageExportOptions(default(GeoPackageVersion)));
            Assert.Throws<ArgumentOutOfRangeException>(() => new GeoPackageExportOptions(default(GeoPackageVersion), LastChange));
            Assert.False(Enum.IsDefined(typeof(GeoPackageVersion), default(GeoPackageVersion)));
        }

        [Fact]
        public void Version_NumericValues_ArePinned()
        {
            Assert.Equal(1, (int)GeoPackageVersion.Version13);
            Assert.Equal(2, (int)GeoPackageVersion.Version14);
            Assert.Equal(2, Enum.GetValues(typeof(GeoPackageVersion)).Length);
        }

        [Fact]
        public void Options_Defaults_AreVersion14_AndNoExplicitLastChange()
        {
            Assert.Equal(GeoPackageVersion.Version14, GeoPackageExportOptions.Default.Version);
            Assert.Null(GeoPackageExportOptions.Default.LastChangeUtc);
            Assert.Equal(GeoPackageVersion.Version14, new GeoPackageExportOptions().Version);
        }

        [Fact]
        public void ResolveLastChange_Prefers_TheExplicitValue_ThenTheIssueTime_ThenTheFixedFallback_NeverTheClock()
        {
            var issued = new DateTimeOffset(2025, 12, 1, 6, 30, 0, TimeSpan.FromHours(2));

            Assert.Equal(LastChange, new GeoPackageExportOptions(GeoPackageVersion.Version14, LastChange).ResolveLastChange(issued));
            Assert.Equal(issued, GeoPackageExportOptions.Default.ResolveLastChange(issued));
            Assert.Equal(GeoPackageExportOptions.FallbackLastChangeUtc, GeoPackageExportOptions.Default.ResolveLastChange(default(DateTimeOffset)));
            Assert.Equal(new DateTimeOffset(1970, 1, 1, 0, 0, 0, TimeSpan.Zero), GeoPackageExportOptions.FallbackLastChangeUtc);
            Assert.Equal(LastChange, new GeoPackageExportOptions(GeoPackageVersion.Version14, LastChange).ResolveLastChange(default(DateTimeOffset)));
        }
    }
}
