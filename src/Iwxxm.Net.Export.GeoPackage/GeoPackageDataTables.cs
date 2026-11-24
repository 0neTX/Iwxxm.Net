using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Threading;
using Iwxxm.Net.Export.GeoPackage.Model;
using Microsoft.Data.Sqlite;

namespace Iwxxm.Net.Export.GeoPackage
{
    /// <summary>
    /// Creates and fills the data tables of a GeoPackage from the document model, and registers them in
    /// <c>gpkg_contents</c> / <c>gpkg_geometry_columns</c> (see <see cref="GeoPackageSchema"/>): one feature table per geometry
    /// family that has rows (<c>iwxxm_points</c>, <c>iwxxm_lines</c>, <c>iwxxm_polygons</c>), then <c>iwxxm_attributes</c>
    /// (features without geometry), <c>iwxxm_reports</c> and <c>iwxxm_metadata</c>.
    /// </summary>
    /// <remarks>
    /// Every value is bound as a parameter; the only text put into SQL are the fixed table and column names of this class.
    /// Rows get their <c>fid</c> from SQLite in insertion order, which is the GeoJSON feature order. The tables use a plain
    /// <c>INTEGER PRIMARY KEY</c> (no <c>AUTOINCREMENT</c>, so no <c>sqlite_sequence</c> table).
    /// </remarks>
    internal static class GeoPackageDataTables
    {
        internal const string PointsTable = "iwxxm_points";
        internal const string LinesTable = "iwxxm_lines";
        internal const string PolygonsTable = "iwxxm_polygons";
        internal const string AttributesTable = "iwxxm_attributes";
        internal const string ReportsTable = "iwxxm_reports";
        internal const string MetadataTable = "iwxxm_metadata";

        internal const string GeometryColumn = "geom";

        // The columns every feature and attribute row has besides the geometry, in table order.
        private const string FeatureColumns = "feature_id, feature_type, report_id, report_type, bulletin_identifier, issue_time_utc, properties";

        private const string FeatureColumnDefinitions =
            "\"feature_id\" TEXT, \"feature_type\" TEXT, \"report_id\" TEXT, \"report_type\" TEXT, \"bulletin_identifier\" TEXT, " +
            "\"issue_time_utc\" DATETIME, \"properties\" TEXT NOT NULL";

        /// <summary>
        /// Writes every table for <paramref name="model"/> inside <paramref name="transaction"/>. The base schema must exist.
        /// </summary>
        /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled; checked before every row.</exception>
        public static void Write(SqliteTransaction transaction, GeoPackageDocumentModel model, GeoPackageWriteOptions options, CancellationToken cancellationToken)
        {
            if (transaction == null)
                throw new ArgumentNullException(nameof(transaction));
            if (model == null)
                throw new ArgumentNullException(nameof(model));
            if (options == null)
                throw new ArgumentNullException(nameof(options));

            string lastChange = GeoPackageDateTime.Format(options.LastChange);

            var points = new List<GeoPackageFeature>();
            var lines = new List<GeoPackageFeature>();
            var polygons = new List<GeoPackageFeature>();
            var withoutGeometry = new List<GeoPackageFeature>();
            foreach (GeoPackageFeature feature in model.Features)
            {
                cancellationToken.ThrowIfCancellationRequested();
                switch (feature.Geometry?.Kind)
                {
                    case null:
                        withoutGeometry.Add(feature);
                        break;
                    case GeoPackageGeometryKind.Point:
                        points.Add(feature);
                        break;
                    case GeoPackageGeometryKind.MultiLineString:
                        lines.Add(feature);
                        break;
                    case GeoPackageGeometryKind.MultiPolygon:
                        polygons.Add(feature);
                        break;
                    default:
                        throw new InvalidOperationException($"Unsupported geometry kind {feature.Geometry!.Kind}.");
                }
            }

            // A geometry table with no rows is not created at all.
            WriteFeatureTable(transaction, PointsTable, "POINT", "IWXXM features with a point geometry", points, lastChange, cancellationToken);
            WriteFeatureTable(transaction, LinesTable, "MULTILINESTRING", "IWXXM features with a line geometry", lines, lastChange, cancellationToken);
            WriteFeatureTable(transaction, PolygonsTable, "MULTIPOLYGON", "IWXXM features with a polygon geometry", polygons, lastChange, cancellationToken);

            WriteAttributes(transaction, withoutGeometry, lastChange, cancellationToken);
            WriteReports(transaction, model.Reports, lastChange, cancellationToken);
            WriteMetadata(transaction, model, options, lastChange);
        }

        private static void WriteFeatureTable(
            SqliteTransaction transaction,
            string table,
            string geometryType,
            string description,
            List<GeoPackageFeature> features,
            string lastChange,
            CancellationToken cancellationToken)
        {
            if (features.Count == 0)
                return;

            GeoPackageSqlite.Execute(
                transaction,
                $"CREATE TABLE \"{table}\" (\"fid\" INTEGER PRIMARY KEY NOT NULL, \"{GeometryColumn}\" {geometryType} NOT NULL, {FeatureColumnDefinitions})");

            var extent = new ExtentBuilder();
            using (SqliteCommand command = GeoPackageSqlite.CreateCommand(
                transaction,
                $"INSERT INTO \"{table}\" ({GeometryColumn}, {FeatureColumns}) VALUES ($geom, $featureId, $featureType, $reportId, $reportType, $bulletin, $issueTime, $properties)"))
            {
                SqliteParameter geom = command.Parameters.Add("$geom", SqliteType.Blob);
                var columns = new FeatureParameters(command);

                foreach (GeoPackageFeature feature in features)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    GeoPackageGeometry geometry = feature.Geometry!;
                    geom.Value = GeoPackageGeometryEncoder.Encode(geometry, GeoPackageSchema.Wgs84SrsId);
                    columns.Set(feature);
                    command.ExecuteNonQuery();
                    extent.Include(geometry.Envelope());
                }
            }

            GeoPackageSchema.AddContents(transaction, table, GeoPackageSchema.FeaturesDataType, description, lastChange, extent.Result, GeoPackageSchema.Wgs84SrsId);
            GeoPackageSchema.AddGeometryColumn(transaction, table, GeometryColumn, geometryType);
        }

        private static void WriteAttributes(SqliteTransaction transaction, List<GeoPackageFeature> features, string lastChange, CancellationToken cancellationToken)
        {
            // Created even when empty: unlike the geometry tables it is the one place a consumer looks for features that
            // have no geometry, so a query against it should not fail with "no such table".
            GeoPackageSqlite.Execute(transaction, $"CREATE TABLE \"{AttributesTable}\" (\"fid\" INTEGER PRIMARY KEY NOT NULL, {FeatureColumnDefinitions})");

            using (SqliteCommand command = GeoPackageSqlite.CreateCommand(
                transaction,
                $"INSERT INTO \"{AttributesTable}\" ({FeatureColumns}) VALUES ($featureId, $featureType, $reportId, $reportType, $bulletin, $issueTime, $properties)"))
            {
                var columns = new FeatureParameters(command);
                foreach (GeoPackageFeature feature in features)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    columns.Set(feature);
                    command.ExecuteNonQuery();
                }
            }

            RegisterAttributesTable(transaction, AttributesTable, "IWXXM features without a geometry", lastChange);
        }

        private static void WriteReports(SqliteTransaction transaction, IReadOnlyList<GeoPackageReportRecord> reports, string lastChange, CancellationToken cancellationToken)
        {
            GeoPackageSqlite.Execute(
                transaction,
                $"CREATE TABLE \"{ReportsTable}\" (\"fid\" INTEGER PRIMARY KEY NOT NULL, \"report_id\" TEXT NOT NULL, \"report_type\" TEXT NOT NULL, " +
                "\"report_status\" TEXT NOT NULL, \"permissible_usage\" TEXT NOT NULL, \"issue_time_utc\" DATETIME NOT NULL, \"bulletin_identifier\" TEXT)");

            using (SqliteCommand command = GeoPackageSqlite.CreateCommand(
                transaction,
                $"INSERT INTO \"{ReportsTable}\" (report_id, report_type, report_status, permissible_usage, issue_time_utc, bulletin_identifier) " +
                "VALUES ($reportId, $reportType, $status, $usage, $issueTime, $bulletin)"))
            {
                SqliteParameter reportId = command.Parameters.Add("$reportId", SqliteType.Text);
                SqliteParameter reportType = command.Parameters.Add("$reportType", SqliteType.Text);
                SqliteParameter status = command.Parameters.Add("$status", SqliteType.Text);
                SqliteParameter usage = command.Parameters.Add("$usage", SqliteType.Text);
                SqliteParameter issueTime = command.Parameters.Add("$issueTime", SqliteType.Text);
                SqliteParameter bulletin = command.Parameters.Add("$bulletin", SqliteType.Text);

                foreach (GeoPackageReportRecord report in reports)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    reportId.Value = report.ReportId;
                    reportType.Value = report.ReportType.ToString();
                    status.Value = report.ReportStatus;
                    usage.Value = report.PermissibleUsage;
                    issueTime.Value = GeoPackageDateTime.Format(report.IssueTime);
                    bulletin.Value = ValueOf(report.BulletinIdentifier);
                    command.ExecuteNonQuery();
                }
            }

            RegisterAttributesTable(transaction, ReportsTable, "One row per report of the exported document", lastChange);
        }

        private static void WriteMetadata(SqliteTransaction transaction, GeoPackageDocumentModel model, GeoPackageWriteOptions options, string lastChange)
        {
            GeoPackageSqlite.Execute(
                transaction,
                $"CREATE TABLE \"{MetadataTable}\" (\"fid\" INTEGER PRIMARY KEY NOT NULL, \"key\" TEXT NOT NULL UNIQUE, \"value\" TEXT NOT NULL)");

            var entries = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("geopackage_version", options.Version == GeoPackageVersion.Version13 ? "1.3" : "1.4"),
                new KeyValuePair<string, string>("library_version", LibraryVersion),
                new KeyValuePair<string, string>("source_document_id", model.DocumentId),
                new KeyValuePair<string, string>("source_document_type", model.DocumentType.ToString()),
            };
            if (model.BulletinIdentifier != null)
                entries.Add(new KeyValuePair<string, string>("bulletin_identifier", model.BulletinIdentifier));
            entries.Add(new KeyValuePair<string, string>("report_count", model.Reports.Count.ToString(CultureInfo.InvariantCulture)));

            using (SqliteCommand command = GeoPackageSqlite.CreateCommand(transaction, $"INSERT INTO \"{MetadataTable}\" (\"key\", \"value\") VALUES ($key, $value)"))
            {
                SqliteParameter key = command.Parameters.Add("$key", SqliteType.Text);
                SqliteParameter value = command.Parameters.Add("$value", SqliteType.Text);
                foreach (KeyValuePair<string, string> entry in entries)
                {
                    key.Value = entry.Key;
                    value.Value = entry.Value;
                    command.ExecuteNonQuery();
                }
            }

            RegisterAttributesTable(transaction, MetadataTable, "Library version, source document and GeoPackage version", lastChange);
        }

        // An attributes table has no extent; GDAL gives it the "undefined geographic" reference system, so does this.
        private static void RegisterAttributesTable(SqliteTransaction transaction, string table, string description, string lastChange) =>
            GeoPackageSchema.AddContents(transaction, table, GeoPackageSchema.AttributesDataType, description, lastChange, null, GeoPackageSchema.UndefinedGeographicSrsId);

        private static object ValueOf(string? text) => text == null ? DBNull.Value : (object)text;

        /// <summary>The version of this library: the informational version without the source revision suffix.</summary>
        internal static string LibraryVersion { get; } = ReadLibraryVersion();

        private static string ReadLibraryVersion()
        {
            Assembly assembly = typeof(GeoPackageDataTables).Assembly;
            string? version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            if (string.IsNullOrEmpty(version))
                return assembly.GetName().Version?.ToString() ?? "unknown";

            int plus = version!.IndexOf('+');
            return plus < 0 ? version : version.Substring(0, plus);
        }

        /// <summary>The parameters of the seven fixed columns shared by the feature and attribute tables.</summary>
        private sealed class FeatureParameters
        {
            private readonly SqliteParameter _featureId;
            private readonly SqliteParameter _featureType;
            private readonly SqliteParameter _reportId;
            private readonly SqliteParameter _reportType;
            private readonly SqliteParameter _bulletin;
            private readonly SqliteParameter _issueTime;
            private readonly SqliteParameter _properties;

            public FeatureParameters(SqliteCommand command)
            {
                _featureId = command.Parameters.Add("$featureId", SqliteType.Text);
                _featureType = command.Parameters.Add("$featureType", SqliteType.Text);
                _reportId = command.Parameters.Add("$reportId", SqliteType.Text);
                _reportType = command.Parameters.Add("$reportType", SqliteType.Text);
                _bulletin = command.Parameters.Add("$bulletin", SqliteType.Text);
                _issueTime = command.Parameters.Add("$issueTime", SqliteType.Text);
                _properties = command.Parameters.Add("$properties", SqliteType.Text);
            }

            public void Set(GeoPackageFeature feature)
            {
                _featureId.Value = ValueOf(feature.FeatureId);
                _featureType.Value = ValueOf(feature.FeatureType);
                _reportId.Value = ValueOf(feature.ReportId);
                _reportType.Value = ValueOf(feature.ReportType);
                _bulletin.Value = ValueOf(feature.BulletinIdentifier);
                _issueTime.Value = feature.IssueTime.HasValue ? GeoPackageDateTime.Format(feature.IssueTime.Value) : (object)DBNull.Value;
                _properties.Value = GeoPackagePropertiesJson.Write(feature.Properties);
            }
        }

        /// <summary>Accumulates the plain minimum and maximum of the envelopes of the geometries written to one table.</summary>
        private struct ExtentBuilder
        {
            private bool _any;
            private double _minX, _maxX, _minY, _maxY;

            public void Include(GeoPackageEnvelope? envelope)
            {
                if (!envelope.HasValue)
                    return;

                GeoPackageEnvelope e = envelope.Value;
                if (!_any)
                {
                    _minX = e.MinX;
                    _maxX = e.MaxX;
                    _minY = e.MinY;
                    _maxY = e.MaxY;
                    _any = true;
                    return;
                }

                if (e.MinX < _minX) _minX = e.MinX;
                if (e.MaxX > _maxX) _maxX = e.MaxX;
                if (e.MinY < _minY) _minY = e.MinY;
                if (e.MaxY > _maxY) _maxY = e.MaxY;
            }

            public GeoPackageEnvelope? Result => _any ? new GeoPackageEnvelope(_minX, _maxX, _minY, _maxY) : (GeoPackageEnvelope?)null;
        }
    }
}
