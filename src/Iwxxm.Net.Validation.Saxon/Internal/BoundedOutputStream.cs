using System;
using System.IO;

namespace Iwxxm.Net.Validation.Saxon.Internal
{
    /// <summary>
    /// Write-only in-memory stream that refuses to grow beyond a fixed number of bytes (AUD-M14). Used as the SVRL
    /// destination of the Schematron transform so that a document producing a huge report cannot exhaust memory: the
    /// write that crosses the limit throws <see cref="OutputLimitExceededException"/> and the transform is abandoned.
    /// </summary>
    /// <remarks>
    /// Intentional copy: this type exists twice, here and in <c>Iwxxm.Net/Infrastructure/Validation/BoundedOutputStream.cs</c>. The Saxon package references only the public API of the core and its internals are not visible to it (and must stay non-public), so it carries its own copy.
    /// Keep both copies in sync when changing either one.
    /// </remarks>
    internal sealed class BoundedOutputStream : Stream
    {
        private readonly MemoryStream _buffer = new MemoryStream();
        private readonly long _limit;

        public BoundedOutputStream(long limit)
        {
            if (limit <= 0)
                throw new ArgumentOutOfRangeException(nameof(limit));
            _limit = limit;
        }

        public override bool CanRead => false;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => _buffer.Length;

        /// <summary>
        /// Gets a value indicating whether a write was refused. Checked by callers because a transform engine may wrap or
        /// replace the <see cref="OutputLimitExceededException"/> it receives.
        /// </summary>
        public bool LimitExceeded { get; private set; }

        public override long Position
        {
            get => _buffer.Length;
            set => throw new NotSupportedException();
        }

        /// <summary>Opens a read-only view over the bytes written so far.</summary>
        public MemoryStream OpenRead() => new MemoryStream(_buffer.GetBuffer(), 0, (int)_buffer.Length, writable: false);

        public override void Write(byte[] buffer, int offset, int count)
        {
            if (_buffer.Length + count > _limit)
            {
                LimitExceeded = true;
                throw new OutputLimitExceededException(_limit);
            }

            _buffer.Write(buffer, offset, count);
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        // The managed buffer is intentionally not released on Dispose: an XmlWriter created with CloseOutput may dispose
        // this stream before the report is read back through OpenRead(). MemoryStream holds no unmanaged resources.
    }

    /// <summary>Thrown by <see cref="BoundedOutputStream"/> when a write would exceed its limit.</summary>
    internal sealed class OutputLimitExceededException : IOException
    {
        public OutputLimitExceededException(long limit)
            : base($"The output exceeded the maximum allowed size of {limit} bytes.")
        {
            Limit = limit;
        }

        public long Limit { get; }
    }
}
