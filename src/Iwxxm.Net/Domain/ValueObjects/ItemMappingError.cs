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

        public bool Equals(ItemMappingError? other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Index == other.Index &&
                   string.Equals(ItemId, other.ItemId, StringComparison.Ordinal) &&
                   ValueEquality.SequenceEqual(Errors, other.Errors);
        }

        public override bool Equals(object? obj) => obj is ItemMappingError other && Equals(other);

        public override int GetHashCode()
        {
            int hash = ValueEquality.Add(ValueEquality.Seed, Index);
            hash = ValueEquality.Add(hash, ItemId);
            return ValueEquality.AddSequence(hash, Errors);
        }

        public static bool operator ==(ItemMappingError? left, ItemMappingError? right) => Equals(left, right);

        public static bool operator !=(ItemMappingError? left, ItemMappingError? right) => !Equals(left, right);
    }
}
