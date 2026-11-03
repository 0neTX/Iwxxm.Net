using System;
using System.IO;
using Iwxxm.Net.Application.UseCases;
using Xunit;

namespace Iwxxm.Net.Tests
{
    /// <summary>Both use cases (dispatch and SIGWX) report the same STREAM-* code for the same stream condition.</summary>
    public sealed class StreamErrorCodeTests
    {
        private sealed class WriteOnlyStream : MemoryStream
        {
            public override bool CanRead => false;
        }

        private sealed class ThrowingStream : Stream
        {
            private readonly Exception _toThrow;
            public ThrowingStream(Exception toThrow) { _toThrow = toThrow; }
            public override bool CanRead => true;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => throw new NotSupportedException();
            public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
            public override void Flush() { }
            public override int Read(byte[] buffer, int offset, int count) => throw _toThrow;
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        }

        [Fact]
        public void EmptyStream_IsStreamEmpty_ForBothUseCases()
        {
            Assert.Equal("STREAM-EMPTY", Assert.Single(IwxxmParser.Default.Process(new MemoryStream()).Errors).Code);
            Assert.Equal("STREAM-EMPTY", Assert.Single(IwxxmSigwxParser.Default.Process(new MemoryStream()).Errors).Code);
        }

        [Fact]
        public void UnreadableStream_IsStreamUnreadable_ForBothUseCases()
        {
            Assert.Equal("STREAM-UNREADABLE", Assert.Single(IwxxmParser.Default.Process(new WriteOnlyStream()).Errors).Code);
            Assert.Equal("STREAM-UNREADABLE", Assert.Single(IwxxmSigwxParser.Default.Process(new WriteOnlyStream()).Errors).Code);
        }

        [Fact]
        public void IoFailure_IsStreamReadError()
        {
            Assert.Equal("STREAM-READ-ERROR",
                Assert.Single(IwxxmParser.Default.Process(new ThrowingStream(new IOException("disk"))).Errors).Code);
        }

        [Fact]
        public void OutOfMemory_WhileReadingAByteStream_IsNotConvertedToAnError()
        {
            Assert.Throws<OutOfMemoryException>(() =>
                BoundedDocumentBuffer.Read(new ThrowingStream(new OutOfMemoryException()), IwxxmParserOptions.Default));
        }
    }
}
