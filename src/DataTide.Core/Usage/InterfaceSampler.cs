namespace DataTide.Core.Usage;

/// <summary>Keeps the last counter reading per interface and turns each new reading into deltas and speeds.</summary>
public sealed class InterfaceSampler
{
    readonly Dictionary<string, (long rx, long tx, DateTime at)> last = [];

    /// <summary>
    /// Feeds one reading of every interface. The first reading of an interface only sets its baseline,
    /// so traffic from before the app started is never counted.
    /// </summary>
    public IReadOnlyList<InterfaceSample> Sample(IEnumerable<NetInterface> interfaces, DateTime now)
    {
        var result = new List<InterfaceSample>();
        var seen = new HashSet<string>();
        foreach (var i in interfaces)
        {
            seen.Add(i.Id);
            if (last.TryGetValue(i.Id, out var prev))
            {
                double secs = (now - prev.at).TotalSeconds;
                if (secs <= 0) continue;
                long rx = CounterDelta.Compute(prev.rx, i.Received, secs);
                long tx = CounterDelta.Compute(prev.tx, i.Sent, secs);
                result.Add(new InterfaceSample(i.Id, i.Name, rx, tx, rx / secs, tx / secs));
            }
            last[i.Id] = (i.Received, i.Sent, now);
        }
        foreach (var gone in last.Keys.Where(k => !seen.Contains(k)).ToList()) last.Remove(gone);
        return result;
    }

    /// <summary>Forgets every baseline (after sleep), so the gap is not counted as one burst.</summary>
    public void Reset() => last.Clear();
}
