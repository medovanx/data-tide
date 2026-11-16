namespace DataTide.Core.Usage;

/// <summary>Totals for one interface over a report's range.</summary>
public sealed record InterfaceTotal(string InterfaceId, string InterfaceName, long Received, long Sent)
{
    public long Total => Received + Sent;
}
