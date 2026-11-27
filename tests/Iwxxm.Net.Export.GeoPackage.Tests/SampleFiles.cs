using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Iwxxm.Net.Export.GeoPackage.Tests
{
    /// <summary>
    /// The official WMO examples of the core test project, linked into this project's output (see the csproj), never copied.
    /// </summary>
    internal static class SampleFiles
    {
        private static readonly string Directory = Path.Combine(AppContext.BaseDirectory, "Samples", "wmo-examples");

        private static readonly string ExtraDirectory = Path.Combine(AppContext.BaseDirectory, "Samples", "extra");

        /// <summary>The full path of one of the extra samples (SIGWX documents).</summary>
        internal static string ExtraPathOf(string fileName) => Path.Combine(ExtraDirectory, fileName);

        /// <summary>The extra sample file names, ordered, as xunit theory data.</summary>
        internal static IEnumerable<object[]> Extra() =>
            System.IO.Directory.GetFiles(ExtraDirectory, "*.xml").Select(Path.GetFileName).OrderBy(n => n, StringComparer.Ordinal).Select(n => new object[] { n! });

        /// <summary>The full path of one sample.</summary>
        internal static string PathOf(string fileName) => Path.Combine(Directory, fileName);

        /// <summary>The sample file names, ordered, as xunit theory data.</summary>
        internal static IEnumerable<object[]> All() =>
            System.IO.Directory.GetFiles(Directory, "*.xml").Select(Path.GetFileName).OrderBy(n => n, StringComparer.Ordinal).Select(n => new object[] { n! });

        /// <summary>The full paths of the WMO examples followed by the extra (SIGWX) samples, each group ordered.</summary>
        internal static IEnumerable<string> AllPaths() =>
            All().Select(a => PathOf((string)a[0])).Concat(Extra().Select(a => ExtraPathOf((string)a[0])));
    }
}
