using System;

namespace Iwxxm.Net.Reports
{
    /// <summary>
    /// Immutable Value Object holding a present or forecast aerodrome weather code (WMO code list
    /// <c>AerodromePresentOrForecastWeather</c>, e.g. <c>+TSRA</c>, <c>-DZ</c>, <c>FG</c>), translated by the
    /// Anti-Corruption Layer from the Linked Data reference of the document.
    /// </summary>
    /// <remarks>
    /// <para>The official list is a combinatorial catalogue of 402 intensity / descriptor / precipitation combinations,
    /// so it is not modelled as an enumeration: <see cref="Notation"/> is the code and <see cref="IsKnown"/> tells whether
    /// it belongs to the official list valid for IWXXM 2025-2.</para>
    /// <para>The official RDF of this list publishes its codes under the WMO Manual on Codes table 4678 register
    /// (<c>http://codes.wmo.int/306/4678/{code}</c>), which is therefore the reference form accepted as official.</para>
    /// <para>A value outside the official list is never dropped: <see cref="IsKnown"/> is <c>false</c> and
    /// <see cref="Notation"/> keeps the raw value — the code that follows the official register, or the reference exactly
    /// as reported when it belongs to another register.</para>
    /// <para>Equality is structural and exact (<see cref="Notation"/> ordinal and <see cref="IsKnown"/>).</para>
    /// </remarks>
    public sealed class AerodromeWeather : IEquatable<AerodromeWeather>
    {
        /// <summary>Initializes an aerodrome weather code.</summary>
        /// <param name="notation">Code as reported (see the type remarks); required.</param>
        /// <param name="isKnown">Whether <paramref name="notation"/> is a code of the official list.</param>
        /// <exception cref="ArgumentException"><paramref name="notation"/> is <c>null</c>, empty or whitespace.</exception>
        public AerodromeWeather(string notation, bool isKnown)
        {
            if (string.IsNullOrWhiteSpace(notation))
                throw new ArgumentException("The reported notation of an aerodrome weather code is required.", nameof(notation));

            Notation = notation;
            IsKnown = isKnown;
        }

        /// <summary>Gets the code as reported (official notation, or the raw reference for a foreign register).</summary>
        public string Notation { get; }

        /// <summary>Gets a value indicating whether <see cref="Notation"/> is a code of the official list.</summary>
        public bool IsKnown { get; }

        /// <inheritdoc />
        public bool Equals(AerodromeWeather? other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return IsKnown == other.IsKnown && string.Equals(Notation, other.Notation, StringComparison.Ordinal);
        }

        /// <inheritdoc />
        public override bool Equals(object? obj) => obj is AerodromeWeather other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode() => ValueEquality.Add(ValueEquality.Add(ValueEquality.Seed, Notation), IsKnown);

        /// <inheritdoc />
        public override string ToString() => IsKnown ? Notation : "Unknown (" + Notation + ")";

        /// <summary>Returns <c>true</c> when the two <see cref="AerodromeWeather"/> operands are equal by value.</summary>
        public static bool operator ==(AerodromeWeather? left, AerodromeWeather? right) => Equals(left, right);

        /// <summary>Returns <c>true</c> when the two <see cref="AerodromeWeather"/> operands differ by value.</summary>
        public static bool operator !=(AerodromeWeather? left, AerodromeWeather? right) => !Equals(left, right);
    }
}
