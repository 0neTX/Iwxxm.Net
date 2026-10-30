using System;
using System.IO;
using System.Linq;
using Iwxxm.Net.Reports;
using Xunit;

namespace Iwxxm.Net.Tests
{
    /// <summary>
    /// Contract tests for <see cref="IIwxxmDocumentVisitor{TResult}"/> and <see cref="IwxxmDocument.Accept{TResult}"/>:
    /// every concrete document (report or bulletin) dispatches to the overload of its own type.
    /// </summary>
    public sealed class ReportVisitorTests
    {
        [Fact]
        public void Visitor_DeclaresOneVisitOverloadPerConcreteDocumentType()
        {
            var documentTypes = typeof(IwxxmDocument).Assembly.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract && typeof(IwxxmDocument).IsAssignableFrom(t))
                .OrderBy(t => t.Name, StringComparer.Ordinal)
                .ToList();

            var visitedTypes = typeof(IIwxxmDocumentVisitor<>).GetMethods()
                .Where(m => m.Name == nameof(IIwxxmDocumentVisitor<int>.Visit))
                .Select(m => m.GetParameters().Single().ParameterType)
                .OrderBy(t => t.Name, StringComparer.Ordinal)
                .ToList();

            Assert.Equal(12, documentTypes.Count);
            Assert.Equal(documentTypes, visitedTypes);
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
        public void Accept_CompactSigwxReport_DispatchesToSigwxOverload()
        {
            var result = IwxxmSigwxParser.ParseFile(Path.Combine("Samples", "sigwx-swh-2025-2.xml"));
            Assert.True(result.IsSuccess, string.Join(" | ", result.Errors));

            Assert.Equal(typeof(Sigwx), result.Value.Accept(new TypeRecordingVisitor()));
        }

        [Fact]
        public void Accept_NullVisitor_ThrowsArgumentNullException()
        {
            var result = IwxxmParser.ParseFile(Path.Combine("Samples", "wmo-examples", "metar-A3-1.xml"));
            Assert.True(result.IsSuccess, string.Join(" | ", result.Errors));

            Assert.Throws<ArgumentNullException>(() => result.Value.Accept<Type>(null!));
        }

        private sealed class TypeRecordingVisitor : IIwxxmDocumentVisitor<Type>
        {
            public Type Visit(Metar metar) => typeof(Metar);
            public Type Visit(Speci speci) => typeof(Speci);
            public Type Visit(Taf report) => typeof(Taf);
            public Type Visit(Sigmet report) => typeof(Sigmet);
            public Type Visit(Airmet report) => typeof(Airmet);
            public Type Visit(Vona report) => typeof(Vona);
            public Type Visit(Qvaci report) => typeof(Qvaci);
            public Type Visit(Vaa report) => typeof(Vaa);
            public Type Visit(Tca report) => typeof(Tca);
            public Type Visit(SpaceWx report) => typeof(SpaceWx);
            public Type Visit(Sigwx report) => typeof(Sigwx);
            public Type Visit(MeteorologicalBulletin bulletin) => typeof(MeteorologicalBulletin);
        }
    }
}
