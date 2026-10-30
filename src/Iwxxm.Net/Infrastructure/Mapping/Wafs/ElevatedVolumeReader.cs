using System.Collections.Generic;
using Iwxxm.Net.Reports;
using Iwxxm20252.Generated;

namespace Iwxxm.Net.Infrastructure.Mapping.Wafs
{
    /// <summary>
    /// Reads an <c>iwxxm:ElevatedVolume</c> (horizontal polygon or circle plus lower/upper elevation), the geometry shared by the
    /// WAFS cloud, turbulence and airframe icing features.
    /// </summary>
    internal static class ElevatedVolumeReader
    {
        public static Result<(IReadOnlyList<GeoSurface> Areas, VerticalLimit? Base, VerticalLimit Top)> Read(
            ElevatedVolumeType? volume,
            string context)
        {
            if (volume == null)
            {
                return Result<(IReadOnlyList<GeoSurface>, VerticalLimit?, VerticalLimit)>.Failure(
                    "ACL-WAFS-MISSING-VOLUME",
                    $"Feature at '{context}' is missing an <iwxxm:ElevatedVolume> geometry.",
                    ErrorCategory.DomainMapping,
                    context);
            }

            var surfaceResult = SigwxGeometryReader.ReadElevatedVolumeSurfaces(volume, context);
            if (surfaceResult.IsFailure)
                return Result<(IReadOnlyList<GeoSurface>, VerticalLimit?, VerticalLimit)>.Failure(surfaceResult.Errors);

            if (volume.UpperElevation == null)
            {
                return Result<(IReadOnlyList<GeoSurface>, VerticalLimit?, VerticalLimit)>.Failure(
                    "ACL-WAFS-MISSING-UPPER-ELEV",
                    $"ElevatedVolume at '{context}' is missing the mandatory <iwxxm:upperElevation>.",
                    ErrorCategory.DomainMapping,
                    context);
            }

            var errors = new List<ValidationError>();
            VerticalLimit? baseLimit = SigwxMeasureReader.ReadVertical(volume.LowerElevation, volume.LowerVerticalReference, $"{context}/lowerElevation", errors);
            VerticalLimit? topLimit = SigwxMeasureReader.ReadVertical(volume.UpperElevation, volume.UpperVerticalReference, $"{context}/upperElevation", errors);
            if (errors.Count > 0 || !topLimit.HasValue)
                return Result<(IReadOnlyList<GeoSurface>, VerticalLimit?, VerticalLimit)>.Failure(errors);

            return Result<(IReadOnlyList<GeoSurface>, VerticalLimit?, VerticalLimit)>.Success((surfaceResult.Value, baseLimit, topLimit.Value));
        }
    }
}
