using System;
using System.IO;

namespace Iwxxm.Net.Application.UseCases
{
    /// <summary>
    /// Reads an input stream into a size-bounded in-memory buffer so that every pipeline stage
    /// (schema validation, business-rule validation, mapping) can read the document from position 0.
    /// Enforces <see cref="IwxxmParserOptions.MaxDocumentBytes"/> before and while buffering.
    /// </summary>
    internal static class BoundedDocumentBuffer
    {
        private const int ChunkSize = 8192;

        /// <summary>
        /// Buffers <paramref name="input"/> into a byte array or returns the failure describing why it could not.
        /// </summary>
        /// <param name="input">Caller-owned input stream (never closed).</param>
        /// <param name="options">Size limits.</param>
        /// <param name="emptyCode">Error code reported for a zero-length stream (kept for error-code stability).</param>
        /// <param name="unreadableCode">Error code reported for a non-readable stream (kept for error-code stability).</param>
        public static Result<byte[]> Read(Stream input, IwxxmParserOptions options, string emptyCode, string unreadableCode)
        {
            if (input == null)
                throw new ArgumentNullException(nameof(input));
            if (options == null)
                throw new ArgumentNullException(nameof(options));

            if (!input.CanRead)
            {
                return Result<byte[]>.Failure(
                    unreadableCode,
                    "The input XML stream is not readable.",
                    ErrorCategory.MalformedInput);
            }

            if (input.CanSeek && (input.Length - input.Position) > options.MaxDocumentBytes)
            {
                return Result<byte[]>.Failure(
                    "XML-SIZE-LIMIT",
                    $"The input XML stream length ({input.Length - input.Position} bytes) exceeds the maximum allowed limit of {options.MaxDocumentBytes} bytes.",
                    ErrorCategory.MalformedInput);
            }

            byte[] payload;
            try
            {
                using (var buffer = new MemoryStream())
                {
                    var chunk = new byte[ChunkSize];
                    long totalBytesRead = 0;
                    int bytesRead;
                    while ((bytesRead = input.Read(chunk, 0, chunk.Length)) > 0)
                    {
                        totalBytesRead += bytesRead;
                        if (totalBytesRead > options.MaxDocumentBytes)
                        {
                            return Result<byte[]>.Failure(
                                "XML-SIZE-LIMIT",
                                $"The input XML stream exceeds the maximum allowed limit of {options.MaxDocumentBytes} bytes.",
                                ErrorCategory.MalformedInput);
                        }

                        buffer.Write(chunk, 0, bytesRead);
                    }

                    payload = buffer.ToArray();
                }
            }
            catch (Exception ex)
            {
                return Result<byte[]>.Failure(
                    "STREAM-READ-ERROR",
                    $"Failed to read the input XML stream: {ex.Message}",
                    ErrorCategory.MalformedInput);
            }

            if (payload.Length == 0)
            {
                return Result<byte[]>.Failure(
                    emptyCode,
                    "The input XML stream is empty (0 bytes).",
                    ErrorCategory.MalformedInput);
            }

            return Result<byte[]>.Success(payload);
        }

        /// <summary>Opens a fresh read-only, seekable view over the buffered document.</summary>
        public static MemoryStream OpenView(byte[] payload) => new MemoryStream(payload, writable: false);
    }
}
