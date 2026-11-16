namespace DataTide.Limiting.Ndis;

/// <summary>
/// Sizes and flags from the WinpkFilter header include/Common.h (wiresock/ndisapi tag v3.6.2, commit ca8726c),
/// copied by hand because we talk to the driver directly instead of shipping ndisapi.dll.
/// </summary>
static class NdisConstants
{
    /// <summary>ADAPTER_NAME_SIZE (Common.h line 89).</summary>
    public const int AdapterNameSize = 256;
    /// <summary>ADAPTER_LIST_SIZE (Common.h line 90).</summary>
    public const int AdapterListSize = 32;
    /// <summary>ETHER_ADDR_LENGTH (Common.h line 91).</summary>
    public const int EtherAddrLength = 6;
    /// <summary>MAX_ETHER_FRAME of the standard driver build (Common.h line 96).</summary>
    public const int MaxEtherFrame = 1514;
    /// <summary>MAX_ETHER_FRAME when the driver is built with JUMBO_FRAME_SUPPORTED (Common.h line 94); our buffers are this big so either build fits.</summary>
    public const int MaxJumboFrame = 9014;

    /// <summary>MSTCP_FLAG_SENT_TUNNEL: outgoing packets are handed to us instead of the adapter (Common.h line 100).</summary>
    public const uint MstcpFlagSentTunnel = 0x00000001;
    /// <summary>MSTCP_FLAG_RECV_TUNNEL: incoming packets are handed to us instead of the TCP/IP stack (Common.h line 101).</summary>
    public const uint MstcpFlagRecvTunnel = 0x00000002;
    public const uint TunnelMode = MstcpFlagSentTunnel | MstcpFlagRecvTunnel;

    /// <summary>PACKET_FLAG_ON_SEND in INTERMEDIATE_BUFFER.m_dwDeviceFlags (Common.h line 118).</summary>
    public const uint PacketFlagOnSend = 0x00000001;
    /// <summary>PACKET_FLAG_ON_RECEIVE in INTERMEDIATE_BUFFER.m_dwDeviceFlags (Common.h line 119).</summary>
    public const uint PacketFlagOnReceive = 0x00000002;
}
