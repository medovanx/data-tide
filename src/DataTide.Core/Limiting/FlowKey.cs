using System.Net;

namespace DataTide.Core.Limiting;

/// <summary>
/// The local end of a TCP or UDP flow: protocol (6 or 17), local address and port.
/// This is what the Windows connection tables map to a process id.
/// </summary>
public readonly record struct FlowKey(byte Protocol, IPAddress LocalAddress, ushort LocalPort)
{
    public override string ToString() => $"{(Protocol == 6 ? "TCP" : "UDP")} {LocalAddress}:{LocalPort}";
}
