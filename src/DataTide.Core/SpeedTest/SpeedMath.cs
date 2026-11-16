namespace DataTide.Core.SpeedTest;

/// <summary>Turns raw speed-test measurements into the numbers people expect from a speed test.</summary>
public static class SpeedMath
{
    /// <summary>
    /// Throughput in bits per second from per-slice samples. The first <paramref name="warmupSeconds"/> are left out
    /// (TCP is still ramping up), then the slowest and fastest tenth of the rest are trimmed, like common speed tests do.
    /// </summary>
    public static double BitsPerSecond(IReadOnlyList<SpeedSample> samples, double warmupSeconds = 1.5)
    {
        var steady = samples.Where(s => s.Seconds > 0).ToList();
        double t = 0;
        var kept = new List<double>();
        foreach (var s in steady)
        {
            t += s.Seconds;
            if (t > warmupSeconds) kept.Add(s.Bytes * 8 / s.Seconds);
        }
        if (kept.Count == 0) kept = steady.Select(s => s.Bytes * 8 / s.Seconds).ToList();
        if (kept.Count == 0) return 0;
        kept.Sort();
        int trim = kept.Count / 10;
        var middle = kept.Skip(trim).Take(kept.Count - 2 * trim).ToList();
        return middle.Count == 0 ? kept.Average() : middle.Average();
    }

    /// <summary>Latency as the median of round trips, and jitter as the mean difference between consecutive round trips.</summary>
    public static (double pingMs, double jitterMs) Latency(IReadOnlyList<double> roundTripsMs)
    {
        if (roundTripsMs.Count == 0) return (0, 0);
        var sorted = roundTripsMs.OrderBy(x => x).ToList();
        double median = sorted.Count % 2 == 1 ? sorted[sorted.Count / 2] : (sorted[sorted.Count / 2 - 1] + sorted[sorted.Count / 2]) / 2;
        double jitter = 0;
        for (int i = 1; i < roundTripsMs.Count; i++) jitter += Math.Abs(roundTripsMs[i] - roundTripsMs[i - 1]);
        return (median, roundTripsMs.Count > 1 ? jitter / (roundTripsMs.Count - 1) : 0);
    }
}
