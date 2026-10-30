using System;
using System.Xml.Linq;
using Iwxxm.Net.Infrastructure.Serialization;
using Iwxxm20252.Generated;
using Xunit;

namespace Iwxxm.Net.Tests
{
    /// <summary>
    /// Verifies the reflection-based reader that replaced <c>XmlSerializer</c> once the generated transport model became internal.
    /// </summary>
    public sealed class AttributeDrivenXmlReaderTests
    {
        private const string Gml = "http://www.opengis.net/gml/3.2";
        private const string Iwxxm = "http://icao.int/iwxxm/2025-2";
        private const string Xsi = "http://www.w3.org/2001/XMLSchema-instance";

        [Fact]
        public void Read_PopulatesAttributesTextAndSpecifiedCompanions()
        {
            var element = XElement.Parse(
                $"<gml:posList xmlns:gml='{Gml}' srsDimension='3' count='2' unknown='x'>1 2 3 4 5 6</gml:posList>");

            var dto = AttributeDrivenXmlReader.Read<DirectPositionListType>(element);

            Assert.Equal("1 2 3 4 5 6", dto.Value);
            Assert.Equal(3, dto.SrsDimension);
            Assert.True(dto.SrsDimensionSpecified);
            Assert.Equal(2, dto.Count);
            Assert.True(dto.CountSpecified);
        }

        [Fact]
        public void Read_LeavesSpecifiedFalseWhenAttributeIsAbsent()
        {
            var dto = AttributeDrivenXmlReader.Read<DirectPositionListType>(XElement.Parse($"<gml:posList xmlns:gml='{Gml}'>1 2</gml:posList>"));

            Assert.False(dto.SrsDimensionSpecified);
            Assert.Equal(2, dto.SrsDimension);
        }

        [Fact]
        public void Read_NilledNullableElementYieldsNull()
        {
            var element = XElement.Parse(
                $"<iwxxm:WAFSJetStreamWindSymbol xmlns:iwxxm='{Iwxxm}' xmlns:xsi='{Xsi}'>" +
                "<iwxxm:windSpeed xsi:nil='true' nilReason='missing'/></iwxxm:WAFSJetStreamWindSymbol>");

            var dto = AttributeDrivenXmlReader.Read<WAFSJetStreamWindSymbolType>(element);

            Assert.Null(dto.WindSpeed);
        }

        [Fact]
        public void Read_NilledElevationKeepsItsNilReason()
        {
            // Vertical measures are not declared nullable so that the nilReason reaches VerticalLimit.Nil (DDD-08).
            var element = XElement.Parse(
                $"<iwxxm:ElevatedLevel xmlns:iwxxm='{Iwxxm}' xmlns:xsi='{Xsi}'>" +
                "<iwxxm:elevation xsi:nil='true' nilReason='missing'/></iwxxm:ElevatedLevel>");

            var dto = AttributeDrivenXmlReader.Read<ElevatedLevelType>(element);

            Assert.NotNull(dto.Elevation);
            Assert.True(dto.Elevation!.IsNil);
            Assert.Equal("missing", dto.Elevation.NilReason);
            Assert.Null(dto.Elevation.Value);
        }

        [Fact]
        public void Read_AppendsRepeatedElementsToListMembers()
        {
            var element = XElement.Parse(
                $"<gml:segments xmlns:gml='{Gml}'>" +
                "<gml:LineStringSegment><gml:posList>1 2 3 4</gml:posList></gml:LineStringSegment>" +
                "<gml:LineStringSegment><gml:posList>5 6 7 8</gml:posList></gml:LineStringSegment>" +
                "<gml:Unknown/></gml:segments>");

            var dto = AttributeDrivenXmlReader.Read<CurveSegmentArrayPropertyType>(element);

            Assert.Equal(2, dto.Items.Count);
            Assert.Equal("5 6 7 8", Assert.IsType<LineStringSegmentType>(dto.Items[1]).PosList!.Value);
        }

        [Fact]
        public void Read_OrderedChoiceList_KeepsDocumentOrderAcrossElementNames()
        {
            var element = XElement.Parse(
                $"<gml:segments xmlns:gml='{Gml}'>" +
                "<gml:CubicSpline><gml:posList>1 2 3 4</gml:posList></gml:CubicSpline>" +
                "<gml:LineStringSegment><gml:posList>5 6 7 8</gml:posList></gml:LineStringSegment>" +
                "<gml:CircleByCenterPoint><gml:pos>1 2</gml:pos><gml:radius uom='km'>3</gml:radius></gml:CircleByCenterPoint>" +
                "</gml:segments>");

            var dto = AttributeDrivenXmlReader.Read<CurveSegmentArrayPropertyType>(element);

            Assert.Collection(
                dto.Items,
                s => Assert.Equal("1 2 3 4", Assert.IsType<CubicSplineType>(s).PosList!.Value),
                s => Assert.Equal("5 6 7 8", Assert.IsType<LineStringSegmentType>(s).PosList!.Value),
                s => Assert.Equal("km", Assert.IsType<CircleByCenterPointType>(s).Radius!.Uom));
        }

        [Fact]
        public void Read_InvalidScalarThrowsInvalidOperationException()
        {
            var element = XElement.Parse($"<gml:posList xmlns:gml='{Gml}' srsDimension='two'>1 2</gml:posList>");

            Assert.Throws<InvalidOperationException>(() => AttributeDrivenXmlReader.Read<DirectPositionListType>(element));
        }
    }
}
