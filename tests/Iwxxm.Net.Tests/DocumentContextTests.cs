using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using Iwxxm.Net.Application.Ports.Out;
using Iwxxm.Net.Extensibility;
using Iwxxm.Net.Infrastructure.Mapping;
using Iwxxm.Net.Infrastructure.Validation;
using Iwxxm.Net.Infrastructure.Xml;
using Iwxxm.Net.Reports;
using Iwxxm.Net.Tests.DataFidelity;
using Xunit;

namespace Iwxxm.Net.Tests
{
    /// <summary>
    /// The facade reads the input once and hands a neutral, sanitized <see cref="IwxxmDocumentContext"/> to the ports
    /// (HEX-01, AUD-N3, AUD-L5); the facades keep their convenience members apart from the port contract (HEX-02).
    /// </summary>
    public sealed class DocumentContextTests
    {
        [Fact]
        public void Context_IsParsedOnce_AndSharedByTheValidatorAndTheMapper()
        {
            var context = new IwxxmDocumentContext(File.ReadAllBytes(SampleDocuments.PathOf("metar-A3-1.xml")));

            Assert.Empty(XsdValidator.CreateFullSuiteValidator().Validate(context));
            ParsedXml afterValidation = SecureXml.Load(context);
            Assert.True(new IwxxmSuiteMapper().Map(context).IsSuccess);

            Assert.Same(afterValidation, SecureXml.Load(context));
        }

        [Fact]
        public void Context_CopiesTheCallerBuffer_AndOpensIndependentReadOnlyStreams()
        {
            byte[] bytes = Encoding.UTF8.GetBytes("<a/>");
            var context = new IwxxmDocumentContext(bytes);
            bytes[1] = (byte)'b';

            using (Stream first = context.OpenRead())
            using (Stream second = context.OpenRead())
            {
                Assert.False(first.CanWrite);
                first.ReadByte();
                Assert.Equal('a', (char)first.ReadByte());
                Assert.Equal(0, second.Position);
            }

            Assert.Equal(4, context.Length);
            Assert.Same(IwxxmParserOptions.Default, context.Options);
        }

        [Fact]
        public void EmbeddedSigwx_IsMappedFromTheParsedElement_WithoutReserialization()
        {
            XElement root = XDocument.Load(SampleDocuments.PathOf("WAFS-Example.xml")).Root!;
            IwxxmDocumentContext embedded = SecureXml.ForElement(root, IwxxmParserOptions.Default);

            Result<Sigwx> result = new SigwxDocumentMapper().Map(embedded);

            Assert.True(result.IsSuccess, string.Join(" | ", result.Errors));
            Assert.False(embedded.IsMaterialized);
        }

        [Fact]
        public void EmbeddedContext_ProducesItsBytes_WhenACustomAdapterAsksForThem()
        {
            XElement root = XDocument.Load(SampleDocuments.PathOf("WAFS-Example.xml")).Root!;
            IwxxmDocumentContext embedded = SecureXml.ForElement(root, IwxxmParserOptions.Default);

            using (Stream stream = embedded.OpenRead())
            {
                Assert.Equal(root.Name, XDocument.Load(stream).Root!.Name);
            }

            Assert.True(embedded.IsMaterialized);
        }

        [Fact]
        public void MalformedInput_IsRejectedOnceByTheFacade_BeforeAnyAdapter()
        {
            var validator = new CountingValidator();
            IwxxmParser parser = new IwxxmParserBuilder().UseSchemaValidator(validator).UseSchematronValidator(validator).Build();

            Result<IwxxmDocument> result = parser.Process("<iwxxm:METAR xmlns:iwxxm=\"http://icao.int/iwxxm/2025-2\">");

            Assert.Equal("XML-MALFORMED", Assert.Single(result.Errors).Code);
            Assert.Equal(ErrorCategory.MalformedInput, result.Errors[0].Category);
            Assert.Equal(0, validator.Calls);
        }

        [Fact]
        public void GeoJsonExporter_RejectsNullDocument_AndAppliesDefaultOptionsWhenNone()
        {
            var exporter = new CapturingExporter();
            IwxxmGeoJsonExporter facade = new IwxxmParserBuilder().UseGeoJsonExporter(exporter).BuildGeoJsonExporter();
            IwxxmDocument document = IwxxmParser.Default.Process(new MemoryStream(File.ReadAllBytes(SampleDocuments.PathOf("metar-A3-1.xml")))).Value;

            Assert.Throws<ArgumentNullException>(() => facade.Export(null!));
            Assert.Equal(0, exporter.Calls);

            facade.Export(document);
            Assert.Same(GeoJsonExportOptions.Default, exporter.LastOptions);

            IwxxmParser parser = new IwxxmParserBuilder().UseGeoJsonExporter(exporter).Build();
            Assert.True(parser.ProcessToGeoJson(File.ReadAllText(SampleDocuments.PathOf("metar-A3-1.xml"))).IsSuccess);
            Assert.Equal(2, exporter.Calls);
            Assert.Same(GeoJsonExportOptions.Default, exporter.LastOptions);
        }

        private sealed class CapturingExporter : IGeoJsonExporter
        {
            public int Calls { get; private set; }

            public GeoJsonExportOptions? LastOptions { get; private set; }

            public string Export(IwxxmDocument document, GeoJsonExportOptions options)
            {
                Calls++;
                LastOptions = options;
                return "{}";
            }
        }

        private sealed class CountingValidator : ISchemaValidator, ISchematronValidator
        {
            public int Calls { get; private set; }

            public System.Collections.Generic.IReadOnlyList<ValidationError> Validate(IwxxmDocumentContext document)
            {
                Calls++;
                return Array.Empty<ValidationError>();
            }
        }
    }
}
