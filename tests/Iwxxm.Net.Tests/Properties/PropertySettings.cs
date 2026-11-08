using System;
using CsCheck;

namespace Iwxxm.Net.Tests.Properties
{
    /// <summary>
    /// Shared settings of the property-based tests (CsCheck, test-only; the core package keeps zero runtime dependencies).
    /// <para>
    /// How to read and reproduce a run:
    /// <list type="bullet">
    /// <item>Every property runs <see cref="Iterations"/> cases, single-threaded, from a FIXED seed (one constant per property
    /// group below), so a CI run is reproducible: the same code always sees the same cases.</item>
    /// <item>When a property fails, CsCheck shrinks the case and the exception message prints the shrunk input (through the
    /// <c>print</c> function of each property) together with a <c>Seed</c>. To replay that case alone, pass it as
    /// <c>seed:</c> to <see cref="Run{T}"/> (or set the environment variable <c>CsCheck_Seed</c>) and set the iteration
    /// count to 1.</item>
    /// <item>The seeds are opaque CsCheck seed strings. Changing a seed only changes WHICH cases are explored; it does not
    /// change what is asserted. Exploring more of the input space is done by raising <see cref="Iterations"/>
    /// locally (each property is sized to stay well under about two seconds at the default).</item>
    /// </list>
    /// </para>
    /// </summary>
    internal static class PropertySettings
    {
        /// <summary>Number of generated cases per property.</summary>
        public const long Iterations = 200;

        /// <summary>Fixed seed of the <c>GeoCoordinate</c> properties.</summary>
        public const string GeoCoordinateSeed = "0N0XIzNsQ0O2";

        /// <summary>Fixed seed of the length and speed properties.</summary>
        public const string MeasureSeed = "0LcaBbRYS7Zy";

        /// <summary>Fixed seed of the GeoJSON geometry properties.</summary>
        public const string GeometrySeed = "0QOiJyfJrGP5";

        /// <summary>Fixed seed of the StripClipper differential test.</summary>
        public const string StripClipperSeed = "0Hn2VqXc8TrA";

        /// <summary>
        /// Runs <paramref name="property"/> (it asserts and throws on violation) on <see cref="Iterations"/> generated cases,
        /// starting from <paramref name="seed"/>, on a single thread, printing a failing case with <paramref name="print"/>.
        /// </summary>
        public static void Run<T>(Gen<T> gen, string seed, Action<T> property, Func<T, string>? print = null, long iterations = Iterations)
        {
            gen.Sample(property, seed: seed, iter: iterations, threads: 1, print: print ?? Check.Print);
        }
    }
}
