using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Iwxxm.Net
{
    /// <summary>
    /// Immutable functional Result&lt;T&gt; monad implementation in vanilla C#
    /// to avoid exception-driven control flow across architectural boundaries.
    /// </summary>
    /// <typeparam name="T">Type of the encapsulated success value.</typeparam>
    public sealed class Result<T>
    {
        private static readonly IReadOnlyList<ValidationError> EmptyErrors =
            new ReadOnlyCollection<ValidationError>(new List<ValidationError>(0));

        private readonly T? _value;

        /// <summary>Gets a value indicating whether the operation succeeded and <see cref="Value"/> is available.</summary>
        public bool IsSuccess { get; }
        /// <summary>Gets a value indicating whether the operation failed; <see cref="Errors"/> then holds at least one finding.</summary>
        public bool IsFailure => !IsSuccess;
        /// <summary>Gets the findings that made the operation fail; always empty for a success.</summary>
        public IReadOnlyList<ValidationError> Errors { get; }

        /// <summary>
        /// Non-fatal problems of a successful result (<see cref="ValidationSeverity.Warning"/>), e.g. the items of a
        /// bulletin or WAFS chart that could not be mapped while the others were. Always empty for a failure.
        /// </summary>
        public IReadOnlyList<ValidationError> Warnings { get; }

        /// <summary>Gets a value indicating whether the result succeeded with warnings.</summary>
        public bool HasWarnings => Warnings.Count > 0;

        /// <summary>
        /// Gets the encapsulated value when <see cref="IsSuccess"/> is true.
        /// Throws <see cref="InvalidOperationException"/> if accessed on a failed result.
        /// </summary>
        public T Value
        {
            get
            {
                if (!IsSuccess)
                {
                    throw new InvalidOperationException(
                        $"Cannot access Value on a failed Result. Errors: {string.Join("; ", Errors)}");
                }
                return _value!;
            }
        }

        private Result(bool isSuccess, T? value, IReadOnlyList<ValidationError> errors, IReadOnlyList<ValidationError> warnings)
        {
            IsSuccess = isSuccess;
            _value = value;
            Errors = errors;
            Warnings = warnings;
        }

        /// <summary>Creates a successful result without warnings.</summary>
        /// <param name="value">The value to encapsulate; must not be <c>null</c>.</param>
        /// <exception cref="ArgumentNullException"><paramref name="value"/> is <c>null</c>.</exception>
        public static Result<T> Success(T value)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            return new Result<T>(true, value, EmptyErrors, EmptyErrors);
        }

        /// <summary>Creates a successful result that carries non-fatal <paramref name="warnings"/>.</summary>
        public static Result<T> Success(T value, IEnumerable<ValidationError>? warnings)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            List<ValidationError> list = warnings?.ToList() ?? new List<ValidationError>(0);
            return new Result<T>(true, value, EmptyErrors, list.Count == 0 ? EmptyErrors : new ReadOnlyCollection<ValidationError>(list));
        }

        /// <summary>Creates a failed result with one finding.</summary>
        /// <param name="error">The finding that caused the failure; must not be <c>null</c>.</param>
        /// <exception cref="ArgumentNullException"><paramref name="error"/> is <c>null</c>.</exception>
        public static Result<T> Failure(ValidationError error)
        {
            if (error == null)
                throw new ArgumentNullException(nameof(error));

            return new Result<T>(false, default, new ReadOnlyCollection<ValidationError>(new[] { error }), EmptyErrors);
        }

        /// <summary>Creates a failed result with every given finding.</summary>
        /// <param name="errors">The findings that caused the failure; must not be <c>null</c> or empty.</param>
        /// <exception cref="ArgumentNullException"><paramref name="errors"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentException"><paramref name="errors"/> is empty.</exception>
        public static Result<T> Failure(IEnumerable<ValidationError> errors)
        {
            if (errors == null)
                throw new ArgumentNullException(nameof(errors));

            var errorList = errors.ToList();
            if (errorList.Count == 0)
                throw new ArgumentException("A failed Result must contain at least one ValidationError.", nameof(errors));

            return new Result<T>(false, default, new ReadOnlyCollection<ValidationError>(errorList), EmptyErrors);
        }

        /// <summary>Creates a failed result with one error-severity finding built from the given parts.</summary>
        /// <param name="code">The stable machine-readable code of the finding; must not be blank.</param>
        /// <param name="message">The human-readable description; must not be blank.</param>
        /// <param name="category">The processing stage that produced the finding.</param>
        /// <param name="location">Where the finding is (XPath, source or element name); <c>null</c> when unknown.</param>
        public static Result<T> Failure(string code, string message, ErrorCategory category, string? location = null)
        {
            return Failure(new ValidationError(code, message, category, ValidationSeverity.Error, location));
        }

        /// <summary>Gets the value when the result succeeded.</summary>
        /// <param name="value">The value on success; <c>default</c> on failure.</param>
        /// <returns><c>true</c> when the result succeeded.</returns>
        public bool TryGetValue(out T value)
        {
            value = IsSuccess ? _value! : default!;
            return IsSuccess;
        }
    }
}
