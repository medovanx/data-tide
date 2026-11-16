namespace DataTide.Core.Usage;

/// <summary>Bytes received and sent on one interface during one day.</summary>
public sealed record DailyUsage(DateOnly Date, string InterfaceId, string InterfaceName, long Received, long Sent)
{
    public long Total => Received + Sent;
}
