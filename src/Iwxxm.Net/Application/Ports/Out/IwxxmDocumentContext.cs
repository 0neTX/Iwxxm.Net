using System;
using System.IO;
using System.Threading;

namespace Iwxxm.Net.Extensibility
{
    /// <summary>
    /// Neutral, already-sanitized view of one input document handed by the use cases to every outbound port
    /// (schema validator, business-rule validator, document mappers) (HEX-01).
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>The use case reads the input once: it enforces <see cref="IwxxmParserOptions.MaxDocumentBytes"/>, turns text
    /// input into UTF-8 (AUD-M5) and has the document parsed once with the hardened reader settings of
    /// <see cref="Options"/> (DTD prohibited, no resolver, character quotas) before any adapter runs. A document that
    /// reaches an adapter through the pipeline is therefore well formed, within the limits and free of DTDs.</item>
    /// <item><see cref="OpenRead"/> returns a fresh read-only stream over the document bytes for adapters that consume
    /// raw XML (e.g. an XSLT engine); built-in adapters share the parsed tree instead of parsing again.</item>
    /// <item>A context is immutable from the point of view of its consumers and safe to read concurrently.</item>
    /// </list>
    /// </remarks>
    public sealed class IwxxmDocumentContext
    {
        private readonly object _gate = new object();
        private readonly Func<byte[]>? _materialize;
        private byte[]? _document;
        private object? _parsed;

        /// <summary>
        /// Creates a context over raw document bytes (for example to call an adapter directly). The bytes are copied.
        /// </summary>
        /// <param name="document">Raw XML bytes (encoding detected from the byte-order mark or declaration).</param>
        /// <param name="options">Reader limits; <see cref="IwxxmParserOptions.Default"/> when <c>null</c>.</param>
        public IwxxmDocumentContext(byte[] document, IwxxmParserOptions? options = null)
            : this((byte[])(document ?? throw new ArgumentNullException(nameof(document))).Clone(), options, null, null)
        {
        }

        private IwxxmDocumentContext(byte[]? document, IwxxmParserOptions? options, Func<byte[]>? materialize, object? parsed)
        {
            _document = document;
            _materialize = materialize;
            _parsed = parsed;
            Options = options ?? IwxxmParserOptions.Default;
        }

        /// <summary>Gets the limits applied when the document is read.</summary>
        public IwxxmParserOptions Options { get; }

        /// <summary>Gets the document length in bytes.</summary>
        public long Length => Bytes.LongLength;

        /// <summary>Opens a fresh read-only, seekable stream positioned at the start of the document.</summary>
        public Stream OpenRead()
        {
            byte[] bytes = Bytes;
            return new MemoryStream(bytes, 0, bytes.Length, writable: false);
        }

        /// <summary>Gets a value indicating whether the document bytes exist (false for an embedded document never read as bytes).</summary>
        internal bool IsMaterialized => Volatile.Read(ref _document) != null;

        private byte[] Bytes
        {
            get
            {
                byte[]? bytes = Volatile.Read(ref _document);
                if (bytes != null)
                    return bytes;

                lock (_gate)
                {
                    if (_document == null)
                    {
                        Volatile.Write(ref _document, _materialize!());
                    }

                    return _document!;
                }
            }
        }

        /// <summary>Creates a context over a buffer owned by the caller (no copy).</summary>
        internal static IwxxmDocumentContext FromBuffer(byte[] document, IwxxmParserOptions options) =>
            new IwxxmDocumentContext(document, options, null, null);

        /// <summary>
        /// Creates a context for an already parsed document (e.g. a report embedded in a bulletin); the bytes are produced by
        /// <paramref name="serialize"/> only if an adapter asks for them.
        /// </summary>
        internal static IwxxmDocumentContext FromParsed(object parsed, Func<byte[]> serialize, IwxxmParserOptions options) =>
            new IwxxmDocumentContext(null, options, serialize ?? throw new ArgumentNullException(nameof(serialize)), parsed ?? throw new ArgumentNullException(nameof(parsed)));

        /// <summary>
        /// Returns the parsed representation of the document, parsing it with <paramref name="parse"/> on first use (once per
        /// context, thread-safe). A parse failure is not cached and propagates to the caller.
        /// </summary>
        internal T GetOrAddParsed<T>(Func<IwxxmDocumentContext, T> parse)
            where T : class
        {
            if (Volatile.Read(ref _parsed) is T cached)
                return cached;

            lock (_gate)
            {
                if (_parsed is T existing)
                    return existing;

                T created = parse(this) ?? throw new InvalidOperationException("The document parser returned null.");
                Volatile.Write(ref _parsed, created);
                return created;
            }
        }
    }
}
