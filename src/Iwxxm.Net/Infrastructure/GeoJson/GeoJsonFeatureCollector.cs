using System;
using System.Collections.Generic;
using Iwxxm.Net.Infrastructure.GeoJson.Features;
using Iwxxm.Net.Reports;

namespace Iwxxm.Net.Infrastructure.GeoJson
{
    /// <summary>
    /// <see cref="IIwxxmReportVisitor{TResult}"/> that collects the GeoJSON features of a report, delegating each report
    /// family to its feature builder. Bulletins are expanded recursively, tagging every feature of a contained report
    /// with the <c>bulletinIdentifier</c>.
    /// </summary>
    internal sealed class GeoJsonFeatureCollector : IIwxxmReportVisitor<IReadOnlyList<GeoJsonFeature>>
    {
        private readonly string? _bulletinId;

        public GeoJsonFeatureCollector(string? bulletinId = null)
        {
            _bulletinId = bulletinId;
        }

        public IReadOnlyList<GeoJsonFeature> Visit(AerodromeWeatherReport report) =>
            Build(features => AerodromeFeatureBuilder.AddAerodromeObservationFeature(report, _bulletinId, features));

        public IReadOnlyList<GeoJsonFeature> Visit(TafReport report) =>
            Build(features => AerodromeFeatureBuilder.AddTafFeature(report, _bulletinId, features));

        public IReadOnlyList<GeoJsonFeature> Visit(SigmetReport report) =>
            Build(features => EnRouteHazardFeatureBuilder.AddSigmetFeatures(report, _bulletinId, features));

        public IReadOnlyList<GeoJsonFeature> Visit(AirmetReport report) =>
            Build(features => EnRouteHazardFeatureBuilder.AddAirmetFeatures(report, _bulletinId, features));

        public IReadOnlyList<GeoJsonFeature> Visit(VonaReport report) =>
            Build(features => VolcanicFeatureBuilder.AddVonaFeatures(report, _bulletinId, features));

        public IReadOnlyList<GeoJsonFeature> Visit(QvaciReport report) =>
            Build(features => VolcanicFeatureBuilder.AddQvaciFeatures(report, _bulletinId, features));

        public IReadOnlyList<GeoJsonFeature> Visit(VolcanicAshAdvisoryReport report) =>
            Build(features => VolcanicFeatureBuilder.AddVaaFeatures(report, _bulletinId, features));

        public IReadOnlyList<GeoJsonFeature> Visit(TropicalCycloneAdvisoryReport report) =>
            Build(features => TropicalCycloneAdvisoryFeatureBuilder.AddTcaFeatures(report, _bulletinId, features));

        public IReadOnlyList<GeoJsonFeature> Visit(SpaceWeatherAdvisoryReport report) =>
            Build(features => SpaceWeatherFeatureBuilder.AddSpaceWxFeatures(report, _bulletinId, features));

        public IReadOnlyList<GeoJsonFeature> Visit(SignificantWeatherReport report) =>
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
