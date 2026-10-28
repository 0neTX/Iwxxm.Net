using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml;

namespace Iwxxm.Net.Infrastructure.Resources
{
    /// <summary>
    /// Centralized reader for embedded assembly resources (<see cref="Assembly.GetManifestResourceStream(string)"/>)
    /// bundled inside the NuGet package.
    /// </summary>
    internal static class EmbeddedResourceLoader
    {
        private static readonly Assembly ResourceAssembly = typeof(EmbeddedResourceLoader).Assembly;

        public static Stream GetRequiredResourceStream(string resourceLogicalOrSuffixName)
        {
            if (string.IsNullOrWhiteSpace(resourceLogicalOrSuffixName))
                throw new ArgumentException("Embedded resource name cannot be null or whitespace.", nameof(resourceLogicalOrSuffixName));

            // Attempt direct lookup by exact LogicalName
            Stream? stream = ResourceAssembly.GetManifestResourceStream(resourceLogicalOrSuffixName);
            if (stream != null)
            {
                return stream;
            }

            // Fallback lookup by case-insensitive suffix match across manifest resource names
            string? matchedName = ResourceAssembly
                .GetManifestResourceNames()
                .FirstOrDefault(name => name.EndsWith(resourceLogicalOrSuffixName, StringComparison.OrdinalIgnoreCase));

            if (matchedName != null)
            {
                stream = ResourceAssembly.GetManifestResourceStream(matchedName);
                if (stream != null)
                {
                    return stream;
                }
            }

            string available = string.Join(", ", ResourceAssembly.GetManifestResourceNames());
            throw new FileNotFoundException(
                $"Embedded resource '{resourceLogicalOrSuffixName}' was not found in assembly '{ResourceAssembly.FullName}'. Available resources: [{available}]");
        }
    }

    /// <summary>
    /// Custom <see cref="XmlUrlResolver"/> that intercepts XSD <c>&lt;xs:import&gt;</c> and <c>&lt;xs:include&gt;</c>
    /// directives (e.g., <c>schemaLocation="gml.xsd"</c> or <c>schemaLocation="xlink.xsd"</c>) and resolves them
    /// directly from embedded assembly streams without performing any file-system or network I/O.
    /// </summary>
    internal sealed class EmbeddedXmlUrlResolver : XmlUrlResolver
    {
        public override Uri ResolveUri(Uri? baseUri, string? relativeUri)
        {
            if (!string.IsNullOrWhiteSpace(relativeUri))
            {
                string fileName = Path.GetFileName(relativeUri);
                return new Uri($"embedded://iwxxm-resources/{fileName}");
            }

            return base.ResolveUri(baseUri, relativeUri);
        }

        public override object GetEntity(Uri absoluteUri, string? role, Type? ofObjectToReturn)
        {
            if (absoluteUri == null)
                throw new ArgumentNullException(nameof(absoluteUri));

            string fileName = Path.GetFileName(absoluteUri.LocalPath);
            if (string.IsNullOrWhiteSpace(fileName))
            {
                fileName = absoluteUri.Host;
            }

            return EmbeddedResourceLoader.GetRequiredResourceStream(fileName);
        }
    }
}
