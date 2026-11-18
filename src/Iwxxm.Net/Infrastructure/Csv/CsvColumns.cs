using System;
using System.Collections.Generic;
using System.Globalization;
using Iwxxm.Net.Infrastructure.GeoJson;
using Iwxxm.Net.Infrastructure.GeoJson.Writing;
using Iwxxm.Net.Reports;

namespace Iwxxm.Net.Infrastructure.Csv
{
    /// <summary>
    /// Canonical unified column schema and row container for the IWXXM 2025-2 CSV export.
    /// </summary>
    internal static class CsvColumns
    {
        public const int BulletinId = 0;
        public const int ReportId = 1;
        public const int ReportType = 2;
        public const int FeatureType = 3;
        public const int FeatureId = 4;
        public const int ReportStatus = 5;
        public const int PermissibleUsage = 6;
        public const int PermissibleUsageReason = 7;
        public const int IssueTimeUtc = 8;
        public const int ValidFromUtc = 9;
        public const int ValidUntilUtc = 10;
        public const int StationOrCentre = 11;
        public const int Lat = 12;
        public const int Lon = 13;
        public const int GeometryWkt = 14;
        public const int LowerLimit = 15;
        public const int UpperLimit = 16;
        public const int WindDirectionDeg = 17;
        public const int WindSpeed = 18;
        public const int WindSpeedUnit = 19;
        public const int AirTemperatureC = 20;
        public const int QnhHpa = 21;
        public const int VisibilityM = 22;
        public const int Phenomenon = 23;
        public const int Intensity = 24;
        public const int Details = 25;

        public const int Count = 26;

        public static readonly string[] Headers =
        {
            "BulletinId",
            "ReportId",
            "ReportType",
            "FeatureType",
            "FeatureId",
            "ReportStatus",
            "PermissibleUsage",
            "PermissibleUsageReason",
            "IssueTimeUtc",
            "ValidFromUtc",
            "ValidUntilUtc",
            "StationOrCentre",
            "Lat",
            "Lon",
            "GeometryWkt",
            "LowerLimit",
            "UpperLimit",
            "WindDirectionDeg",
            "WindSpeed",
            "WindSpeedUnit",
            "AirTemperatureC",
            "QnhHpa",
            "VisibilityM",
            "Phenomenon",
            "Intensity",
            "Details"
        };
    }

    internal readonly struct CsvCell
    {
        public CsvCell(string? value, bool isNumeric)
        {
            Value = value;
            IsNumeric = isNumeric;
        }

        public string? Value { get; }
        public bool IsNumeric { get; }
    }

    internal sealed class CsvRow
    {
        private readonly CsvCell[] _cells = new CsvCell[CsvColumns.Count];

        public CsvCell this[int index] => _cells[index];

        public void SetText(int index, string? value) => _cells[index] = new CsvCell(value, isNumeric: false);

        public void SetNumeric(int index, double? value)
        {
            if (!value.HasValue || double.IsNaN(value.Value) || double.IsInfinity(value.Value))
            {
                _cells[index] = default;
                return;
            }

            _cells[index] = new CsvCell(value.Value.ToString("G17", CultureInfo.InvariantCulture), isNumeric: true);
        }

        public void SetInstant(int index, DateTimeOffset? instant)
        {
            string? text = GeoJsonProperties.FormatInstant(instant);
            if (text != null)
                SetText(index, text);
        }

        public static CsvRow CreateBase(
            IwxxmReport report,
            string featureType,
            string? featureId,
            string? bulletinId,
            string? stationOrCentre = null,
            DateTimeOffset? validFrom = null,
            DateTimeOffset? validUntil = null)
        {
            var row = new CsvRow();
            row.SetText(CsvColumns.BulletinId, bulletinId);
            row.SetText(CsvColumns.ReportId, report.Id);
            row.SetText(CsvColumns.ReportType, report.ReportType.ToString());
            row.SetText(CsvColumns.FeatureType, featureType);
            row.SetText(CsvColumns.FeatureId, featureId);
            row.SetText(CsvColumns.ReportStatus, report.ReportStatus.ToString());
            row.SetText(CsvColumns.PermissibleUsage, report.PermissibleUsage.ToString());
            row.SetText(CsvColumns.PermissibleUsageReason, report.PermissibleUsageReason?.ToString());
            row.SetInstant(CsvColumns.IssueTimeUtc, report.IssueTimeUtc);
            row.SetInstant(CsvColumns.ValidFromUtc, validFrom);
            row.SetInstant(CsvColumns.ValidUntilUtc, validUntil);
            row.SetText(CsvColumns.StationOrCentre, stationOrCentre);
            return row;
        }

        public void SetPoint(GeoCoordinate point, bool includeWkt)
        {
            SetNumeric(CsvColumns.Lat, point.Lat);
            SetNumeric(CsvColumns.Lon, point.Lon);
            if (includeWkt)
                SetText(CsvColumns.GeometryWkt, WktGeometryFormatter.Format(GeoJsonGeometryFactory.Point(point)));
        }

        public void SetLineString(IReadOnlyList<GeoCoordinate> coordinates, bool includeWkt)
        {
            if (includeWkt && coordinates.Count > 0)
                SetText(CsvColumns.GeometryWkt, WktGeometryFormatter.Format(GeoJsonGeometryFactory.LineString(coordinates)));
        }

        public void SetSurface(GeoSurface? surface, bool includeWkt)
        {
            if (surface == null)
                return;

            if (surface is GeoCircle circle)
            {
                SetNumeric(CsvColumns.Lat, circle.Centre.Lat);
                SetNumeric(CsvColumns.Lon, circle.Centre.Lon);
            }

            if (includeWkt)
                SetText(CsvColumns.GeometryWkt, WktGeometryFormatter.Format(GeoJsonGeometryFactory.Surface(surface)));
        }

        public void SetSurfaces(IReadOnlyList<GeoSurface> surfaces, bool includeWkt)
        {
            if (surfaces.Count == 0)
                return;

            if (surfaces.Count == 1)
            {
                SetSurface(surfaces[0], includeWkt);
                return;
            }

            if (includeWkt)
                SetText(CsvColumns.GeometryWkt, WktGeometryFormatter.FormatSurfaces(surfaces));
        }

        public void SetVerticalLimits(VerticalLimit? lower, VerticalLimit? upper)
        {
            if (lower != null)
                SetText(CsvColumns.LowerLimit, lower.ToString());
            if (upper != null)
                SetText(CsvColumns.UpperLimit, upper.ToString());
        }

        public void SetWind(double? directionDegrees, Speed? speed)
        {
            SetNumeric(CsvColumns.WindDirectionDeg, directionDegrees);
            if (speed.HasValue)
            {
                SetNumeric(CsvColumns.WindSpeed, speed.Value.Amount);
                SetText(CsvColumns.WindSpeedUnit, speed.Value.UnitOfMeasure);
            }
        }
    }
}
