using System;
using System.Collections.Generic;
using Iwxxm.Net.Infrastructure.GeoJson;

namespace Iwxxm.Net.Export.GeoPackage.Model
{
    /// <summary>
    /// A GeoJSON feature of the exported document together with the values of the fixed columns of a feature table.
    /// </summary>
    /// <remarks>
    /// <para>The fixed-column values are read from the feature properties under the same keys the GeoJSON exporter
    /// writes (<c>featureType</c>, <c>reportId</c>, <c>reportType</c>, <c>bulletinIdentifier</c>, <c>issueTimeUtc</c>), so
    /// the table and the <c>properties</c> JSON can never disagree. Each value is the first property with that key whose
    /// value is a string; a missing key, or a value that is not a string, yields <c>null</c>. The feature id is the
    /// feature id as the GeoJSON writer sees it: an empty or white-space id is <c>null</c> (the GeoJSON writer omits
    /// the <c>id</c> member then).</para>
    /// <para>The issue time is kept as the ISO 8601 round-trip text of the property and parsed by <see cref="GeoPackageDateTime"/>.</para>
    /// </remarks>
    internal sealed class GeoPackageFeature
    {
        public GeoPackageFeature(GeoJsonFeature feature)
        {
            Feature = feature;
            FeatureId = string.IsNullOrWhiteSpace(feature.Id) ? null : feature.Id;
            FeatureType = StringProperty(feature.Properties, "featureType");
            ReportId = StringProperty(feature.Properties, "reportId");
            ReportType = StringProperty(feature.Properties, "reportType");
            BulletinIdentifier = StringProperty(feature.Properties, "bulletinIdentifier");
            IssueTimeText = StringProperty(feature.Properties, "issueTimeUtc");
            IssueTime = GeoPackageDateTime.ParseIso(IssueTimeText);
        }

        /// <summary>The GeoJSON feature: geometry (already split at the antimeridian) and ordered properties.</summary>
        public GeoJsonFeature Feature { get; }

        /// <summary>The feature id, or <c>null</c> when the GeoJSON feature carries none.</summary>
        public string? FeatureId { get; }

        /// <summary>The <c>featureType</c> property (for example <c>METAR_OBSERVATION</c>), or <c>null</c> when absent.</summary>
        public string? FeatureType { get; }

        /// <summary>The <c>reportId</c> property, or <c>null</c> when absent.</summary>
        public string? ReportId { get; }

        /// <summary>The <c>reportType</c> property (the name of the report type), or <c>null</c> when absent.</summary>
        public string? ReportType { get; }

        /// <summary>The <c>bulletinIdentifier</c> property, or <c>null</c> for a feature of a standalone report.</summary>
        public string? BulletinIdentifier { get; }

        /// <summary>The <c>issueTimeUtc</c> property as written by the GeoJSON exporter, or <c>null</c> when absent.</summary>
        public string? IssueTimeText { get; }

        /// <summary>The issue time parsed from <see cref="IssueTimeText"/>, or <c>null</c> when the property is absent or is not ISO 8601 text with an offset.</summary>
        public DateTimeOffset? IssueTime { get; }

        /// <summary>The ordered properties, including entries with a <c>null</c> value (those are never written).</summary>
        public IReadOnlyList<(string Key, object? Value)> Properties => Feature.Properties;

        /// <summary>The first property named <paramref name="key"/> whose value is a string, or <c>null</c> when there is none.</summary>
        internal static string? StringProperty(IReadOnlyList<(string Key, object? Value)> properties, string key)
        {
            foreach ((string propertyKey, object? value) in properties)
            {
                if (propertyKey == key && value is string text)
                    return text;
            }

            return null;
        }
    }
}
