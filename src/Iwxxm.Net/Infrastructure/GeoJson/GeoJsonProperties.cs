using System;
using System.Collections.Generic;
using System.Globalization;
using Iwxxm.Net.Reports;

namespace Iwxxm.Net.Infrastructure.GeoJson
{
    /// <summary>
    /// Builds the <c>properties</c> of GeoJSON features: the base report properties and the as-reported encoding of
    /// measures (value plus unit, no conversion).
    /// </summary>
    internal static class GeoJsonProperties
    {
        public static List<(string Key, object? Value)> CreateBase(
            IIwxxmReport report,
            string featureType,
            string? bulletinId)
        {
            var list = new List<(string Key, object? Value)>(12)
            {
                ("featureType", featureType),
                ("reportId", report.Id),
                ("reportType", report.ReportType.ToString()),
                ("issueTimeUtc", FormatInstant(report.IssueTimeUtc))
            };

            if (!string.IsNullOrWhiteSpace(bulletinId))
            {
                list.Add(("bulletinIdentifier", bulletinId));
            }

            return list;
        }

        /// <summary>Formats an instant as ISO 8601 round-trip text (<c>O</c>, invariant culture).</summary>
        public static string FormatInstant(DateTimeOffset instant) => instant.ToString("O", CultureInfo.InvariantCulture);

        /// <summary>Formats an optional instant as ISO 8601 round-trip text, or <c>null</c>.</summary>
        public static string? FormatInstant(DateTimeOffset? instant) => instant?.ToString("O", CultureInfo.InvariantCulture);

        /// <summary>
        /// Emits a speed as reported: <c>{name}</c> holds the numeric value and <c>{name}Uom</c> the UCUM unit
        /// (<c>m/s</c>, <c>[kn_i]</c> or <c>km/h</c>). No unit conversion is applied.
        /// </summary>
        public static void AddSpeed(List<(string Key, object? Value)> props, string name, Speed? speed)
        {
            props.Add((name, speed?.Value));
            props.Add((name + "Uom", speed?.UnitOfMeasure));
        }

        /// <summary>
        /// Emits a length as reported: <c>{name}</c> holds the numeric value and <c>{name}Uom</c> the UCUM unit
        /// (<c>m</c>, <c>[ft_i]</c>, ...). No unit conversion is applied.
        /// </summary>
        public static void AddLength(List<(string Key, object? Value)> props, string name, Length? length)
        {
            props.Add((name, length?.Value));
            props.Add((name + "Uom", length?.UnitOfMeasure));
        }

        /// <summary>
        /// Emits a vertical limit as reported: <c>{name}</c> holds the numeric value, <c>{name}Uom</c> the unit
        /// (<c>FL</c>, <c>FT</c>, <c>M</c>, <c>SM</c>, <c>km</c>), <c>{name}Reference</c> the vertical reference
        /// (<c>SFC</c>, <c>MSL</c>, <c>W84</c>, <c>STD</c>, <c>OTHER</c>), <c>{name}SpecialValue</c> <c>GND</c>/<c>UNL</c>/...,
        /// <c>{name}NilReason</c> the nil reason and <c>{name}FlightLevel</c> the derived flight level (STD only).
        /// Absent components are omitted; no conversion is applied.
        /// </summary>
        public static void AddVerticalLimit(List<(string Key, object? Value)> props, string name, VerticalLimit? limit)
        {
            if (!limit.HasValue)
                return;

            VerticalLimit value = limit.Value;
            props.Add((name, value.Value));
            props.Add((name + "Uom", value.UnitOfMeasure));
            props.Add((name + "Reference", value.ReferenceCode));
            props.Add((name + "SpecialValue", value.SpecialValueCode));
            props.Add((name + "NilReason", value.IsNil ? (value.NilReason ?? "nil") : null));
            props.Add((name + "FlightLevel", value.AsFlightLevel()?.ToString()));
        }
    }
}
