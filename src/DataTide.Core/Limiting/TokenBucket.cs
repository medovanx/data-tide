namespace DataTide.Core.Limiting;

/// <summary>
/// Classic token bucket: tokens (bytes) refill at <see cref="Rate"/> per second up to <see cref="Burst"/>.
/// Time is passed in as seconds so the bucket can be tested without a clock.
/// </summary>
public sealed class TokenBucket
{
    double tokens;
    double last;

    public double Rate { get; private set; }
    public double Burst { get; private set; }

    public TokenBucket(double bytesPerSecond, double now)
    {
        last = now;
        SetRate(bytesPerSecond);
        tokens = Burst;
    }

    /// <summary>Changes the rate; the burst is a quarter second of traffic, never less than two full packets.</summary>
    public void SetRate(double bytesPerSecond)
    {
        Rate = Math.Max(1, bytesPerSecond);
        Burst = Math.Max(Rate / 4, 2 * 1514);
        tokens = Math.Min(tokens, Burst);
    }

    void Refill(double now)
    {
        if (now > last) tokens = Math.Min(Burst, tokens + (now - last) * Rate);
        last = Math.Max(last, now);
    }

    /// <summary>Takes <paramref name="bytes"/> if the bucket holds them.</summary>
    public bool TryTake(int bytes, double now)
    {
        Refill(now);
        if (tokens < bytes) return false;
        tokens -= bytes;
        return true;
    }

    /// <summary>Seconds until <paramref name="bytes"/> can be taken (0 when they can be now).</summary>
    public double WaitFor(int bytes, double now)
    {
        Refill(now);
        return tokens >= bytes ? 0 : (bytes - tokens) / Rate;
    }
}
