namespace DataTide.Limiting;

/// <summary>
/// Per-program bytes per second over one-second windows, plus dropped bytes, for every program the limiter sees.
/// The worker adds, the pipe thread reads a snapshot. Programs quiet for two minutes are forgotten.
/// </summary>
sealed class TrafficMeter
{
    const double ForgetSeconds = 120;

    sealed class Entry(string path)
    {
        public readonly string Path = path;
        public long Down, Up, Dropped;
        public double DownRate, UpRate, LastSeen;
    }

    readonly object gate = new();
    readonly Dictionary<string, Entry> entries = new(StringComparer.OrdinalIgnoreCase);
    double windowStart;

    public void Add(string path, Direction dir, int bytes, double now)
    {
        lock (gate)
        {
            var e = Get(path);
            if (dir == Direction.Download) e.Down += bytes; else e.Up += bytes;
            e.LastSeen = now;
        }
    }

    public void Drop(string path, int bytes)
    {
        lock (gate) Get(path).Dropped += bytes;
    }

    Entry Get(string path)
    {
        if (!entries.TryGetValue(path, out var e)) entries[path] = e = new Entry(path);
        return e;
    }

    /// <summary>Closes the window once a second has passed, turning byte counts into rates.</summary>
    public void Roll(double now)
    {
        double elapsed = now - windowStart;
        if (elapsed < 1) return;
        lock (gate)
        {
            List<string>? stale = null;
            foreach (var e in entries.Values)
            {
                e.DownRate = e.Down / elapsed;
                e.UpRate = e.Up / elapsed;
                e.Down = e.Up = 0;
                if (now - e.LastSeen > ForgetSeconds) (stale ??= []).Add(e.Path);
            }
            if (stale != null) foreach (var p in stale) entries.Remove(p);
        }
        windowStart = now;
    }

    public IReadOnlyList<ProcessTraffic> Snapshot()
    {
        lock (gate)
            return [.. entries.Values.Select(e => new ProcessTraffic(e.Path, e.DownRate, e.UpRate, e.Dropped))
                .OrderByDescending(t => t.DownBytesPerSecond + t.UpBytesPerSecond)];
    }
}
