using System.Collections.Generic;

namespace Iwxxm.Net.Reports
{
    /// <summary>
    /// Helpers for the structural equality of the reference-type value objects (DDD-09): exact comparison of every
    /// component (no tolerance), collections compared by content and in order, and hash codes that agree with
    /// <c>Equals</c> on every target framework.
    /// </summary>
    internal static class ValueEquality
    {
        /// <summary>Initial value of a combined hash code.</summary>
        internal const int Seed = 17;

        private const int Multiplier = 31;

        /// <summary>Adds <paramref name="value"/> (compared with <see cref="EqualityComparer{T}.Default"/>) to <paramref name="hash"/>.</summary>
        internal static int Add<T>(int hash, T value) =>
            unchecked((hash * Multiplier) + (value == null ? 0 : EqualityComparer<T>.Default.GetHashCode(value)));

        /// <summary>
        /// Adds a floating-point component. <c>0.0</c> and <c>-0.0</c> are equal (and every <c>NaN</c> equals every
        /// <c>NaN</c>) for <see cref="double.Equals(double)"/>, but .NET Framework hashes them differently; both zeros and
        /// all NaNs therefore get one hash code here.
        /// </summary>
        internal static int AddDouble(int hash, double? value)
        {
            int component;
            if (!value.HasValue)
                component = 0;
            else if (double.IsNaN(value.Value))
                component = 1;
            else if (value.Value == 0.0)
                component = 2;
            else
                component = value.Value.GetHashCode();

            return unchecked((hash * Multiplier) + component);
        }

        /// <summary>Adds every element of <paramref name="items"/>, in order, to <paramref name="hash"/>.</summary>
        internal static int AddSequence<T>(int hash, IReadOnlyList<T>? items)
        {
            if (items == null)
                return Add(hash, 0);

            hash = Add(hash, items.Count);
            foreach (T item in items)
            {
                hash = Add(hash, item);
            }

            return hash;
        }

        /// <summary>Compares two lists element by element, in order, with <see cref="EqualityComparer{T}.Default"/>.</summary>
        internal static bool SequenceEqual<T>(IReadOnlyList<T>? left, IReadOnlyList<T>? right)
        {
            if (ReferenceEquals(left, right))
                return true;
            if (left == null || right == null || left.Count != right.Count)
                return false;

            EqualityComparer<T> comparer = EqualityComparer<T>.Default;
            for (int i = 0; i < left.Count; i++)
            {
                if (!comparer.Equals(left[i], right[i]))
                    return false;
            }

            return true;
        }
    }
}
