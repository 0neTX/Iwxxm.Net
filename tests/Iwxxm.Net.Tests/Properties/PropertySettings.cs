using System;
using CsCheck;

namespace Iwxxm.Net.Tests.Properties
{
    /// <summary>
    /// Shared settings of the property-based tests (CsCheck, test-only; the core package keeps zero runtime dependencies).
    /// <para>
    /// How to read and reproduce a run:
    /// <list type="bullet">
    /// <item>Every property runs <see cref="Iterations"/> cases on a single thread. The seed of a property group below is the seed of
    /// the FIRST case only: CsCheck (2.14) draws the seed of every later case at random, so the other cases differ from run to run
    /// (also with the <c>CsCheck_Seed</c> environment variable). A run is therefore not reproducible as a whole, and a rare failure
    /// can show up in any CI run.</item>
    /// <item>When a property fails, CsCheck shrinks the case and the exception message prints the shrunk input (through the
    /// <c>print</c> function of each property) together with a <c>Seed</c>, the seed of THAT case. To replay it, pass it as
    /// <c>seed:</c> to <see cref="Run{T}"/> with <c>iterations: 1</c>, and keep it as a test of its own.</item>
    /// <item>Raising <see cref="Iterations"/> locally explores more of the input space (each property is sized to stay well under
    /// about two seconds at the default); a failure found that way is replayed with its own seed as above.</item>
    /// </list>
    /// </para>
    /// </summary>
    internal static class PropertySettings
    {
        /// <summary>Number of generated cases per property.</summary>
        public const long Iterations = 200;

        /// <summary>Seed of the first case of the <c>GeoCoordinate</c> properties.</summary>
        public const string GeoCoordinateSeed = "0N0XIzNsQ0O2";

        /// <summary>Seed of the first case of the length and speed properties.</summary>
        public const string MeasureSeed = "0LcaBbRYS7Zy";

        /// <summary>Seed of the first case of the GeoJSON geometry properties.</summary>
        public const string GeometrySeed = "0QOiJyfJrGP5";

        /// <summary>Seed of the first case of the StripClipper differential test.</summary>
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
