using System.Linq;
using System.Text.Json;
using Iwxxm.Net.Reports;
using Xunit;

namespace Iwxxm.Net.Tests.DataFidelity
{
    /// <summary>
    /// <c>iwxxm:Volcano/activityStatus</c> and <c>sourceElevation</c> are read for VONA and QVACI exactly as for WAFS SIGWX
    /// (previously dropped silently), and written to GeoJSON.
    /// </summary>
    public sealed class VolcanoStatusAndElevationTests
    {
        private const string VonaSample = "vona-A7-1.xml";
        private const string QvaciSample = "qvaci-Example.xml";
        private const string VonaElevation = "<iwxxm:elevation uom=\"M\">1536</iwxxm:elevation>";
        private const string QvaciElevation = "<iwxxm:elevation uom=\"M\">155</iwxxm:elevation>";

        private static Volcano MapVolcano(string xml)
        {
            Result<IwxxmDocument> result = SampleDocuments.MapDirectly(xml);
            Assert.True(result.IsSuccess, string.Join(" | ", result.Errors));
            return result.Value switch
            {
                Vona v => Assert.Single(v.Volcanoes),
                Qvaci q => Assert.Single(q.Volcanoes),
                _ => throw new Xunit.Sdk.XunitException("Unexpected document type.")
            };
        }

        [Fact]
        public void Vona_Volcano_CarriesActivityStatusAndSourceElevation()
        {
            Volcano volcano = MapVolcano(SampleDocuments.Text(VonaSample));

            Assert.Equal(VolcanoActivityStatus.DecreasedActivity, volcano.ActivityStatus);
            VerticalLimit elevation = volcano.SourceElevation!.Value;
            Assert.Equal(1536, elevation.Amount);
            Assert.Equal(VerticalUnit.Meters, elevation.Unit);
            Assert.Equal(VerticalReference.Msl, elevation.Reference);
        }

        [Fact]
        public void Qvaci_Volcano_CarriesSourceElevation_AndNoStatusWhenAbsent()
        {
            Volcano volcano = MapVolcano(SampleDocuments.Text(QvaciSample));

            Assert.Null(volcano.ActivityStatus);
            VerticalLimit elevation = volcano.SourceElevation!.Value;
            Assert.Equal(155, elevation.Amount);
            Assert.Equal(VerticalUnit.Meters, elevation.Unit);
            Assert.Equal(VerticalReference.Msl, elevation.Reference);
        }

        [Fact]
        public void Qvaci_Volcano_ReadsActivityStatus()
        {
            string xml = SampleDocuments.Text(QvaciSample).Replace(
                "</iwxxm:sourceElevation>", "</iwxxm:sourceElevation><iwxxm:activityStatus>ERUPTION_ONGOING</iwxxm:activityStatus>");

            Assert.Equal(VolcanoActivityStatus.EruptionOngoing, MapVolcano(xml).ActivityStatus);
        }

        [Theory]
        [InlineData(VonaSample, VonaElevation)]
        [InlineData(QvaciSample, QvaciElevation)]
        public void NilElevation_MapsToNull_LikeTheWafsPath(string sample, string elevation)
        {
            string xml = SampleDocuments.Text(sample).Replace(
                elevation, "<iwxxm:elevation uom=\"M\" xsi:nil=\"true\" nilReason=\"http://codes.wmo.int/common/nil/unknown\"/>");

            Assert.Null(MapVolcano(xml).SourceElevation);
        }

        [Theory]
        [InlineData(VonaSample, VonaElevation)]
        [InlineData(QvaciSample, QvaciElevation)]
        public void UnsupportedElevationUnit_IsAMappingError(string sample, string elevation)
        {
            string xml = SampleDocuments.Text(sample).Replace(elevation, elevation.Replace("uom=\"M\"", "uom=\"yd\""));

            Result<IwxxmDocument> result = SampleDocuments.MapDirectly(xml);

            Assert.True(result.IsFailure);
            Assert.Contains(result.Errors, e => e.Code == "ACL-VERTICAL-UOM-UNSUPPORTED");
        }

        [Theory]
        [InlineData(VonaSample, "<iwxxm:activityStatus>DECREASED_ACTIVITY</iwxxm:activityStatus>")]
        [InlineData(QvaciSample, "</iwxxm:sourceElevation>")]
        public void UnknownActivityStatus_IsTheWafsMappingError(string sample, string anchor)
        {
            string bogus = "<iwxxm:activityStatus>BOGUS</iwxxm:activityStatus>";
            string xml = SampleDocuments.Text(sample).Replace(
                anchor, anchor.StartsWith("</") ? anchor + bogus : bogus);

            Result<IwxxmDocument> result = SampleDocuments.MapDirectly(xml);

            Assert.True(result.IsFailure);
            Assert.Contains(result.Errors, e => e.Code == "ACL-VOLCANO-ACTIVITY-INVALID");
        }

        [Fact]
        public void Vona_GeoJson_WritesActivityStatusAndSourceElevation()
        {
            JsonElement props = VolcanoProperties(VonaSample);

            Assert.Equal("DecreasedActivity", props.GetProperty("activityStatus").GetString());
            Assert.Equal(1536, props.GetProperty("sourceElevation").GetDouble());
            Assert.Equal("M", props.GetProperty("sourceElevationUom").GetString());
            Assert.Equal("MSL", props.GetProperty("sourceElevationReference").GetString());
        }

        [Fact]
        public void Qvaci_GeoJson_WritesSourceElevation_AndOmitsAbsentStatus()
        {
            JsonElement props = VolcanoProperties(QvaciSample);

            Assert.Equal(155, props.GetProperty("sourceElevation").GetDouble());
            Assert.Equal("M", props.GetProperty("sourceElevationUom").GetString());
            Assert.False(props.TryGetProperty("activityStatus", out _));
        }

        private static JsonElement VolcanoProperties(string sample)
        {
            Result<string> result = IwxxmParser.ParseFileToGeoJson(SampleDocuments.PathOf(sample));
            Assert.True(result.IsSuccess, string.Join(" | ", result.Errors));
            using JsonDocument doc = JsonDocument.Parse(result.Value);
            return doc.RootElement.GetProperty("features").EnumerateArray()
                .Select(f => f.GetProperty("properties"))
                .First(p => p.TryGetProperty("iavceiNumber", out _))
                .Clone();
        }
    }
}
