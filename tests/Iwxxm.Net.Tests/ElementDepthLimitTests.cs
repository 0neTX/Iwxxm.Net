using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using Iwxxm.Net.Extensibility;
using Iwxxm.Net.Infrastructure.Validation;
using Iwxxm.Net.Infrastructure.Xml;
using Iwxxm.Net.Reports;
using Xunit;

namespace Iwxxm.Net.Tests
{
    /// <summary>
    /// AUD-N1: a schema-valid but deeply nested document (a <c>collect:MeteorologicalBulletin</c> may contain itself) used to
    /// overflow the stack of <c>XDocument.Validate</c> (about 24,500 levels, an uncatchable process crash). The nesting is now
    /// limited by <see cref="IwxxmParserOptions.MaxElementDepth"/> (default 128, root element = depth 1) in the single secure
    /// read, before any tree is built: these tests exercise the limit on every entry point. Heavy payloads are generated
    /// lazily by a stream and never held as strings.
    /// </summary>
    public sealed class ElementDepthLimitTests
    {
        private const string CollectNs = "http://def.wmo.int/collect/2014";
        private const string GmlNs = "http://www.opengis.net/gml/3.2";
        private const string Header = "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<c:B xmlns:c=\"" + CollectNs + "\" xmlns:gml=\"" + GmlNs + "\" gml:id=\"b0\">";

        // ---------------------------------------------------------------- options

        [Fact]
        public void Default_Is128_AndTheValueMustBePositive()
        {
            Assert.Equal(128, IwxxmParserOptions.Default.MaxElementDepth);
            Assert.Equal(7, new IwxxmParserOptions(maxElementDepth: 7).MaxElementDepth);
            Assert.Throws<ArgumentOutOfRangeException>(() => new IwxxmParserOptions(maxElementDepth: 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new IwxxmParserOptions(maxElementDepth: -1));
        }

        [Fact]
        public void Builder_PassesTheLimitToBothFacades()
        {
            var options = new IwxxmParserOptions(maxElementDepth: 5);
            var builder = new IwxxmParserBuilder().WithOptions(options);
            Assert.Equal(5, builder.Options.MaxElementDepth);

            Assert.Equal("XML-DEPTH-LIMIT", Assert.Single(builder.Build().Process(Sample("metar-A3-1.xml")).Errors).Code);
            Assert.Equal("XML-DEPTH-LIMIT", Assert.Single(builder.BuildSigwxParser().Process(Sample("WAFS-Example.xml")).Errors).Code);
        }

        // ---------------------------------------------------------------- the boundary on every entry point

        public static IEnumerable<object[]> EntryPoints() => new[]
        {
            new object[] { "Parse(string)" },
            new object[] { "Parse(Stream)" },
            new object[] { "Parse(non-seekable Stream)" },
            new object[] { "Process(TextReader)" },
            new object[] { "ParseFile" },
            new object[] { "ParseAs<T>(string)" },
            new object[] { "ParseBulletin(string)" },
            new object[] { "ParseBulletin(Stream)" },
            new object[] { "ParseToGeoJson(string)" },
            new object[] { "Sigwx.Parse(string)" },
            new object[] { "Sigwx.Parse(Stream)" },
            new object[] { "Sigwx.Parse(non-seekable Stream)" },
            new object[] { "Sigwx.ParseFile" }
        };

        [Theory]
        [MemberData(nameof(EntryPoints))]
        public void DepthExactlyAtTheLimit_PassesTheDepthCheck(string entryPoint)
        {
            IReadOnlyList<ValidationError> errors = Invoke(entryPoint, Nest(128), IwxxmParser.Default, IwxxmSigwxParser.Default);

            // The synthetic nest is not a valid report, but the depth gate must let it through.
            Assert.DoesNotContain(errors, e => e.Code == "XML-DEPTH-LIMIT");
            Assert.NotEmpty(errors);
            Assert.NotEqual("XML-MALFORMED", errors[0].Code);
        }

        [Theory]
        [MemberData(nameof(EntryPoints))]
        public void DepthOneOverTheLimit_IsRejected_OnEveryEntryPoint(string entryPoint)
        {
            IReadOnlyList<ValidationError> errors = Invoke(entryPoint, Nest(129), IwxxmParser.Default, IwxxmSigwxParser.Default);

            ValidationError error = Assert.Single(errors);
            Assert.Equal("XML-DEPTH-LIMIT", error.Code);
            Assert.Equal(ErrorCategory.MalformedInput, error.Category);
            Assert.Contains("128", error.Message, StringComparison.Ordinal);
            Assert.True(error.LineNumber > 0, "the line is reported");
            Assert.True(error.LinePosition > 0, "the position is reported");
        }

        [Theory]
        [MemberData(nameof(EntryPoints))]
        public void CustomLowerLimit_IsEnforced_OnEveryEntryPoint(string entryPoint)
        {
            var builder = new IwxxmParserBuilder().WithOptions(new IwxxmParserOptions(maxElementDepth: 10));

            IReadOnlyList<ValidationError> over = Invoke(entryPoint, Nest(11), builder.Build(), builder.BuildSigwxParser());
            IReadOnlyList<ValidationError> at = Invoke(entryPoint, Nest(10), builder.Build(), builder.BuildSigwxParser());

            ValidationError error = Assert.Single(over);
            Assert.Equal("XML-DEPTH-LIMIT", error.Code);
            Assert.Contains("10", error.Message, StringComparison.Ordinal);
            Assert.DoesNotContain(at, e => e.Code == "XML-DEPTH-LIMIT");
        }

        [Fact]
        public void ARealValidReport_SucceedsExactlyAtItsOwnDepth_AndFailsOneLevelBelow()
        {
            string metar = Sample("metar-A3-1.xml");
            int depth = DepthOf(XDocument.Parse(metar).Root!);

            var exact = new IwxxmParserBuilder().WithOptions(new IwxxmParserOptions(maxElementDepth: depth)).Build();
            var below = new IwxxmParserBuilder().WithOptions(new IwxxmParserOptions(maxElementDepth: depth - 1)).Build();

            Assert.True(exact.Process(metar).IsSuccess);
            Assert.Equal("XML-DEPTH-LIMIT", Assert.Single(below.Process(metar).Errors).Code);
        }

        // ---------------------------------------------------------------- direct use of the public ports

        [Fact]
        public void XsdValidator_InvokedDirectly_ReportsTheDepthLimit()
        {
            var context = new IwxxmDocumentContext(Encoding.UTF8.GetBytes(Nest(129)));

            ValidationError error = Assert.Single(XsdValidator.CreateFullSuiteValidator(IwxxmParserOptions.Default).Validate(context));

            Assert.Equal("XML-DEPTH-LIMIT", error.Code);
            Assert.Equal(ErrorCategory.MalformedInput, error.Category);
        }

        [Fact]
        public void XsltSchematronValidator_InvokedDirectly_ReportsTheDepthLimit()
        {
            var context = new IwxxmDocumentContext(Encoding.UTF8.GetBytes(Nest(129)));

            ValidationError error = Assert.Single(new XsltSchematronValidator(IwxxmParserOptions.Default).Validate(context));

            Assert.Equal("XML-DEPTH-LIMIT", error.Code);
            Assert.True(error.LineNumber > 0);
        }

        [Fact]
        public void SecureXmlParser_ReportsTheDepthLimit_ForAContextBuiltWithItsOwnOptions()
        {
            var context = new IwxxmDocumentContext(Encoding.UTF8.GetBytes(Nest(4)), new IwxxmParserOptions(maxElementDepth: 3));

            Assert.Equal("XML-DEPTH-LIMIT", Assert.Single(SecureXml.Parser.Parse(context)).Code);
            Assert.Empty(SecureXml.Parser.Parse(new IwxxmDocumentContext(Encoding.UTF8.GetBytes(Nest(3)), new IwxxmParserOptions(maxElementDepth: 3))));
        }

        // ---------------------------------------------------------------- real documents are far below the limit

        [Fact]
        public void EveryOfficialExample_PassesTheDepthCheck()
        {
            string[] files = Directory.GetFiles(Path.Combine("Samples", "wmo-examples"), "*.xml");
            Assert.Equal(29, files.Length);

            foreach (string file in files)
            {
                var context = new IwxxmDocumentContext(File.ReadAllBytes(file));
                Assert.True(SecureXml.Parser.Parse(context).Count == 0, file);

                int depth = DepthOf(SecureXml.Load(context).Root);
                Assert.True(depth <= 24, file + " nests " + depth + " levels");
            }
        }

        [Fact]
        public void TheMetOfficeSample_PassesTheDepthCheck_AtDepth16()
        {
            var context = new IwxxmDocumentContext(File.ReadAllBytes("Samples/external/metoffice/NEW(2025-2)SIGWX-260826T0600-06-48.xml"));

            Assert.Empty(SecureXml.Parser.Parse(context));
            Assert.Equal(16, DepthOf(SecureXml.Load(context).Root));
        }

        // ---------------------------------------------------------------- crash payloads are rejected, fast

        [Fact]
        public void TheFormerCrashPayload_25000Levels_NowReturnsAFailure()
        {
            // Run on the code before AUD-N1 was fixed, this schema-valid nest overflowed the stack inside XDocument.Validate
            // and killed the process; it is now stopped by the streaming depth check before any tree exists.
            using (var stream = new NestedStream(25000, seekable: true))
            {
                var stopwatch = Stopwatch.StartNew();
                Result<IwxxmDocument> result = IwxxmParser.Default.Process(stream);
                stopwatch.Stop();

                Assert.True(result.IsFailure);
                Assert.Equal("XML-DEPTH-LIMIT", Assert.Single(result.Errors).Code);
                Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(5), "took " + stopwatch.Elapsed);
            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void HundredThousandNestedBulletins_AreRejectedQuickly(bool seekable)
        {
            using (var stream = new NestedStream(100000, seekable))
            {
                var stopwatch = Stopwatch.StartNew();
                Result<IwxxmDocument> result = IwxxmParser.Default.Process(stream);
                stopwatch.Stop();

                Assert.Equal("XML-DEPTH-LIMIT", Assert.Single(result.Errors).Code);
                Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(5), "took " + stopwatch.Elapsed);
            }

            using (var stream = new NestedStream(100000, seekable))
            {
                Assert.Equal("XML-DEPTH-LIMIT", Assert.Single(IwxxmSigwxParser.Default.Process(stream).Errors).Code);
            }
        }

#if !NETFRAMEWORK
        [Fact]
        public void OneMillionNestedElements_WithinTheSizeLimit_AreRejectedInBoundedTime()
        {
            using (var stream = new NestedStream(1000000, seekable: false))
            {
                var stopwatch = Stopwatch.StartNew();
                Result<IwxxmDocument> result = IwxxmParser.Default.Process(stream);
                stopwatch.Stop();

                Assert.True(stream.TotalLength < IwxxmParserOptions.Default.MaxDocumentBytes, "the payload is within the size limit");
                Assert.Equal("XML-DEPTH-LIMIT", Assert.Single(result.Errors).Code);
                Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(5), "took " + stopwatch.Elapsed);
            }
        }
#endif

        // ---------------------------------------------------------------- helpers

        private static string Sample(string fileName) => File.ReadAllText(Path.Combine("Samples", "wmo-examples", fileName));

        private static int DepthOf(XElement root) => root.DescendantsAndSelf().Max(e => e.AncestorsAndSelf().Count());

        /// <summary>
        /// A well-formed document whose deepest element has exactly <paramref name="depth"/> levels (root = 1): nested
        /// <c>collect:MeteorologicalBulletin</c> / <c>collect:meteorologicalInformation</c> pairs, the shape of the crash payload.
        /// </summary>
        private static string Nest(int depth)
        {
            var builder = new StringBuilder(Header);
            var open = new Stack<string>();
            open.Push("c:B");
            for (int level = 2; level <= depth; level++)
            {
                string name = level % 2 == 0 ? "c:M" : "c:B";
                builder.Append('<').Append(name).Append('>');
                open.Push(name);
            }

            while (open.Count > 0)
            {
                builder.Append("</").Append(open.Pop()).Append('>');
            }

            return builder.ToString();
        }

        private static IReadOnlyList<ValidationError> Invoke(string entryPoint, string xml, IwxxmParser parser, IwxxmSigwxParser sigwx)
        {
            string path = Path.Combine(Path.GetTempPath(), "iwxxm-depth-" + Guid.NewGuid().ToString("N") + ".xml");
            File.WriteAllText(path, xml);
            try
            {
                switch (entryPoint)
                {
                    case "Parse(string)": return parser.Process(xml).Errors;
                    case "Parse(Stream)": return parser.Process(Utf8(xml)).Errors;
                    case "Parse(non-seekable Stream)": return parser.Process(new NonSeekable(xml)).Errors;
                    case "Process(TextReader)": return parser.Process(new StringReader(xml)).Errors;
                    case "ParseFile": return parser.ProcessFile(path).Errors;
                    case "ParseAs<T>(string)": return parser.ProcessAs<Metar>(xml).Errors;
                    case "ParseBulletin(string)": return parser.ProcessBulletin(xml).Errors;
                    case "ParseBulletin(Stream)": return parser.ProcessBulletin(Utf8(xml)).Errors;
                    case "ParseToGeoJson(string)": return parser.ProcessToGeoJson(xml).Errors;
                    case "Sigwx.Parse(string)": return sigwx.Process(xml).Errors;
                    case "Sigwx.Parse(Stream)": return sigwx.Process(Utf8(xml)).Errors;
                    case "Sigwx.Parse(non-seekable Stream)": return sigwx.Process(new NonSeekable(xml)).Errors;
                    case "Sigwx.ParseFile": return sigwx.ProcessFile(path).Errors;
                    default: throw new ArgumentOutOfRangeException(nameof(entryPoint), entryPoint, null);
                }
            }
            finally
            {
                File.Delete(path);
            }
        }

        private static Stream Utf8(string xml) => new MemoryStream(Encoding.UTF8.GetBytes(xml));

        private sealed class NonSeekable : MemoryStream
        {
            public NonSeekable(string xml)
                : base(Encoding.UTF8.GetBytes(xml))
            {
            }

            public override bool CanSeek => false;

            public override long Length => throw new NotSupportedException();
        }

        /// <summary>
        /// A well-formed, schema-shaped nest of about <c>levels</c> alternating <c>collect:MeteorologicalBulletin</c> /
        /// <c>collect:meteorologicalInformation</c> elements (the crash payload shape), generated on demand: the payload
        /// is never held in memory.
        /// </summary>
        private sealed class NestedStream : Stream
        {
            private static readonly byte[] OpenTags = Encoding.ASCII.GetBytes("<c:M><c:B>");
            private static readonly byte[] CloseTags = Encoding.ASCII.GetBytes("</c:B></c:M>");
            private static readonly byte[] EndTag = Encoding.ASCII.GetBytes("</c:B>");

            private readonly byte[] _header = Encoding.UTF8.GetBytes(Header);
            private readonly int _opens;
            private readonly bool _seekable;
            private long _position;

            public NestedStream(int levels, bool seekable)
            {
                _opens = levels / 2;
                _seekable = seekable;
                TotalLength = _header.Length + ((long)_opens * (OpenTags.Length + CloseTags.Length)) + EndTag.Length;
            }

            public long TotalLength { get; }

            public override bool CanRead => true;

            public override bool CanSeek => _seekable;

            public override bool CanWrite => false;

            public override long Length => _seekable ? TotalLength : throw new NotSupportedException();

            public override long Position
            {
                get => _seekable ? _position : throw new NotSupportedException();
                set => throw new NotSupportedException();
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                int n = 0;
                while (n < count && _position < TotalLength)
                {
                    buffer[offset + n++] = ByteAt(_position++);
                }

                return n;
            }

            private byte ByteAt(long position)
            {
                if (position < _header.Length)
                    return _header[position];

                position -= _header.Length;
                long openBytes = (long)_opens * OpenTags.Length;
                if (position < openBytes)
                    return OpenTags[(int)(position % OpenTags.Length)];

                position -= openBytes;
                long closeBytes = (long)_opens * CloseTags.Length;
                return position < closeBytes ? CloseTags[(int)(position % CloseTags.Length)] : EndTag[(int)(position - closeBytes)];
            }

            public override void Flush()
            {
            }

            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

            public override void SetLength(long value) => throw new NotSupportedException();

            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        }
    }
}
