using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;

namespace Iwxxm.Net.Reports
{
    /// <summary>
    /// Immutable Value Object describing one item of a container document that could not be mapped: a report of a
    /// <see cref="MeteorologicalBulletin"/> (<c>collect:meteorologicalInformation</c>) or a feature of a WAFS SIGWX chart
    /// (<c>iwxxm:feature</c>). The container keeps every successfully mapped item and lists the failed ones here.
    /// </summary>
    public sealed class ItemMappingError : IEquatable<ItemMappingError>
    {
        /// <summary>Creates the description of one item that could not be mapped.</summary>
        /// <param name="index">The zero-based position of the item in its container; must not be negative.</param>
        /// <param name="itemId">The <c>gml:id</c> of the item; a blank value is stored as <c>null</c>.</param>
        /// <param name="errors">The mapping errors of the item; must contain at least one.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is negative.</exception>
        /// <exception cref="ArgumentNullException"><paramref name="errors"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentException"><paramref name="errors"/> is empty.</exception>
        public ItemMappingError(int index, string? itemId, IEnumerable<ValidationError> errors)
        {
            if (index < 0)
                throw new ArgumentOutOfRangeException(nameof(index), index, "The item index cannot be negative.");

            Index = index;
            ItemId = string.IsNullOrWhiteSpace(itemId) ? null : itemId;
            Errors = new ReadOnlyCollection<ValidationError>((errors ?? throw new ArgumentNullException(nameof(errors))).ToList());
            if (Errors.Count == 0)
                throw new ArgumentException("An item mapping error needs at least one error.", nameof(errors));
        }

        /// <summary>Zero-based position of the item in the container, in document order.</summary>
        public int Index { get; }

        /// <summary><c>gml:id</c> of the item, or <c>null</c> when it has none.</summary>
        public string? ItemId { get; }

        /// <summary>Mapping errors of the item.</summary>
        public IReadOnlyList<ValidationError> Errors { get; }

        /// <summary>
        /// The item errors as <see cref="ValidationSeverity.Warning"/>s of the container result: same code and category,
        /// message prefixed with the item index and identifier.
        /// </summary>
        public IEnumerable<ValidationError> ToWarnings(string itemKind) =>
            Errors.Select(e => new ValidationError(
                e.Code,
                string.Format(CultureInfo.InvariantCulture, "{0} #{1}{2} was skipped: {3}", itemKind, Index, ItemId == null ? string.Empty : " ('" + ItemId + "')", e.Message),
                e.Category,
                ValidationSeverity.Warning,
                e.Location ?? ItemId,
                e.LineNumber,
                e.LinePosition));

        /// <inheritdoc />
        public bool Equals(ItemMappingError? other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Index == other.Index &&
                   string.Equals(ItemId, other.ItemId, StringComparison.Ordinal) &&
                   ValueEquality.SequenceEqual(Errors, other.Errors);
        }

        /// <inheritdoc />
        public override bool Equals(object? obj) => obj is ItemMappingError other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode()
        {
            int hash = ValueEquality.Add(ValueEquality.Seed, Index);
            hash = ValueEquality.Add(hash, ItemId);
            return ValueEquality.AddSequence(hash, Errors);
        }

        /// <summary>Returns <c>true</c> when the two <see cref="ItemMappingError"/> operands are equal by value.</summary>
        public static bool operator ==(ItemMappingError? left, ItemMappingError? right) => Equals(left, right);

        /// <summary>Returns <c>true</c> when the two <see cref="ItemMappingError"/> operands differ by value.</summary>
        public static bool operator !=(ItemMappingError? left, ItemMappingError? right) => !Equals(left, right);
    }
}
