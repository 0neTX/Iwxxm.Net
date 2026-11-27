using System;
using System.Collections.Generic;
using System.Text;
using Iwxxm.Net.Infrastructure.GeoJson.Writing;

namespace Iwxxm.Net.Export.GeoPackage
{
    /// <summary>
    /// Writes the <c>properties</c> column of a feature table: the same JSON text as the <c>properties</c> member of the
    /// feature in the GeoJSON exporter output (compact, same key order, same number and string formatting).
    /// </summary>
    internal static class GeoPackagePropertiesJson
    {
        /// <summary>
        /// Writes <paramref name="properties"/> as a compact JSON object. Entries with a <c>null</c> value are omitted, like
        /// the GeoJSON exporter does; no properties give <c>{}</c>. Reuses the writer of the GeoJSON exporter, including its
        /// rules for numbers (shortest round-trip text, a non-finite number is <c>null</c>) and string escaping.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="properties"/> is <c>null</c>.</exception>
        public static string Write(IReadOnlyList<(string Key, object? Value)> properties)
        {
            if (properties == null)
                throw new ArgumentNullException(nameof(properties));

            var builder = new StringBuilder(Math.Max(64, properties.Count * 32));
            GeoJsonFeatureCollectionWriter.WriteProperties(new Rfc8259JsonWriter(builder, indented: false), properties);
            return builder.ToString();
        }
    }
}
