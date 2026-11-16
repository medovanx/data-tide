namespace DataTide.Core.Usage;

/// <summary>A network interface as the logger sees it: a stable id plus a name for reports.</summary>
public sealed record NetInterface(string Id, string Name, string Description, long Received, long Sent);
