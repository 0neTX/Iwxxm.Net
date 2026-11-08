using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Iwxxm.Net.Tests.Scale
{
    /// <summary>One measurement of a scenario at one input size: median wall-clock time and median allocated bytes.</summary>
    internal sealed class ScalePoint
    {
        public ScalePoint(int size, double milliseconds, long allocatedBytes, object? lastResult)
        {
            Size = size;
            Milliseconds = milliseconds;
            AllocatedBytes = allocatedBytes;
            LastResult = lastResult;
        }

        public int Size { get; }

        public double Milliseconds { get; }

        /// <summary>Bytes allocated by the measuring thread, or -1 when the target framework cannot report it (net48).</summary>
        public long AllocatedBytes { get; }

        public object? LastResult { get; }
    }

    /// <summary>The measurements of one scenario phase at increasing input sizes, with growth ratios and log-log exponents.</summary>
    internal sealed class ScaleSeries
    {
        /// <summary>
        /// A 4x larger input may cost at most 10x more time (exponent about 1.66; linear is 4x, quadratic 16x). The bound is
        /// applied as a power of the size ratio, so it is the same exponent whatever the number of steps.
        /// </summary>
        public const double MaxTimeExponent = 1.66;

        /// <summary>A 4x larger input may allocate at most 6x more (exponent about 1.29).</summary>
        public const double MaxMemoryExponent = 1.29;

        /// <summary>Times shorter than this are noise on a shared runner: the ratio is computed against this floor.</summary>
        public const double TimeFloorMilliseconds = 10.0;

        public ScaleSeries(string name, string unit, IReadOnlyList<ScalePoint> points)
        {
            Name = name;
            Unit = unit;
            Points = points;
        }

        public string Name { get; }

        public string Unit { get; }

        public IReadOnlyList<ScalePoint> Points { get; }

        public bool HasMemory => Points.All(p => p.AllocatedBytes >= 0);

        private ScalePoint First => Points[0];

        private ScalePoint Last => Points[Points.Count - 1];

        public double SizeRatio => (double)Last.Size / First.Size;

        public double TimeRatio => Last.Milliseconds / Math.Max(First.Milliseconds, TimeFloorMilliseconds);

        public double MemoryRatio => (double)Last.AllocatedBytes / Math.Max(1, First.AllocatedBytes);

        public double MaxTimeRatio => Math.Pow(SizeRatio, MaxTimeExponent);

        public double MaxMemoryRatio => Math.Pow(SizeRatio, MaxMemoryExponent);

        /// <summary>Least-squares slope of log(time) over log(size): 1 is linear, 2 is quadratic.</summary>
        public double TimeExponent => Slope(Points.Select(p => Math.Max(p.Milliseconds, 0.001)).ToList());

        public double MemoryExponent => HasMemory ? Slope(Points.Select(p => (double)Math.Max(1, p.AllocatedBytes)).ToList()) : double.NaN;

        private double Slope(IReadOnlyList<double> values)
        {
            double[] x = Points.Select(p => Math.Log(p.Size)).ToArray();
            double[] y = values.Select(v => Math.Log(v)).ToArray();
            double mx = x.Average();
            double my = y.Average();
            double numerator = 0;
            double denominator = 0;
            for (int i = 0; i < x.Length; i++)
            {
                numerator += (x[i] - mx) * (y[i] - my);
                denominator += (x[i] - mx) * (x[i] - mx);
            }

            return numerator / denominator;
        }

        public string ToTable()
        {
            var sb = new StringBuilder();
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "[scale] {0}", Name));
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "  {0,-12} {1,10} {2,10} {3,10} {4,10}", Unit, "ms", "MB", "ms ratio", "MB ratio"));
            for (int i = 0; i < Points.Count; i++)
            {
                ScalePoint p = Points[i];
                string timeRatio = i == 0 ? "-" : (p.Milliseconds / Math.Max(Points[i - 1].Milliseconds, 0.001)).ToString("0.00", CultureInfo.InvariantCulture);
                string memory = p.AllocatedBytes < 0 ? "n/a" : (p.AllocatedBytes / 1048576.0).ToString("0.00", CultureInfo.InvariantCulture);
                string memoryRatio = i == 0 || p.AllocatedBytes < 0 ? "-" : ((double)p.AllocatedBytes / Math.Max(1, Points[i - 1].AllocatedBytes)).ToString("0.00", CultureInfo.InvariantCulture);
                sb.AppendLine(string.Format(
                    CultureInfo.InvariantCulture, "  {0,-12} {1,10:0.0} {2,10} {3,10} {4,10}", p.Size, p.Milliseconds, memory, timeRatio, memoryRatio));
            }

            sb.AppendLine(string.Format(
                CultureInfo.InvariantCulture,
                "  size x{0:0.#}: time x{1:0.0} (limit x{2:0.0}), memory {3} (limit x{4:0.0}); exponent time {5:0.00}, memory {6}",
                SizeRatio,
                TimeRatio,
                MaxTimeRatio,
                HasMemory ? "x" + MemoryRatio.ToString("0.0", CultureInfo.InvariantCulture) : "n/a",
                MaxMemoryRatio,
                TimeExponent,
                HasMemory ? MemoryExponent.ToString("0.00", CultureInfo.InvariantCulture) : "n/a"));
            return sb.ToString();
        }
    }

    internal static class ScaleMeasurement
    {
        /// <summary>
        /// Runs <paramref name="action"/> once as a warm-up (JIT, caches, lazy initialisation), then <paramref name="repetitions"/>
        /// times, each after a full collection, and keeps the median time and the median allocation of the timed runs.
        /// </summary>
        public static ScalePoint Measure(int size, Func<object?> action, int repetitions = 3)
        {
            object? result = action();
            var times = new List<double>(repetitions);
            var allocations = new List<long>(repetitions);
            for (int i = 0; i < repetitions; i++)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();

                long before = AllocatedBytes();
                var stopwatch = Stopwatch.StartNew();
                result = action();
                stopwatch.Stop();
                long after = AllocatedBytes();

                times.Add(stopwatch.Elapsed.TotalMilliseconds);
                allocations.Add(before < 0 ? -1 : after - before);
            }

            times.Sort();
            allocations.Sort();
            return new ScalePoint(size, times[times.Count / 2], allocations[allocations.Count / 2], result);
        }

        public static long AllocatedBytes()
        {
#if NETFRAMEWORK
            return -1;
#else
            return GC.GetAllocatedBytesForCurrentThread();
#endif
        }
    }
}
