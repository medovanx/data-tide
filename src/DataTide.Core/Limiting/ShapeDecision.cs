namespace DataTide.Core.Limiting;

/// <summary>What to do with a packet: send it now, hold it in the queue, or drop it because the queue is full.</summary>
public enum ShapeDecision { Pass, Queued, Dropped }
