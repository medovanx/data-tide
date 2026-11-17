using System.Buffers.Binary;
using System.Net;
using DataTide.Core.Limiting;

namespace DataTide.Core.Tests;

public class PacketParserTests
{
    static readonly IPAddress Local4 = IPAddress.Parse("192.168.1.20");
    static readonly IPAddress Remote4 = IPAddress.Parse("93.184.216.34");
    static readonly IPAddress Local6 = IPAddress.Parse("2001:db8::20");
    static readonly IPAddress Remote6 = IPAddress.Parse("2606:2800:220:1::34");

    /// <summary>Ethernet + IPv4 (optionally with options or VLAN tag) + the first bytes of a TCP/UDP header.</summary>
    static byte[] Ipv4Frame(byte proto, IPAddress src, IPAddress dst, ushort srcPort, ushort dstPort,
        int optionWords = 0, bool vlan = false, ushort fragment = 0)
    {
        int eth = vlan ? 18 : 14, ihl = 20 + optionWords * 4;
        var f = new byte[eth + ihl + 20];
        if (vlan)
        {
            BinaryPrimitives.WriteUInt16BigEndian(f.AsSpan(12), 0x8100);
            BinaryPrimitives.WriteUInt16BigEndian(f.AsSpan(14), 0x0005);
            BinaryPrimitives.WriteUInt16BigEndian(f.AsSpan(16), 0x0800);
        }
        else BinaryPrimitives.WriteUInt16BigEndian(f.AsSpan(12), 0x0800);
        var ip = f.AsSpan(eth);
        ip[0] = (byte)(0x40 | (ihl / 4));
        BinaryPrimitives.WriteUInt16BigEndian(ip[2..], (ushort)(ihl + 20));
        BinaryPrimitives.WriteUInt16BigEndian(ip[6..], fragment);
        ip[8] = 64;
        ip[9] = proto;
        src.GetAddressBytes().CopyTo(ip[12..]);
        dst.GetAddressBytes().CopyTo(ip[16..]);
        BinaryPrimitives.WriteUInt16BigEndian(ip[ihl..], srcPort);
        BinaryPrimitives.WriteUInt16BigEndian(ip[(ihl + 2)..], dstPort);
        return f;
    }

    static byte[] Ipv6Frame(byte nextHeader, IPAddress src, IPAddress dst, ushort srcPort, ushort dstPort)
    {
        var f = new byte[14 + 40 + 20];
        BinaryPrimitives.WriteUInt16BigEndian(f.AsSpan(12), 0x86DD);
        var ip = f.AsSpan(14);
        ip[0] = 0x60;
        BinaryPrimitives.WriteUInt16BigEndian(ip[4..], 20);
        ip[6] = nextHeader;
        ip[7] = 64;
        src.GetAddressBytes().CopyTo(ip[8..]);
        dst.GetAddressBytes().CopyTo(ip[24..]);
        BinaryPrimitives.WriteUInt16BigEndian(ip[40..], srcPort);
        BinaryPrimitives.WriteUInt16BigEndian(ip[42..], dstPort);
        return f;
    }

    [Theory]
    [InlineData((byte)6)]
    [InlineData((byte)17)]
    public void Ipv4_upload_takes_the_source_as_local_end(byte proto)
    {
        var frame = Ipv4Frame(proto, Local4, Remote4, 50123, 443);
        Assert.True(PacketParser.TryGetLocalEnd(frame, Direction.Upload, out var key));
        Assert.Equal(new FlowKey(proto, Local4, 50123), key);
    }

    [Theory]
    [InlineData((byte)6)]
    [InlineData((byte)17)]
    public void Ipv4_download_takes_the_destination_as_local_end(byte proto)
    {
        var frame = Ipv4Frame(proto, Remote4, Local4, 443, 50123);
        Assert.True(PacketParser.TryGetLocalEnd(frame, Direction.Download, out var key));
        Assert.Equal(new FlowKey(proto, Local4, 50123), key);
    }

    [Theory]
    [InlineData((byte)6)]
    [InlineData((byte)17)]
    public void Ipv6_upload_and_download_find_the_local_end(byte proto)
    {
        Assert.True(PacketParser.TryGetLocalEnd(Ipv6Frame(proto, Local6, Remote6, 40000, 53), Direction.Upload, out var up));
        Assert.Equal(new FlowKey(proto, Local6, 40000), up);

        Assert.True(PacketParser.TryGetLocalEnd(Ipv6Frame(proto, Remote6, Local6, 53, 40000), Direction.Download, out var down));
        Assert.Equal(new FlowKey(proto, Local6, 40000), down);
    }

    [Fact]
    public void Remote_port_is_the_far_end_both_ways()
    {
        var up = Ipv4Frame(17, Local4, Remote4, 50000, 53);
        Assert.True(PacketParser.TryGetFlow(up, Direction.Upload, out var k1, out var r1));
        Assert.Equal(50000, k1.LocalPort);
        Assert.Equal(53, r1);
        var down = Ipv4Frame(17, Remote4, Local4, 53, 50000);
        Assert.True(PacketParser.TryGetFlow(down, Direction.Download, out var k2, out var r2));
        Assert.Equal(50000, k2.LocalPort);
        Assert.Equal(53, r2);
    }

    [Fact]
    public void Ipv4_options_are_skipped_using_the_header_length()
    {
        var frame = Ipv4Frame(6, Remote4, Local4, 80, 61000, optionWords: 2);
        Assert.True(PacketParser.TryGetLocalEnd(frame, Direction.Download, out var key));
        Assert.Equal(new FlowKey(6, Local4, 61000), key);
    }

    [Fact]
    public void Vlan_tag_is_stepped_over()
    {
        var frame = Ipv4Frame(17, Local4, Remote4, 5353, 5353, vlan: true);
        Assert.True(PacketParser.TryGetLocalEnd(frame, Direction.Upload, out var key));
        Assert.Equal(new FlowKey(17, Local4, 5353), key);
    }

    [Fact]
    public void A_first_fragment_still_has_ports()
    {
        var frame = Ipv4Frame(17, Local4, Remote4, 7000, 8000, fragment: 0x2000); // more-fragments, offset 0
        Assert.True(PacketParser.TryGetLocalEnd(frame, Direction.Upload, out var key));
        Assert.Equal((ushort)7000, key.LocalPort);
    }

    [Fact]
    public void A_later_fragment_has_no_ports()
    {
        var frame = Ipv4Frame(17, Local4, Remote4, 7000, 8000, fragment: 0x00B9);
        Assert.False(PacketParser.TryGetLocalEnd(frame, Direction.Upload, out _));
    }

    [Fact]
    public void Icmp_is_not_a_flow()
    {
        Assert.False(PacketParser.TryGetLocalEnd(Ipv4Frame(1, Local4, Remote4, 0, 0), Direction.Upload, out _));
        Assert.False(PacketParser.TryGetLocalEnd(Ipv6Frame(58, Local6, Remote6, 0, 0), Direction.Upload, out _));
    }

    [Fact]
    public void Arp_and_short_frames_are_rejected()
    {
        var arp = new byte[42];
        BinaryPrimitives.WriteUInt16BigEndian(arp.AsSpan(12), 0x0806);
        Assert.False(PacketParser.TryGetLocalEnd(arp, Direction.Download, out _));
        Assert.False(PacketParser.TryGetLocalEnd(new byte[10], Direction.Download, out _));

        var cut = Ipv4Frame(6, Local4, Remote4, 1, 2)[..(14 + 20 + 2)]; // ports cut off
        Assert.False(PacketParser.TryGetLocalEnd(cut, Direction.Upload, out _));
    }

    [Fact]
    public void Wrong_ip_version_nibble_is_rejected()
    {
        var frame = Ipv4Frame(6, Local4, Remote4, 1, 2);
        frame[14] = 0x65;
        Assert.False(PacketParser.TryGetLocalEnd(frame, Direction.Upload, out _));
    }
}
