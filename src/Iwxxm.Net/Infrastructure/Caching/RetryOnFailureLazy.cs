using System;
using System.Threading;

namespace Iwxxm.Net.Infrastructure.Caching
{
    /// <summary>
    /// Thread-safe lazy initializer that caches a successfully created value but never caches a failure (AUD-L6).
    /// <para>
    /// <see cref="Lazy{T}"/> with <see cref="LazyThreadSafetyMode.ExecutionAndPublication"/> stores the exception thrown by
    /// its factory and rethrows it forever, so one transient failure (e.g. an out-of-memory condition while compiling a
    /// schema set) would disable validation for the life of the process. Here the factory runs at most once at a time;
    /// when it throws, the exception propagates to that caller and the next access runs the factory again.
    /// </para>
    /// </summary>
    /// <remarks>
    /// Intentional copy: this type exists twice, here and in <c>Iwxxm.Net.Validation.Saxon/Internal/RetryOnFailureLazy.cs</c>. The core package has zero runtime dependencies and the Saxon package must not rely on the core's internals, and the types must not become public API, so neither project can reference the other's copy.
    /// Keep both copies in sync when changing either one.
    /// </remarks>
    /// <typeparam name="T">Type of the cached value.</typeparam>
    internal sealed class RetryOnFailureLazy<T>
        where T : class
    {
        private readonly Func<T> _factory;
        private readonly object _gate = new object();
        private T? _value;

        public RetryOnFailureLazy(Func<T> factory)
        {
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        }

        /// <summary>Gets a value indicating whether a value has been created successfully.</summary>
        public bool IsValueCreated => Volatile.Read(ref _value) != null;

        /// <summary>Gets the cached value, creating it (or retrying a previously failed creation) when needed.</summary>
        public T Value
        {
            get
            {
                T? value = Volatile.Read(ref _value);
                if (value != null)
                {
                    return value;
                }

                lock (_gate)
                {
                    if (_value == null)
                    {
                        T created = _factory() ?? throw new InvalidOperationException("The lazy factory returned null.");
                        Volatile.Write(ref _value, created);
                    }

                    return _value!;
                }
            }
        }
    }
}
