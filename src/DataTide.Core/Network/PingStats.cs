namespace DataTide.Core.Network;

/// <summary>
/// The last N pings to one host: null for a ping that got no reply. Gives the latest round trip, the average,
/// jitter (mean change between consecutive replies) and loss over the window. Thread-safe.
/// </summary>
public sealed class PingStats(int window = 60)
{
    readonly object gate = new();
    readonly Queue<double?> samples = new();

    public void Add(double? roundTripMs)
    {
        lock (gate)
        {
            samples.Enqueue(roundTripMs);
            while (samples.Count > window) samples.Dequeue();
        }
    }

    public void Clear()
    {
        lock (gate) samples.Clear();
    }

    public IReadOnlyList<double?> Samples()
    {
        lock (gate) return [.. samples];
    }

    /// <summary>Latest reply (null if the last ping was lost or none yet), average, jitter and loss percent.</summary>
    public (double? last, double average, double jitter, double lossPercent, int count) Summary()
    {
        var s = Samples();
        if (s.Count == 0) return (null, 0, 0, 0, 0);
        var ok = s.Where(x => x.HasValue).Select(x => x!.Value).ToList();
        double jitter = 0;
        for (int i = 1; i < ok.Count; i++) jitter += Math.Abs(ok[i] - ok[i - 1]);
        return (s[^1], ok.Count == 0 ? 0 : ok.Average(), ok.Count > 1 ? jitter / (ok.Count - 1) : 0,
                100.0 * (s.Count - ok.Count) / s.Count, s.Count);
    }
}
