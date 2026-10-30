using System.Linq;
using Iwxxm.Net.Reports;
using Iwxxm20252.Generated;

namespace Iwxxm.Net.Infrastructure.Mapping.Wafs
{
    /// <summary>Extracts <see cref="Turbulence"/> areas from WAFS <c>TURBULENCE</c> features.</summary>
    internal static class TurbulenceExtractor
    {
        public static Result<Turbulence> FromWafsFeature(MeteorologicalFeatureType feature)
        {
            string context = $"MeteorologicalFeature[@gml:id='{feature.Id}']";
            var volumeRes = ElevatedVolumeReader.Read(feature.PhenomenonGeometry?.ElevatedVolume, context);
            if (volumeRes.IsFailure)
                return Result<Turbulence>.Failure(volumeRes.Errors);

            string? degreeHref = feature.PhenomenonProperties
                .Select(p => p.DegreeOfTurbulence?.Href)
                .FirstOrDefault(h => !string.IsNullOrWhiteSpace(h));

            return Turbulence.Create(
                id: feature.Id,
                rawDegreeReference: degreeHref,
                baseLimit: volumeRes.Value.Base,
                topLimit: volumeRes.Value.Top,
                area: volumeRes.Value.Areas[0],
                additionalAreas: volumeRes.Value.Areas.Skip(1));
        }
    }
}
