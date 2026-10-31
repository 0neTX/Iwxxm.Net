using System.Collections.Generic;
using System.Linq;
using Iwxxm.Net.Infrastructure.Mapping.CodeLists;
using Iwxxm.Net.Reports;
using Iwxxm20252.Generated;

namespace Iwxxm.Net.Infrastructure.Mapping.Wafs
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
                distribution: WmoCodeRegistry.CloudDistributions.Translate(cloudAmount),
                cloudType: WmoCodeRegistry.CloudTypes.Translate(cloudType),
                baseLimit: volumeRes.Value.Base,
                topLimit: volumeRes.Value.Top,
                area: volumeRes.Value.Areas[0],
                additionalAreas: volumeRes.Value.Areas.Skip(1));
        }

        public static Result<Cloud> FromCompact(CloudDistributionFeatureType dto)
        {
            string context = $"cloud[@gml:id='{dto.Id}']";
            PolygonType? polygonDto = dto.Geometry?.Polygon;

            SigwxGeometryReader.SrsAttributes polyAttributes = polygonDto == null
                ? SigwxGeometryReader.SrsAttributes.None
                : SigwxGeometryReader.SrsAttributes.From(polygonDto.SrsName, polygonDto.AxisLabels, polygonDto.SrsDimensionSpecified, polygonDto.SrsDimension);

            var surfaceResult = SigwxGeometryReader.ReadSurface(polygonDto?.Exterior, polygonDto?.Interiors ?? new List<AbstractRingPropertyType>(), $"{context}/geometry", polyAttributes);
            if (surfaceResult.IsFailure)
                return Result<Cloud>.Failure(surfaceResult.Errors);

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
                distribution: WmoCodeRegistry.CloudDistributions.Translate(dto.CloudAmount),
                cloudType: WmoCodeRegistry.CloudTypes.Translate(dto.CloudType),
                baseLimit: baseLimit,
                topLimit: topLimit.Value,
                area: surfaceResult.Value);
        }
    }
}
