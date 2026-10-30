using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Xml.Linq;
using Iwxxm.Net.Infrastructure.Mapping;
using Iwxxm.Net.Reports;
using Xunit;

namespace Iwxxm.Net.Tests
{
    /// <summary>
    /// Verifies typed report metadata (DDD-03, AUD-M8, AUD-U8) and the absence of invented default values
    /// (DDD-07, AUD-H5) against the official WMO examples and against documents the mapper receives directly.
    /// </summary>
    /// <remarks>
    /// Required-versus-optional decisions follow the official schemas:
    /// <list type="bullet">
    /// <item><c>common.xsd</c> ReportType: <c>reportStatus</c> and <c>permissibleUsage</c> are <c>use="required"</c>;
    /// <c>permissibleUsageReason</c> and <c>permissibleUsageSupplementary</c> are optional.</item>
    /// <item><c>issueTime</c> (all reports), METAR/SPECI <c>observationTime</c> (<c>metarSpeci.xsd</c>) and SIGMET/AIRMET
    /// <c>validPeriod</c> (<c>sigmet.xsd</c>, <c>airmet.xsd</c>) have no <c>minOccurs="0"</c>: they are mandatory.</item>
    /// <item>TAF <c>validPeriod</c> is <c>minOccurs="0"</c> (<c>taf.xsd</c>): optional.</item>
    /// <item><c>iwxxm:Volcano/iwxxm:name</c> is mandatory but <c>nillable="true"</c> (<c>metFeature.xsd</c>).</item>
    /// <item>QVACI <c>ashDensityEqualsOrExceeds</c> is a mandatory <c>gml:MeasureType</c> (<c>qvaci.xsd</c>).</item>
    /// </list>
    /// A mandatory element that is absent is a mapping error; a mandatory property given only by an
    /// <c>xlink:href</c> reference that cannot be resolved in the document (remote or unknown identifier) is mapped to <c>null</c>.
    /// </remarks>
    public sealed class ReportMetadataAndNoInventedValuesTests
    {
        private static readonly XNamespace Iwxxm = "http://icao.int/iwxxm/2025-2";
        private static readonly XNamespace Gml = "http://www.opengis.net/gml/3.2";
        private static readonly XNamespace Collect = "http://def.wmo.int/collect/2014";

        public static IEnumerable<object[]> OfficialExamples() => IwxxmSuiteParserTests.GetAllOfficialWmoExamples();

        [Theory]
        [MemberData(nameof(OfficialExamples))]
        public void OfficialExamples_TypedMetadataAndIssueTime_MatchTheDocument(string path)
        {
            var result = IwxxmParser.ParseFile(path);
            Assert.True(result.IsSuccess, string.Join(" | ", result.Errors));

            XElement root = XDocument.Load(path).Root!;
            List<XElement> reportElements = root.Name == Collect + "MeteorologicalBulletin"
                ? root.Elements(Collect + "meteorologicalInformation").Select(m => m.Elements().First()).ToList()
                : new List<XElement> { root };

            List<IwxxmReport> reports = result.Value is MeteorologicalBulletin bulletin
                ? bulletin.Reports.ToList()
                : new List<IwxxmReport> { (IwxxmReport)result.Value };

            Assert.Equal(reportElements.Count, reports.Count);
            for (int i = 0; i < reports.Count; i++)
            {
                XElement element = reportElements[i];
                IwxxmReport report = reports[i];

                Assert.Equal(ExpectedStatus(element.Attribute("reportStatus")!.Value), report.ReportStatus);
                Assert.Equal(ExpectedUsage(element.Attribute("permissibleUsage")!.Value), report.PermissibleUsage);
                Assert.Equal(element.Attribute("permissibleUsageSupplementary")?.Value, report.PermissibleUsageSupplementary);
                Assert.Equal(
                    element.Attribute("permissibleUsageReason") == null ? (PermissibleUsageReason?)null : ExpectedReason(element.Attribute("permissibleUsageReason")!.Value),
                    report.PermissibleUsageReason);

                string issueTimeText = element.Element(Iwxxm + "issueTime")!.Descendants(Gml + "timePosition").First().Value;
                Assert.Equal(DateTimeOffset.Parse(issueTimeText, CultureInfo.InvariantCulture), report.IssueTimeUtc);
            }
        }

        [Theory]
        [MemberData(nameof(OfficialExamples))]
        public void OfficialExamples_ContainNoInventedSentinelValues(string path)
        {
            var result = IwxxmParser.ParseFile(path);
            Assert.True(result.IsSuccess, string.Join(" | ", result.Errors));

            var offenders = new List<string>();
            Inspect(result.Value, "report", offenders, new HashSet<object>(ReferenceEqualityComparer.Instance), 0);

            Assert.True(offenders.Count == 0, "Invented values found: " + string.Join(", ", offenders));
        }

        [Theory]
        [MemberData(nameof(OfficialExamples))]
        public void OfficialExamples_TimesArePresentOnlyWhenTheDocumentGivesThemInline(string path)
        {
            var result = IwxxmParser.ParseFile(path);
            Assert.True(result.IsSuccess, string.Join(" | ", result.Errors));
            XElement root = XDocument.Load(path).Root!;

            foreach (IwxxmReport report in result.Value is MeteorologicalBulletin b ? b.Reports : new[] { (IwxxmReport)result.Value })
            {
                XElement element = root.Name == Collect + "MeteorologicalBulletin"
                    ? root.Descendants().First(e => (string?)e.Attribute(Gml + "id") == report.Id)
                    : root;

                switch (report)
                {
                    case AerodromeObservation metar:
                        Assert.Equal(InlineInstant(element.Element(Iwxxm + "observationTime")), metar.ObservationTimeUtc);
                        break;
                    case Sigmet sigmet:
                        Assert.Equal(InlineBegin(element.Element(Iwxxm + "validPeriod")), sigmet.ValidFromUtc);
                        Assert.Equal(InlineEnd(element.Element(Iwxxm + "validPeriod")), sigmet.ValidUntilUtc);
                        break;
                    case Airmet airmet:
                        Assert.Equal(InlineBegin(element.Element(Iwxxm + "validPeriod")), airmet.ValidFromUtc);
                        Assert.Equal(InlineEnd(element.Element(Iwxxm + "validPeriod")), airmet.ValidUntilUtc);
                        break;
                    case Taf taf:
                        Assert.Equal(InlineBegin(element.Element(Iwxxm + "validPeriod")), taf.ValidFromUtc);
                        Assert.Equal(InlineEnd(element.Element(Iwxxm + "validPeriod")), taf.ValidUntilUtc);
                        break;
                }
            }
        }

        [Fact]
        public void Metar_AmendmentExampleStatus_IsMappedToTheEnum()
        {
            string? amended = Directory.GetFiles("Samples/wmo-examples", "*.xml")
                .FirstOrDefault(f => File.ReadAllText(f).Contains("reportStatus=\"AMENDMENT\""));
            Assert.NotNull(amended);

            var result = IwxxmParser.ParseFile(amended!);

            Assert.True(result.IsSuccess, string.Join(" | ", result.Errors));
            IwxxmReport report = result.Value is MeteorologicalBulletin b ? b.Reports[0] : (IwxxmReport)result.Value;
            Assert.Equal(ReportStatus.Amendment, report.ReportStatus);
        }

        [Fact]
        public void Metar_NonOperationalExercise_IsNeverPresentedAsOperational()
        {
            string xml = File.ReadAllText("Samples/wmo-examples/metar-A3-1.xml").Replace(
                "permissibleUsage=\"OPERATIONAL\"",
                "permissibleUsage=\"NON-OPERATIONAL\" permissibleUsageReason=\"EXERCISE\" permissibleUsageSupplementary=\"Regional exercise\"");

            var result = new IwxxmParserBuilder().WithoutSchematron().Build().ProcessAs<Metar>(xml);

            Assert.True(result.IsSuccess, string.Join(" | ", result.Errors));
            Assert.Equal(PermissibleUsage.NonOperational, result.Value.PermissibleUsage);
            Assert.Equal(PermissibleUsageReason.Exercise, result.Value.PermissibleUsageReason);
            Assert.Equal("Regional exercise", result.Value.PermissibleUsageSupplementary);
        }

        [Theory]
        [InlineData("permissibleUsage=\"OPERATIONAL\"", "", "ACL-PERMISSIBLE-USAGE-MISSING")]
        [InlineData("permissibleUsage=\"OPERATIONAL\"", "permissibleUsage=\"operational\"", "ACL-PERMISSIBLE-USAGE-INVALID")]
        [InlineData("reportStatus=\"NORMAL\"", "", "ACL-REPORT-STATUS-MISSING")]
        [InlineData("reportStatus=\"NORMAL\"", "reportStatus=\"NORMALL\"", "ACL-REPORT-STATUS-INVALID")]
        [InlineData("permissibleUsage=\"OPERATIONAL\"", "permissibleUsage=\"NON-OPERATIONAL\" permissibleUsageReason=\"DRILL\"", "ACL-PERMISSIBLE-USAGE-REASON-INVALID")]
        public void Mapper_MissingOrUnknownRequiredMetadata_IsAMappingErrorNotADefault(string original, string replacement, string expectedCode)
        {
            var result = MapDirectly(File.ReadAllText("Samples/wmo-examples/metar-A3-1.xml").Replace(original, replacement));

            Assert.True(result.IsFailure);
            Assert.Contains(result.Errors, e => e.Code == expectedCode && e.Category == ErrorCategory.DomainMapping);
        }

        [Fact]
        public void Mapper_MissingIssueTime_IsAMappingError()
        {
            XDocument doc = XDocument.Load("Samples/wmo-examples/sigmet-A6-1a-TS.xml");
            doc.Root!.Element(Iwxxm + "issueTime")!.Remove();

            var result = MapDirectly(doc.ToString());

            Assert.True(result.IsFailure);
            Assert.Contains(result.Errors, e => e.Code == "ACL-ISSUE-TIME-MISSING");
        }

        [Fact]
        public void Mapper_IssueTimeGivenOnlyByReference_IsAnExplicitMappingError()
        {
            XDocument doc = XDocument.Load("Samples/wmo-examples/metar-A3-1.xml");
            XElement issueTime = doc.Root!.Element(Iwxxm + "issueTime")!;
            issueTime.RemoveNodes();
            issueTime.SetAttributeValue(XNamespace.Get("http://www.w3.org/1999/xlink") + "href", "#uuid.somewhere-else");

            var result = MapDirectly(doc.ToString());

            Assert.True(result.IsFailure);
            Assert.Contains(result.Errors, e => e.Code == "ACL-ISSUE-TIME-MISSING" && e.Message.Contains("xlink:href"));
        }

        [Fact]
        public void Mapper_UnparseableTimePosition_IsAMappingError()
        {
            string xml = File.ReadAllText("Samples/wmo-examples/metar-A3-1.xml").Replace(
                "<gml:timePosition>2012-08-22T16:30:00Z</gml:timePosition>",
                "<gml:timePosition>not-a-time</gml:timePosition>");

            var result = MapDirectly(xml);

            Assert.True(result.IsFailure);
            Assert.Contains(result.Errors, e => e.Code == "ACL-TIME-INVALID");
        }

        [Fact]
        public void Metar_ObservationTimeGivenByARemoteReference_IsNullInsteadOfTheIssueTime()
        {
            XDocument doc = XDocument.Load("Samples/wmo-examples/metar-A3-1.xml");
            XElement observationTime = doc.Root!.Element(Iwxxm + "observationTime")!;
            observationTime.RemoveNodes();
            observationTime.SetAttributeValue(XNamespace.Get("http://www.w3.org/1999/xlink") + "href", "http://example.org/other.xml#uuid.e5460ae4-98a4-48fa-bbfc-21799896f1f2");

            var result = MapDirectly(doc.ToString());

            Assert.True(result.IsSuccess, string.Join(" | ", result.Errors));
            var metar = Assert.IsType<Metar>(result.Value);
            Assert.Null(metar.ObservationTimeUtc);
            Assert.Equal(new DateTimeOffset(2012, 8, 22, 16, 30, 0, TimeSpan.Zero), metar.IssueTimeUtc);
        }

        [Fact]
        public void Metar_ObservationTimeGivenByALocalReference_IsResolvedToTheReferencedInstant()
        {
            XDocument doc = XDocument.Load("Samples/wmo-examples/metar-A3-1.xml");
            XElement observationTime = doc.Root!.Element(Iwxxm + "observationTime")!;
            observationTime.RemoveNodes();
            observationTime.SetAttributeValue(XNamespace.Get("http://www.w3.org/1999/xlink") + "href", "#uuid.e5460ae4-98a4-48fa-bbfc-21799896f1f2");

            var result = MapDirectly(doc.ToString());

            Assert.True(result.IsSuccess, string.Join(" | ", result.Errors));
            var metar = Assert.IsType<Metar>(result.Value);
            Assert.Equal(metar.IssueTimeUtc, metar.ObservationTimeUtc);
        }

        [Fact]
        public void Metar_MissingObservationTime_IsAMappingError()
        {
            XDocument doc = XDocument.Load("Samples/wmo-examples/metar-A3-1.xml");
            doc.Root!.Element(Iwxxm + "observationTime")!.Remove();

            var result = MapDirectly(doc.ToString());

            Assert.True(result.IsFailure);
            Assert.Contains(result.Errors, e => e.Code == "ACL-OBSERVATION-TIME-MISSING");
        }

        [Theory]
        [InlineData("Samples/wmo-examples/sigmet-A6-1a-TS.xml")]
        [InlineData("Samples/wmo-examples/airmet-A6-1a-TS.xml")]
        public void SigmetAndAirmet_ValidPeriodGivenByReference_IsNullInsteadOfTheIssueTime(string path)
        {
            XDocument doc = XDocument.Load(path);
            XElement validPeriod = doc.Root!.Element(Iwxxm + "validPeriod")!;
            validPeriod.RemoveNodes();
            validPeriod.SetAttributeValue(XNamespace.Get("http://www.w3.org/1999/xlink") + "href", "#uuid.elsewhere");

            var result = MapDirectly(doc.ToString());

            Assert.True(result.IsSuccess, string.Join(" | ", result.Errors));
            switch (result.Value)
            {
                case Sigmet sigmet:
                    Assert.Null(sigmet.ValidFromUtc);
                    Assert.Null(sigmet.ValidUntilUtc);
                    break;
                case Airmet airmet:
                    Assert.Null(airmet.ValidFromUtc);
                    Assert.Null(airmet.ValidUntilUtc);
                    break;
                default:
                    Assert.Fail("Unexpected report type " + result.Value.GetType().Name);
                    break;
            }
        }

        [Theory]
        [InlineData("Samples/wmo-examples/sigmet-A6-1a-TS.xml")]
        [InlineData("Samples/wmo-examples/airmet-A6-1a-TS.xml")]
        public void SigmetAndAirmet_MissingValidPeriod_IsAMappingError(string path)
        {
            XDocument doc = XDocument.Load(path);
            doc.Root!.Element(Iwxxm + "validPeriod")!.Remove();

            var result = MapDirectly(doc.ToString());

            Assert.True(result.IsFailure);
            Assert.Contains(result.Errors, e => e.Code == "ACL-VALID-PERIOD-MISSING");
        }

        [Fact]
        public void Qvaci_MissingAshDensity_IsAMappingErrorInsteadOfZero()
        {
            XDocument doc = XDocument.Load("Samples/wmo-examples/qvaci-Example.xml");
            doc.Descendants(Iwxxm + "ashDensityEqualsOrExceeds").First().Remove();

            var result = MapDirectly(doc.ToString());

            Assert.True(result.IsFailure);
            Assert.Contains(result.Errors, e => e.Code == "ACL-ASH-DENSITY-MISSING");
        }

        [Fact]
        public void Qvaci_AshDensity_IsTheReportedValue()
        {
            XDocument doc = XDocument.Load("Samples/wmo-examples/qvaci-Example.xml");
            double expected = double.Parse(doc.Descendants(Iwxxm + "ashDensityEqualsOrExceeds").First().Value, CultureInfo.InvariantCulture);

            var qvaci = IwxxmParser.ParseFileAs<Qvaci>("Samples/wmo-examples/qvaci-Example.xml");

            Assert.True(qvaci.IsSuccess, string.Join(" | ", qvaci.Errors));
            Assert.Equal(expected, qvaci.Value.AshConcentrationClouds[0].AshDensityMgPerM3);
        }

        [Fact]
        public void Vona_NilVolcanoName_IsNullInsteadOfUnknown()
        {
            XDocument doc = XDocument.Load("Samples/wmo-examples/vona-A7-1.xml");
            XElement name = doc.Descendants(Iwxxm + "Volcano").First().Element(Iwxxm + "name")!;
            name.RemoveNodes();
            name.SetAttributeValue(XNamespace.Get("http://www.w3.org/2001/XMLSchema-instance") + "nil", "true");
            name.SetAttributeValue("nilReason", "unknown");

            var result = MapDirectly(doc.ToString());

            Assert.True(result.IsSuccess, string.Join(" | ", result.Errors));
            var vona = Assert.IsType<Vona>(result.Value);
            Assert.Null(Assert.Single(vona.Volcanoes).Name);
        }

        [Fact]
        public void Vona_MissingVolcanoName_IsAMappingError()
        {
            XDocument doc = XDocument.Load("Samples/wmo-examples/vona-A7-1.xml");
            doc.Descendants(Iwxxm + "Volcano").First().Element(Iwxxm + "name")!.Remove();

            var result = MapDirectly(doc.ToString());

            Assert.True(result.IsFailure);
            Assert.Contains(result.Errors, e => e.Code == "ACL-VOLCANO-NAME-MISSING");
        }

        [Fact]
        public void Bulletin_MetadataIsDerivedFromItsReports_NeverInvented()
        {
            var bulletin = IwxxmParser.ParseBulletinFile("Samples/wmo-examples/metar-NIL-collect.xml");
            Assert.True(bulletin.IsSuccess, string.Join(" | ", bulletin.Errors));

            IwxxmReport child = Assert.Single(bulletin.Value.Reports);
            Assert.Equal(child.IssueTimeUtc, bulletin.Value.IssueTimeUtc);
            Assert.NotEqual(DateTimeOffset.MinValue, bulletin.Value.IssueTimeUtc);
            Assert.Equal(child.ReportStatus, bulletin.Value.ReportStatus);
            Assert.Equal(child.PermissibleUsage, bulletin.Value.PermissibleUsage);
        }

        [Fact]
        public void Bulletin_WithANonOperationalReport_IsNonOperational()
        {
            IwxxmReport operational = CreateMetar("op", ReportStatus.Normal, PermissibleUsage.Operational, null, new DateTimeOffset(2026, 1, 1, 10, 0, 0, TimeSpan.Zero));
            IwxxmReport exercise = CreateMetar("ex", ReportStatus.Correction, PermissibleUsage.NonOperational, PermissibleUsageReason.Exercise, new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero));

            var bulletin = MeteorologicalBulletin.Create("b1", "A_TEST", new[]
            {
                Result<IwxxmReport>.Success(operational),
                Result<IwxxmReport>.Success(exercise)
            });

            Assert.True(bulletin.IsSuccess, string.Join(" | ", bulletin.Errors));
            Assert.Equal(PermissibleUsage.NonOperational, bulletin.Value.PermissibleUsage);
            Assert.Equal(PermissibleUsageReason.Exercise, bulletin.Value.PermissibleUsageReason);
            Assert.Equal(ReportStatus.Correction, bulletin.Value.ReportStatus);
            Assert.Equal(new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero), bulletin.Value.IssueTimeUtc);
        }

        [Fact]
        public void Bulletin_WithoutAnyReport_CannotBeCreated()
        {
            var bulletin = MeteorologicalBulletin.Create("b1", "A_TEST", Array.Empty<Result<IwxxmReport>>());

            Assert.True(bulletin.IsFailure);
            Assert.Equal("DOMAIN-BULLETIN-EMPTY", bulletin.Errors[0].Code);
        }

        [Fact]
        public void Factories_RejectUndefinedMetadataValues()
        {
            var result = Metar.Create(
                "m1", new DateTimeOffset(2026, 1, 1, 10, 0, 0, TimeSpan.Zero), null,
                default(ReportStatus), (PermissibleUsage)42, false, "YUDO", null, null);

            Assert.True(result.IsFailure);
            Assert.Contains(result.Errors, e => e.Code == "DOMAIN-REPORT-STATUS-INVALID");
            Assert.Contains(result.Errors, e => e.Code == "DOMAIN-PERMISSIBLE-USAGE-INVALID");
        }

        private static Metar CreateMetar(string id, ReportStatus status, PermissibleUsage usage, PermissibleUsageReason? reason, DateTimeOffset issueTime)
        {
            var result = Metar.Create(
                id, issueTime, issueTime, status, usage, false, "YUDO", null, null,
                permissibleUsageReason: reason);
            Assert.True(result.IsSuccess, string.Join(" | ", result.Errors));
            return result.Value;
        }

        private static Result<IwxxmDocument> MapDirectly(string xml)
        {
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(xml)))
            {
                return new IwxxmSuiteMapper().Map(stream);
            }
        }

        private static ReportStatus ExpectedStatus(string code) => code switch
        {
            "NORMAL" => ReportStatus.Normal,
            "AMENDMENT" => ReportStatus.Amendment,
            "CORRECTION" => ReportStatus.Correction,
            _ => throw new InvalidOperationException(code)
        };

        private static PermissibleUsage ExpectedUsage(string code) => code switch
        {
            "OPERATIONAL" => PermissibleUsage.Operational,
            "NON-OPERATIONAL" => PermissibleUsage.NonOperational,
            _ => throw new InvalidOperationException(code)
        };

        private static PermissibleUsageReason ExpectedReason(string code) => code switch
        {
            "TEST" => PermissibleUsageReason.Test,
            "EXERCISE" => PermissibleUsageReason.Exercise,
            _ => throw new InvalidOperationException(code)
        };

        private static DateTimeOffset? InlineInstant(XElement? property) =>
            Parse(property?.Element(Gml + "TimeInstant")?.Element(Gml + "timePosition")?.Value);

        private static DateTimeOffset? InlineBegin(XElement? property) =>
            Parse(property?.Element(Gml + "TimePeriod")?.Element(Gml + "beginPosition")?.Value);

        private static DateTimeOffset? InlineEnd(XElement? property) =>
            Parse(property?.Element(Gml + "TimePeriod")?.Element(Gml + "endPosition")?.Value);

        private static DateTimeOffset? Parse(string? text) =>
            string.IsNullOrWhiteSpace(text) ? (DateTimeOffset?)null : DateTimeOffset.Parse(text!, CultureInfo.InvariantCulture);

        /// <summary>
        /// Walks the public object graph of a report and records sentinel values that the previous mapper invented
        /// (<see cref="DateTimeOffset.MinValue"/>, the literal <c>UNKNOWN</c>).
        /// </summary>
        private static void Inspect(object? value, string path, List<string> offenders, HashSet<object> visited, int depth)
        {
            if (value == null || depth > 8)
                return;

            switch (value)
            {
                case DateTimeOffset dto:
                    if (dto == DateTimeOffset.MinValue) offenders.Add(path);
                    return;
                case string s:
                    if (string.Equals(s, "UNKNOWN", StringComparison.Ordinal)) offenders.Add(path + "=" + s);
                    return;
                case IEnumerable enumerable:
                    int index = 0;
                    foreach (object? item in enumerable)
                    {
                        Inspect(item, $"{path}[{index++}]", offenders, visited, depth + 1);
                    }
                    return;
            }

            Type type = value.GetType();
            if (type.IsPrimitive || type.IsEnum || value is decimal)
                return;

            if (!type.IsValueType && !visited.Add(value))
                return;

            if (type.Namespace != typeof(IwxxmReport).Namespace && !(type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Result<>)))
                return;

            foreach (PropertyInfo property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (property.GetIndexParameters().Length > 0)
                    continue;

                object? propertyValue;
                try
                {
                    propertyValue = property.GetValue(value);
                }
                catch (TargetInvocationException)
                {
                    continue; // e.g. Result<T>.Value on a failed result
                }

                Inspect(propertyValue, path + "." + property.Name, offenders, visited, depth + 1);
            }
        }
    }
}
