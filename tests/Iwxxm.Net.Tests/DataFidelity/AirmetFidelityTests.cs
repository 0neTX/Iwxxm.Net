using System;
using System.Linq;
using System.Text.Json;
using System.Xml.Linq;
using Iwxxm.Net.Reports;
using Xunit;
using static Iwxxm.Net.Tests.DataFidelity.SampleDocuments;

namespace Iwxxm.Net.Tests.DataFidelity
{
    /// <summary>
    /// AUD-H3: AIRMET cancellation (<c>isCancelReport</c>, <c>cancelledReportSequenceNumber</c>,
    /// <c>cancelledReportValidPeriod</c>) and the AIRMET-specific evolving-condition content of <c>airmet.xsd</c>
    /// (<c>cloudBase</c>, <c>cloudTop</c>, <c>surfaceVisibility</c>, <c>surfaceVisibilityCause</c>,
    /// <c>surfaceWindDirection</c>, <c>surfaceWindSpeed</c>).
    /// </summary>
    public sealed class AirmetFidelityTests
    {
        /// <summary>
        /// "YUDD AIRMET 2 VALID 151600/151800 YUSO- YUDD SHANLON FIR CNL AIRMET 1 151520/151800", derived from the
        /// official airmet-A6-1a-TS.xml following the structure of sigmet-A6-1b-CNL.xml (AIRMET.AIRMET-3).
        /// </summary>
        internal static XDocument CancellationAirmet()
        {
            XDocument doc = Load("airmet-A6-1a-TS.xml");
            XElement root = doc.Root!;
            root.SetAttributeValue("isCancelReport", "true");
            root.Element(IwxxmNs + "sequenceNumber")!.Value = "2";
            root.Element(IwxxmNs + "phenomenon")!.Remove();
            root.Element(IwxxmNs + "analysis")!.Remove();
            XElement validPeriod = root.Element(IwxxmNs + "validPeriod")!;
            validPeriod.Descendants(GmlNs + "beginPosition").Single().Value = "2014-05-15T16:00:00Z";
            validPeriod.AddAfterSelf(
                new XElement(IwxxmNs + "cancelledReportSequenceNumber", "1"),
                new XElement(IwxxmNs + "cancelledReportValidPeriod",
                    new XElement(GmlNs + "TimePeriod", new XAttribute(GmlNs + "id", "uuid.cancelled-airmet-period"),
                        new XElement(GmlNs + "beginPosition", "2014-05-15T15:20:00Z"),
                        new XElement(GmlNs + "endPosition", "2014-05-15T18:00:00Z"))));
            return doc;
        }

        [Fact]
        public void CancellationAirmet_IsDistinguishable_AndExposesNoActiveHazard()
        {
            var result = IwxxmParser.ParseAs<Airmet>(CancellationAirmet().ToString());

            Assert.True(result.IsSuccess, string.Join(" | ", result.Errors));
            Airmet airmet = result.Value;
            Assert.True(airmet.IsCancelReport);
            Assert.False(airmet.IsActiveHazard);
            Assert.Equal("1", airmet.CancelledReportSequenceNumber);
            Assert.Equal(new DateTimeOffset(2014, 5, 15, 15, 20, 0, TimeSpan.Zero), airmet.CancelledReportValidFromUtc);
            Assert.Equal(new DateTimeOffset(2014, 5, 15, 18, 0, 0, TimeSpan.Zero), airmet.CancelledReportValidUntilUtc);
            Assert.Null(airmet.Phenomenon);
            Assert.Empty(airmet.EvolvingConditions);
        }

        [Fact]
        public void CancellationAirmet_GeoJson_HasNoHazardVolume()
        {
            var result = IwxxmParser.ParseToGeoJson(CancellationAirmet().ToString());

            Assert.True(result.IsSuccess, string.Join(" | ", result.Errors));
            using JsonDocument json = JsonDocument.Parse(result.Value);
            Assert.Equal(0, json.RootElement.GetProperty("features").GetArrayLength());
        }

        [Fact]
        public void OrdinaryAirmet_IsAnActiveHazard_WithoutCancellationData()
        {
            var result = IwxxmParser.ParseFileAs<Airmet>(PathOf("airmet-A6-1a-TS.xml"));

            Assert.True(result.IsSuccess, string.Join(" | ", result.Errors));
            Assert.False(result.Value.IsCancelReport);
            Assert.True(result.Value.IsActiveHazard);
            Assert.Null(result.Value.CancelledReportSequenceNumber);
            Assert.Null(result.Value.CancelledReportValidFromUtc);
            AirmetEvolvingCondition condition = Assert.Single(result.Value.EvolvingConditions);
            Assert.Equal("uuid.deeb1ae5-0094-4ec8-af92-aa7b3f156a3f", condition.Id);
            Assert.Equal("WEAKEN", condition.Volume.IntensityChange);
            Assert.Null(condition.CloudBase);
            Assert.Null(condition.SurfaceVisibility);
            Assert.Empty(condition.SurfaceVisibilityCauses);
        }

        [Fact]
        public void CancellationAirmet_CarryingAnEvolvingCondition_IsRejectedByTheDomain()
        {
            var volume = new AirmetEvolvingCondition(new EnRoutePhenomenonVolume("v1", null, null, null));

            var result = Airmet.Create(
                "a1", new DateTimeOffset(2014, 5, 15, 16, 0, 0, TimeSpan.Zero), ReportStatus.Normal, PermissibleUsage.Operational,
                "YUDD", null, null, null, null, "2", null, null,
                evolvingConditions: new[] { volume },
                isCancelReport: true);

            Assert.True(result.IsFailure);
            Assert.Contains(result.Errors, e => e.Code == "DOMAIN-AIRMET-CANCEL-CONTENT");
        }

        private static XDocument AirmetWithContent(params XElement[] content)
        {
            XDocument doc = Load("airmet-A6-1a-TS.xml");
            XElement condition = doc.Descendants(IwxxmNs + "AIRMETEvolvingCondition").Single();
            condition.Element(IwxxmNs + "geometry")!.AddAfterSelf(content.Cast<object>().ToArray());
            return doc;
        }

        [Fact]
        public void BrokenCloudAirmet_CloudBaseAndTop_KeepValueUnitAndReference()
        {
            XDocument doc = AirmetWithContent(
                new XElement(IwxxmNs + "cloudBase", new XAttribute("uom", "[ft_i]"), "1000"),
                new XElement(IwxxmNs + "cloudBaseReference", "SFC"),
                new XElement(IwxxmNs + "cloudTop", new XAttribute("uom", "m"), "3000"),
                new XElement(IwxxmNs + "cloudTopReference", "MSL"),
                new XElement(IwxxmNs + "cloudTopAbove", "true"));

            var result = IwxxmParser.ParseAs<Airmet>(doc.ToString());

            Assert.True(result.IsSuccess, string.Join(" | ", result.Errors));
            AirmetEvolvingCondition condition = Assert.Single(result.Value.EvolvingConditions);
            Assert.Equal(1000.0, condition.CloudBase!.Value.Amount);
            Assert.Equal(VerticalUnit.Feet, condition.CloudBase.Value.Unit);
            Assert.Equal(VerticalReference.Sfc, condition.CloudBase.Value.Reference);
            Assert.Equal(3000.0, condition.CloudTop!.Value.Amount);
            Assert.Equal(VerticalUnit.Meters, condition.CloudTop.Value.Unit);
            Assert.Equal(VerticalReference.Msl, condition.CloudTop.Value.Reference);
            Assert.True(condition.IsCloudTopAbove);
        }

        [Fact]
        public void SurfaceVisibilityAirmet_VisibilityAndCauses_AreMapped()
        {
            XDocument doc = AirmetWithContent(
                new XElement(IwxxmNs + "surfaceVisibility", new XAttribute("uom", "m"), "3000"),
                new XElement(IwxxmNs + "surfaceVisibilityCause", new XAttribute(XlinkNs + "href", "http://codes.wmo.int/49-2/WeatherCausingVisibilityReduction/BR")),
                new XElement(IwxxmNs + "surfaceVisibilityCause", new XAttribute(XlinkNs + "href", "http://codes.wmo.int/49-2/WeatherCausingVisibilityReduction/FU")));

            var result = IwxxmParser.ParseAs<Airmet>(doc.ToString());

            Assert.True(result.IsSuccess, string.Join(" | ", result.Errors));
            AirmetEvolvingCondition condition = Assert.Single(result.Value.EvolvingConditions);
            Assert.Equal(new Length(3000.0, LengthUnit.Meters), condition.SurfaceVisibility);
            Assert.Equal(
                new[] { VisibilityReductionCause.Mist, VisibilityReductionCause.Smoke },
                condition.SurfaceVisibilityCauses.Select(c => c.Code).ToArray());
            Assert.Equal(new[] { "BR", "FU" }, condition.SurfaceVisibilityCauses.Select(c => c.Notation).ToArray());
        }

        [Fact]
        public void SurfaceWindAirmet_DirectionAndSpeed_AreMappedInTheReportedUnit()
        {
            XDocument doc = AirmetWithContent(
                new XElement(IwxxmNs + "surfaceWindDirection", new XAttribute("uom", "deg"), "50"),
                new XElement(IwxxmNs + "surfaceWindSpeed", new XAttribute("uom", "[kn_i]"), "40"));

            var result = IwxxmParser.ParseAs<Airmet>(doc.ToString());

            Assert.True(result.IsSuccess, string.Join(" | ", result.Errors));
            AirmetEvolvingCondition condition = Assert.Single(result.Value.EvolvingConditions);
            Assert.Equal(50.0, condition.SurfaceWindDirectionDegrees);
            Assert.Equal(new Speed(40.0, SpeedUnit.Knots), condition.SurfaceWindSpeed);

            var geoJson = IwxxmParser.ParseToGeoJson(doc.ToString());
            Assert.True(geoJson.IsSuccess, string.Join(" | ", geoJson.Errors));
            using JsonDocument json = JsonDocument.Parse(geoJson.Value);
            JsonElement properties = json.RootElement.GetProperty("features")[0].GetProperty("properties");
            Assert.Equal(40.0, properties.GetProperty("surfaceWindSpeed").GetDouble());
            Assert.Equal("[kn_i]", properties.GetProperty("surfaceWindSpeedUom").GetString());
            Assert.Equal(50.0, properties.GetProperty("surfaceWindDirectionDegrees").GetDouble());
        }

        [Fact]
        public void CancellationSigmet_OfficialExample_KeepsTheCancelledValidPeriod()
        {
            var result = IwxxmParser.ParseFileAs<Sigmet>(PathOf("sigmet-A6-1b-CNL.xml"));

            Assert.True(result.IsSuccess, string.Join(" | ", result.Errors));
            Assert.True(result.Value.IsCancelReport);
            XElement period = Load("sigmet-A6-1b-CNL.xml").Root!.Element(IwxxmNs + "cancelledReportValidPeriod")!;
            Assert.Equal(DateTimeOffset.Parse(period.Descendants(GmlNs + "beginPosition").Single().Value), result.Value.CancelledReportValidFromUtc);
            Assert.Equal(DateTimeOffset.Parse(period.Descendants(GmlNs + "endPosition").Single().Value), result.Value.CancelledReportValidUntilUtc);
        }
    }
}
