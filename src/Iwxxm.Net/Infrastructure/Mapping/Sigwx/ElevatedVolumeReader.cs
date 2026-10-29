using System.Collections.Generic;
using Iwxxm.Net.Reports;
using Iwxxm20252.Generated;

namespace Iwxxm.Net.Infrastructure.Mapping.Sigwx
{
    /// <summary>
    /// Reads an <c>iwxxm:ElevatedVolume</c> (horizontal polygon plus lower/upper elevation), the geometry shared by the
    /// WAFS cloud, turbulence and airframe icing features.
    /// </summary>
    internal static class ElevatedVolumeReader
    {
        public static Result<(GeoPolygon Polygon, VerticalLimit? Base, VerticalLimit Top)> Read(
            ElevatedVolumeType? volume,
            string context)
        {
            if (volume == null)
            {
                return Result<(GeoPolygon, VerticalLimit?, VerticalLimit)>.Failure(
                    "ACL-WAFS-MISSING-VOLUME",
                    $"Feature at '{context}' is missing an <iwxxm:ElevatedVolume> geometry.",
                    ErrorCategory.DomainMapping,
                    context);
            }

            var ringCoordsResult = SigwxGeometryReader.ReadElevatedVolumeRingCoordinates(volume, context);
            if (ringCoordsResult.IsFailure)
                return Result<(GeoPolygon, VerticalLimit?, VerticalLimit)>.Failure(ringCoordsResult.Errors);

            var polyResult = GeoPolygon.Create(ringCoordsResult.Value, context);
            if (polyResult.IsFailure)
                return Result<(GeoPolygon, VerticalLimit?, VerticalLimit)>.Failure(polyResult.Errors);

            if (volume.UpperElevation == null)
            {
                return Result<(GeoPolygon, VerticalLimit?, VerticalLimit)>.Failure(
                    "ACL-WAFS-MISSING-UPPER-ELEV",
                    $"ElevatedVolume at '{context}' is missing the mandatory <iwxxm:upperElevation>.",
                    ErrorCategory.DomainMapping,
                    context);
            }

            var errors = new List<ValidationError>();
            VerticalLimit? baseLimit = SigwxMeasureReader.ReadVertical(volume.LowerElevation, volume.LowerVerticalReference, $"{context}/lowerElevation", errors);
            VerticalLimit? topLimit = SigwxMeasureReader.ReadVertical(volume.UpperElevation, volume.UpperVerticalReference, $"{context}/upperElevation", errors);
            if (errors.Count > 0 || !topLimit.HasValue)
                return Result<(GeoPolygon, VerticalLimit?, VerticalLimit)>.Failure(errors);

            return Result<(GeoPolygon, VerticalLimit?, VerticalLimit)>.Success((polyResult.Value, baseLimit, topLimit.Value));
        }
    }
}
