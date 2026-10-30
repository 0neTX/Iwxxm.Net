using System.Linq;
using Iwxxm.Net.Reports;
using Iwxxm20252.Generated;

namespace Iwxxm.Net.Infrastructure.Mapping.Wafs
{
    /// <summary>Extracts <see cref="AirframeIcing"/> areas from WAFS <c>AIRFRAME_ICING</c> features.</summary>
    internal static class AirframeIcingExtractor
    {
        public static Result<AirframeIcing> FromWafsFeature(MeteorologicalFeatureType feature)
        {
            string context = $"MeteorologicalFeature[@gml:id='{feature.Id}']";
            var volumeRes = ElevatedVolumeReader.Read(feature.PhenomenonGeometry?.ElevatedVolume, context);
            if (volumeRes.IsFailure)
                return Result<AirframeIcing>.Failure(volumeRes.Errors);

            string? degreeHref = feature.PhenomenonProperties
                .Select(p => p.DegreeOfIcing?.Href)
                .FirstOrDefault(h => !string.IsNullOrWhiteSpace(h));

            return AirframeIcing.Create(
                id: feature.Id,
                rawDegreeReference: degreeHref,
                baseLimit: volumeRes.Value.Base,
                topLimit: volumeRes.Value.Top,
                area: volumeRes.Value.Polygon);
        }
    }
}
