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

        public bool IsSuccess { get; }
        public bool IsFailure => !IsSuccess;
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

        public static Result<T> Failure(ValidationError error)
        {
            if (error == null)
                throw new ArgumentNullException(nameof(error));

            return new Result<T>(false, default, new ReadOnlyCollection<ValidationError>(new[] { error }), EmptyErrors);
        }

        public static Result<T> Failure(IEnumerable<ValidationError> errors)
        {
            if (errors == null)
                throw new ArgumentNullException(nameof(errors));

            var errorList = errors.ToList();
            if (errorList.Count == 0)
                throw new ArgumentException("A failed Result must contain at least one ValidationError.", nameof(errors));

            return new Result<T>(false, default, new ReadOnlyCollection<ValidationError>(errorList), EmptyErrors);
        }

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

        /// <summary>
        /// Projects a successful value with <paramref name="map"/>; a failure is propagated with its errors.
        /// The warnings of a successful result are kept.
        /// </summary>
        public Result<TResult> Map<TResult>(Func<T, TResult> map)
        {
            if (map == null)
                throw new ArgumentNullException(nameof(map));

            return IsSuccess
                ? Result<TResult>.Success(map(_value!), Warnings)
                : Result<TResult>.Failure(Errors);
        }

        /// <summary>
        /// Chains an operation that can itself fail: on success runs <paramref name="bind"/> and returns its result with
        /// the warnings of both results (this one first); a failure is propagated without running <paramref name="bind"/>.
        /// </summary>
        public Result<TResult> Bind<TResult>(Func<T, Result<TResult>> bind)
        {
            if (bind == null)
                throw new ArgumentNullException(nameof(bind));

            if (IsFailure)
                return Result<TResult>.Failure(Errors);

            Result<TResult> next = bind(_value!) ?? throw new InvalidOperationException("The bind function returned null.");
            if (next.IsFailure || Warnings.Count == 0)
                return next;

            return Result<TResult>.Success(next._value!, Warnings.Concat(next.Warnings));
        }

        /// <summary>Reduces the result to a single value: <paramref name="onSuccess"/> or <paramref name="onFailure"/>.</summary>
        public TResult Match<TResult>(Func<T, TResult> onSuccess, Func<IReadOnlyList<ValidationError>, TResult> onFailure)
        {
            if (onSuccess == null)
                throw new ArgumentNullException(nameof(onSuccess));
            if (onFailure == null)
                throw new ArgumentNullException(nameof(onFailure));

            return IsSuccess ? onSuccess(_value!) : onFailure(Errors);
        }

        /// <summary>Runs <paramref name="onSuccess"/> or <paramref name="onFailure"/>.</summary>
        public void Match(Action<T> onSuccess, Action<IReadOnlyList<ValidationError>> onFailure)
        {
            if (onSuccess == null)
                throw new ArgumentNullException(nameof(onSuccess));
            if (onFailure == null)
                throw new ArgumentNullException(nameof(onFailure));

            if (IsSuccess)
                onSuccess(_value!);
            else
                onFailure(Errors);
        }
    }
}
