namespace DataTide.Limiting;

/// <summary>One program's current speed (over the last second) and the bytes the limiter has dropped for it so far.</summary>
public sealed record ProcessTraffic(string ExePath, double DownBytesPerSecond, double UpBytesPerSecond, long DroppedBytes);
