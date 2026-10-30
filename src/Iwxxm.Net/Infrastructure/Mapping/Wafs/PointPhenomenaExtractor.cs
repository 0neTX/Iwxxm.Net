using System;
using System.Linq;
using Iwxxm.Net.Reports;
using Iwxxm20252.Generated;

namespace Iwxxm.Net.Infrastructure.Mapping.Wafs
{
    /// <summary>
    /// Extracts the point phenomena of a SIGWX forecast: volcanoes (WAFS and compact), tropical cyclones, sandstorms
    /// and radiation incidents.
    /// </summary>
    internal static class PointPhenomenaExtractor
    {
        public static Result<Volcano> VolcanoFromWafsFeature(MeteorologicalFeatureType feature)
        {
            var ptRes = ReadFeaturePoint(feature);
            if (ptRes.IsFailure)
                return Result<Volcano>.Failure(ptRes.Errors);

            WAFSVolcanoType? volcanoProp = feature.PhenomenonProperties
                .Select(p => p.Volcano)
                .FirstOrDefault(v => v != null);

            // iwxxm:name is nillable: a nil or absent name stays null (never invented).
            string? name = string.IsNullOrWhiteSpace(volcanoProp?.Name?.NilReason) ? volcanoProp?.Name?.Value : null;
            string? wmoNumber = volcanoProp?.IAVCEINumber?.Value ?? volcanoProp?.WmoNumber;

            return Volcano.Create(
                id: feature.Id,
                name: name,
                wmoNumber: wmoNumber,
                position: ptRes.Value,
                eruptionTimeUtc: null);
        }

        public static Result<Volcano> VolcanoFromCompact(WAFSVolcanoType dto)
        {
            string context = $"volcano[@gml:id='{dto.Id}']";
            var posResult = SigwxGeometryReader.ParsePoint(dto.Location?.Point, $"{context}/location");
            if (posResult.IsFailure)
                return Result<Volcano>.Failure(posResult.Errors);

            DateTimeOffset? eruptionUtc = null;
            if (dto.EruptionTimeSpecified)
            {
                eruptionUtc = SigwxTimeReader.NormalizeToUtc(dto.EruptionTime);
            }

            return Volcano.Create(
                id: dto.Id,
                name: string.IsNullOrWhiteSpace(dto.Name?.NilReason) ? dto.Name?.Value : null,
                wmoNumber: dto.WmoNumber ?? dto.IAVCEINumber?.Value,
                position: posResult.Value,
                eruptionTimeUtc: eruptionUtc);
        }

        public static Result<TropicalCyclone> TropicalCycloneFromWafsFeature(MeteorologicalFeatureType feature)
        {
            var ptRes = ReadFeaturePoint(feature);
            if (ptRes.IsFailure)
                return Result<TropicalCyclone>.Failure(ptRes.Errors);

            TropicalCycloneType? tcProp = feature.PhenomenonProperties
                .Select(p => p.TropicalCyclone)
                .FirstOrDefault(tc => tc != null);

            string name = tcProp?.Name?.Value ?? string.Empty;
            return TropicalCyclone.Create(feature.Id, name, ptRes.Value);
        }

        public static Result<Sandstorm> SandstormFromWafsFeature(MeteorologicalFeatureType feature)
        {
            var ptRes = ReadFeaturePoint(feature);
            if (ptRes.IsFailure)
                return Result<Sandstorm>.Failure(ptRes.Errors);

            return Sandstorm.Create(feature.Id, ptRes.Value);
        }

        public static Result<RadiationIncident> RadiationIncidentFromWafsFeature(MeteorologicalFeatureType feature)
        {
            var ptRes = ReadFeaturePoint(feature);
            if (ptRes.IsFailure)
                return Result<RadiationIncident>.Failure(ptRes.Errors);

            RadiationIncidentType? radProp = feature.PhenomenonProperties
                .Select(p => p.RadiationIncident)
                .FirstOrDefault(r => r != null);

            string? siteName = radProp?.SiteName != null && string.IsNullOrWhiteSpace(radProp.SiteName.NilReason)
                ? radProp.SiteName.Value
                : null;

            DateTimeOffset? incidentTime = radProp?.IncidentTime?.TimeInstant != null
                ? SigwxTimeReader.NormalizeToUtc(radProp.IncidentTime.TimeInstant.TimePosition)
                : null;

            return RadiationIncident.Create(feature.Id, siteName, ptRes.Value, incidentTime);
        }

        private static Result<GeoCoordinate> ReadFeaturePoint(MeteorologicalFeatureType feature) =>
            SigwxGeometryReader.ParsePoint(
                feature.PhenomenonGeometry?.Point,
                $"MeteorologicalFeature[@gml:id='{feature.Id}']/phenomenonGeometry");
    }
}
