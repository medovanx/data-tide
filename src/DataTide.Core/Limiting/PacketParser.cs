using System.Buffers.Binary;
using System.Net;

namespace DataTide.Core.Limiting;

/// <summary>Reads just enough of an Ethernet frame (IPv4 or IPv6, then TCP or UDP) to find the local end of its flow.</summary>
public static class PacketParser
{
    public const int EthernetHeader = 14;

    /// <summary>
    /// For an outgoing frame the local end is the source; for an incoming one it is the destination.
    /// Returns false for anything that is not TCP or UDP over IP (ARP, ICMP, fragments without ports...).
    /// </summary>
    public static bool TryGetLocalEnd(ReadOnlySpan<byte> frame, Direction dir, out FlowKey key)
    {
        key = default;
        if (frame.Length < EthernetHeader) return false;
        int off = 12;
        ushort type = BinaryPrimitives.ReadUInt16BigEndian(frame[off..]);
        off += 2;
        if (type == 0x8100 && frame.Length >= off + 4) // 802.1Q VLAN tag
        {
            type = BinaryPrimitives.ReadUInt16BigEndian(frame[(off + 2)..]);
            off += 4;
        }
        var ip = frame[off..];
        byte proto;
        IPAddress local;
        ReadOnlySpan<byte> l4;
        if (type == 0x0800)
        {
            if (ip.Length < 20 || ip[0] >> 4 != 4) return false;
            int ihl = (ip[0] & 0x0F) * 4;
            if (ihl < 20 || ip.Length < ihl) return false;
            // A later fragment has no ports.
            if ((BinaryPrimitives.ReadUInt16BigEndian(ip[6..]) & 0x1FFF) != 0) return false;
            proto = ip[9];
            local = new IPAddress(dir == Direction.Upload ? ip.Slice(12, 4) : ip.Slice(16, 4));
            l4 = ip[ihl..];
        }
        else if (type == 0x86DD)
        {
            if (ip.Length < 40 || ip[0] >> 4 != 6) return false;
            proto = ip[6]; // extension headers are not followed: such flows pass unshaped
            local = new IPAddress(dir == Direction.Upload ? ip.Slice(8, 16) : ip.Slice(24, 16));
            l4 = ip[40..];
        }
        else return false;

        if (proto is not (6 or 17) || l4.Length < 4) return false;
        ushort port = BinaryPrimitives.ReadUInt16BigEndian(dir == Direction.Upload ? l4 : l4[2..]);
        key = new FlowKey(proto, local, port);
        return true;
    }
}
