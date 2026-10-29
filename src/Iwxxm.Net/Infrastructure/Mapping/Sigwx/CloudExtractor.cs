using System.Collections.Generic;
using System.Linq;
using Iwxxm.Net.Reports;
using Iwxxm20252.Generated;

namespace Iwxxm.Net.Infrastructure.Mapping.Sigwx
{
    /// <summary>
    /// Extracts <see cref="Cloud"/> phenomena (cumulonimbus and other significant cloud areas) from WAFS <c>CLOUD</c>
    /// features and compact <c>cloud</c> elements.
    /// </summary>
    internal static class CloudExtractor
    {
        public static Result<Cloud> FromWafsFeature(MeteorologicalFeatureType feature)
        {
            string context = $"MeteorologicalFeature[@gml:id='{feature.Id}']";
            var volumeRes = ElevatedVolumeReader.Read(feature.PhenomenonGeometry?.ElevatedVolume, context);
            if (volumeRes.IsFailure)
                return Result<Cloud>.Failure(volumeRes.Errors);

            string? cloudAmount = feature.PhenomenonProperties
                .Select(p => p.CloudDistribution?.Href)
                .FirstOrDefault(h => !string.IsNullOrWhiteSpace(h));

            string? cloudType = feature.PhenomenonProperties
                .Select(p => p.CloudType?.Href)
                .FirstOrDefault(h => !string.IsNullOrWhiteSpace(h));

            return Cloud.Create(
                id: feature.Id,
                cloudAmount: cloudAmount,
                cloudType: cloudType,
                baseLimit: volumeRes.Value.Base,
                topLimit: volumeRes.Value.Top,
                area: volumeRes.Value.Polygon);
        }

        public static Result<Cloud> FromCompact(CloudDistributionFeatureType dto)
        {
            string context = $"cloud[@gml:id='{dto.Id}']";
            PolygonType? polygonDto = dto.Geometry?.Polygon;

            int polyDim = polygonDto != null && polygonDto.SrsDimensionSpecified && polygonDto.SrsDimension > 0
                ? polygonDto.SrsDimension
                : 2;

            var coordsResult = SigwxGeometryReader.ReadRingCoordinates(polygonDto?.Exterior, $"{context}/geometry", polyDim);
            if (coordsResult.IsFailure)
                return Result<Cloud>.Failure(coordsResult.Errors);

            var polygonResult = GeoPolygon.Create(coordsResult.Value, context);
            if (polygonResult.IsFailure)
                return Result<Cloud>.Failure(polygonResult.Errors);

            if (dto.Top == null)
            {
                return Result<Cloud>.Failure(
                    "ACL-CLOUD-MISSING-TOP",
                    $"Element <iwxxm:top> is required at '{context}'.",
                    ErrorCategory.DomainMapping,
                    context);
            }

            var limitErrors = new List<ValidationError>();
            VerticalLimit? baseLimit = SigwxMeasureReader.ReadVertical(dto.Base, null, $"{context}/base", limitErrors);
            VerticalLimit? topLimit = SigwxMeasureReader.ReadVertical(dto.Top, null, $"{context}/top", limitErrors);
            if (limitErrors.Count > 0 || !topLimit.HasValue)
                return Result<Cloud>.Failure(limitErrors);

            return Cloud.Create(
                id: dto.Id,
                cloudAmount: dto.CloudAmount,
                cloudType: dto.CloudType,
                baseLimit: baseLimit,
                topLimit: topLimit.Value,
                area: polygonResult.Value);
        }
    }
}
