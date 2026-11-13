using System;
using System.Collections.Generic;
using Iwxxm.Net.Reports;

namespace Iwxxm.Net.Infrastructure.Mapping.CodeLists
{
    /// <summary>
    /// Anti-Corruption Layer table that translates references of one closed WMO code list into
    /// <see cref="CodeListValue{TCode}"/> Domain values, and back into references for Linked-Data outputs (GeoJSON).
    /// </summary>
    /// <remarks>
    /// The table is static data checked against the embedded official RDF file (<see cref="ResourcePath"/>) by
    /// <c>CodeListTranslationTests</c>: its codes must equal the codes valid for IWXXM 2025-2, and every Domain enum member
    /// other than <c>Unknown</c> must appear exactly once.
    /// </remarks>
    internal sealed class CodeListTable<TCode>
        where TCode : struct, Enum
    {
        private readonly Dictionary<string, TCode> _codes;

        internal CodeListTable(string registerUri, string resourcePath, IDictionary<string, TCode> codes)
        {
            RegisterUri = registerUri;
            ResourcePath = resourcePath;
            _codes = new Dictionary<string, TCode>(codes, StringComparer.Ordinal);
        }

        /// <summary>Gets the URI of the official register (e.g. <c>http://codes.wmo.int/49-2/SigWxPhenomena</c>).</summary>
        internal string RegisterUri { get; }

        /// <summary>Gets the embedded RDF file of the register, relative to <c>Resources/</c>.</summary>
        internal string ResourcePath { get; }

        /// <summary>Gets the code-to-member table.</summary>
        internal IReadOnlyDictionary<string, TCode> Codes => _codes;

        /// <summary>
        /// Translates a reference: <c>null</c> for an absent or blank reference (absent / nil element). An official code of
        /// the register maps to its member with the code as <see cref="CodeListValue{TCode}.Notation"/>; anything else (an
        /// unlisted code of the register, a case variant, another register, a bare token) is <c>Unknown</c> with the
        /// reference exactly as written, so that <see cref="ToReference"/> restores it byte for byte.
        /// </summary>
        internal CodeListValue<TCode>? Translate(string? href)
        {
            if (string.IsNullOrWhiteSpace(href))
                return null;

            string? code = CodeListReference.TryGetCode(href!, RegisterUri);
            if (code != null && _codes.TryGetValue(code, out TCode member))
                return new CodeListValue<TCode>(member, code);

            return new CodeListValue<TCode>(CodeListValue<TCode>.UnknownCode, href!);
        }

        /// <summary>Rebuilds the exact original reference of a translated value, or <c>null</c>.</summary>
        internal string? ToReference(CodeListValue<TCode>? value) =>
            value == null ? null : CodeListReference.Rebuild(value.Notation, RegisterUri);
    }
}
