using System;
using System.Collections.Generic;
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

    /// <summary>
    /// Air-gapped <see cref="XmlResolver"/> that serves XSD <c>&lt;xs:import&gt;</c>/<c>&lt;xs:include&gt;</c> targets
    /// and XSLT <c>document()</c> calls exclusively from embedded assembly resources.
    /// <list type="bullet">
    /// <item>Absolute <c>schemaLocation</c> URLs used by the official schemas (<c>http://schemas.wmo.int/...</c>,
    /// <c>http://schemas.opengis.net/...</c>, <c>http://www.aixm.aero/schema/...</c>, <c>http://www.w3.org/...</c>)
    /// are mapped onto the byte-identical copies under <c>Resources/Xsd/Official</c>.</item>
    /// <item>Relative locations are resolved against the including document's base URI, so official relative
    /// includes resolve to canonical URLs and internal resources use the <c>embedded://iwxxm-resources/</c> scheme.</item>
    /// <item>Any other URI (file system, unknown hosts, resources that are not embedded) fails with an
    /// <see cref="XmlException"/>. No network or file-system I/O is ever performed.</item>
    /// </list>
    /// </summary>
    internal sealed class EmbeddedXmlUrlResolver : XmlResolver
    {
        internal const string EmbeddedScheme = "embedded";
        internal const string EmbeddedHost = "iwxxm-resources";
        internal static readonly Uri EmbeddedRootUri = new Uri(EmbeddedScheme + "://" + EmbeddedHost + "/");

        private const string OfficialRoot = "Xsd/Official/";
        private const string ExternalRoot = OfficialRoot + "externalSchema/";

        /// <summary>
        /// Canonical schema URL prefixes and the embedded folder that mirrors them, ordered from the
        /// most specific to the most generic prefix. The mapping mirrors the layout of the official
        /// wmo-im/iwxxm repository (<c>IWXXM/</c> and <c>externalSchema/</c>).
        /// </summary>
        private static readonly KeyValuePair<string, string>[] UrlPrefixMap =
        {
            new KeyValuePair<string, string>("http://schemas.wmo.int/iwxxm/2025-2/", OfficialRoot + "IWXXM/"),
            new KeyValuePair<string, string>("http://schemas.wmo.int/", ExternalRoot + "schemas.wmo.int/"),
            new KeyValuePair<string, string>("http://schemas.opengis.net/", ExternalRoot + "schemas.opengis.net/"),
            new KeyValuePair<string, string>("http://www.aixm.aero/schema/", ExternalRoot + "aero/aixm/"),
            new KeyValuePair<string, string>("http://www.w3.org/1999/xlink.xsd", ExternalRoot + "org/w3c/1999/xlink.xsd"),
            new KeyValuePair<string, string>("http://www.w3.org/2001/xml.xsd", ExternalRoot + "org/w3c/2001/xml.xsd")
        };

        public override ICredentials Credentials
        {
            set { /* Credentials are never used: the resolver performs no network access. */ }
        }

        public override Uri ResolveUri(Uri? baseUri, string? relativeUri)
        {
            if (string.IsNullOrWhiteSpace(relativeUri))
            {
                return baseUri ?? EmbeddedRootUri;
            }

            string trimmed = relativeUri!.Trim();
            if (Uri.TryCreate(trimmed, UriKind.Absolute, out Uri? absolute) &&
                (absolute.Scheme == Uri.UriSchemeHttp ||
                 absolute.Scheme == Uri.UriSchemeHttps ||
                 absolute.Scheme == EmbeddedScheme))
            {
                return absolute;
            }

            Uri effectiveBase = baseUri != null && baseUri.IsAbsoluteUri ? baseUri : EmbeddedRootUri;
            return new Uri(effectiveBase, trimmed);
        }

        public override object GetEntity(Uri absoluteUri, string? role, Type? ofObjectToReturn)
        {
            if (absoluteUri == null)
                throw new ArgumentNullException(nameof(absoluteUri));

            if (ofObjectToReturn != null && ofObjectToReturn != typeof(Stream) && ofObjectToReturn != typeof(object))
            {
                throw new XmlException(
                    $"The embedded resolver only returns streams; '{ofObjectToReturn.FullName}' was requested for '{absoluteUri}'.");
            }

            string? resourcePath = MapToResourcePath(absoluteUri);
            Stream? stream = resourcePath != null ? EmbeddedResourceLoader.TryOpen(resourcePath) : null;
            if (stream == null)
            {
                throw new XmlException(
                    $"The URI '{absoluteUri}' is not available in the embedded air-gapped resource catalog. " +
                    "Network and file-system access are disabled.");
            }

            return stream;
        }

        /// <summary>
        /// Maps an absolute URI to the embedded resource path (relative to <c>Resources/</c>), or returns
        /// <c>null</c> when the URI is outside the embedded catalog.
        /// </summary>
        internal static string? MapToResourcePath(Uri absoluteUri)
        {
            if (!absoluteUri.IsAbsoluteUri)
                return null;

            if (absoluteUri.Scheme == EmbeddedScheme)
            {
                if (!string.Equals(absoluteUri.Host, EmbeddedHost, StringComparison.OrdinalIgnoreCase))
                    return null;

                string path = Uri.UnescapeDataString(absoluteUri.AbsolutePath).TrimStart('/');
                return IsSafeRelativePath(path) ? path : null;
            }

            if (absoluteUri.Scheme != Uri.UriSchemeHttp && absoluteUri.Scheme != Uri.UriSchemeHttps)
                return null;

            if (!string.IsNullOrEmpty(absoluteUri.Query) || !string.IsNullOrEmpty(absoluteUri.Fragment))
                return null;

            // Canonical form: http scheme, lower-case host, unescaped path.
            string canonical = "http://" + absoluteUri.Host.ToLowerInvariant() + Uri.UnescapeDataString(absoluteUri.AbsolutePath);
            foreach (KeyValuePair<string, string> mapping in UrlPrefixMap)
            {
                if (mapping.Key.EndsWith("/", StringComparison.Ordinal))
                {
                    if (canonical.StartsWith(mapping.Key, StringComparison.Ordinal))
                    {
                        string path = mapping.Value + canonical.Substring(mapping.Key.Length);
                        return IsSafeRelativePath(path) ? path : null;
                    }
                }
                else if (string.Equals(canonical, mapping.Key, StringComparison.Ordinal))
                {
                    return mapping.Value;
                }
            }

            return null;
        }

        private static bool IsSafeRelativePath(string path)
        {
            if (path.Length == 0 || path.IndexOf('\\') >= 0)
                return false;

            foreach (string segment in path.Split('/'))
            {
                if (segment.Length == 0 || segment == "." || segment == "..")
                    return false;
            }

            return true;
        }
    }
}
