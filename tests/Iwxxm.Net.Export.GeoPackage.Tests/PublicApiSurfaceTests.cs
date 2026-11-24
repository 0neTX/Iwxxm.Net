using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Xunit;

namespace Iwxxm.Net.Export.GeoPackage.Tests
{
    /// <summary>
    /// Keeps the public surface of the package small and intended: the writer, the data model and the SQLite plumbing stay
    /// internal, and no public signature exposes a Microsoft.Data.Sqlite type. The error code is documented in
    /// <c>docs/REFERENCE.md</c> (the core <c>ErrorCodeDocumentationTests</c> also scans this package's source).
    /// </summary>
    public sealed class PublicApiSurfaceTests
    {
        private static readonly Assembly Package = typeof(IwxxmGeoPackageExporter).Assembly;

        [Fact]
        public void ExportedTypes_AreExactlyTheIntendedOnes()
        {
            string[] exported = Package.GetExportedTypes().Select(t => t.FullName!).OrderBy(n => n, StringComparer.Ordinal).ToArray();

            Assert.Equal(
                new[]
                {
                    "Iwxxm.Net.Export.GeoPackage.GeoPackageErrorCodes",
                    "Iwxxm.Net.Export.GeoPackage.GeoPackageExportOptions",
                    "Iwxxm.Net.Export.GeoPackage.GeoPackageVersion",
                    "Iwxxm.Net.Export.GeoPackage.IwxxmGeoPackageExporter",
                    "Iwxxm.Net.IwxxmParserGeoPackageExtensions",
                },
                exported);
        }

        [Fact]
        public void PublicSignatures_ExposeNoSqliteType()
        {
            foreach (Type type in Package.GetExportedTypes())
            {
                foreach (MethodBase member in type.GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                    .OfType<MethodBase>().Where(m => m.IsPublic || m.IsFamily))
                {
                    IEnumerable<Type> used = member.GetParameters().Select(p => p.ParameterType);
                    if (member is MethodInfo method)
                        used = used.Concat(new[] { method.ReturnType });

                    Assert.DoesNotContain(used.SelectMany(Flatten), t => (t.Namespace ?? string.Empty).StartsWith("Microsoft.Data", StringComparison.Ordinal));
                }
            }
        }

        private static IEnumerable<Type> Flatten(Type type)
        {
            yield return type;
            foreach (Type argument in type.IsGenericType ? type.GetGenericArguments().SelectMany(Flatten) : Enumerable.Empty<Type>())
                yield return argument;
            if (type.HasElementType)
            {
                foreach (Type element in Flatten(type.GetElementType()!))
                    yield return element;
            }
        }

        [Fact]
        public void ParserExtensions_AreExtensionMethodsOnTheParser_OrStaticMethodsOverTheDefaultParser()
        {
            Type extensions = typeof(IwxxmParserGeoPackageExtensions);
            MethodInfo[] methods = extensions.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);

            string[] extensionNames = methods.Where(m => m.IsDefined(typeof(System.Runtime.CompilerServices.ExtensionAttribute), false))
                .Select(m => m.Name).Distinct().OrderBy(n => n, StringComparer.Ordinal).ToArray();
            string[] staticNames = methods.Where(m => !m.IsDefined(typeof(System.Runtime.CompilerServices.ExtensionAttribute), false))
                .Select(m => m.Name).Distinct().OrderBy(n => n, StringComparer.Ordinal).ToArray();

            Assert.Equal(new[] { "ProcessFileToGeoPackage", "ProcessToGeoPackage" }, extensionNames);
            Assert.Equal(new[] { "ParseFileToGeoPackage", "ParseToGeoPackage" }, staticNames);
            Assert.All(methods, m => Assert.Equal(typeof(Result<string>), m.ReturnType));
            Assert.All(methods.Where(m => m.IsDefined(typeof(System.Runtime.CompilerServices.ExtensionAttribute), false)), m => Assert.Equal(typeof(IwxxmParser), m.GetParameters()[0].ParameterType));
        }

        [Fact]
        public void ErrorCode_IsDocumentedInTheReference_AndWrittenOnlyInItsConstant()
        {
            string root = RepositoryRoot();
            string reference = File.ReadAllText(Path.Combine(root, "docs", "REFERENCE.md"));
            Assert.Equal("GEOPACKAGE-EXPORT-ERROR", GeoPackageErrorCodes.ExportError);
            Assert.Contains("| `GEOPACKAGE-EXPORT-ERROR` |", reference, StringComparison.Ordinal);

            var literal = new Regex("\"GEOPACKAGE-EXPORT-ERROR\"", RegexOptions.CultureInvariant);
            string separator = Path.DirectorySeparatorChar.ToString();
            string[] writers = Directory.EnumerateFiles(Path.Combine(root, "src", "Iwxxm.Net.Export.GeoPackage"), "*.cs", SearchOption.AllDirectories)
                .Where(f => !f.Contains(separator + "obj" + separator) && !f.Contains(separator + "bin" + separator))
                .Where(f => literal.IsMatch(File.ReadAllText(f)))
                .Select(Path.GetFileName)
                .ToArray()!;
            Assert.Equal(new[] { "GeoPackageErrorCodes.cs" }, writers);
        }

        private static string RepositoryRoot()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null && !File.Exists(Path.Combine(directory.FullName, "Iwxxm.Net.slnx")))
                directory = directory.Parent;

            Assert.True(directory != null, "Iwxxm.Net.slnx was not found above the test output directory.");
            return directory!.FullName;
        }
    }
}
