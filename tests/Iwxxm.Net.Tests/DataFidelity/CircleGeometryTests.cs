using System;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Xml.Linq;
using Iwxxm.Net.Reports;
using Xunit;
using static Iwxxm.Net.Tests.DataFidelity.SampleDocuments;

namespace Iwxxm.Net.Tests.DataFidelity
{
    /// <summary>
    /// AUD-H1: <c>gml:CircleByCenterPoint</c> is modelled as a <see cref="GeoCircle"/> (centre plus radius with its unit)
    /// inside the closed <see cref="GeoSurface"/> union, and exported to GeoJSON as a densified polygon plus the
    /// <c>center</c>, <c>radius</c>, <c>radiusUom</c> properties.
    /// </summary>
    public sealed class CircleGeometryTests
    {
        private const double EarthRadiusMeters = 6371008.8;

        [Fact]
        public void GeoSurface_IsAClosedUnion_OfPolygonAndCircle()
        {
            Type surface = typeof(GeoSurface);
            Assert.True(surface.IsAbstract && surface.IsPublic);
            Assert.All(
                surface.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic),
                c => Assert.False(c.IsPublic || c.IsFamily || c.IsFamilyOrAssembly));
            var cases = surface.Assembly.GetTypes().Where(t => t.BaseType == surface).OrderBy(t => t.Name).ToArray();
            Assert.Equal(new[] { typeof(GeoCircle), typeof(GeoPolygon) }, cases);
            Assert.All(cases, t => Assert.True(t.IsSealed));
        }

        private static GeoSurface Polygon() =>
            GeoPolygon.Create(new[] { new GeoCoordinate(0, 0), new GeoCoordinate(0, 10), new GeoCoordinate(10, 10), new GeoCoordinate(0, 0) }).Value;

        private static GeoSurface Circle() =>
            GeoCircle.Create(new GeoCoordinate(5, 5), new Length(100, LengthUnit.Kilometers)).Value;

        [Fact]
        public void GeoSurface_MatchWithActions_RunsExactlyTheHandlerOfTheConcreteCase()
        {
            var calls = new System.Collections.Generic.List<string>();

            Polygon().Match(p => calls.Add("polygon " + p.ExteriorRing.Count), c => calls.Add("circle"));
            Circle().Match(p => calls.Add("polygon"), c => calls.Add("circle " + c.Radius.Amount));

            Assert.Equal(new[] { "polygon 4", "circle 100" }, calls);
        }

        [Fact]
        public void GeoSurface_MatchWithActions_RejectsNullHandlers_BeforeRunningAnything()
        {
            bool ran = false;

            Assert.Throws<ArgumentNullException>(() => Polygon().Match((Action<GeoPolygon>)null!, c => { ran = true; }));
            Assert.Throws<ArgumentNullException>(() => Polygon().Match(p => { ran = true; }, (Action<GeoCircle>)null!));
            Assert.False(ran);
        }

        [Fact]
        public void GeoSurface_MatchWithActions_AndTheFunctionOverload_AgreeWithPatternMatching()
        {
            foreach (GeoSurface surface in new[] { Polygon(), Circle() })
            {
                string byPattern = surface switch
                {
                    GeoPolygon _ => "polygon",
                    GeoCircle _ => "circle",
                    _ => throw new InvalidOperationException("The union is closed.")
                };
                string byFunction = surface.Match(_ => "polygon", _ => "circle");
                string byAction = string.Empty;
                surface.Match(_ => { byAction = "polygon"; }, _ => { byAction = "circle"; });

                Assert.Equal(byPattern, byFunction);
                Assert.Equal(byPattern, byAction);
            }
        }

        [Fact]
        public void TropicalCycloneSigmet_WithinRadiusOfCentre_IsAGeoCircle()
        {
            var result = IwxxmParser.ParseFileAs<Sigmet>(PathOf("sigmet-A6-2-TC.xml"));

            Assert.True(result.IsSuccess, string.Join(" | ", result.Errors));
            EnRoutePhenomenonVolume volume = Assert.Single(result.Value.EvolvingConditions);
            GeoCircle circle = Assert.IsType<GeoCircle>(volume.Area);
            Assert.Equal(new GeoCoordinate(27.1, -73.1), circle.Centre);
            Assert.Equal(new Length(250.0, LengthUnit.NauticalMiles), circle.Radius);
            Assert.Equal(500.0, volume.UpperLimit!.Value.Amount);
            Assert.Equal("circle", volume.Area!.Match(_ => "polygon", _ => "circle"));
        }

        [Fact]
        public void TropicalCycloneAdvisory_CumulonimbusLocation_IsAGeoCircle()
        {
            var result = IwxxmParser.ParseFileAs<Tca>(PathOf("tc-advisory-A2-2.xml"));

            Assert.True(result.IsSuccess, string.Join(" | ", result.Errors));
            EnRoutePhenomenonVolume cb = Assert.Single(result.Value.CumulonimbusCloudLocations);
            GeoCircle circle = Assert.IsType<GeoCircle>(cb.Area);
            Assert.Equal(new GeoCoordinate(27.1, -73.1), circle.Centre);
            Assert.Equal(new Length(250.0, LengthUnit.NauticalMiles), circle.Radius);
            Assert.Equal(VerticalReference.Std, cb.UpperLimit!.Value.Reference);
        }

        [Fact]
        public void SpaceWeatherAdvisory_DaysideCircle_IsKept_AndNightsidePlaceholderCentreHasNoExtent()
        {
            var result = IwxxmParser.ParseFileAs<SpaceWx>(PathOf("spacewx-A7-3.xml"));

            Assert.True(result.IsSuccess, string.Join(" | ", result.Errors));
            SpaceWeatherAnalysisRegion severe = result.Value.Analyses[0].Regions.Single(a => a.Intensity == "SEV");
            Assert.Contains(severe.LocationIndicators, l => l.Code == SpaceWeatherLocation.Dayside);
            GeoCircle dayside = Assert.Single(severe.Areas.OfType<GeoCircle>());
            Assert.Equal(new GeoCoordinate(-16.71, 70.94), dayside.Centre);
            Assert.Equal(new Length(10100.0, LengthUnit.Kilometers), dayside.Radius);
            Assert.Equal(4, severe.Areas.OfType<GeoPolygon>().Count());

            SpaceWeatherAnalysisRegion moderate = result.Value.Analyses[0].Regions.Single(a => a.Intensity == "MOD");
            Assert.Contains(moderate.LocationIndicators, l => l.Code == SpaceWeatherLocation.Nightside);
            Assert.Empty(moderate.Areas);
        }

        [Fact]
        public void GeoJson_Circle_IsADensifiedCounterclockwisePolygonWithRadiusProperties()
        {
            var result = IwxxmParser.ParseFileToGeoJson(PathOf("sigmet-A6-2-TC.xml"));

            Assert.True(result.IsSuccess, string.Join(" | ", result.Errors));
            using JsonDocument json = JsonDocument.Parse(result.Value);
            JsonElement feature = json.RootElement.GetProperty("features").EnumerateArray()
                .Single(f => f.GetProperty("properties").GetProperty("featureType").GetString() == "SIGMET_EVOLVING_VOLUME");
            JsonElement geometry = feature.GetProperty("geometry");
            Assert.Equal("Polygon", geometry.GetProperty("type").GetString());
            double[][] ring = Ring(geometry.GetProperty("coordinates")[0]);
            Assert.Equal(65, ring.Length);
            Assert.Equal(ring[0], ring[64]);
            Assert.True(SignedArea(ring) > 0, "The exterior ring must be counterclockwise (RFC 7946 §3.1.6).");
            foreach (double[] position in ring)
            {
                Assert.InRange(Haversine(27.1, -73.1, position[1], position[0]), 250 * 1852.0 - 1.0, 250 * 1852.0 + 1.0);
            }

            JsonElement properties = feature.GetProperty("properties");
            Assert.Equal(250.0, properties.GetProperty("radius").GetDouble());
            Assert.Equal("[nmi_i]", properties.GetProperty("radiusUom").GetString());
            Assert.Equal(new[] { -73.1, 27.1 }, properties.GetProperty("center").EnumerateArray().Select(e => e.GetDouble()).ToArray());
            Assert.Equal(64, properties.GetProperty("circleSegments").GetInt32());
        }

        [Fact]
        public void GeoJson_DaysideCircleEnclosingTheSouthPole_IsAValidLongitudeLatitudeRing()
        {
            var result = IwxxmParser.ParseFileToGeoJson(PathOf("spacewx-A7-3.xml"));

            Assert.True(result.IsSuccess, string.Join(" | ", result.Errors));
            using JsonDocument json = JsonDocument.Parse(result.Value);
            JsonElement feature = json.RootElement.GetProperty("features").EnumerateArray()
                .Single(f => f.GetProperty("properties").TryGetProperty("radius", out _));
            double[][] ring = Ring(feature.GetProperty("geometry").GetProperty("coordinates")[0]);
            Assert.Equal(ring[0], ring[ring.Length - 1]);
            Assert.All(ring, p => Assert.InRange(p[0], -180.0, 180.0));
            Assert.All(ring, p => Assert.InRange(p[1], -90.0, 90.0));
            Assert.Contains(ring, p => p[1] == -90.0);
            Assert.True(SignedArea(ring) > 0);
            Assert.Equal(10100.0, feature.GetProperty("properties").GetProperty("radius").GetDouble());
            Assert.Equal("km", feature.GetProperty("properties").GetProperty("radiusUom").GetString());
        }

        [Fact]
        public void Sigwx_CircleShapedTurbulence_IsMappedAndDoesNotFailTheChart()
        {
            XDocument doc = Load("WAFS-Example.xml");
            XElement segments = doc.Descendants(IwxxmNs + "ElevatedVolume")
                .First(v => (string?)v.Attribute(GmlNs + "id") == "uuid.138a9477-e1fd-43e2-8060-0eb6a093d0eb")
                .Descendants(GmlNs + "segments").Single();
            segments.ReplaceNodes(new XElement(GmlNs + "CircleByCenterPoint", new XAttribute("numArc", "1"),
                new XElement(GmlNs + "pos", "50.0 -60.0"),
                new XElement(GmlNs + "radius", new XAttribute("uom", "km"), "300")));

            var result = IwxxmSigwxParser.Parse(doc.ToString());

            Assert.True(result.IsSuccess, string.Join(" | ", result.Errors));
            Turbulence turbulence = result.Value.Turbulences.Single(t => t.Area is GeoCircle);
            var circle = (GeoCircle)turbulence.Area;
            Assert.Equal(new GeoCoordinate(50.0, -60.0), circle.Centre);
            Assert.Equal(new Length(300.0, LengthUnit.Kilometers), circle.Radius);
            Assert.Equal(IwxxmSigwxParser.ParseFile(PathOf("WAFS-Example.xml")).Value.Turbulences.Count, result.Value.Turbulences.Count);
        }

        [Fact]
        public void GeoCircle_RequiresAStrictlyPositiveRadius()
        {
            var result = GeoCircle.Create(new GeoCoordinate(0, 0), new Length(0.0, LengthUnit.Kilometers));

            Assert.True(result.IsFailure);
            Assert.Equal("GEO-CIRCLE-RADIUS-INVALID", result.Errors[0].Code);
        }

        internal static double[][] Ring(JsonElement ring) =>
            ring.EnumerateArray().Select(p => p.EnumerateArray().Select(n => n.GetDouble()).ToArray()).ToArray();

        internal static double SignedArea(double[][] ring)
        {
            double area = 0;
            for (int i = 0, j = ring.Length - 1; i < ring.Length; j = i++)
                area += (ring[j][0] * ring[i][1]) - (ring[i][0] * ring[j][1]);
            return area / 2;
        }

        private static double Haversine(double lat1, double lon1, double lat2, double lon2)
        {
            double r = Math.PI / 180;
            double dLat = (lat2 - lat1) * r;
            double dLon = (lon2 - lon1) * r;
            double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) + Math.Cos(lat1 * r) * Math.Cos(lat2 * r) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            return 2 * EarthRadiusMeters * Math.Asin(Math.Sqrt(a));
        }
    }
}
