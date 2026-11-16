namespace DataTide.Core.Usage;

/// <summary>Bytes received and sent on one interface during one hour (local time).</summary>
public sealed record UsageRecord(DateTime Hour, string InterfaceId, string InterfaceName, long Received, long Sent)
{
    public long Total => Received + Sent;
}
