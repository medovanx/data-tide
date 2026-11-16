namespace DataTide.Limiting;

/// <summary>What the elevated helper reports: whether limiting works, why not if it does not, and per-program traffic.</summary>
public sealed record LimiterStatus(bool Running, string? Error, IReadOnlyList<ProcessTraffic> Processes);
