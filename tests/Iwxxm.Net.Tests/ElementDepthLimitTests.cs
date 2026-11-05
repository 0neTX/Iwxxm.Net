using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using Iwxxm.Net.Extensibility;
using Iwxxm.Net.Infrastructure.Validation;
using Iwxxm.Net.Infrastructure.Xml;
using Iwxxm.Net.Reports;
using Xunit;
using Xunit.Abstractions;

namespace Iwxxm.Net.Tests
{
    /// <summary>
    /// AUD-N1: a schema-valid but deeply nested document (a <c>collect:MeteorologicalBulletin</c> may contain itself through
    /// <c>collect:meteorologicalInformation</c>) used to overflow the stack of <c>XDocument.Validate</c> (about 25,000 levels,
    /// an uncatchable process crash). The nesting is now limited by <see cref="IwxxmParserOptions.MaxElementDepth"/> (default
    /// 128, root element = depth 1) in the single secure read, before any tree is built: these tests exercise the limit on
    /// every entry point. The payloads have the real shape (bulletins with their required <c>gml:id</c> and
    /// <c>collect:bulletinIdentifier</c>) and are generated lazily by a stream. NEVER feed an over-deep document to
    /// <c>XDocument.Validate</c> in a test: it would kill the test host, so every deep case runs with the default or a lower limit
    /// (the only raised limit is used for a payload that is at most a few hundred levels deep).
    /// </summary>
    public sealed class ElementDepthLimitTests
    {
        private const string CollectNs = "http://def.wmo.int/collect/2014";
        private const string GmlNs = "http://www.opengis.net/gml/3.2";
        private const string Prolog = "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n";
        private const string RootNamespaces = " xmlns:collect=\"" + CollectNs + "\" xmlns:gml=\"" + GmlNs + "\"";
        private const string BulletinClose = "</collect:meteorologicalInformation><collect:bulletinIdentifier>b</collect:bulletinIdentifier></collect:MeteorologicalBulletin>";

        /// <summary>The documented default limit; the one test that pins it uses this literal, every other test derives from the live option.</summary>
        private const int DocumentedDefaultDepth = 128;

        private const int CustomLimit = 10;
        private const int FormerCrashDepth = 25000;
        private const int HundredThousandDepth = 100000;
        private const int OneMillionDepth = 1000000;

        // A schema-valid nest that is raised-limit friendly: 200 bulletins = 400 levels plus the embedded report.
        private const int SchemaValidBulletins = 200;
        private const int RaisedDepthLimit = 1000;

        private static readonly int Limit = IwxxmParserOptions.Default.MaxElementDepth;
        private static readonly string LimitText = Limit.ToString(CultureInfo.InvariantCulture);
        private static readonly TimeSpan RejectionBudget = TimeSpan.FromSeconds(5);

        private readonly ITestOutputHelper _output;

        public ElementDepthLimitTests(ITestOutputHelper output) => _output = output;

        // ---------------------------------------------------------------- options

        [Fact]
        public void Default_Is128_AndTheValueMustBePositive()
        {
            Assert.Equal(DocumentedDefaultDepth, IwxxmParserOptions.Default.MaxElementDepth);
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
        public void DepthExactlyAtTheLimit_PassesTheDepthGate_AndFailsDownstreamWithTheExpectedCode(string entryPoint)
        {
            // The synthetic nest (innermost meteorologicalInformation empty) is schema-invalid on purpose, so the failure that
            // follows the depth gate is specific and deterministic for each entry point.
            IReadOnlyList<ValidationError> errors = Invoke(entryPoint, Nest(Limit), IwxxmParser.Default, IwxxmSigwxParser.Default);

            Assert.DoesNotContain(errors, e => e.Code == "XML-DEPTH-LIMIT");
            Assert.NotEmpty(errors);
            Assert.Equal(ExpectedCodeAtTheLimit(entryPoint), errors[0].Code);
        }

        [Theory]
        [MemberData(nameof(EntryPoints))]
        public void DepthOneOverTheLimit_IsRejected_OnEveryEntryPoint(string entryPoint)
        {
            IReadOnlyList<ValidationError> errors = Invoke(entryPoint, Nest(Limit + 1), IwxxmParser.Default, IwxxmSigwxParser.Default);

            ValidationError error = Assert.Single(errors);
            Assert.Equal("XML-DEPTH-LIMIT", error.Code);
            Assert.Equal(ErrorCategory.MalformedInput, error.Category);
            Assert.Contains(LimitText, error.Message, StringComparison.Ordinal);
            Assert.True(error.LineNumber > 0, "the line is reported");
            Assert.True(error.LinePosition > 0, "the position is reported");
        }

        [Theory]
        [MemberData(nameof(EntryPoints))]
        public void CustomLowerLimit_IsEnforced_OnEveryEntryPoint(string entryPoint)
        {
            var builder = new IwxxmParserBuilder().WithOptions(new IwxxmParserOptions(maxElementDepth: CustomLimit));

            IReadOnlyList<ValidationError> over = Invoke(entryPoint, Nest(CustomLimit + 1), builder.Build(), builder.BuildSigwxParser());
            IReadOnlyList<ValidationError> at = Invoke(entryPoint, Nest(CustomLimit), builder.Build(), builder.BuildSigwxParser());

            ValidationError error = Assert.Single(over);
            Assert.Equal("XML-DEPTH-LIMIT", error.Code);
            Assert.Contains(CustomLimit.ToString(CultureInfo.InvariantCulture), error.Message, StringComparison.Ordinal);
            Assert.DoesNotContain(at, e => e.Code == "XML-DEPTH-LIMIT");
            Assert.Equal(ExpectedCodeAtTheLimit(entryPoint), at[0].Code);
        }

        [Fact]
        public void ASchemaValidNest_BeyondTheDefault_IsAcceptedByTheXsdStage_WithARaisedLimit()
        {
            // 200 nested bulletins around a real METAR are genuinely schema-valid (about 420 levels). Only this payload uses a
            // raised limit, and it is far below the stack depth at which tree validation overflows.
            string xml = SchemaValidNest(SchemaValidBulletins);
            int depth = DepthOf(XDocument.Parse(xml).Root!);
            Assert.True(depth > Limit && depth < RaisedDepthLimit, "depth " + depth);

            IwxxmParser raised = new IwxxmParserBuilder().WithOptions(new IwxxmParserOptions(maxElementDepth: RaisedDepthLimit)).Build();

            // The default limit refuses it ...
            Assert.Equal("XML-DEPTH-LIMIT", Assert.Single(IwxxmParser.Default.Process(xml).Errors).Code);

            // ... the raised limit lets the XSD stage accept it (no XSD-* finding comes from the nesting) ...
            Assert.Empty(XsdValidator.CreateFullSuiteValidator(new IwxxmParserOptions(maxElementDepth: RaisedDepthLimit))
                .Validate(new IwxxmDocumentContext(Encoding.UTF8.GetBytes(xml), new IwxxmParserOptions(maxElementDepth: RaisedDepthLimit))));

            // ... and the mapper then rejects the nested bulletin envelope.
            IReadOnlyList<ValidationError> errors = raised.Process(xml).Errors;
            Assert.DoesNotContain(errors, e => e.Code.StartsWith("XSD-", StringComparison.Ordinal));
            Assert.Equal(new[] { "COLLECT-NESTED-BULLETIN", "DOMAIN-BULLETIN-EMPTY" }, errors.Select(e => e.Code).ToArray());
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
            var context = new IwxxmDocumentContext(Encoding.UTF8.GetBytes(Nest(Limit + 1)));

            ValidationError error = Assert.Single(XsdValidator.CreateFullSuiteValidator(IwxxmParserOptions.Default).Validate(context));

            Assert.Equal("XML-DEPTH-LIMIT", error.Code);
            Assert.Equal(ErrorCategory.MalformedInput, error.Category);
        }

        [Fact]
        public void XsltSchematronValidator_InvokedDirectly_ReportsTheDepthLimit()
        {
            var context = new IwxxmDocumentContext(Encoding.UTF8.GetBytes(Nest(Limit + 1)));

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
            // Run on the code before AUD-N1 was fixed, a schema-valid nest of this size overflowed the stack inside
            // XDocument.Validate and killed the process; it is now stopped by the streaming depth check before any tree exists.
            AssertRejectedQuickly(FormerCrashDepth, seekable: true);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void HundredThousandNestedLevels_AreRejectedQuickly(bool seekable)
        {
            AssertRejectedQuickly(HundredThousandDepth, seekable);

            using (var stream = new NestedStream(HundredThousandDepth, seekable))
            {
                Assert.Equal("XML-DEPTH-LIMIT", Assert.Single(IwxxmSigwxParser.Default.Process(stream).Errors).Code);
            }
        }

#if !NETFRAMEWORK
        // Excluded on .NET Framework: the payload (about 80 MB) is buffered whole by Process(Stream) and a 32-bit net48 test host
        // cannot do that comfortably (memory and time); the 25,000 and 100,000 level cases above still run there.
        [Fact]
        public void OneMillionNestedLevels_AreRejectedInBoundedTime()
        {
            // The payload is larger than the default 50 MiB size limit, which would reject it before the depth check; the size
            // limits are raised so the depth limit (left at its default) is what stops it.
            var options = new IwxxmParserOptions(maxDocumentBytes: 256L * 1024L * 1024L, maxCharactersInDocument: 256L * 1024L * 1024L);
            IwxxmParser parser = new IwxxmParserBuilder().WithOptions(options).Build();
            Assert.Equal(Limit, options.MaxElementDepth);

            using (var stream = new NestedStream(OneMillionDepth, seekable: false))
            {
                var stopwatch = Stopwatch.StartNew();
                Result<IwxxmDocument> result = parser.Process(stream);
                stopwatch.Stop();

                _output.WriteLine(OneMillionDepth + " levels, " + stream.TotalLength + " bytes, rejected in " + stopwatch.ElapsedMilliseconds + " ms");
                Assert.Equal("XML-DEPTH-LIMIT", Assert.Single(result.Errors).Code);
                Assert.True(stopwatch.Elapsed < RejectionBudget, "took " + stopwatch.Elapsed);
            }
        }
#endif

        private void AssertRejectedQuickly(int depth, bool seekable)
        {
            using (var stream = new NestedStream(depth, seekable))
            {
                var stopwatch = Stopwatch.StartNew();
                Result<IwxxmDocument> result = IwxxmParser.Default.Process(stream);
                stopwatch.Stop();

                _output.WriteLine(depth + " levels, " + stream.TotalLength + " bytes, seekable=" + seekable + ", rejected in " + stopwatch.ElapsedMilliseconds + " ms");
                Assert.True(stream.TotalLength < IwxxmParserOptions.Default.MaxDocumentBytes, "the payload is within the size limit");
                Assert.Equal("XML-DEPTH-LIMIT", Assert.Single(result.Errors).Code);
                Assert.True(stopwatch.Elapsed < RejectionBudget, "took " + stopwatch.Elapsed);
            }
        }

        // ---------------------------------------------------------------- helpers

        private static string Sample(string fileName) => File.ReadAllText(Path.Combine("Samples", "wmo-examples", fileName));

        private static int DepthOf(XElement root) => root.DescendantsAndSelf().Max(e => e.AncestorsAndSelf().Count());

        /// <summary>
        /// The error the parser reports for <see cref="Nest"/> at exactly the limit: the depth gate passes and the first
        /// downstream stage objects: the XSD stage on the bulletin parsers, and the SIGWX parser, which rejects the bulletin root.
        /// </summary>
        private static string ExpectedCodeAtTheLimit(string entryPoint) =>
            entryPoint.StartsWith("Sigwx.", StringComparison.Ordinal) ? "XSD-UNEXPECTED-ROOT-ELEMENT" : "XSD-SCHEMA-VIOLATION";

        /// <summary>
        /// Real-shape nest: <paramref name="bulletins"/> <c>collect:MeteorologicalBulletin</c> elements, each with its required
        /// unique <c>gml:id</c>, one <c>collect:meteorologicalInformation</c> holding the next bulletin, and the required
        /// <c>collect:bulletinIdentifier</c>; <paramref name="core"/> sits inside the innermost information element.
        /// </summary>
        private static IEnumerable<string> Pieces(int bulletins, string core)
        {
            for (int i = 1; i <= bulletins; i++)
            {
                yield return (i == 1 ? Prolog + "<collect:MeteorologicalBulletin" + RootNamespaces : "<collect:MeteorologicalBulletin")
                    + " gml:id=\"b" + i.ToString(CultureInfo.InvariantCulture) + "\"><collect:meteorologicalInformation>";
            }

            yield return core;

            for (int i = 0; i < bulletins; i++)
            {
                yield return BulletinClose;
            }
        }

        /// <summary>
        /// The pieces of a document whose deepest element is exactly at <paramref name="depth"/> (root = 1): bulletins sit on
        /// odd depths and their information elements on even ones; the innermost information element is empty (even depth) or
        /// holds one unknown element (odd depth), so the document is well-formed and the nesting is real-shaped but not schema-valid.
        /// </summary>
        private static IEnumerable<string> DepthPieces(int depth) =>
            Pieces(depth / 2, depth % 2 == 0 ? string.Empty : "<collect:unexpected/>");

        private static string Nest(int depth) => string.Concat(DepthPieces(depth));

        /// <summary>A genuinely schema-valid nest: bulletins around a real METAR report.</summary>
        private static string SchemaValidNest(int bulletins)
        {
            string metar = XDocument.Parse(Sample("metar-A3-1.xml")).Root!.ToString(SaveOptions.DisableFormatting);
            return string.Concat(Pieces(bulletins, metar));
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
        /// A real-shape nest (see <see cref="Pieces"/>) whose deepest element is at <c>depth</c>, generated on demand so that the
        /// payload itself is never held in memory (the parser under test still buffers what it reads).
        /// </summary>
        private sealed class NestedStream : Stream
        {
            private readonly IEnumerator<string> _pieces;
            private readonly bool _seekable;
            private byte[] _current = new byte[0];
            private int _offset;
            private long _position;

            public NestedStream(int depth, bool seekable)
            {
                _pieces = DepthPieces(depth).GetEnumerator();
                _seekable = seekable;
                long total = 0;
                foreach (string piece in DepthPieces(depth))
                {
                    total += Encoding.UTF8.GetByteCount(piece);
                }

                TotalLength = total;
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
                int written = 0;
                while (written < count)
                {
                    if (_offset == _current.Length)
                    {
                        if (!_pieces.MoveNext())
                            break;

                        _current = Encoding.UTF8.GetBytes(_pieces.Current);
                        _offset = 0;
                        continue;
                    }

                    int n = Math.Min(count - written, _current.Length - _offset);
                    Buffer.BlockCopy(_current, _offset, buffer, offset + written, n);
                    _offset += n;
                    written += n;
                }

                _position += written;
                return written;
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                    _pieces.Dispose();

                base.Dispose(disposing);
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
