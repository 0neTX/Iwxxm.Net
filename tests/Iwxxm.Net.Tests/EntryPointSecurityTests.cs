using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Iwxxm.Net.Reports;
using Iwxxm.Net.Tests.DataFidelity;
using Xunit;

namespace Iwxxm.Net.Tests
{
    /// <summary>
    /// Negative security tests on every entry point (AUD-M21): DOCTYPE, external entities (XXE) and entity expansion
    /// (billion laughs) are rejected before any content is read; size limits report the specific <c>XML-SIZE-LIMIT</c> code;
    /// every public facade member is exercised.
    /// </summary>
    public sealed class EntryPointSecurityTests
    {
        private const string XxePayload =
            "<?xml version=\"1.0\"?><!DOCTYPE iwxxm:METAR [<!ENTITY xxe SYSTEM \"file:///etc/passwd\">]>" +
            "<iwxxm:METAR xmlns:iwxxm=\"http://icao.int/iwxxm/2025-2\">&xxe;</iwxxm:METAR>";

        private const string BillionLaughsPayload =
            "<?xml version=\"1.0\"?><!DOCTYPE lolz [<!ENTITY lol \"lol\">" +
            "<!ENTITY lol1 \"&lol;&lol;&lol;&lol;&lol;&lol;&lol;&lol;&lol;&lol;\">" +
            "<!ENTITY lol2 \"&lol1;&lol1;&lol1;&lol1;&lol1;&lol1;&lol1;&lol1;&lol1;&lol1;\">" +
            "<!ENTITY lol3 \"&lol2;&lol2;&lol2;&lol2;&lol2;&lol2;&lol2;&lol2;&lol2;&lol2;\">]>" +
            "<lolz>&lol3;</lolz>";

        private const string PlainDoctypePayload =
            "<?xml version=\"1.0\"?><!DOCTYPE iwxxm:WAFSSignificantWeatherForecast>" +
            "<iwxxm:WAFSSignificantWeatherForecast xmlns:iwxxm=\"http://icao.int/iwxxm/2025-2\"/>";

        public static IEnumerable<object[]> Payloads() => new[]
        {
            new object[] { "xxe", XxePayload },
            new object[] { "billion-laughs", BillionLaughsPayload },
            new object[] { "doctype", PlainDoctypePayload }
        };

        public static IEnumerable<object[]> EntryPoints() => new[]
        {
            new object[] { "Parse(string)" },
            new object[] { "Parse(Stream)" },
            new object[] { "ParseFile" },
            new object[] { "Process(TextReader)" },
            new object[] { "ParseAs(string)" },
            new object[] { "ParseBulletin(string)" },
            new object[] { "ParseBulletin(Stream)" },
            new object[] { "ParseBulletinFile" },
            new object[] { "ParseToGeoJson(string)" },
            new object[] { "ParseFileToGeoJson" },
            new object[] { "Sigwx.Parse(string)" },
            new object[] { "Sigwx.Parse(Stream)" },
            new object[] { "Sigwx.ParseFile" },
            new object[] { "Sigwx.Process(TextReader)" }
        };

        public static IEnumerable<object[]> PayloadsByEntryPoint()
        {
            foreach (object[] payload in Payloads())
            {
                foreach (object[] entry in EntryPoints())
                {
                    yield return new[] { entry[0], payload[0], payload[1] };
                }
            }
        }

        [Theory]
        [MemberData(nameof(PayloadsByEntryPoint))]
        public void DtdPayloads_AreRejectedAsMalformedInput_OnEveryEntryPoint(string entryPoint, string payloadName, string payload)
        {
            IReadOnlyList<ValidationError> errors = Invoke(entryPoint, payload, IwxxmParser.Default, IwxxmSigwxParser.Default);

            ValidationError error = Assert.Single(errors);
            Assert.Equal("XML-MALFORMED", error.Code);
            Assert.Equal(ErrorCategory.MalformedInput, error.Category);
            Assert.DoesNotContain("root:", error.Message, StringComparison.Ordinal);
            Assert.False(string.IsNullOrEmpty(payloadName));
        }

        [Theory]
        [MemberData(nameof(EntryPoints))]
        public void OversizedInput_IsReportedWithTheSizeLimitCode_OnEveryEntryPoint(string entryPoint)
        {
            var options = new IwxxmParserOptions(maxDocumentBytes: 512);
            var builder = new IwxxmParserBuilder().WithOptions(options);
            string metar = SampleDocuments.Text(entryPoint.StartsWith("Sigwx", StringComparison.Ordinal) ? "WAFS-Example.xml" : "metar-A3-1.xml");

            IReadOnlyList<ValidationError> errors = Invoke(entryPoint, metar, builder.Build(), builder.BuildSigwxParser());

            Assert.Equal("XML-SIZE-LIMIT", Assert.Single(errors).Code);
        }

        [Fact]
        public void OversizedNonSeekableStream_IsReportedWithTheSizeLimitCode()
        {
            IwxxmParser parser = new IwxxmParserBuilder().WithOptions(new IwxxmParserOptions(maxDocumentBytes: 512)).Build();
            using (var stream = new NonSeekableStream(Encoding.UTF8.GetBytes(SampleDocuments.Text("metar-A3-1.xml"))))
            {
                Assert.Equal("XML-SIZE-LIMIT", Assert.Single(parser.Process(stream).Errors).Code);
            }
        }

        [Fact]
        public void CharacterQuota_IsEnforcedByTheSharedParser()
        {
            IwxxmParser parser = new IwxxmParserBuilder().WithOptions(new IwxxmParserOptions(maxCharactersInDocument: 256)).Build();

            ValidationError error = Assert.Single(parser.Process(SampleDocuments.Text("metar-A3-1.xml")).Errors);

            Assert.Equal("XML-MALFORMED", error.Code);
            Assert.Equal(ErrorCategory.MalformedInput, error.Category);
        }

        [Fact]
        public void EveryPublicFacadeMember_ParsesTheOfficialExamples()
        {
            string metarPath = SampleDocuments.PathOf("metar-A3-1.xml");
            string bulletinPath = SampleDocuments.PathOf("metar-NIL-collect.xml");
            string wafsPath = SampleDocuments.PathOf("WAFS-Example.xml");
            string metar = File.ReadAllText(metarPath);
            string bulletin = File.ReadAllText(bulletinPath);
            IwxxmParser parser = IwxxmParser.Default;

            Assert.IsType<Metar>(IwxxmParser.ParseAs<Metar>(metar).Value);
            Assert.IsType<Metar>(IwxxmParser.ParseAs<Metar>(Open(metarPath)).Value);
            Assert.IsType<Metar>(IwxxmParser.ParseFileAs<Metar>(metarPath).Value);
            Assert.Equal("IWXXM-UNEXPECTED-REPORT-TYPE", Assert.Single(IwxxmParser.ParseAs<Taf>(metar).Errors).Code);
            Assert.IsType<Metar>(parser.ProcessAs<Metar>(Open(metarPath)).Value);
            Assert.IsType<Metar>(parser.ProcessFileAs<Metar>(metarPath).Value);

            Assert.True(IwxxmParser.ParseBulletin(Open(bulletinPath)).IsSuccess);
            Assert.True(IwxxmParser.ParseBulletinFile(bulletinPath).IsSuccess);
            Assert.True(parser.ProcessBulletin(bulletin).IsSuccess);
            Assert.True(parser.ProcessBulletin(new StringReader(bulletin)).IsSuccess);
            Assert.True(parser.ProcessBulletinFile(bulletinPath).IsSuccess);
            Assert.Equal("DISPATCH-NOT-A-BULLETIN", Assert.Single(parser.ProcessBulletin(metar).Errors).Code);

            string fromStream = IwxxmParser.ParseToGeoJson(Open(metarPath)).Value;
            Assert.Equal(fromStream, IwxxmParser.ParseToGeoJson(metar).Value);
            Assert.Equal(fromStream, IwxxmParser.ParseFileToGeoJson(metarPath).Value);
            Assert.Equal(fromStream, parser.ProcessToGeoJson(Open(metarPath)).Value);
            Assert.Equal(fromStream, parser.ProcessToGeoJson(metar).Value);
            Assert.Equal(fromStream, parser.ProcessFileToGeoJson(metarPath).Value);
            Assert.Equal(fromStream, IwxxmGeoJsonExporter.ToGeoJson(IwxxmParser.Parse(metar).Value).Value);
            Assert.Equal(fromStream, IwxxmGeoJsonExporter.Default.Export(IwxxmParser.Parse(metar).Value).Value);
            Assert.Contains("\n", IwxxmParser.ParseToGeoJson(metar, new GeoJsonExportOptions(indented: true)).Value, StringComparison.Ordinal);

            Assert.True(IwxxmSigwxParser.Parse(Open(wafsPath)).IsSuccess);
            Assert.True(IwxxmSigwxParser.ParseFile(wafsPath).IsSuccess);
            Assert.True(IwxxmSigwxParser.Default.ProcessFile(wafsPath).IsSuccess);
            Assert.True(IwxxmSigwxParser.Default.Process(new StringReader(File.ReadAllText(wafsPath))).IsSuccess);
        }

        [Fact]
        public void NullAndWhitespaceArguments_AreArgumentErrors_OrTypedFailures()
        {
            Assert.Throws<ArgumentNullException>(() => IwxxmParser.Default.Process((Stream)null!));
            Assert.Throws<ArgumentNullException>(() => IwxxmParser.Default.Process((TextReader)null!));
            Assert.Throws<ArgumentNullException>(() => IwxxmSigwxParser.Default.Process((TextReader)null!));
            Assert.Throws<ArgumentException>(() => IwxxmParser.ParseFile(" "));
            Assert.Equal("XML-EMPTY-STRING", Assert.Single(IwxxmParser.Parse("  ").Errors).Code);
            Assert.Equal("XML-EMPTY-STRING", Assert.Single(IwxxmParser.Default.Process(new StringReader("  ")).Errors).Code);
        }

        private static IReadOnlyList<ValidationError> Invoke(string entryPoint, string xml, IwxxmParser parser, IwxxmSigwxParser sigwx)
        {
            string path = Path.Combine(Path.GetTempPath(), "iwxxm-entry-" + Guid.NewGuid().ToString("N") + ".xml");
            File.WriteAllText(path, xml);
            try
            {
                switch (entryPoint)
                {
                    case "Parse(string)": return parser.Process(xml).Errors;
                    case "Parse(Stream)": return parser.Process(Utf8(xml)).Errors;
                    case "ParseFile": return parser.ProcessFile(path).Errors;
                    case "Process(TextReader)": return parser.Process(new StringReader(xml)).Errors;
                    case "ParseAs(string)": return parser.ProcessAs<Metar>(xml).Errors;
                    case "ParseBulletin(string)": return parser.ProcessBulletin(xml).Errors;
                    case "ParseBulletin(Stream)": return parser.ProcessBulletin(Utf8(xml)).Errors;
                    case "ParseBulletinFile": return parser.ProcessBulletinFile(path).Errors;
                    case "ParseToGeoJson(string)": return parser.ProcessToGeoJson(xml).Errors;
                    case "ParseFileToGeoJson": return parser.ProcessFileToGeoJson(path).Errors;
                    case "Sigwx.Parse(string)": return sigwx.Process(xml).Errors;
                    case "Sigwx.Parse(Stream)": return sigwx.Process(Utf8(xml)).Errors;
                    case "Sigwx.ParseFile": return sigwx.ProcessFile(path).Errors;
                    case "Sigwx.Process(TextReader)": return sigwx.Process(new StringReader(xml)).Errors;
                    default: throw new ArgumentOutOfRangeException(nameof(entryPoint), entryPoint, null);
                }
            }
            finally
            {
                File.Delete(path);
            }
        }

        private static Stream Utf8(string xml) => new MemoryStream(Encoding.UTF8.GetBytes(xml));

        private static Stream Open(string path) => new MemoryStream(File.ReadAllBytes(path));

        private sealed class NonSeekableStream : MemoryStream
        {
            public NonSeekableStream(byte[] buffer)
                : base(buffer)
            {
            }

            public override bool CanSeek => false;

            public override long Length => throw new NotSupportedException();
        }
    }
}
