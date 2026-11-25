using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Iwxxm.Net.Export.GeoPackage.Model;
using Microsoft.Data.Sqlite;
using Xunit;
using static Iwxxm.Net.Export.GeoPackage.Tests.GeoPackageTestDatabase;

namespace Iwxxm.Net.Export.GeoPackage.Tests
{
    public sealed class GeoPackageWriterTests : IDisposable
    {
        private static readonly DateTimeOffset LastChange = new DateTimeOffset(2026, 5, 6, 7, 8, 9, 123, TimeSpan.Zero);

        private readonly string _directory = Path.Combine(Path.GetTempPath(), "iwxxm-gpkg-writer-" + Guid.NewGuid().ToString("N"));

        public GeoPackageWriterTests() => Directory.CreateDirectory(_directory);

        public void Dispose() => Directory.Delete(_directory, recursive: true);

        private string PathOf(string name = "out.gpkg") => Path.Combine(_directory, name);

        private static GeoPackageDocumentModel Model(string sample = "metar-A3-1.xml") =>
            GeoPackageDataTablesTests.Read(SampleFiles.PathOf(sample));

        private static GeoPackageWriteOptions Options(GeoPackageVersion version = GeoPackageVersion.Version14) => new GeoPackageWriteOptions(LastChange, version);

        private static string Dump(string path)
        {
            using (SqliteConnection connection = GeoPackageSqlite.Open(path))
                return CanonicalDump(connection);
        }

        private string[] FilesInDirectory() => Directory.GetFileSystemEntries(_directory).Select(Path.GetFileName).OrderBy(n => n, StringComparer.Ordinal).ToArray()!;

        [Fact]
        public void Write_ProducesAGeoPackageThatPassesTheSqliteChecks()
        {
            string path = PathOf();
            GeoPackageWriter.Write(path, Model(), Options());

            Assert.Equal(new[] { "out.gpkg" }, FilesInDirectory());
            using (SqliteConnection connection = GeoPackageSqlite.Open(path))
            {
                Assert.Equal("1196444487", Scalar(connection, "PRAGMA application_id"));
                Assert.Equal("10400", Scalar(connection, "PRAGMA user_version"));
                Assert.Equal("ok", Scalar(connection, "PRAGMA integrity_check"));
                Assert.Empty(Rows(connection, "PRAGMA foreign_key_check"));
                Assert.Equal("3", Scalar(connection, "SELECT count(*) FROM gpkg_spatial_ref_sys"));
                Assert.NotEqual("0", Scalar(connection, "SELECT count(*) FROM gpkg_contents"));
            }
        }

        [Fact]
        public void Write_Version13_DeclaresTheOlderUserVersion()
        {
            string path = PathOf();
            GeoPackageWriter.Write(path, Model(), Options(GeoPackageVersion.Version13));

            using (SqliteConnection connection = GeoPackageSqlite.Open(path))
            {
                Assert.Equal("10300", Scalar(connection, "PRAGMA user_version"));
                Assert.Equal("1.3", Scalar(connection, "SELECT value FROM iwxxm_metadata WHERE key = 'geopackage_version'"));
            }
        }

        [Fact]
        public void Write_UsesTheGivenLastChange_ForEveryTable()
        {
            string path = PathOf();
            GeoPackageWriter.Write(path, Model(), Options());

            using (SqliteConnection connection = GeoPackageSqlite.Open(path))
            {
                Assert.Equal(new string?[] { "2026-05-06T07:08:09.123Z" }, Rows(connection, "SELECT DISTINCT last_change FROM gpkg_contents").Single());
            }
        }

        [Fact]
        public void Write_SameInputTwice_GivesTheSameCanonicalDump()
        {
            // The files are compared by content, not by bytes: the header carries the SQLite library version.
            GeoPackageDocumentModel model = Model("metar-NIL-collect.xml");
            GeoPackageWriter.Write(PathOf("a.gpkg"), model, Options());
            GeoPackageWriter.Write(PathOf("b.gpkg"), model, Options());

            Assert.Equal(Dump(PathOf("a.gpkg")), Dump(PathOf("b.gpkg")));
        }

        [Fact]
        public void Write_ADifferentLastChange_ChangesOnlyThatColumn()
        {
            GeoPackageDocumentModel model = Model();
            GeoPackageWriter.Write(PathOf("a.gpkg"), model, Options());
            GeoPackageWriter.Write(PathOf("b.gpkg"), model, new GeoPackageWriteOptions(LastChange.AddDays(1)));

            string first = Dump(PathOf("a.gpkg"));
            string second = Dump(PathOf("b.gpkg"));
            Assert.NotEqual(first, second);
            Assert.Equal(first, second.Replace("2026-05-07T07:08:09.123Z", "2026-05-06T07:08:09.123Z"));
        }

        [Fact]
        public void Write_ReplacesAnExistingFile()
        {
            string path = PathOf();
            File.WriteAllText(path, "not a database");

            GeoPackageWriter.Write(path, Model(), Options());

            Assert.Equal(new[] { "out.gpkg" }, FilesInDirectory());
            using (SqliteConnection connection = GeoPackageSqlite.Open(path))
                Assert.Equal("ok", Scalar(connection, "PRAGMA integrity_check"));
        }

        [Fact]
        public void Write_TheFileIsNotLocked_AfterTheCall()
        {
            string path = PathOf();
            GeoPackageWriter.Write(path, Model(), Options());

            File.Delete(path);
            Assert.False(File.Exists(path));
        }

        // ---- failures ------------------------------------------------------------------------------------------------

        [Fact]
        public void Write_ToAMissingDirectory_Throws_AndCreatesNothing()
        {
            string path = Path.Combine(_directory, "missing", "out.gpkg");

            Assert.Throws<DirectoryNotFoundException>(() => GeoPackageWriter.Write(path, Model(), Options()));

            Assert.False(Directory.Exists(Path.Combine(_directory, "missing")));
            Assert.Empty(FilesInDirectory());
        }

        [Fact]
        public void Write_ToADirectoryPath_Throws_AndLeavesTheDirectory()
        {
            string path = PathOf("a-directory");
            Directory.CreateDirectory(path);

            Assert.Throws<IOException>(() => GeoPackageWriter.Write(path, Model(), Options()));

            Assert.Equal(new[] { "a-directory" }, FilesInDirectory());
        }

        [Fact]
        public void Write_WithBadArguments_Throws()
        {
            GeoPackageDocumentModel model = Model();
            Assert.Throws<ArgumentNullException>(() => GeoPackageWriter.Write(null!, model, Options()));
            Assert.Throws<ArgumentNullException>(() => GeoPackageWriter.Write(PathOf(), null!, Options()));
            Assert.Throws<ArgumentNullException>(() => GeoPackageWriter.Write(PathOf(), model, null!));
            Assert.Throws<ArgumentException>(() => GeoPackageWriter.Write(" ", model, Options()));
            Assert.Empty(FilesInDirectory());
        }

        [Fact]
        public void Write_Cancelled_BeforeStarting_LeavesNothing()
        {
            using (var cancellation = new CancellationTokenSource())
            {
                cancellation.Cancel();
                Assert.Throws<OperationCanceledException>(() => GeoPackageWriter.Write(PathOf(), Model(), Options(), cancellation.Token));
            }

            Assert.Empty(FilesInDirectory());
        }

        [Fact]
        public void Write_Cancelled_AfterTheFeatureTablesWereWritten_LeavesNoFile()
        {
            GeoPackageDocumentModel model = Model();
            using (var cancellation = new CancellationTokenSource())
            {
                // The reports are written after the feature tables: cancel when the writer first reads them.
                var changed = new GeoPackageDocumentModel(
                    model.DocumentId, model.DocumentType, model.BulletinIdentifier, new Triggering<GeoPackageReportRecord>(model.Reports, cancellation.Cancel), model.Features);

                Assert.Throws<OperationCanceledException>(() => GeoPackageWriter.Write(PathOf(), changed, Options(), cancellation.Token));
            }

            Assert.Empty(FilesInDirectory());
        }

        [Fact]
        public void Write_WithAFeatureThatCannotBeEncoded_LeavesNoFile_AndKeepsAnExistingOne()
        {
            GeoPackageDocumentModel model = Model();
            GeoPackageFeature first = model.Features.First(f => f.Geometry != null);
            var bad = first.WithGeometry(GeoPackageGeometry.ForPoint(new GeoPackagePosition(double.NaN, 0)));
            var changed = new GeoPackageDocumentModel(model.DocumentId, model.DocumentType, model.BulletinIdentifier, model.Reports, model.Features.Concat(new[] { bad }).ToList());

            // Nothing there before: nothing after.
            Assert.Throws<ArgumentException>(() => GeoPackageWriter.Write(PathOf(), changed, Options()));
            Assert.Empty(FilesInDirectory());

            // A previous export stays as it was.
            string path = PathOf();
            GeoPackageWriter.Write(path, model, Options());
            byte[] before = File.ReadAllBytes(path);
            Assert.Throws<ArgumentException>(() => GeoPackageWriter.Write(path, changed, Options()));
            Assert.Equal(new[] { "out.gpkg" }, FilesInDirectory());
            Assert.Equal(before, File.ReadAllBytes(path));
        }

        [Theory]
        [InlineData("-wal")]
        [InlineData("-journal")]
        public void Write_OverAnExistingTarget_WithASidecarNextToIt_Throws_AndChangesNothing(string suffix)
        {
            string path = PathOf();
            GeoPackageWriter.Write(path, Model(), Options());
            byte[] before = File.ReadAllBytes(path);
            File.WriteAllText(path + suffix, "left by another process");

            IOException error = Assert.Throws<IOException>(() => GeoPackageWriter.Write(path, Model("metar-NIL-collect.xml"), Options()));

            Assert.Contains("in use or needs recovery", error.Message, StringComparison.Ordinal);
            Assert.Contains(path + suffix, error.Message, StringComparison.Ordinal);
            Assert.Equal(before, File.ReadAllBytes(path));
            Assert.Equal(new[] { "out.gpkg", "out.gpkg" + suffix }, FilesInDirectory());
        }

        [Fact]
        public void Write_OverANewTarget_IgnoresASidecarThatHasNoDatabase()
        {
            string path = PathOf();
            File.WriteAllText(path + "-wal", "stale");

            GeoPackageWriter.Write(path, Model(), Options());

            Assert.Equal(new[] { "out.gpkg", "out.gpkg-wal" }, FilesInDirectory());
        }

        [Fact]
        public void Write_WithAVeryLongTargetName_StillBuildsTheTemporaryFile()
        {
            // The temporary name does not contain the target name, so a target name at the file name limit still works.
            string name = new string('a', 240) + ".gpkg";
            string path = PathOf(name);

            GeoPackageWriter.Write(path, Model(), Options());

            Assert.Equal(new[] { name }, FilesInDirectory());
        }

        /// <summary>A list that runs an action the first time it is enumerated.</summary>
        private sealed class Triggering<T> : IReadOnlyList<T>
        {
            private readonly IReadOnlyList<T> _inner;
            private readonly Action _trigger;

            public Triggering(IReadOnlyList<T> inner, Action trigger)
            {
                _inner = inner;
                _trigger = trigger;
            }

            public int Count => _inner.Count;

            public T this[int index] => _inner[index];

            public IEnumerator<T> GetEnumerator()
            {
                _trigger();
                return _inner.GetEnumerator();
            }

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }
    }
}
