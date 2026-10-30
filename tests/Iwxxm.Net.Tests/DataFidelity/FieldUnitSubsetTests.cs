using System.Linq;
using System.Xml.Linq;
using Xunit;
using static Iwxxm.Net.Tests.DataFidelity.SampleDocuments;

namespace Iwxxm.Net.Tests.DataFidelity
{
    /// <summary>
    /// AUD-N4: per-field unit subsets of the official rules (<c>iwxxm.sch</c>) are enforced: a unit that is valid in
    /// general but not allowed for the field is a mapping error naming the field. Fixtures are the official examples with
    /// one <c>@uom</c> changed (or one optional element added).
    /// </summary>
    public sealed class FieldUnitSubsetTests
    {
        private const string NotAllowed = "ACL-MEASURE-UOM-NOT-ALLOWED";
        private const string Mismatch = "ACL-MEASURE-UOM-MISMATCH";

        // sample, element (first occurrence), forbidden unit, expected code, iwxxm.sch rule
        [Theory]
        [InlineData("metar-A3-1.xml", "meanWindSpeed", "km/h", NotAllowed)]               // SPECI.AerodromeSurfaceWind-6
        [InlineData("speci-A3-2.xml", "meanWindSpeed", "km/h", NotAllowed)]               // SPECI.AerodromeSurfaceWind-6
        [InlineData("taf-A5-1.xml", "meanWindSpeed", "km/h", NotAllowed)]                 // common wind speed subset
        [InlineData("metar-A3-1.xml", "meanWindDirection", "rad", Mismatch)]              // SPECI.AerodromeSurfaceWind-5
        [InlineData("metar-A3-1.xml", "airTemperature", "K", Mismatch)]                   // SPECI.MeteorologicalAerodromeObservation-3
        [InlineData("metar-A3-1.xml", "dewpointTemperature", "[degF]", Mismatch)]         // SPECI.MeteorologicalAerodromeObservation-4
        [InlineData("metar-A3-1.xml", "qnh", "[in_i'Hg]", Mismatch)]                      // SPECI.MeteorologicalAerodromeObservation-5
        [InlineData("metar-A3-1.xml", "prevailingVisibility", "km", Mismatch)]            // SPECI.AerodromeHorizontalVisibility-3
        [InlineData("metar-A3-1.xml", "meanRVR", "[ft_i]", Mismatch)]                     // SPECI.AerodromeRunwayVisualRange-1
        [InlineData("sigmet-A6-1a-TS.xml", "speedOfMotion", "m/s", NotAllowed)]           // SIGMET.SIGMETEvolvingCondition-3
        [InlineData("sigmet-A6-1a-TS.xml", "directionOfMotion", "rad", Mismatch)]         // SIGMET.SIGMETEvolvingCondition-2
        [InlineData("va-advisory-A7-2.xml", "speedOfMotion", "m/s", NotAllowed)]          // VolcanicAshAdvisory.VolcanicAshCloud-2
        [InlineData("tc-advisory-A2-2.xml", "maximumSurfaceWindSpeed", "km/h", NotAllowed)] // TropicalCycloneObservedConditions-2
        [InlineData("tc-advisory-A2-2.xml", "centralPressure", "Pa", Mismatch)]           // TropicalCycloneObservedConditions-1
        public void UnitOutsideTheFieldSubset_IsAMappingErrorNamingTheField(string sample, string element, string uom, string expectedCode)
        {
            XDocument doc = Load(sample);
            XElement measure = doc.Descendants(IwxxmNs + element).First(e => e.Attribute("uom") != null);
            measure.SetAttributeValue("uom", uom);

            var result = MapDirectly(doc);

            Assert.True(result.IsFailure, $"{element} in {uom} must be rejected.");
            ValidationError error = Assert.Single(result.Errors.Where(e => e.Code == expectedCode));
            Assert.Contains(element, error.Location ?? error.Message);
        }

        [Fact]
        public void CloudLayerBase_InKilometres_IsNotAllowed()
        {
            XDocument doc = Load("taf-A5-1.xml");
            XElement layerBase = doc.Descendants(IwxxmNs + "CloudLayer").First().Element(IwxxmNs + "base")!;
            layerBase.SetAttributeValue("uom", "km"); // Common.CloudLayer-1: m or [ft_i]

            var result = MapDirectly(doc);

            Assert.True(result.IsFailure);
            Assert.Contains(result.Errors, e => e.Code == NotAllowed && e.Message.Contains("'base'"));
        }

        [Theory]
        [InlineData("cloudBase", "km", "[ft_i]")]        // AIRMET.AIRMETEvolvingCondition-1
        [InlineData("cloudTop", "[mi_i]", "m")]          // AIRMET.AIRMETEvolvingCondition-2
        [InlineData("surfaceVisibility", "km", "m")]     // AIRMET.AIRMETEvolvingCondition-5
        [InlineData("surfaceWindSpeed", "km/h", "[kn_i]")] // AIRMET.AIRMETEvolvingCondition-6
        public void AirmetContent_UnitOutsideTheFieldSubset_IsAMappingError(string element, string forbidden, string allowed)
        {
            foreach ((string uom, bool shouldFail) in new[] { (forbidden, true), (allowed, false) })
            {
                XDocument doc = Load("airmet-A6-1a-TS.xml");
                XElement condition = doc.Descendants(IwxxmNs + "AIRMETEvolvingCondition").Single();
                var content = new System.Collections.Generic.List<XElement> { new XElement(IwxxmNs + element, new XAttribute("uom", uom), "100") };
                if (element == "surfaceWindSpeed")
                    content.Insert(0, new XElement(IwxxmNs + "surfaceWindDirection", new XAttribute("uom", "deg"), "90"));
                condition.Element(IwxxmNs + "geometry")!.AddAfterSelf(content.Cast<object>().ToArray());

                var result = MapDirectly(doc);

                Assert.Equal(shouldFail, result.IsFailure);
                if (shouldFail)
                    Assert.Contains(result.Errors, e => e.Code == NotAllowed && e.Message.Contains("'" + element + "'"));
            }
        }

        [Theory]
        [InlineData("m/s")]
        [InlineData("[kn_i]")]
        public void AllowedWindUnits_AreAccepted(string uom)
        {
            XDocument doc = Load("metar-A3-1.xml");
            doc.Descendants(IwxxmNs + "meanWindSpeed").First().SetAttributeValue("uom", uom);

            Assert.True(MapDirectly(doc).IsSuccess);
        }
    }
}
