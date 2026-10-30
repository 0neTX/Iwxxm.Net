//------------------------------------------------------------------------------
// HAND-MAINTAINED transport model. This file is NOT the output of a code generator.
//
// Internal XML transport classes for the SIGWX roots (iwxxm:WAFSSignificantWeatherForecast and the non-standard
// compact iwxxm:SIGWXReport), shaped after the hand-written, NON-STANDARD legacy schema subset under
// Resources/Xsd/Legacy. They contain hand-written code (List<T> collections, SigwxDocumentEnvelope, constructors,
// helper logic) that no generator reproduces: edit this file by hand and keep it consistent with the Legacy XSDs.
// tools/generate-xsd-classes.sh only writes an xscgen reference scaffold to artifacts/xscgen-reference/ for manual
// comparison; it never overwrites this file. The official WMO IWXXM 2025-2 schemas (Resources/Xsd/Official) are
// used for validation only and are not the source of these types.
// The file name and the Iwxxm20252.Generated namespace are historical; note that Roslyn still treats a file named
// *.Generated.cs as generated code (analyzers skip it), so review changes here with extra care.
//------------------------------------------------------------------------------

#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Xml.Schema;
using System.Xml.Serialization;

namespace Iwxxm20252.Generated
{
    internal static class XmlNamespaces
    {
        public const string Iwxxm = "http://icao.int/iwxxm/2025-2";
        public const string Gml = "http://www.opengis.net/gml/3.2";
        public const string Xlink = "http://www.w3.org/1999/xlink";
    }

    /// <summary>
    /// Discriminated transport envelope holding the deserialized XSD root object
    /// (<see cref="WAFSSignificantWeatherForecastType"/> or <see cref="SIGWXReportType"/>).
    /// </summary>
    internal sealed class SigwxDocumentEnvelope
    {
        public WAFSSignificantWeatherForecastType? WafsForecast { get; }
        public SIGWXReportType? CompactReport { get; }

        public SigwxDocumentEnvelope(WAFSSignificantWeatherForecastType wafsForecast)
        {
            WafsForecast = wafsForecast ?? throw new ArgumentNullException(nameof(wafsForecast));
        }

        public SigwxDocumentEnvelope(SIGWXReportType compactReport)
        {
            CompactReport = compactReport ?? throw new ArgumentNullException(nameof(compactReport));
        }
    }

    [Serializable]
    [DebuggerStepThrough]
    [XmlType("CodeWithAuthorityType", Namespace = XmlNamespaces.Gml)]
    internal partial class CodeWithAuthorityType
    {
        [XmlText]
        public string Value { get; set; } = string.Empty;

        [XmlAttribute("codeSpace", DataType = "anyURI")]
        public string CodeSpace { get; set; } = string.Empty;
    }

    [Serializable]
    [DebuggerStepThrough]
    [XmlType("AbstractGMLType", Namespace = XmlNamespaces.Gml)]
    internal abstract partial class AbstractGMLType
    {
        [XmlElement("identifier", Namespace = XmlNamespaces.Gml)]
        public CodeWithAuthorityType? Identifier { get; set; }

        [XmlAttribute("id", Namespace = XmlNamespaces.Gml, Form = XmlSchemaForm.Qualified, DataType = "ID")]
        public string Id { get; set; } = string.Empty;
    }

    [Serializable]
    [DebuggerStepThrough]
    [XmlType("ReferenceType", Namespace = XmlNamespaces.Gml)]
    internal partial class ReferenceType
    {
        [XmlAttribute("href", Namespace = XmlNamespaces.Xlink, Form = XmlSchemaForm.Qualified, DataType = "anyURI")]
        public string? Href { get; set; }

        [XmlAttribute("title", Namespace = XmlNamespaces.Xlink, Form = XmlSchemaForm.Qualified)]
        public string? Title { get; set; }

        [XmlAttribute("nilReason")]
        public string? NilReason { get; set; }
    }

    [Serializable]
    [DebuggerStepThrough]
    [XmlType("MeasureType", Namespace = XmlNamespaces.Gml)]
    internal partial class MeasureType
    {
        [XmlText]
        public string RawValue { get; set; } = string.Empty;

        [XmlAttribute("uom")]
        public string Uom { get; set; } = string.Empty;

        [XmlAttribute("nilReason")]
        public string? NilReason { get; set; }

        [XmlIgnore]
        public bool IsNil => !string.IsNullOrWhiteSpace(NilReason) || string.IsNullOrWhiteSpace(RawValue);

        /// <summary>Parsed numeric value, or <c>null</c> when the text is not a finite number (never defaulted to 0; NaN/Infinity rejected).</summary>
        [XmlIgnore]
        public double? Value =>
            double.TryParse(RawValue?.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double v) && !double.IsNaN(v) && !double.IsInfinity(v)
                ? v
                : (double?)null;
    }

    [Serializable]
    [DebuggerStepThrough]
    [XmlType("StringWithNilReasonType", Namespace = XmlNamespaces.Iwxxm)]
    internal partial class StringWithNilReasonType
    {
        [XmlText]
        public string Value { get; set; } = string.Empty;

        [XmlAttribute("nilReason")]
        public string? NilReason { get; set; }
    }

    [Serializable]
    [DebuggerStepThrough]
    [XmlType("DirectPositionType", Namespace = XmlNamespaces.Gml)]
    internal partial class DirectPositionType
    {
        [XmlText]
        public string Value { get; set; } = string.Empty;

        [XmlAttribute("srsName", DataType = "anyURI")]
        public string? SrsName { get; set; }

        [XmlAttribute("srsDimension")]
        public int SrsDimension { get; set; } = 2;

        [XmlIgnore]
        public bool SrsDimensionSpecified { get; set; }

        [XmlAttribute("axisLabels")]
        public string? AxisLabels { get; set; }
    }

    [Serializable]
    [DebuggerStepThrough]
    [XmlType("DirectPositionListType", Namespace = XmlNamespaces.Gml)]
    internal partial class DirectPositionListType
    {
        [XmlText]
        public string Value { get; set; } = string.Empty;

        [XmlAttribute("srsName", DataType = "anyURI")]
        public string? SrsName { get; set; }

        [XmlAttribute("srsDimension")]
        public int SrsDimension { get; set; } = 2;

        [XmlIgnore]
        public bool SrsDimensionSpecified { get; set; }

        [XmlAttribute("axisLabels")]
        public string? AxisLabels { get; set; }

        [XmlAttribute("count")]
        public int Count { get; set; }

        [XmlIgnore]
        public bool CountSpecified { get; set; }
    }

    [Serializable]
    [DebuggerStepThrough]
    [XmlType("PointType", Namespace = XmlNamespaces.Gml)]
    [XmlRoot("Point", Namespace = XmlNamespaces.Gml, IsNullable = false)]
    internal partial class PointType : AbstractGMLType
    {
        [XmlElement("pos", Namespace = XmlNamespaces.Gml)]
        public DirectPositionType? Pos { get; set; }

        [XmlAttribute("srsName", DataType = "anyURI")]
        public string? SrsName { get; set; }

        [XmlAttribute("srsDimension")]
        public int SrsDimension { get; set; } = 2;

        [XmlIgnore]
        public bool SrsDimensionSpecified { get; set; }

        [XmlAttribute("axisLabels")]
        public string? AxisLabels { get; set; }
    }

    [Serializable]
    [DebuggerStepThrough]
    [XmlType("LineStringSegmentType", Namespace = XmlNamespaces.Gml)]
    internal partial class LineStringSegmentType : AbstractCurveSegmentType
    {
        [XmlElement("posList", Namespace = XmlNamespaces.Gml)]
        public DirectPositionListType? PosList { get; set; }
    }

    [Serializable]
    [DebuggerStepThrough]
    [XmlType("CubicSplineType", Namespace = XmlNamespaces.Gml)]
    internal partial class CubicSplineType : AbstractCurveSegmentType
    {
        [XmlElement("posList", Namespace = XmlNamespaces.Gml)]
        public DirectPositionListType? PosList { get; set; }

        [XmlElement("vectorAtStart", Namespace = XmlNamespaces.Gml)]
        public DirectPositionType? VectorAtStart { get; set; }

        [XmlElement("vectorAtEnd", Namespace = XmlNamespaces.Gml)]
        public DirectPositionType? VectorAtEnd { get; set; }
    }

    [Serializable]
    [DebuggerStepThrough]
    [XmlType("CurveSegmentArrayPropertyType", Namespace = XmlNamespaces.Gml)]
    internal partial class CurveSegmentArrayPropertyType
    {
        /// <summary>Curve segments in document order (<c>gml:segments</c> is an ordered sequence).</summary>
        [XmlElement("LineStringSegment", typeof(LineStringSegmentType), Namespace = XmlNamespaces.Gml)]
        [XmlElement("CubicSpline", typeof(CubicSplineType), Namespace = XmlNamespaces.Gml)]
        [XmlElement("CircleByCenterPoint", typeof(CircleByCenterPointType), Namespace = XmlNamespaces.Gml)]
        public List<AbstractCurveSegmentType> Items { get; set; } = new List<AbstractCurveSegmentType>();
    }

    /// <summary>Base of the supported <c>gml:AbstractCurveSegment</c> substitutions.</summary>
    [Serializable]
    [DebuggerStepThrough]
    [XmlType("AbstractCurveSegmentType", Namespace = XmlNamespaces.Gml)]
    internal abstract partial class AbstractCurveSegmentType
    {
    }

    /// <summary><c>gml:CircleByCenterPoint</c>: centre position and radius (with <c>@uom</c>).</summary>
    [Serializable]
    [DebuggerStepThrough]
    [XmlType("CircleByCenterPointType", Namespace = XmlNamespaces.Gml)]
    internal partial class CircleByCenterPointType : AbstractCurveSegmentType
    {
        [XmlElement("pos", Namespace = XmlNamespaces.Gml)]
        public DirectPositionType? Pos { get; set; }

        [XmlElement("radius", Namespace = XmlNamespaces.Gml)]
        public MeasureType? Radius { get; set; }
    }

    [Serializable]
    [DebuggerStepThrough]
    [XmlType("CurveType", Namespace = XmlNamespaces.Gml)]
    [XmlRoot("Curve", Namespace = XmlNamespaces.Gml, IsNullable = false)]
    internal partial class CurveType : AbstractGMLType
    {
        [XmlElement("segments", Namespace = XmlNamespaces.Gml)]
        public CurveSegmentArrayPropertyType? Segments { get; set; }

        [XmlAttribute("srsName", DataType = "anyURI")]
        public string? SrsName { get; set; }

        [XmlAttribute("srsDimension")]
        public int SrsDimension { get; set; } = 2;

        [XmlIgnore]
        public bool SrsDimensionSpecified { get; set; }

        [XmlAttribute("axisLabels")]
        public string? AxisLabels { get; set; }
    }

    [Serializable]
    [DebuggerStepThrough]
    [XmlType("GmlCurvePropertyType", Namespace = XmlNamespaces.Gml)]
    internal partial class GmlCurvePropertyType
    {
        [XmlElement("Curve", Namespace = XmlNamespaces.Gml)]
        public CurveType? Curve { get; set; }
    }

    [Serializable]
    [DebuggerStepThrough]
    [XmlType("LinearRingType", Namespace = XmlNamespaces.Gml)]
    internal partial class LinearRingType
    {
        [XmlElement("posList", Namespace = XmlNamespaces.Gml)]
        public DirectPositionListType? PosList { get; set; }
    }

    [Serializable]
    [DebuggerStepThrough]
    [XmlType("RingType", Namespace = XmlNamespaces.Gml)]
    internal partial class RingType
    {
        [XmlElement("curveMember", Namespace = XmlNamespaces.Gml)]
        public List<GmlCurvePropertyType> CurveMembers { get; set; } = new List<GmlCurvePropertyType>();
    }

    [Serializable]
    [DebuggerStepThrough]
    [XmlType("AbstractRingPropertyType", Namespace = XmlNamespaces.Gml)]
    internal partial class AbstractRingPropertyType
    {
        [XmlElement("LinearRing", Namespace = XmlNamespaces.Gml)]
        public LinearRingType? LinearRing { get; set; }

        [XmlElement("Ring", Namespace = XmlNamespaces.Gml)]
        public RingType? Ring { get; set; }
    }

    [Serializable]
    [DebuggerStepThrough]
    [XmlType("PolygonPatchType", Namespace = XmlNamespaces.Gml)]
    internal partial class PolygonPatchType
    {
        [XmlElement("exterior", Namespace = XmlNamespaces.Gml)]
        public AbstractRingPropertyType? Exterior { get; set; }

        [XmlElement("interior", Namespace = XmlNamespaces.Gml)]
        public List<AbstractRingPropertyType> Interiors { get; set; } = new List<AbstractRingPropertyType>();
    }

    [Serializable]
    [DebuggerStepThrough]
    [XmlType("SurfacePatchArrayPropertyType", Namespace = XmlNamespaces.Gml)]
    internal partial class SurfacePatchArrayPropertyType
    {
        [XmlElement("PolygonPatch", Namespace = XmlNamespaces.Gml)]
        public List<PolygonPatchType> PolygonPatches { get; set; } = new List<PolygonPatchType>();
    }

    [Serializable]
    [DebuggerStepThrough]
    [XmlType("PolygonType", Namespace = XmlNamespaces.Gml)]
    [XmlRoot("Polygon", Namespace = XmlNamespaces.Gml, IsNullable = false)]
    internal partial class PolygonType : AbstractGMLType
    {
        [XmlElement("exterior", Namespace = XmlNamespaces.Gml)]
        public AbstractRingPropertyType? Exterior { get; set; }

        [XmlElement("interior", Namespace = XmlNamespaces.Gml)]
        public List<AbstractRingPropertyType> Interiors { get; set; } = new List<AbstractRingPropertyType>();

        [XmlAttribute("srsName", DataType = "anyURI")]
        public string? SrsName { get; set; }

        [XmlAttribute("srsDimension")]
        public int SrsDimension { get; set; } = 2;

        [XmlIgnore]
        public bool SrsDimensionSpecified { get; set; }

        [XmlAttribute("axisLabels")]
        public string? AxisLabels { get; set; }
    }

    [Serializable]
    [DebuggerStepThrough]
    [XmlType("TimeInstantType", Namespace = XmlNamespaces.Gml)]
    internal partial class TimeInstantType : AbstractGMLType
    {
        [XmlElement("timePosition", Namespace = XmlNamespaces.Gml, DataType = "dateTime")]
        public DateTime TimePosition { get; set; }
    }

    [Serializable]
    [DebuggerStepThrough]
    [XmlType("TimePeriodType", Namespace = XmlNamespaces.Gml)]
    internal partial class TimePeriodType : AbstractGMLType
    {
        [XmlElement("beginPosition", Namespace = XmlNamespaces.Gml, DataType = "dateTime")]
        public DateTime BeginPosition { get; set; }

        [XmlElement("endPosition", Namespace = XmlNamespaces.Gml, DataType = "dateTime")]
        public DateTime EndPosition { get; set; }
    }

    [Serializable]
    [DebuggerStepThrough]
    [XmlType("TimeInstantPropertyType", Namespace = XmlNamespaces.Iwxxm)]
    internal partial class TimeInstantPropertyType
    {
        [XmlElement("TimeInstant", Namespace = XmlNamespaces.Gml)]
        public TimeInstantType? TimeInstant { get; set; }

        [XmlAttribute("nilReason")]
        public string? NilReason { get; set; }
    }

    [Serializable]
    [DebuggerStepThrough]
    [XmlType("TimePeriodPropertyType", Namespace = XmlNamespaces.Iwxxm)]
    internal partial class TimePeriodPropertyType
    {
        [XmlElement("TimePeriod", Namespace = XmlNamespaces.Gml)]
        public TimePeriodType? TimePeriod { get; set; }

        [XmlAttribute("nilReason")]
        public string? NilReason { get; set; }
    }

    // =========================================================================
    // WMO IWXXM 2025-2 WAFSSignificantWeatherForecast Generated Types
    // =========================================================================

    [Serializable]
    [DebuggerStepThrough]
    [XmlType("ElevatedEnvelopeType", Namespace = XmlNamespaces.Iwxxm)]
    internal partial class ElevatedEnvelopeType
    {
        [XmlElement("lowerCorner", Namespace = XmlNamespaces.Gml)]
        public DirectPositionType? LowerCorner { get; set; }

        [XmlElement("upperCorner", Namespace = XmlNamespaces.Gml)]
        public DirectPositionType? UpperCorner { get; set; }

        [XmlElement("upperElevation", Namespace = XmlNamespaces.Iwxxm)]
        public MeasureType? UpperElevation { get; set; }

        [XmlElement("upperVerticalReference", Namespace = XmlNamespaces.Iwxxm)]
        public string? UpperVerticalReference { get; set; }

        [XmlElement("lowerElevation", Namespace = XmlNamespaces.Iwxxm)]
        public MeasureType? LowerElevation { get; set; }

        [XmlElement("lowerVerticalReference", Namespace = XmlNamespaces.Iwxxm)]
        public string? LowerVerticalReference { get; set; }
    }

    [Serializable]
    [DebuggerStepThrough]
    [XmlType("ElevatedEnvelopePropertyType", Namespace = XmlNamespaces.Iwxxm)]
    internal partial class ElevatedEnvelopePropertyType
    {
        [XmlElement("ElevatedEnvelope", Namespace = XmlNamespaces.Iwxxm)]
        public ElevatedEnvelopeType? ElevatedEnvelope { get; set; }
    }

    [Serializable]
    [DebuggerStepThrough]
    [XmlType("ElevatedPointType", Namespace = XmlNamespaces.Iwxxm)]
    internal partial class ElevatedPointType : PointType
    {
        [XmlElement("elevation", Namespace = XmlNamespaces.Iwxxm)]
        public MeasureType? Elevation { get; set; }

        [XmlElement("verticalReference", Namespace = XmlNamespaces.Iwxxm)]
        public string? VerticalReference { get; set; }
    }

    [Serializable]
    [DebuggerStepThrough]
    [XmlType("ElevatedPointPropertyType", Namespace = XmlNamespaces.Iwxxm)]
    internal partial class ElevatedPointPropertyType
    {
        [XmlElement("ElevatedPoint", Namespace = XmlNamespaces.Iwxxm)]
        public ElevatedPointType? ElevatedPoint { get; set; }
    }

    [Serializable]
    [DebuggerStepThrough]
    [XmlType("ElevatedLevelType", Namespace = XmlNamespaces.Iwxxm)]
    internal partial class ElevatedLevelType : AbstractGMLType
    {
        [XmlElement("elevation", Namespace = XmlNamespaces.Iwxxm)]
        public MeasureType? Elevation { get; set; }

        [XmlElement("verticalReference", Namespace = XmlNamespaces.Iwxxm)]
        public string? VerticalReference { get; set; }
    }

    [Serializable]
    [DebuggerStepThrough]
    [XmlType("ElevatedVolumeType", Namespace = XmlNamespaces.Iwxxm)]
    internal partial class ElevatedVolumeType : AbstractGMLType
    {
        [XmlElement("patches", Namespace = XmlNamespaces.Gml)]
        public SurfacePatchArrayPropertyType? Patches { get; set; }

        [XmlElement("upperElevation", Namespace = XmlNamespaces.Iwxxm)]
        public MeasureType? UpperElevation { get; set; }

        [XmlElement("upperVerticalReference", Namespace = XmlNamespaces.Iwxxm)]
        public string? UpperVerticalReference { get; set; }

        [XmlElement("lowerElevation", Namespace = XmlNamespaces.Iwxxm)]
        public MeasureType? LowerElevation { get; set; }

        [XmlElement("lowerVerticalReference", Namespace = XmlNamespaces.Iwxxm)]
        public string? LowerVerticalReference { get; set; }

        [XmlAttribute("srsName", DataType = "anyURI")]
        public string? SrsName { get; set; }

        [XmlAttribute("srsDimension")]
        public int SrsDimension { get; set; } = 2;

        [XmlIgnore]
        public bool SrsDimensionSpecified { get; set; }

        [XmlAttribute("axisLabels")]
        public string? AxisLabels { get; set; }
    }

    [Serializable]
    [DebuggerStepThrough]
    [XmlType("WAFSJetStreamWindSymbolType", Namespace = XmlNamespaces.Iwxxm)]
    internal partial class WAFSJetStreamWindSymbolType : AbstractGMLType
    {
        [XmlElement("location", Namespace = XmlNamespaces.Iwxxm)]
        public ElevatedPointPropertyType? Location { get; set; }

        [XmlElement("windSpeed", Namespace = XmlNamespaces.Iwxxm, IsNullable = true)]
        public MeasureType? WindSpeed { get; set; }

        [XmlElement("IsotachUpperElevation", Namespace = XmlNamespaces.Iwxxm)]
        public MeasureType? IsotachUpperElevation { get; set; }

        [XmlElement("IsotachUpperVerticalReference", Namespace = XmlNamespaces.Iwxxm)]
        public string? IsotachUpperVerticalReference { get; set; }

        [XmlElement("IsotachLowerElevation", Namespace = XmlNamespaces.Iwxxm)]
        public MeasureType? IsotachLowerElevation { get; set; }

        [XmlElement("IsotachLowerVerticalReference", Namespace = XmlNamespaces.Iwxxm)]
        public string? IsotachLowerVerticalReference { get; set; }
    }

    [Serializable]
    [DebuggerStepThrough]
    [XmlType("WAFSVolcanoType", Namespace = XmlNamespaces.Iwxxm)]
    internal partial class WAFSVolcanoType : AbstractGMLType
    {
        [XmlElement("name", Namespace = XmlNamespaces.Iwxxm, IsNullable = true)]
        public StringWithNilReasonType? Name { get; set; }

        [XmlElement("IAVCEINumber", Namespace = XmlNamespaces.Iwxxm, IsNullable = true)]
        public StringWithNilReasonType? IAVCEINumber { get; set; }

        [XmlElement("wmoNumber", Namespace = XmlNamespaces.Iwxxm)]
        public string? WmoNumber { get; set; }

        [XmlElement("location", Namespace = XmlNamespaces.Iwxxm)]
        public PointPropertyType? Location { get; set; }

        [XmlElement("eruptionTime", Namespace = XmlNamespaces.Iwxxm, DataType = "dateTime")]
        public DateTime EruptionTime { get; set; }

        [XmlIgnore]
        public bool EruptionTimeSpecified { get; set; }
    }

    [Serializable]
    [DebuggerStepThrough]
    [XmlType("TropicalCycloneType", Namespace = XmlNamespaces.Iwxxm)]
    internal partial class TropicalCycloneType : AbstractGMLType
    {
        [XmlElement("name", Namespace = XmlNamespaces.Iwxxm, IsNullable = true)]
        public StringWithNilReasonType? Name { get; set; }
    }

    [Serializable]
    [DebuggerStepThrough]
    [XmlType("RadiationIncidentType", Namespace = XmlNamespaces.Iwxxm)]
    internal partial class RadiationIncidentType : AbstractGMLType
    {
        [XmlElement("siteName", Namespace = XmlNamespaces.Iwxxm, IsNullable = true)]
        public StringWithNilReasonType? SiteName { get; set; }

        [XmlElement("incidentTime", Namespace = XmlNamespaces.Iwxxm)]
        public TimeInstantPropertyType? IncidentTime { get; set; }
    }

    [Serializable]
    [DebuggerStepThrough]
    [XmlType("PhenomenonGeometryPropertyType", Namespace = XmlNamespaces.Iwxxm)]
    internal partial class PhenomenonGeometryPropertyType
    {
        [XmlElement("Curve", Namespace = XmlNamespaces.Gml)]
        public CurveType? Curve { get; set; }

        [XmlElement("Polygon", Namespace = XmlNamespaces.Gml)]
        public PolygonType? Polygon { get; set; }

        [XmlElement("Point", Namespace = XmlNamespaces.Gml)]
        public PointType? Point { get; set; }

        [XmlElement("ElevatedVolume", Namespace = XmlNamespaces.Iwxxm)]
        public ElevatedVolumeType? ElevatedVolume { get; set; }

        [XmlElement("ElevatedPoint", Namespace = XmlNamespaces.Iwxxm)]
        public ElevatedPointType? ElevatedPoint { get; set; }

        [XmlAttribute("nilReason")]
        public string? NilReason { get; set; }
    }

    [Serializable]
    [DebuggerStepThrough]
    [XmlType("PhenomenonPropertyRoleType", Namespace = XmlNamespaces.Iwxxm)]
    internal partial class PhenomenonPropertyRoleType
    {
        [XmlElement("WAFSJetStreamWindSymbol", Namespace = XmlNamespaces.Iwxxm)]
        public WAFSJetStreamWindSymbolType? JetStreamWindSymbol { get; set; }

        [XmlElement("DegreeOfTurbulence", Namespace = XmlNamespaces.Iwxxm)]
        public ReferenceType? DegreeOfTurbulence { get; set; }

        [XmlElement("DegreeOfIcing", Namespace = XmlNamespaces.Iwxxm)]
        public ReferenceType? DegreeOfIcing { get; set; }

        [XmlElement("CloudDistribution", Namespace = XmlNamespaces.Iwxxm)]
        public ReferenceType? CloudDistribution { get; set; }

        [XmlElement("CloudType", Namespace = XmlNamespaces.Iwxxm)]
        public ReferenceType? CloudType { get; set; }

        [XmlElement("ElevatedLevel", Namespace = XmlNamespaces.Iwxxm)]
        public ElevatedLevelType? ElevatedLevel { get; set; }

        [XmlElement("Volcano", Namespace = XmlNamespaces.Iwxxm)]
        public WAFSVolcanoType? Volcano { get; set; }

        [XmlElement("TropicalCyclone", Namespace = XmlNamespaces.Iwxxm)]
        public TropicalCycloneType? TropicalCyclone { get; set; }

        [XmlElement("RadiationIncident", Namespace = XmlNamespaces.Iwxxm)]
        public RadiationIncidentType? RadiationIncident { get; set; }

        [XmlAttribute("nilReason")]
        public string? NilReason { get; set; }
    }

    [Serializable]
    [DebuggerStepThrough]
    [XmlType("OriginatingCentrePropertyType", Namespace = XmlNamespaces.Iwxxm)]
    internal partial class OriginatingCentrePropertyType
    {
        [XmlElement("WorldAreaForecastCentre", Namespace = XmlNamespaces.Iwxxm)]
        public string? WorldAreaForecastCentre { get; set; }
    }

    [Serializable]
    [DebuggerStepThrough]
    [XmlType("MeteorologicalFeatureType", Namespace = XmlNamespaces.Iwxxm)]
    internal partial class MeteorologicalFeatureType : AbstractGMLType
    {
        [XmlElement("issueTime", Namespace = XmlNamespaces.Iwxxm)]
        public TimeInstantPropertyType? IssueTime { get; set; }

        [XmlElement("originatingCentre", Namespace = XmlNamespaces.Iwxxm)]
        public OriginatingCentrePropertyType? OriginatingCentre { get; set; }

        [XmlElement("phenomenonCategory", Namespace = XmlNamespaces.Iwxxm)]
        public string? PhenomenonCategory { get; set; }

        [XmlElement("phenomenonBaseTime", Namespace = XmlNamespaces.Iwxxm)]
        public TimeInstantPropertyType? PhenomenonBaseTime { get; set; }

        [XmlElement("phenomenonTime", Namespace = XmlNamespaces.Iwxxm)]
        public TimeInstantPropertyType? PhenomenonTime { get; set; }

        [XmlElement("phenomenon", Namespace = XmlNamespaces.Iwxxm)]
        public ReferenceType? Phenomenon { get; set; }

        [XmlElement("phenomenonGeometry", Namespace = XmlNamespaces.Iwxxm)]
        public PhenomenonGeometryPropertyType? PhenomenonGeometry { get; set; }

        [XmlElement("phenomenonProperty", Namespace = XmlNamespaces.Iwxxm)]
        public List<PhenomenonPropertyRoleType> PhenomenonProperties { get; set; } = new List<PhenomenonPropertyRoleType>();
    }

    [Serializable]
    [DebuggerStepThrough]
    [XmlType("MeteorologicalFeaturePropertyType", Namespace = XmlNamespaces.Iwxxm)]
    internal partial class MeteorologicalFeaturePropertyType
    {
        [XmlElement("MeteorologicalFeature", Namespace = XmlNamespaces.Iwxxm)]
        public MeteorologicalFeatureType? MeteorologicalFeature { get; set; }

        [XmlAttribute("href", Namespace = XmlNamespaces.Xlink, Form = XmlSchemaForm.Qualified, DataType = "anyURI")]
        public string? Href { get; set; }
    }

    [Serializable]
    [DebuggerStepThrough]
    [XmlType("WAFSSignificantWeatherForecastType", Namespace = XmlNamespaces.Iwxxm)]
    [XmlRoot("WAFSSignificantWeatherForecast", Namespace = XmlNamespaces.Iwxxm, IsNullable = false)]
    internal partial class WAFSSignificantWeatherForecastType : AbstractGMLType
    {
        [XmlElement("boundingPeriod", Namespace = XmlNamespaces.Iwxxm)]
        public TimePeriodPropertyType? BoundingPeriod { get; set; }

        [XmlElement("boundingVolume", Namespace = XmlNamespaces.Iwxxm)]
        public ElevatedEnvelopePropertyType? BoundingVolume { get; set; }

        [XmlElement("phenomenaList", Namespace = XmlNamespaces.Iwxxm)]
        public List<ReferenceType> PhenomenaList { get; set; } = new List<ReferenceType>();

        [XmlElement("issueTime", Namespace = XmlNamespaces.Iwxxm)]
        public TimeInstantPropertyType? IssueTime { get; set; }

        [XmlElement("originatingCentre", Namespace = XmlNamespaces.Iwxxm)]
        public OriginatingCentrePropertyType? OriginatingCentre { get; set; }

        [XmlElement("phenomenonCategory", Namespace = XmlNamespaces.Iwxxm)]
        public string PhenomenonCategory { get; set; } = string.Empty;

        [XmlElement("phenomenonBaseTime", Namespace = XmlNamespaces.Iwxxm)]
        public TimeInstantPropertyType? PhenomenonBaseTime { get; set; }

        [XmlElement("phenomenonTime", Namespace = XmlNamespaces.Iwxxm)]
        public TimeInstantPropertyType? PhenomenonTime { get; set; }

        [XmlElement("feature", Namespace = XmlNamespaces.Iwxxm)]
        public List<MeteorologicalFeaturePropertyType> Features { get; set; } = new List<MeteorologicalFeaturePropertyType>();

        // Required attributes (common.xsd): no default value, a missing attribute stays null (never invented).
        [XmlAttribute("reportStatus")]
        public string? ReportStatus { get; set; }

        [XmlAttribute("permissibleUsage")]
        public string? PermissibleUsage { get; set; }

        [XmlAttribute("permissibleUsageReason")]
        public string? PermissibleUsageReason { get; set; }

        [XmlAttribute("permissibleUsageSupplementary")]
        public string? PermissibleUsageSupplementary { get; set; }
    }

    // =========================================================================
    // Compact SIGWXReport Generated Types
    // =========================================================================

    [Serializable]
    [DebuggerStepThrough]
    [XmlType("CurvePropertyType", Namespace = XmlNamespaces.Iwxxm)]
    internal partial class CurvePropertyType
    {
        [XmlElement("Curve", Namespace = XmlNamespaces.Gml)]
        public CurveType? Curve { get; set; }
    }

    [Serializable]
    [DebuggerStepThrough]
    [XmlType("PolygonPropertyType", Namespace = XmlNamespaces.Iwxxm)]
    internal partial class PolygonPropertyType
    {
        [XmlElement("Polygon", Namespace = XmlNamespaces.Gml)]
        public PolygonType? Polygon { get; set; }
    }

    [Serializable]
    [DebuggerStepThrough]
    [XmlType("PointPropertyType", Namespace = XmlNamespaces.Iwxxm)]
    internal partial class PointPropertyType
    {
        [XmlElement("Point", Namespace = XmlNamespaces.Gml)]
        public PointType? Point { get; set; }
    }

    [Serializable]
    [DebuggerStepThrough]
    [XmlType("JetStreamCorePointType", Namespace = XmlNamespaces.Iwxxm)]
    internal partial class JetStreamCorePointType
    {
        [XmlElement("location", Namespace = XmlNamespaces.Iwxxm)]
        public PointPropertyType? Location { get; set; }

        [XmlElement("flightLevel", Namespace = XmlNamespaces.Iwxxm)]
        public MeasureType? FlightLevel { get; set; }

        [XmlElement("windSpeed", Namespace = XmlNamespaces.Iwxxm)]
        public MeasureType? WindSpeed { get; set; }

        [XmlElement("lowerFlightLevel", Namespace = XmlNamespaces.Iwxxm)]
        public MeasureType? LowerFlightLevel { get; set; }

        [XmlElement("upperFlightLevel", Namespace = XmlNamespaces.Iwxxm)]
        public MeasureType? UpperFlightLevel { get; set; }
    }

    [Serializable]
    [DebuggerStepThrough]
    [XmlType("JetStreamType", Namespace = XmlNamespaces.Iwxxm)]
    internal partial class JetStreamType : AbstractGMLType
    {
        [XmlElement("axis", Namespace = XmlNamespaces.Iwxxm)]
        public CurvePropertyType? Axis { get; set; }

        [XmlElement("flightLevel", Namespace = XmlNamespaces.Iwxxm)]
        public MeasureType? FlightLevel { get; set; }

        [XmlElement("windSpeed", Namespace = XmlNamespaces.Iwxxm)]
        public MeasureType? WindSpeed { get; set; }

        [XmlElement("corePoint", Namespace = XmlNamespaces.Iwxxm)]
        public List<JetStreamCorePointType> CorePoints { get; set; } = new List<JetStreamCorePointType>();
    }

    [Serializable]
    [DebuggerStepThrough]
    [XmlType("CloudDistributionFeatureType", Namespace = XmlNamespaces.Iwxxm)]
    internal partial class CloudDistributionFeatureType : AbstractGMLType
    {
        [XmlElement("cloudAmount", Namespace = XmlNamespaces.Iwxxm)]
        public string CloudAmount { get; set; } = string.Empty;

        [XmlElement("cloudType", Namespace = XmlNamespaces.Iwxxm)]
        public string CloudType { get; set; } = string.Empty;

        [XmlElement("base", Namespace = XmlNamespaces.Iwxxm)]
        public MeasureType? Base { get; set; }

        [XmlElement("top", Namespace = XmlNamespaces.Iwxxm)]
        public MeasureType? Top { get; set; }

        [XmlElement("geometry", Namespace = XmlNamespaces.Iwxxm)]
        public PolygonPropertyType? Geometry { get; set; }
    }

    [Serializable]
    [DebuggerStepThrough]
    [XmlType("TropopausePointType", Namespace = XmlNamespaces.Iwxxm)]
    internal partial class TropopausePointType
    {
        [XmlElement("location", Namespace = XmlNamespaces.Iwxxm)]
        public PointPropertyType? Location { get; set; }

        [XmlElement("flightLevel", Namespace = XmlNamespaces.Iwxxm)]
        public MeasureType? FlightLevel { get; set; }
    }

    [Serializable]
    [DebuggerStepThrough]
    [XmlType("TropopauseType", Namespace = XmlNamespaces.Iwxxm)]
    internal partial class TropopauseType : AbstractGMLType
    {
        [XmlElement("levelType", Namespace = XmlNamespaces.Iwxxm)]
        public string? LevelType { get; set; }

        [XmlElement("spotHeight", Namespace = XmlNamespaces.Iwxxm)]
        public List<TropopausePointType> SpotHeights { get; set; } = new List<TropopausePointType>();
    }

    [Serializable]
    [DebuggerStepThrough]
    [XmlType("SIGWXReportType", Namespace = XmlNamespaces.Iwxxm)]
    [XmlRoot("SIGWXReport", Namespace = XmlNamespaces.Iwxxm, IsNullable = false)]
    internal partial class SIGWXReportType : AbstractGMLType
    {
        [XmlElement("issueTime", Namespace = XmlNamespaces.Iwxxm)]
        public TimeInstantPropertyType? IssueTime { get; set; }

        [XmlElement("validPeriod", Namespace = XmlNamespaces.Iwxxm)]
        public TimePeriodPropertyType? ValidPeriod { get; set; }

        [XmlElement("issuingCentre", Namespace = XmlNamespaces.Iwxxm)]
        public string IssuingCentre { get; set; } = string.Empty;

        [XmlElement("lowerLimit", Namespace = XmlNamespaces.Iwxxm)]
        public MeasureType? LowerLimit { get; set; }

        [XmlElement("upperLimit", Namespace = XmlNamespaces.Iwxxm)]
        public MeasureType? UpperLimit { get; set; }

        [XmlElement("jetStream", Namespace = XmlNamespaces.Iwxxm)]
        public List<JetStreamType> JetStreams { get; set; } = new List<JetStreamType>();

        [XmlElement("cloud", Namespace = XmlNamespaces.Iwxxm)]
        public List<CloudDistributionFeatureType> Clouds { get; set; } = new List<CloudDistributionFeatureType>();

        [XmlElement("tropopause", Namespace = XmlNamespaces.Iwxxm)]
        public List<TropopauseType> Tropopauses { get; set; } = new List<TropopauseType>();

        [XmlElement("volcano", Namespace = XmlNamespaces.Iwxxm)]
        public List<WAFSVolcanoType> Volcanoes { get; set; } = new List<WAFSVolcanoType>();

        // Required attributes (common.xsd): no default value, a missing attribute stays null (never invented).
        [XmlAttribute("reportStatus")]
        public string? ReportStatus { get; set; }

        [XmlAttribute("permissibleUsage")]
        public string? PermissibleUsage { get; set; }

        [XmlAttribute("permissibleUsageReason")]
        public string? PermissibleUsageReason { get; set; }

        [XmlAttribute("permissibleUsageSupplementary")]
        public string? PermissibleUsageSupplementary { get; set; }
    }
}
