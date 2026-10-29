using System;
using System.IO;
using System.Linq;
using Iwxxm.Net.Reports;
using Xunit;

namespace Iwxxm.Net.Tests
{
    /// <summary>
    /// Contract tests for <see cref="IIwxxmReportVisitor{TResult}"/> and <see cref="IIwxxmReport.Accept{TResult}"/>:
    /// every concrete report dispatches to the overload of its own type.
    /// </summary>
    public sealed class ReportVisitorTests
    {
        [Fact]
        public void Visitor_DeclaresOneVisitOverloadPerConcreteReportType()
        {
            var reportTypes = typeof(IIwxxmReport).Assembly.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract && typeof(IIwxxmReport).IsAssignableFrom(t))
                .OrderBy(t => t.Name, StringComparer.Ordinal)
                .ToList();

            var visitedTypes = typeof(IIwxxmReportVisitor<>).GetMethods()
                .Where(m => m.Name == nameof(IIwxxmReportVisitor<int>.Visit))
                .Select(m => m.GetParameters().Single().ParameterType)
                .OrderBy(t => t.Name, StringComparer.Ordinal)
                .ToList();

            Assert.Equal(11, reportTypes.Count);
            Assert.Equal(reportTypes, visitedTypes);
        }

        [Theory]
        [MemberData(nameof(IwxxmSuiteParserTests.GetAllOfficialWmoExamples), MemberType = typeof(IwxxmSuiteParserTests))]
        public void Accept_DispatchesToTheOverloadOfTheConcreteType(string xmlFilePath)
        {
            var result = IwxxmParser.ParseFile(xmlFilePath);
            Assert.True(result.IsSuccess, string.Join(" | ", result.Errors));

            Assert.Equal(result.Value.GetType(), result.Value.Accept(new TypeRecordingVisitor()));
        }

        [Fact]
        public void Accept_CompactSigwxReport_DispatchesToSignificantWeatherOverload()
        {
            var result = IwxxmSigwxParser.ParseFile(Path.Combine("Samples", "sigwx-swh-2025-2.xml"));
            Assert.True(result.IsSuccess, string.Join(" | ", result.Errors));

            Assert.Equal(typeof(SignificantWeatherReport), result.Value.Accept(new TypeRecordingVisitor()));
        }

        [Fact]
        public void Accept_NullVisitor_ThrowsArgumentNullException()
        {
            var result = IwxxmParser.ParseFile(Path.Combine("Samples", "wmo-examples", "metar-A3-1.xml"));
            Assert.True(result.IsSuccess, string.Join(" | ", result.Errors));

            Assert.Throws<ArgumentNullException>(() => result.Value.Accept<Type>(null!));
        }

        private sealed class TypeRecordingVisitor : IIwxxmReportVisitor<Type>
        {
            public Type Visit(AerodromeWeatherReport report) => typeof(AerodromeWeatherReport);
            public Type Visit(TafReport report) => typeof(TafReport);
            public Type Visit(SigmetReport report) => typeof(SigmetReport);
            public Type Visit(AirmetReport report) => typeof(AirmetReport);
            public Type Visit(VonaReport report) => typeof(VonaReport);
            public Type Visit(QvaciReport report) => typeof(QvaciReport);
            public Type Visit(VolcanicAshAdvisoryReport report) => typeof(VolcanicAshAdvisoryReport);
            public Type Visit(TropicalCycloneAdvisoryReport report) => typeof(TropicalCycloneAdvisoryReport);
            public Type Visit(SpaceWeatherAdvisoryReport report) => typeof(SpaceWeatherAdvisoryReport);
            public Type Visit(SignificantWeatherReport report) => typeof(SignificantWeatherReport);
            public Type Visit(MeteorologicalBulletin bulletin) => typeof(MeteorologicalBulletin);
        }
    }
}
