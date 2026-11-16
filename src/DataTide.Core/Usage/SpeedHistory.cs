namespace DataTide.Core.Usage;

/// <summary>The last N speed readings (down and up), oldest first, for the sparkline.</summary>
public sealed class SpeedHistory(int capacity = 60)
{
    readonly Queue<(double down, double up)> items = new();

    public int Capacity { get; } = capacity;

    public void Add(double down, double up)
    {
        items.Enqueue((down, up));
        while (items.Count > Capacity) items.Dequeue();
    }

    public IReadOnlyList<(double down, double up)> Items => [.. items];

    public double Peak => items.Count == 0 ? 0 : items.Max(i => Math.Max(i.down, i.up));

    public void Clear() => items.Clear();
}
