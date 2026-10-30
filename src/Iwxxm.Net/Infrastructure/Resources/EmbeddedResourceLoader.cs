using System;
using System.IO;
using System.Net;
using System.Reflection;
using System.Xml;

namespace Iwxxm.Net.Infrastructure.Resources
{
    /// <summary>
    /// Centralized reader for embedded assembly resources (<see cref="Assembly.GetManifestResourceStream(string)"/>)
    /// bundled inside the NuGet package. Resources are addressed by their path relative to the project
    /// <c>Resources/</c> folder (e.g. <c>Xsd/Official/IWXXM/iwxxm.xsd</c>); the manifest logical name is
    /// <c>Iwxxm.Net/Resources/</c> followed by that path (see <c>Iwxxm.Net.csproj</c>).
    /// Lookups are exact: no suffix or case-insensitive fallback is performed, so two files with the same
    /// name in different folders can never be confused.
    /// </summary>
    internal static class EmbeddedResourceLoader
    {
        internal const string LogicalNamePrefix = "Iwxxm.Net/Resources/";

        private static readonly Assembly ResourceAssembly = typeof(EmbeddedResourceLoader).Assembly;

        public static Stream GetRequiredResourceStream(string resourcePath)
        {
            if (string.IsNullOrWhiteSpace(resourcePath))
                throw new ArgumentException("Embedded resource path cannot be null or whitespace.", nameof(resourcePath));

            Stream? stream = TryOpen(resourcePath);
            if (stream != null)
            {
                return stream;
            }

            throw new FileNotFoundException(
                $"Embedded resource '{LogicalNamePrefix}{resourcePath}' was not found in assembly '{ResourceAssembly.FullName}'.");
        }

        public static Stream? TryOpen(string resourcePath)
        {
            if (string.IsNullOrWhiteSpace(resourcePath))
                return null;

            return ResourceAssembly.GetManifestResourceStream(LogicalNamePrefix + resourcePath.TrimStart('/'));
        }
    }
}
