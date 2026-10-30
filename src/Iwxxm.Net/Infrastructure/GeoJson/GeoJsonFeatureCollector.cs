using System;
using System.Collections.Generic;
using Iwxxm.Net.Infrastructure.GeoJson.Features;
using Iwxxm.Net.Reports;

namespace Iwxxm.Net.Infrastructure.GeoJson
{
    /// <summary>
    /// <see cref="IIwxxmDocumentVisitor{TResult}"/> that collects the GeoJSON features of a report, delegating each report
    /// family to its feature builder. Bulletins are expanded recursively, tagging every feature of a contained report
    /// with the <c>bulletinIdentifier</c>.
    /// </summary>
    internal sealed class GeoJsonFeatureCollector : IIwxxmDocumentVisitor<IReadOnlyList<GeoJsonFeature>>
    {
        private readonly string? _bulletinId;

        public GeoJsonFeatureCollector(string? bulletinId = null)
        {
            _bulletinId = bulletinId;
        }

        public IReadOnlyList<GeoJsonFeature> Visit(Metar metar) =>
            Build(features => AerodromeFeatureBuilder.AddAerodromeObservationFeature(metar, _bulletinId, features));

        public IReadOnlyList<GeoJsonFeature> Visit(Speci speci) =>
            Build(features => AerodromeFeatureBuilder.AddAerodromeObservationFeature(speci, _bulletinId, features));

        public IReadOnlyList<GeoJsonFeature> Visit(Taf report) =>
            Build(features => AerodromeFeatureBuilder.AddTafFeature(report, _bulletinId, features));

        public IReadOnlyList<GeoJsonFeature> Visit(Sigmet report) =>
            Build(features => EnRouteHazardFeatureBuilder.AddSigmetFeatures(report, _bulletinId, features));

        public IReadOnlyList<GeoJsonFeature> Visit(Airmet report) =>
            Build(features => EnRouteHazardFeatureBuilder.AddAirmetFeatures(report, _bulletinId, features));

        public IReadOnlyList<GeoJsonFeature> Visit(Vona report) =>
            Build(features => VolcanicFeatureBuilder.AddVonaFeatures(report, _bulletinId, features));

        public IReadOnlyList<GeoJsonFeature> Visit(Qvaci report) =>
            Build(features => VolcanicFeatureBuilder.AddQvaciFeatures(report, _bulletinId, features));

        public IReadOnlyList<GeoJsonFeature> Visit(Vaa report) =>
            Build(features => VolcanicFeatureBuilder.AddVaaFeatures(report, _bulletinId, features));

        public IReadOnlyList<GeoJsonFeature> Visit(Tca report) =>
            Build(features => TcaFeatureBuilder.AddTcaFeatures(report, _bulletinId, features));

        public IReadOnlyList<GeoJsonFeature> Visit(SpaceWx report) =>
            Build(features => SpaceWxFeatureBuilder.AddSpaceWxFeatures(report, _bulletinId, features));

        public IReadOnlyList<GeoJsonFeature> Visit(Sigwx report) =>
            Build(features => SigwxFeatureBuilder.AddSigwxFeatures(report, _bulletinId, features));

        public IReadOnlyList<GeoJsonFeature> Visit(MeteorologicalBulletin bulletin)
        {
            var childCollector = new GeoJsonFeatureCollector(bulletin.BulletinIdentifier);
            var features = new List<GeoJsonFeature>();
            foreach (IwxxmReport child in bulletin.Reports)
            {
                features.AddRange(child.Accept(childCollector));
            }

            return features;
        }

        private static IReadOnlyList<GeoJsonFeature> Build(Action<List<GeoJsonFeature>> addFeatures)
        {
            var features = new List<GeoJsonFeature>();
            addFeatures(features);
            return features;
        }
    }
}
