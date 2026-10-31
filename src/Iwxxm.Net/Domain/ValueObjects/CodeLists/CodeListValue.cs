using System;

namespace Iwxxm.Net.Reports
{
    /// <summary>
    /// Immutable Value Object holding a value of a closed WMO code list, translated by the Anti-Corruption Layer from
    /// the Linked Data reference of the document (<c>xlink:href</c>) into a Domain enumeration.
    /// </summary>
    /// <typeparam name="TCode">
    /// Domain enumeration of the code list. Its member named <c>Unknown</c> stands for a value that is not an official code
    /// (the default value <c>0</c> for the WMO 49-2 / common lists; <c>-1</c> for the WMO BUFR tables, whose member values
    /// equal the official codes, including <c>0</c>); every other member stands for exactly one official code.
    /// </typeparam>
    /// <remarks>
    /// <para>The Domain never stores a code-list URI as its model of a code: <see cref="Code"/> carries the meaning and
    /// <see cref="Notation"/> keeps the code as reported, for information.</para>
    /// <para>A reference that is not a code of the official list valid for IWXXM 2025-2 is never dropped: it yields
    /// <c>Unknown</c> together with the raw value. For a reference into the official register, <see cref="Notation"/>
    /// is the code that follows the register (e.g. <c>XYZ</c>); for a reference into any other register it is the
    /// reference exactly as reported, because a code cannot be detached from a foreign register without changing its
    /// meaning.</para>
    /// <para>Equality is structural and exact: two values are equal when <see cref="Code"/> and <see cref="Notation"/>
    /// (ordinal) are equal.</para>
    /// </remarks>
    public sealed class CodeListValue<TCode> : IEquatable<CodeListValue<TCode>>
        where TCode : struct, Enum
    {
        /// <summary>
        /// Initializes a code-list value.
        /// </summary>
        /// <param name="code">Domain meaning; the default (<c>Unknown</c>) member when the reported value is not an official code.</param>
        /// <param name="notation">Code as reported (see the type remarks); required.</param>
        /// <exception cref="ArgumentException"><paramref name="notation"/> is <c>null</c>, empty or whitespace.</exception>
        public CodeListValue(TCode code, string notation)
        {
            if (string.IsNullOrWhiteSpace(notation))
                throw new ArgumentException("The reported notation of a code-list value is required.", nameof(notation));

            Code = code;
            Notation = notation;
        }

        /// <summary>Gets the Domain meaning of the value, or the default (<c>Unknown</c>) member for a non-official code.</summary>
        public TCode Code { get; }

        /// <summary>Gets the code as reported (official notation, or the raw reference for a foreign register).</summary>
        public string Notation { get; }

        /// <summary>Gets a value indicating whether <see cref="Code"/> is an official code (not the <c>Unknown</c> member).</summary>
        public bool IsKnown => !Code.Equals(UnknownCode);

        /// <summary>The <c>Unknown</c> member of <typeparamref name="TCode"/> (its default value when it declares none).</summary>
        internal static readonly TCode UnknownCode =
            Enum.IsDefined(typeof(TCode), "Unknown") ? (TCode)Enum.Parse(typeof(TCode), "Unknown") : default;

        public bool Equals(CodeListValue<TCode>? other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Code.Equals(other.Code) && string.Equals(Notation, other.Notation, StringComparison.Ordinal);
        }

        public override bool Equals(object? obj) => obj is CodeListValue<TCode> other && Equals(other);

        public override int GetHashCode() => ValueEquality.Add(ValueEquality.Add(ValueEquality.Seed, Code), Notation);

        public override string ToString() => IsKnown ? Code + " (" + Notation + ")" : "Unknown (" + Notation + ")";

        public static bool operator ==(CodeListValue<TCode>? left, CodeListValue<TCode>? right) => Equals(left, right);

        public static bool operator !=(CodeListValue<TCode>? left, CodeListValue<TCode>? right) => !Equals(left, right);
    }
}
