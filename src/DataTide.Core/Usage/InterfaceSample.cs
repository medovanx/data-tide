namespace DataTide.Core.Usage;

/// <summary>What one interface did since the previous sample.</summary>
public sealed record InterfaceSample(string InterfaceId, string Name, long ReceivedDelta, long SentDelta,
                                     double DownBytesPerSecond, double UpBytesPerSecond);
