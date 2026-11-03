using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Iwxxm.Net.Reports;

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

        /// <summary>The stream has zero bytes (both use cases report the same code).</summary>
        internal const string EmptyStreamCode = "STREAM-EMPTY";

        /// <summary>The stream does not support reading.</summary>
        internal const string UnreadableStreamCode = "STREAM-UNREADABLE";

        /// <summary>Reading the stream threw (I/O failure).</summary>
        internal const string StreamReadErrorCode = "STREAM-READ-ERROR";

        /// <summary>
        /// Buffers <paramref name="input"/> into a byte array or returns the failure describing why it could not.
        /// </summary>
        /// <param name="input">Caller-owned input stream (never closed).</param>
        /// <param name="options">Size limits.</param>
        public static Result<byte[]> Read(Stream input, IwxxmParserOptions options)
        {
            if (input == null)
                throw new ArgumentNullException(nameof(input));
            if (options == null)
                throw new ArgumentNullException(nameof(options));

            if (!input.CanRead)
            {
                return Result<byte[]>.Failure(
                    UnreadableStreamCode,
                    "The input XML stream is not readable.",
                    ErrorCategory.MalformedInput);
            }

            if (input.CanSeek && (input.Length - input.Position) > options.MaxDocumentBytes)
            {
                return Result<byte[]>.Failure(
                    SharedErrorCodes.XmlSizeLimit,
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
                                SharedErrorCodes.XmlSizeLimit,
                                $"The input XML stream exceeds the maximum allowed limit of {options.MaxDocumentBytes} bytes.",
                                ErrorCategory.MalformedInput);
                        }

                        buffer.Write(chunk, 0, bytesRead);
                    }

                    payload = buffer.ToArray();
                }
            }
            catch (Exception ex) when (!(ex is OutOfMemoryException))
            {
                return Result<byte[]>.Failure(
                    StreamReadErrorCode,
                    $"Failed to read the input XML stream: {ex.Message}",
                    ErrorCategory.MalformedInput);
            }

            if (payload.Length == 0)
            {
                return Result<byte[]>.Failure(
                    EmptyStreamCode,
                    "The input XML stream is empty (0 bytes).",
                    ErrorCategory.MalformedInput);
            }

            return Result<byte[]>.Success(payload);
        }

        /// <summary>
        /// Reads already-decoded XML text (AUD-M5) into the same size-bounded UTF-8 buffer as <see cref="Read"/>.
        /// </summary>
        /// <remarks>
        /// Text is not bytes: like an <c>XmlReader</c> over a <see cref="StringReader"/>, the encoding named by the XML
        /// declaration does not apply to it. The text is therefore encoded as UTF-8 and the <c>encoding</c> pseudo-attribute
        /// of its declaration (if any) is rewritten to <c>UTF-8</c>, so a string declared <c>UTF-16</c> or <c>ISO-8859-1</c>
        /// reaches every pipeline stage with exactly its characters (previously the UTF-8 bytes were decoded with the
        /// declared encoding: <c>UTF-16</c> failed, <c>ISO-8859-1</c> produced mojibake). A leading byte-order mark
        /// character is dropped. <see cref="IwxxmParserOptions.MaxDocumentBytes"/> applies to the UTF-8 payload and is
        /// enforced while reading.
        /// </remarks>
        public static Result<byte[]> ReadText(TextReader input, IwxxmParserOptions options, string emptyCode)
        {
            if (input == null)
                throw new ArgumentNullException(nameof(input));
            if (options == null)
                throw new ArgumentNullException(nameof(options));

            var text = new StringBuilder();
            try
            {
                var chunk = new char[ChunkSize];
                int read;
                while ((read = input.Read(chunk, 0, chunk.Length)) > 0)
                {
                    // Every character takes at least one UTF-8 byte: stop as soon as the byte limit is certainly exceeded.
                    if (text.Length + read > options.MaxDocumentBytes)
                    {
                        return SizeLimitExceeded(options);
                    }

                    text.Append(chunk, 0, read);
                }
            }
            catch (Exception ex) when (!(ex is OutOfMemoryException))
            {
                return Result<byte[]>.Failure(
                    StreamReadErrorCode,
                    $"Failed to read the input XML text: {ex.Message}",
                    ErrorCategory.MalformedInput);
            }

            string normalized = DeclareUtf8(text.ToString());
            if (normalized.Trim().Length == 0)
            {
                return Result<byte[]>.Failure(
                    emptyCode,
                    "The provided XML text is empty.",
                    ErrorCategory.MalformedInput);
            }

            byte[] payload = Utf8WithoutBom.GetBytes(normalized);
            return payload.LongLength > options.MaxDocumentBytes
                ? SizeLimitExceeded(options)
                : Result<byte[]>.Success(payload);
        }

        private static readonly UTF8Encoding Utf8WithoutBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        // encoding pseudo-attribute of an XML declaration (XML 1.0 EncName production).
        private static readonly Regex EncodingDeclaration =
            new Regex("(\\bencoding\\s*=\\s*)([\"'])[A-Za-z][A-Za-z0-9._-]*\\2", RegexOptions.CultureInvariant);

        /// <summary>Drops a leading BOM character and rewrites the declared encoding of the XML declaration to UTF-8.</summary>
        internal static string DeclareUtf8(string xml)
        {
            if (xml.Length > 0 && xml[0] == '\uFEFF')
            {
                xml = xml.Substring(1);
            }

            if (!xml.StartsWith("<?xml", StringComparison.Ordinal) || xml.Length < 6 || !IsXmlWhitespace(xml[5]))
            {
                return xml;
            }

            int end = xml.IndexOf("?>", StringComparison.Ordinal);
            if (end < 0)
            {
                return xml;
            }

            string declaration = xml.Substring(0, end);
            string rewritten = EncodingDeclaration.Replace(declaration, match => match.Groups[1].Value + match.Groups[2].Value + "UTF-8" + match.Groups[2].Value, 1);
            return ReferenceEquals(rewritten, declaration) || rewritten == declaration ? xml : rewritten + xml.Substring(end);
        }

        private static bool IsXmlWhitespace(char c) => c == ' ' || c == '\t' || c == '\r' || c == '\n';

        private static Result<byte[]> SizeLimitExceeded(IwxxmParserOptions options) =>
            Result<byte[]>.Failure(
                SharedErrorCodes.XmlSizeLimit,
                $"The input XML text exceeds the maximum allowed limit of {options.MaxDocumentBytes} bytes (UTF-8).",
                ErrorCategory.MalformedInput);
    }
}
