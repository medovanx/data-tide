namespace DataTide.Core.Usage;

/// <summary>Turns two readings of a cumulative byte counter into the bytes moved between them.</summary>
public static class CounterDelta
{
    /// <summary>Faster than any real link (100 Gbit/s), so a bigger jump is a counter glitch, not traffic.</summary>
    public const double MaxBytesPerSecond = 12.5e9;

    /// <summary>
    /// Bytes moved between <paramref name="previous"/> and <paramref name="current"/>.
    /// A counter that went backwards was reset (adapter re-enabled, driver reload, sleep):
    /// what it shows now is what moved since the reset. Impossible jumps count as nothing.
    /// </summary>
    public static long Compute(long previous, long current, double elapsedSeconds)
    {
        if (current < 0) return 0;
        long delta = current >= previous ? current - previous : current;
        double limit = MaxBytesPerSecond * Math.Max(elapsedSeconds, 1);
        return delta > limit ? 0 : delta;
    }
}
