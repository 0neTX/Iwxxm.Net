using System;

namespace Iwxxm.Net.Infrastructure.Mapping.CodeLists
{
    /// <summary>
    /// Splits a code-list reference (<c>xlink:href</c>) against an official register and rebuilds it, so that the
    /// translation into Domain values is lossless.
    /// </summary>
    /// <remarks>
    /// A reference <c>{register}/{code}</c> whose code contains neither <c>/</c> nor <c>:</c> is reduced to the code; any
    /// other reference (another register, a nested path, the bare register) is kept verbatim. Official codes contain
    /// neither character while absolute URIs always contain <c>:</c>, so <see cref="Rebuild"/> restores the exact original
    /// reference in both cases. The only exception is a relative reference without <c>/</c> or <c>:</c> (invalid, the
    /// schemas require a code URI): it is kept verbatim in the Domain but rebuilt as a code of the register.
    /// </remarks>
    internal static class CodeListReference
    {
        /// <summary>
        /// Returns the code of <paramref name="href"/> within <paramref name="registerUri"/>, or <c>null</c> when the
        /// reference does not designate a code of that register (it must then be kept verbatim).
        /// </summary>
        internal static string? TryGetCode(string href, string registerUri)
        {
            string prefix = registerUri + "/";
            if (!href.StartsWith(prefix, StringComparison.Ordinal) || href.Length == prefix.Length)
                return null;

            string code = href.Substring(prefix.Length);
            return IsVerbatim(code) ? null : code;
        }

        /// <summary>Rebuilds the original reference from a translated notation (see the type remarks).</summary>
        internal static string Rebuild(string notation, string registerUri) =>
            IsVerbatim(notation) ? notation : registerUri + "/" + notation;

        private static bool IsVerbatim(string value) => value.IndexOf('/') >= 0 || value.IndexOf(':') >= 0;
    }
}
