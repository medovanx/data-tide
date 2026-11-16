namespace DataTide.Core.Limiting;

/// <summary>
/// Per-program, per-direction rate limiting. A packet passes if its program's bucket holds enough bytes,
/// otherwise it waits in a short queue that drains as tokens refill; when that queue is full the packet is dropped,
/// which makes TCP slow down. Holding inbound packets is what limits downloads.
/// Thread-safe; time is passed in as seconds.
/// </summary>
public sealed class Shaper<TPacket>
{
    /// <summary>At most this much traffic waits per program and direction before packets are dropped.</summary>
    public double MaxQueueSeconds { get; set; } = 0.5;
    public int MaxQueuePackets { get; set; } = 400;

    sealed class Lane(TokenBucket bucket)
    {
        public readonly TokenBucket Bucket = bucket;
        public readonly Queue<(TPacket packet, int size)> Waiting = new();
        public long QueuedBytes;
        public long Passed, Dropped;
    }

    readonly object gate = new();
    readonly Dictionary<(string key, Direction dir), Lane> lanes = new();

    /// <summary>
    /// Sets (or clears, with 0) the limit for a program and direction.
    /// Clearing returns the packets that were still waiting, so the caller can send them.
    /// </summary>
    public List<(TPacket packet, Direction dir)> SetLimit(string key, Direction dir, long bytesPerSecond, double now)
    {
        lock (gate)
        {
            var k = (Norm(key), dir);
            if (bytesPerSecond <= 0)
                return lanes.Remove(k, out var gone) ? [.. gone.Waiting.Select(w => (w.packet, dir))] : [];
            if (lanes.TryGetValue(k, out var lane)) lane.Bucket.SetRate(bytesPerSecond);
            else lanes[k] = new Lane(new TokenBucket(bytesPerSecond, now));
            return [];
        }
    }

    /// <summary>Removes every limit and returns all packets that were waiting.</summary>
    public List<(TPacket packet, Direction dir)> ClearAll()
    {
        lock (gate)
        {
            var all = lanes.SelectMany(kv => kv.Value.Waiting.Select(w => (w.packet, kv.Key.dir))).ToList();
            lanes.Clear();
            return all;
        }
    }

    /// <summary>Programs and directions that currently have a limit.</summary>
    public IReadOnlyList<(string key, Direction dir)> Limited()
    {
        lock (gate) return [.. lanes.Keys];
    }

    public bool IsLimited(string key, Direction dir)
    {
        lock (gate) return lanes.ContainsKey((Norm(key), dir));
    }

    static string Norm(string key) => key.ToUpperInvariant();

    /// <summary>Decides for one packet of <paramref name="size"/> bytes belonging to <paramref name="key"/> (null = unknown program).</summary>
    public ShapeDecision Offer(string? key, Direction dir, TPacket packet, int size, double now)
    {
        if (key == null) return ShapeDecision.Pass;
        lock (gate)
        {
            if (!lanes.TryGetValue((Norm(key), dir), out var lane)) return ShapeDecision.Pass;
            // Keep order: nothing overtakes packets already waiting.
            if (lane.Waiting.Count == 0 && lane.Bucket.TryTake(size, now)) { lane.Passed += size; return ShapeDecision.Pass; }
            double maxBytes = Math.Max(lane.Bucket.Rate * MaxQueueSeconds, 3 * 1514);
            if (lane.Waiting.Count >= MaxQueuePackets || lane.QueuedBytes + size > maxBytes)
            {
                lane.Dropped += size;
                return ShapeDecision.Dropped;
            }
            lane.Waiting.Enqueue((packet, size));
            lane.QueuedBytes += size;
            return ShapeDecision.Queued;
        }
    }

    /// <summary>Releases every waiting packet whose turn has come, in order, with its direction.</summary>
    public List<(TPacket packet, Direction dir)> Drain(double now)
    {
        var due = new List<(TPacket, Direction)>();
        lock (gate)
            foreach (var ((_, dir), lane) in lanes)
                while (lane.Waiting.Count > 0 && lane.Bucket.TryTake(lane.Waiting.Peek().size, now))
                {
                    var (p, size) = lane.Waiting.Dequeue();
                    lane.QueuedBytes -= size;
                    lane.Passed += size;
                    due.Add((p, dir));
                }
        return due;
    }

    /// <summary>Seconds until the next waiting packet can go, or null when nothing waits.</summary>
    public double? NextDue(double now)
    {
        lock (gate)
        {
            double? best = null;
            foreach (var lane in lanes.Values)
                if (lane.Waiting.Count > 0)
                {
                    double w = lane.Bucket.WaitFor(lane.Waiting.Peek().size, now);
                    best = best == null ? w : Math.Min(best.Value, w);
                }
            return best;
        }
    }

    /// <summary>Bytes passed and dropped so far for a program and direction.</summary>
    public (long passed, long dropped) Stats(string key, Direction dir)
    {
        lock (gate) return lanes.TryGetValue((Norm(key), dir), out var l) ? (l.Passed, l.Dropped) : (0, 0);
    }
}
