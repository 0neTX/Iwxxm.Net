using System;
using System.IO;
using Iwxxm.Net.Application.UseCases;
using Iwxxm.Net.Reports;
using Xunit;

namespace Iwxxm.Net.Tests
{
    /// <summary>
    /// The default size limit is 50 MiB (52,428,800 bytes) and <c>XML-SIZE-LIMIT</c> holds exactly at that boundary, for
    /// seekable and non-seekable streams. Payloads are generated lazily by a stream (no 50 MB string or file); the
    /// allocation-heavy accept cases run on .NET only (a 32-bit net48 host cannot buffer 50 MB comfortably).
    /// </summary>
    public sealed class DocumentSizeLimitTests
    {
        private static readonly long Limit = IwxxmParserOptions.Default.MaxDocumentBytes;

        [Fact]
        public void Default_IsFiftyMebibytes_AndTheCharacterQuotaCoversEveryEncoding()
        {
            Assert.Equal(50L * 1024 * 1024, Limit);
            Assert.Equal(52_428_800L, Limit);
            Assert.Equal(52_428_800L, IwxxmParserOptions.Default.MaxCharactersInDocument);

            // A character takes at least one byte (UTF-8) and two (UTF-16): a document within the byte limit never exceeds the quota.
            Assert.True(IwxxmParserOptions.Default.MaxCharactersInDocument >= IwxxmParserOptions.Default.MaxDocumentBytes);
        }

        [Fact]
        public void SeekableStream_OneByteOverTheLimit_IsRejectedWithoutReadingIt()
        {
            using (var stream = new GeneratedStream(Limit + 1, seekable: true))
            {
                Result<byte[]> result = BoundedDocumentBuffer.Read(stream, IwxxmParserOptions.Default);

                ValidationError error = Assert.Single(result.Errors);
                Assert.Equal("XML-SIZE-LIMIT", error.Code);
                Assert.Equal(ErrorCategory.MalformedInput, error.Category);
                Assert.Contains((Limit + 1).ToString(), error.Message);
                Assert.Contains(Limit.ToString(), error.Message);
                Assert.Equal(0, stream.BytesRead);
            }
        }

        [Fact]
        public void EntryPoints_RejectAnOversizedStream_WithXmlSizeLimit()
        {
            using (var first = new GeneratedStream(Limit + 1, seekable: true))
                Assert.Equal("XML-SIZE-LIMIT", Assert.Single(IwxxmParser.Parse(first).Errors).Code);
            using (var second = new GeneratedStream(Limit + 1, seekable: true))
                Assert.Equal("XML-SIZE-LIMIT", Assert.Single(IwxxmSigwxParser.Parse(second).Errors).Code);
        }

#if !NETFRAMEWORK
        [Fact]
        public void NonSeekableStream_OneByteOverTheLimit_IsRejected_AfterReadingJustPastTheCap()
        {
            using (var stream = new GeneratedStream(Limit + 1, seekable: false))
            {
                Result<byte[]> result = BoundedDocumentBuffer.Read(stream, IwxxmParserOptions.Default);

                Assert.Equal("XML-SIZE-LIMIT", Assert.Single(result.Errors).Code);
                Assert.Equal(Limit + 1, stream.BytesRead);
            }
        }

        [Fact]
        public void NonSeekableStream_OfUnboundedLength_IsStoppedJustAfterTheCap()
        {
            using (var stream = new GeneratedStream(long.MaxValue, seekable: false))
            {
                var result = IwxxmParser.Parse(stream);

                Assert.Equal("XML-SIZE-LIMIT", Assert.Single(result.Errors).Code);
                Assert.InRange(stream.BytesRead, Limit + 1, Limit + 8192);
            }
        }

        [Theory]
        [InlineData(true, 0)]
        [InlineData(true, 1)]
        [InlineData(false, 0)]
        [InlineData(false, 1)]
        public void Stream_ExactlyAtTheLimit_OrOneByteUnderIt_IsBuffered(bool seekable, int bytesUnder)
        {
            long length = Limit - bytesUnder;
            using (var stream = new GeneratedStream(length, seekable))
            {
                Result<byte[]> result = BoundedDocumentBuffer.Read(stream, IwxxmParserOptions.Default);

                Assert.True(result.IsSuccess, string.Join(" | ", result.Errors));
                Assert.Equal(length, result.Value.LongLength);
            }
        }
#endif

        /// <summary>A stream of <c>length</c> blank bytes that is produced on demand and never held in memory.</summary>
        private sealed class GeneratedStream : Stream
        {
            private readonly long _length;
            private readonly bool _seekable;
            private long _position;

            public GeneratedStream(long length, bool seekable)
            {
                _length = length;
                _seekable = seekable;
            }

            public long BytesRead { get; private set; }

            public override bool CanRead => true;
            public override bool CanSeek => _seekable;
            public override bool CanWrite => false;
            public override long Length => _seekable ? _length : throw new NotSupportedException();
            public override long Position
            {
                get => _seekable ? _position : throw new NotSupportedException();
                set => throw new NotSupportedException();
            }

            public override void Flush()
            {
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                int n = (int)Math.Min(count, _length - _position);
                for (int i = 0; i < n; i++)
                    buffer[offset + i] = (byte)' ';
                _position += n;
                BytesRead += n;
                return n;
            }

            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        }
    }
}
